using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Goods that came in before their bill (a goods receipt note): the boxes are counted onto the shelf now, with no money
/// owed yet. When the supplier's bill comes, it is linked to the receipt, so the bill adds no stock a second time.
/// </summary>
public sealed class GoodsReceipt : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>The order the goods came against, if any: the receipt counts them off it.</summary>
    public Guid? PurchaseOrderId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }


    public string? Note { get; set; }

    /// <summary>The truck or tempo that brought it.</summary>
    public string? VehicleNo { get; set; }

    /// <summary>The transporter's receipt (bilty / LR) number, and who carried it.</summary>
    public string? Bilty { get; set; }

    public string? Transporter { get; set; }

    /// <summary>Weighbridge slip: loaded and empty vehicle weight, in kg. Their difference is the goods' weight.</summary>
    public decimal? GrossWeight { get; set; }

    public decimal? TareWeight { get; set; }

    /// <summary>
    /// A weighbridge shortfall claimed from the supplier: the kilograms short and the entry that took the money off what
    /// the shop owes them. Claimed once; cancelling the receipt puts the money back.
    /// </summary>
    public decimal? ShortKg { get; set; }

    public Guid? ShortEntryId { get; set; }

    /// <summary>The debit note's number, serial within the financial year as GST asks: DN/26-27/1, DN/26-27/2, …</summary>
    public string? DebitNoteNo { get; set; }

    /// <summary>The supplier bill these goods were billed on. Null: still waiting for the bill.</summary>
    public Guid? PurchaseId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

/// <summary>One item that came: its weight (or count) as received, and how many bags, when it comes in bags.</summary>
public sealed record ReceiptLine(Guid ProductId, string Name, decimal Quantity, decimal? Bags = null, decimal? BagSize = null, decimal Damaged = 0);

/// <summary><see cref="Damaged"/>: of the units counted in, how many came broken or spoilt (they go out again as damage).</summary>
public sealed record ReceiptLineRequest(Guid ProductId, decimal Quantity, decimal? Bags = null, decimal? Damaged = null);

public sealed record GoodsReceiptRequest(Guid Id, Guid SupplierId, IReadOnlyList<ReceiptLineRequest>? Lines, Guid? OrderId = null, string? Note = null,
    string? VehicleNo = null, string? Bilty = null, string? Transporter = null, decimal? GrossWeight = null, decimal? TareWeight = null, Guid? GodownId = null);

/// <summary>The supplier's rate per kg for the missing goods: the claim is the kilograms short times this.</summary>
public sealed record ShortClaimRequest(decimal RatePerKg);

public sealed record GoodsReceiptDto(
    Guid Id, int Number, Guid SupplierId, Guid? PurchaseOrderId, DateTimeOffset ReceivedAt, IReadOnlyList<ReceiptLine> Lines, string? Note, Guid? PurchaseId, DateTimeOffset? CancelledAt, string? By,
    string? VehicleNo = null, string? Bilty = null, string? Transporter = null, decimal? GrossWeight = null, decimal? TareWeight = null,
    decimal? ShortKg = null, decimal? ShortClaim = null, string? DebitNoteNo = null);

public static class GoodsReceipts
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapGoodsReceipts(this IEndpointRouteBuilder app)
    {
        // Whoever is at the counter when the boxes come counts them in; the bill and the money stay with the owner.
        var group = app.MapGroup("/api/v1/goods-receipts").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/short-claim", ClaimShortAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>What came on each receipt, from its delivery lines (cancelled ones too, so a cancelled receipt still shows its goods).</summary>
    internal static async Task<ILookup<Guid, ReceiptLine>> LinesAsync(AppDbContext db, IReadOnlyCollection<Guid> receiptIds, CancellationToken ct)
    {
        if (receiptIds.Count == 0)
        {
            return Array.Empty<ReceiptLine>().ToLookup(_ => Guid.Empty);
        }

        var rows = await (from a in db.ArrivalLines.AsNoTracking()
                          where a.ReceiptId != null && receiptIds.Contains(a.ReceiptId.Value)
                          join p in db.Products.AsNoTracking() on a.ProductId equals p.Id into ps
                          from p in ps.DefaultIfEmpty()
                          orderby a.CreatedAt, a.Id
                          select new { ReceiptId = a.ReceiptId!.Value, a.ProductId, Name = p == null ? "" : p.Name, a.Received, a.Bags, BagSize = p == null ? null : p.BagSize, a.Damaged }).ToListAsync(ct);
        return rows.ToLookup(r => r.ReceiptId, r => new ReceiptLine(r.ProductId, r.Name, r.Received, r.Bags, r.BagSize, r.Damaged));
    }

    /// <summary>A supplier's receipts (or the shop's), newest first; <paramref name="open"/>: only those still waiting for a bill.</summary>
    private static async Task<IReadOnlyList<GoodsReceiptDto>> ListAsync(Guid? supplierId, bool? open, AppDbContext db, CancellationToken ct)
    {
        var rows = await (from r in db.GoodsReceipts.AsNoTracking()
                          where (supplierId == null || r.SupplierId == supplierId) && (open != true || (r.PurchaseId == null && r.CancelledAt == null))
                          join u in db.Users.AsNoTracking() on r.CreatedByUserId equals u.Id into users
                          from u in users.DefaultIfEmpty()
                          orderby r.ReceivedAt descending
                          select new { r, By = u == null ? null : u.FullName }).Take(100).ToListAsync(ct);
        var lines = await LinesAsync(db, rows.Select(x => x.r.Id).ToList(), ct);
        var claimIds = rows.Select(x => x.r.ShortEntryId).OfType<Guid>().ToList();
        var claims = await db.SupplierEntries.AsNoTracking().Where(e => claimIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => -e.Amount, ct);
        return rows.Select(x => ToDto(x.r, x.By, lines[x.r.Id].ToList(), x.r.ShortEntryId is { } c ? claims.GetValueOrDefault(c) : null)).ToList();
    }

    private static async Task<GoodsReceiptDto> ToDtoAsync(AppDbContext db, GoodsReceipt r, CancellationToken ct) => ToDto(r, null, (await LinesAsync(db, [r.Id], ct))[r.Id].ToList());

    private static GoodsReceiptDto ToDto(GoodsReceipt r, string? by, IReadOnlyList<ReceiptLine> lines, decimal? claim = null) =>
        new(r.Id, r.Number, r.SupplierId, r.PurchaseOrderId, r.ReceivedAt, lines, r.Note, r.PurchaseId, r.CancelledAt, by, r.VehicleNo, r.Bilty, r.Transporter, r.GrossWeight, r.TareWeight,
            r.ShortEntryId is null ? null : r.ShortKg, claim, r.DebitNoteNo);

    /// <summary>
    /// Claims a weighbridge shortfall from the supplier: what the items add up to, less what the weighbridge says came,
    /// times their rate per kg, comes off what the shop owes them as an adjustment in their khata. Only lines whose weight is
    /// known count, so pieces are never taken for kilograms. Stock is left as counted; correct it from the item if needed.
    /// </summary>
    private static async Task<IResult> ClaimShortAsync(Guid id, ShortClaimRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var receipt = await db.GoodsReceipts.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
        {
            return TypedResults.NotFound();
        }

        if (receipt.CancelledAt is not null || receipt.ShortEntryId is not null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: receipt.CancelledAt is null ? "The shortfall on this receipt is already claimed." : "This receipt is cancelled.");
        }

        if (request.RatePerKg <= 0 || request.RatePerKg > Money.MaxAmount || decimal.Round(request.RatePerKg, 2) != request.RatePerKg)
        {
            return Invalid("RatePerKg", "Enter the rate per kg the supplier charged.");
        }

        if (receipt.GrossWeight is not { } gross || receipt.TareWeight is not { } tare)
        {
            return Invalid("Id", "This receipt has no weighbridge weights.");
        }

        var lines = (await LinesAsync(db, [receipt.Id], ct))[receipt.Id].ToList();
        var ids = lines.Select(l => l.ProductId).Distinct().ToList();
        var kinds = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Unit, p.WeightKg, p.GstRate }, ct);
        var expected = lines.Sum(l => kinds.GetValueOrDefault(l.ProductId) is { } k ? Units.Kg(k.Unit, k.WeightKg, l.Quantity) ?? 0 : 0);
        var shortKg = decimal.Round(expected - (gross - tare), 3);
        if (shortKg < 1)
        {
            return Invalid("Id", "The weighbridge shows no shortfall on what these items add up to.");
        }

        var now = clock.GetUtcNow();
        await Books.EnsureOpenAsync(db, tenant, now, ct);
        // A debit note to the supplier: the goods short at their rate (before tax), plus the GST on them when the shop took
        // input tax on the bill (a regular shop, a supplier with a valid GSTIN), at the items' rates weighted by their kilograms.
        // It goes in as a supplier credit note, so the GST export reverses that input tax and Tally gets a Debit Note.
        var taxable = decimal.Round(shortKg * request.RatePerKg, 2);
        var composition = await db.Shops.AsNoTracking().Select(s => s.GstComposition).FirstOrDefaultAsync(ct);
        var supplierGstin = await db.Suppliers.AsNoTracking().Where(s => s.Id == receipt.SupplierId).Select(s => s.Gstin).FirstOrDefaultAsync(ct);
        var weighed = lines.Select(l => (Kg: kinds.GetValueOrDefault(l.ProductId) is { } k ? Units.Kg(k.Unit, k.WeightKg, l.Quantity) ?? 0 : 0, Rate: kinds.GetValueOrDefault(l.ProductId)?.GstRate ?? 0)).ToList();
        var gstRate = !composition && GstExport.IsValidGstin(supplierGstin) && expected > 0 ? weighed.Sum(w => w.Kg * w.Rate) / expected : 0;
        var gst = decimal.Round(taxable * gstRate / 100, 2);
        var amount = taxable + gst;
        // Numbered within the financial year (April to March): the next after the year's highest.
        var today = IndiaTime.DayOf(now);
        var fy = today.Month >= 4 ? today.Year : today.Year - 1;
        var prefix = $"DN/{fy % 100:D2}-{(fy + 1) % 100:D2}/";
        var used = await db.GoodsReceipts.AsNoTracking().Where(r => r.DebitNoteNo != null && r.DebitNoteNo.StartsWith(prefix)).Select(r => r.DebitNoteNo!).ToListAsync(ct);
        receipt.DebitNoteNo = prefix + (used.Select(n => int.TryParse(n[prefix.Length..], out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1);
        var entry = new SupplierEntry
        {
            Id = Guid.CreateVersion7(), SupplierId = receipt.SupplierId, Kind = SupplierEntryKind.CreditNote, Amount = -amount, Gst = gst, OccurredAt = now, CreatedByUserId = user.UserId()!.Value,
            Note = $"Short {shortKg:0.###} kg @{request.RatePerKg:0.##}/kg, GR #{receipt.Number}"[..Math.Min(40, $"Short {shortKg:0.###} kg @{request.RatePerKg:0.##}/kg, GR #{receipt.Number}".Length)], Reference = receipt.DebitNoteNo,
        };
        db.SupplierEntries.Add(entry);
        (receipt.ShortKg, receipt.ShortEntryId) = (shortKg, entry.Id);

        // The missing goods never came, so they come off stock too: the kilograms short shared over the weighed lines by
        // their weight, back in each item's own unit (whole pieces stay whole). Booked as less bought, not as a loss.
        var userId = user.UserId()!.Value;
        var shares = lines.GroupBy(l => l.ProductId).Select(g =>
        {
            var k = kinds.GetValueOrDefault(g.Key);
            var kg = k is null ? 0 : g.Sum(l => Units.Kg(k.Unit, k.WeightKg, l.Quantity) ?? 0);
            var perUnit = k is null ? null : Units.Kg(k.Unit, k.WeightKg, 1);
            var quantity = kg <= 0 || perUnit is not > 0 ? 0 : shortKg * kg / expected / perUnit.Value;
            return (ProductId: g.Key, Quantity: k is not null && Units.IsWhole(k.Unit) ? Math.Round(quantity) : decimal.Round(quantity, 3));
        }).Where(x => x.Quantity > 0).ToList();
        var shortIds = shares.Select(x => x.ProductId).ToList();
        var products = await db.Products.Where(p => shortIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var (productId, quantity) in shares)
        {
            db.StockMovements.Add(new StockMovement
            {
                RefType = "receipt-short", RefId = receipt.Id, ProductId = productId, Kind = StockMovementKind.Purchase, Quantity = -quantity, OccurredAt = now,
                CreatedByUserId = userId, Note = $"Weighbridge short, GR #{receipt.Number}",
            });
            products[productId].Stock -= quantity;
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(receipt, null, lines, amount));
    }

    private static async Task<IResult> AddAsync(GoodsReceiptRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok(await ToDtoAsync(db, existing, ct));
        }

        var lines = request.Lines ?? [];
        var products = await db.Products.AsNoTracking().Where(p => lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200 || lines.Select(l => l.ProductId).Distinct().Count() != lines.Count
            || lines.Any(l => !products.ContainsKey(l.ProductId) || products[l.ProductId].IsService || !Money.IsValidQuantity(l.Quantity))
            || !await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
        {
            return Invalid("Lines", "Choose the supplier and the items that came, with how many of each.");
        }

        if (lines.Any(l => l.Damaged is < 0 || l.Damaged > l.Quantity))
        {
            return Invalid("Lines", "Damaged can't be more than what came.");
        }

        if (request.Note?.Trim().Length > 200)
        {
            return Invalid("Note", "Keep the note under 200 letters.");
        }

        if (lines.Any(l => l.Bags is { } b && (b < 0 || b > 100_000 || decimal.Round(b, 3) != b)))
        {
            return Invalid("Lines", "Enter the number of bags, or leave it empty.");
        }

        if (request.GrossWeight is { } gross && (gross <= 0 || gross > 200_000 || request.TareWeight is not { } tare || tare < 0 || tare >= gross))
        {
            return Invalid("GrossWeight", "Enter the loaded and empty vehicle weights from the weighbridge slip (loaded more than empty).");
        }

        static string? Short(string? text, int max) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(max, text.Trim().Length)];

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (request.OrderId is { } orderId
            && await PurchaseOrders.ReceiveAsync(db, orderId, request.SupplierId, lines.Select(l => (l.ProductId, l.Quantity)), 1, clock, ct) is { } orderProblem)
        {
            return Invalid("OrderId", orderProblem);
        }

        // One number at a time: two receipts saved at once would otherwise take the same number.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('goods_receipts'))", ct);
        var receipt = new GoodsReceipt
        {
            Id = request.Id, SupplierId = request.SupplierId, PurchaseOrderId = request.OrderId, ReceivedAt = now, CreatedByUserId = userId,
            Number = (await db.GoodsReceipts.MaxAsync(r => (int?)r.Number, ct) ?? 0) + 1,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            VehicleNo = Short(request.VehicleNo, 20)?.ToUpperInvariant(), Bilty = Short(request.Bilty, 40), Transporter = Short(request.Transporter, 80),
            GrossWeight = request.GrossWeight, TareWeight = request.GrossWeight is null ? null : request.TareWeight,
        };
        db.GoodsReceipts.Add(receipt);
        foreach (var line in lines)
        {
            // The cost isn't known until the bill: the item keeps its cost price until then.
            db.StockMovements.Add(new StockMovement
            {
                RefType = "receipt", RefId = receipt.Id, ProductId = line.ProductId, Kind = StockMovementKind.Purchase, Quantity = line.Quantity, Bags = line.Bags is > 0 ? line.Bags : null, OccurredAt = now, CreatedByUserId = userId, Note = $"Goods receipt #{receipt.Number}",
            });
            if (line.Damaged is > 0 and var damaged)
            {
                // Counted in, then straight out as damage: the ledger shows both, and the units can go back to the supplier.
                db.StockMovements.Add(new StockMovement
                {
                    RefType = "receipt", RefId = receipt.Id, ProductId = line.ProductId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Damage, Quantity = -damaged,
                    OccurredAt = now, CreatedByUserId = userId, Note = $"Came damaged · goods receipt #{receipt.Number}",
                });
            }
        }

        if (request.OrderId is { } arrivedOn)
        {
            await GoodsTracking.ArrivedAsync(db, arrivedOn, receipt.Id, ct);
        }

        await Arrivals.AddAsync(db, request.SupplierId, receipt.Id, null, request.OrderId, lines.Select(l => (l.ProductId, l.Quantity, l.Damaged ?? 0, l.Bags is > 0 ? l.Bags : null, (decimal?)null)), now, ct);
        if (request.GodownId is { } godown)
        {
            // Unloaded straight at the godown: the good units go there, not on the shop shelf.
            if (!await Godowns.IsActiveAsync(db, godown, ct))
            {
                return Invalid("GodownId", "That godown wasn't found.");
            }

            await Godowns.MoveForAsync(db, receipt.Id, null, godown, lines.Select(l => (l.ProductId, l.Quantity - (l.Damaged ?? 0))), $"Goods receipt #{receipt.Number}", userId, now, ct);
        }

        await Bardana.CameWithGoodsAsync(db, receipt.Id, request.SupplierId, lines.Sum(l => l.Bags is > 0 ? l.Bags.Value : 0), userId, now, ct);
        await db.SaveChangesAsync(ct);
        var ids = lines.Select(l => l.ProductId).Order().ToArray();
        var quantities = ids.Select(i => lines.Single(l => l.ProductId == i)).Select(l => l.Quantity - (l.Damaged ?? 0)).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock + v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Created($"/api/v1/goods-receipts/{receipt.Id}", await ToDtoAsync(db, receipt, ct));
    }

    /// <summary>A receipt made by mistake, before its bill: the goods come off the shelf again and off the order.</summary>
    private static async Task<IResult> CancelAsync(Guid id, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var receipt = await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
        {
            return TypedResults.NotFound();
        }

        await Books.EnsureOpenAsync(db, tenant, receipt.ReceivedAt, ct);

        if (receipt.PurchaseId is not null)
        {
            return Invalid("Id", "These goods are already on a supplier bill: cancel the bill first.");
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.GoodsReceipts.Where(r => r.Id == id && r.CancelledAt == null && r.PurchaseId == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.CancelledAt, now), ct) == 0)
        {
            return TypedResults.NoContent();
        }

        var lines = (await LinesAsync(db, [receipt.Id], ct))[receipt.Id].ToList();
        if (receipt.PurchaseOrderId is { } orderId)
        {
            await PurchaseOrders.ReceiveAsync(db, orderId, receipt.SupplierId, lines.Select(l => (l.ProductId, l.Quantity)), -1, clock, ct);
        }

        foreach (var line in lines)
        {
            db.StockMovements.Add(new StockMovement
            {
                RefType = "receipt", RefId = receipt.Id, ProductId = line.ProductId, Kind = StockMovementKind.Purchase, Quantity = -line.Quantity, Bags = line.Bags is > 0 ? -line.Bags : null, OccurredAt = now,
                CreatedByUserId = userId, Note = $"Goods receipt #{receipt.Number} cancelled",
            });
            if (line.Damaged > 0)
            {
                // The damage write-off is undone too: those units were never really the shop's.
                db.StockMovements.Add(new StockMovement
                {
                    RefType = "receipt", RefId = receipt.Id, ProductId = line.ProductId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Damage, Quantity = line.Damaged,
                    OccurredAt = now, CreatedByUserId = userId, Note = $"Goods receipt #{receipt.Number} cancelled",
                });
            }
        }

        // A shortfall claimed on goods that never came stands no more: the money goes back on what the shop owes.
        if (receipt.ShortEntryId is { } claimId && await db.SupplierEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == claimId, ct) is { } claim)
        {
            db.SupplierEntries.Add(new SupplierEntry
            {
                SupplierId = receipt.SupplierId, Kind = claim.Kind, Amount = -claim.Amount, Gst = claim.Gst is { } g ? -g : null, OccurredAt = now, CreatedByUserId = userId,
                Note = $"Short claim undone, GR #{receipt.Number}", Reference = claim.Reference is { } dn ? $"{dn}/R" : null,
            });
        }

        // What came off stock for the shortfall goes back on first, since the receipt's whole quantity comes off below.
        var shorts = await db.StockMovements.AsNoTracking().Where(m => m.RefType == "receipt-short" && m.RefId == id).ToListAsync(ct);
        var shortItems = shorts.Select(m => m.ProductId).Distinct().ToList();
        var shortProducts = await db.Products.Where(p => shortItems.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var m in shorts)
        {
            db.StockMovements.Add(new StockMovement
            {
                RefType = "receipt-short", RefId = id, ProductId = m.ProductId, Kind = StockMovementKind.Purchase, Quantity = -m.Quantity, OccurredAt = now,
                CreatedByUserId = userId, Note = $"Goods receipt #{receipt.Number} cancelled",
            });
            shortProducts[m.ProductId].Stock -= m.Quantity;
        }

        await db.ArrivalLines.Where(a => a.ReceiptId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.CancelledAt, now), ct);
        await Godowns.CancelForAsync(db, id, now, ct);
        await Bardana.StrikeForAsync(db, id, now, ct);
        await db.SaveChangesAsync(ct);
        var ids = lines.Select(l => l.ProductId).Order().ToArray();
        var quantities = ids.Select(i => lines.Single(l => l.ProductId == i)).Select(l => l.Quantity - l.Damaged).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock - v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
