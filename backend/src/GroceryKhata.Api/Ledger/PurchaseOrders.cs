using System.Globalization;
using System.Security.Claims;
using System.Text;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

public enum PurchaseOrderStatus
{
    /// <summary>Waiting for goods, in full or in part.</summary>
    Open,

    /// <summary>Everything came, or the owner closed it short.</summary>
    Closed,

    Cancelled,

    /// <summary>Being written: not sent, not counted as on order, and still editable. Placing it makes it Open.</summary>
    Draft,
}

/// <summary>
/// What the shop asked a supplier for. It moves no stock and no money: goods come in on the supplier's bill, which is
/// matched against it line by line, so the order shows what is still to come.
/// </summary>
public sealed class PurchaseOrder : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid SupplierId { get; set; }

    public PurchaseOrderStatus Status { get; set; }

    public string? Note { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    /// <summary>Where it has got to, as last heard (see <see cref="OrderStage"/>), and when it was sent and confirmed.</summary>
    public OrderStage Stage { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public ContactChannel? SentVia { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>How many times the promised date was moved.</summary>
    public int PromiseMoves { get; set; }

    /// <summary>No morning reminders until this date.</summary>
    public DateOnly? SnoozedUntil { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public List<PurchaseOrderLine> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The rate agreed when ordering, to check the bill against. Null: not agreed.</summary>
    public decimal? Rate { get; set; }

    /// <summary>Bags, cartons or boxes the quantity comes in, as told to the supplier. Null: not said.</summary>
    public int? Bags { get; set; }

    /// <summary>Units that came in on bills matched to this order (paid and free).</summary>
    public decimal Received { get; set; }

    /// <summary>Units the shopkeeper said will never come (closed short), with why.</summary>
    public decimal ShortClosed { get; set; }

    public ShortReason? ShortReason { get; set; }
}

public sealed record OrderLineRequest(Guid ProductId, decimal Quantity, decimal? Rate = null, int? Bags = null);

public sealed record PurchaseOrderRequest(Guid Id, Guid SupplierId, IReadOnlyList<OrderLineRequest>? Lines, string? Note = null, DateOnly? ExpectedOn = null, bool Draft = false);

public sealed record OrderLineDto(Guid ProductId, string Name, decimal Quantity, decimal? Rate, decimal Received, decimal ShortClosed = 0, ShortReason? ShortReason = null,
    Catalogue.Unit Unit = Catalogue.Unit.Piece, int? Bags = null, Catalogue.Unit? PackUnit = null);

/// <summary>An order's quantity added up within one unit only: kg is never added to pieces.</summary>
public sealed record UnitTotal(Catalogue.Unit Unit, decimal Quantity);

/// <summary><see cref="Pending"/>: units still to come on the open lines.</summary>
public sealed record PurchaseOrderDto(
    Guid Id, int Number, Guid SupplierId, string SupplierName, PurchaseOrderStatus Status, string? Note, DateOnly? ExpectedOn, DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt, decimal Pending, IReadOnlyList<OrderLineDto> Lines, string Stage = "placed", int PromiseMoves = 0,
    IReadOnlyList<UnitTotal>? Totals = null, int Bags = 0);

/// <summary>A supplier to bring in from another app or a notebook. <see cref="Balance"/>: what the shop owes them now; minus when they owe the shop.</summary>
public sealed record SupplierImportRow(string? Name, string? Phone, string? Gstin, decimal Balance);

public sealed record SupplierImportRequest(IReadOnlyList<SupplierImportRow>? Rows);

public sealed record SupplierImportResult(int Created, int Skipped, IReadOnlyList<string> Problems);

/// <summary>Orders to suppliers, supplier import, and the purchase register for the CA. Owner only, like the rest of buying.</summary>
public static class PurchaseOrders
{
    public static IEndpointRouteBuilder MapPurchaseOrders(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/suppliers").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/orders", ListAsync);
        group.MapPost("/orders", CreateAsync);
        group.MapPut("/orders/{id:guid}", UpdateAsync);
        group.MapPost("/orders/{id:guid}/place", PlaceAsync);
        group.MapPost("/orders/{id:guid}/close", (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) => FinishAsync(id, PurchaseOrderStatus.Closed, db, clock, ct));
        group.MapPost("/orders/{id:guid}/cancel", (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) => FinishAsync(id, PurchaseOrderStatus.Cancelled, db, clock, ct));
        group.MapPost("/import", ImportAsync);
        group.MapGet("/register", RegisterAsync).RequireFeature(Features.GstExport);
        return app;
    }

    /// <summary>Open orders (or all, newest first), for one supplier or every one.</summary>
    private static async Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(Guid? supplierId, bool? all, AppDbContext db, CancellationToken ct)
    {
        var query = db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).Where(o => supplierId == null || o.SupplierId == supplierId);
        query = all == true ? query : query.Where(o => o.Status == PurchaseOrderStatus.Open || o.Status == PurchaseOrderStatus.Draft);
        var orders = await query.OrderByDescending(o => o.Number).Take(200).ToListAsync(ct);
        return await ToDtosAsync(db, orders, ct);
    }

    private static async Task<IReadOnlyList<PurchaseOrderDto>> ToDtosAsync(AppDbContext db, IReadOnlyList<PurchaseOrder> orders, CancellationToken ct)
    {
        var productIds = orders.SelectMany(o => o.Lines).Select(l => l.ProductId).Distinct().ToList();
        var names = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Unit, PackUnit = p.BagSize == null ? (Catalogue.Unit?)null : p.PurchaseUnit ?? Catalogue.Unit.Bag }, ct);
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return orders.Select(o => new PurchaseOrderDto(o.Id, o.Number, o.SupplierId, suppliers.GetValueOrDefault(o.SupplierId) ?? "", o.Status, o.Note, o.ExpectedOn, o.CreatedAt, o.ClosedAt,
            o.Status == PurchaseOrderStatus.Open ? o.Lines.Sum(PendingOf) : 0,
            o.Lines.Select(l => new OrderLineDto(l.ProductId, names.GetValueOrDefault(l.ProductId)?.Name ?? "", l.Quantity, l.Rate, l.Received, l.ShortClosed, l.ShortReason,
                names.GetValueOrDefault(l.ProductId)?.Unit ?? Catalogue.Unit.Piece, l.Bags, names.GetValueOrDefault(l.ProductId)?.PackUnit)).ToList(), GoodsTracking.StageOf(o), o.PromiseMoves,
            o.Lines.GroupBy(l => names.GetValueOrDefault(l.ProductId)?.Unit ?? Catalogue.Unit.Piece).OrderBy(g => g.Key).Select(g => new UnitTotal(g.Key, g.Sum(l => l.Quantity))).ToList(),
            o.Lines.Sum(l => l.Bags ?? 0))).ToList();
    }

    /// <summary>Still to come on a line: ordered less what came and what was closed short, never below zero.</summary>
    internal static decimal PendingOf(PurchaseOrderLine l) => Math.Max(0, l.Quantity - l.Received - l.ShortClosed);

    private static async Task<IResult> CreateAsync(PurchaseOrderRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == request.Id, ct) is { } saved)
        {
            return TypedResults.Ok((await ToDtosAsync(db, [saved], ct))[0]);
        }

        if (request.Id == Guid.Empty)
        {
            return Invalid("Lines", "Choose the items, each once, with how many to order.");
        }

        if (await CheckAsync(request, db, ct) is { } problem)
        {
            return problem;
        }

        var lines = request.Lines!;
        var note = request.Note?.Trim();
        var order = new PurchaseOrder
        {
            Id = request.Id, SupplierId = request.SupplierId, Number = (await db.PurchaseOrders.MaxAsync(o => (int?)o.Number, ct) ?? 0) + 1,
            Status = request.Draft ? PurchaseOrderStatus.Draft : PurchaseOrderStatus.Open,
            Note = string.IsNullOrEmpty(note) ? null : note[..Math.Min(note.Length, 200)], ExpectedOn = request.ExpectedOn, CreatedByUserId = user.UserId()!.Value,
            Lines = lines.Select(l => new PurchaseOrderLine { ProductId = l.ProductId, Quantity = l.Quantity, Rate = l.Rate, Bags = l.Bags }).ToList(),
        };
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, [order], ct))[0]);
    }

    /// <summary>Rewrites a draft: supplier, items, note and expected date. Placed orders can't be edited.</summary>
    private static async Task<IResult> UpdateAsync(Guid id, PurchaseOrderRequest request, AppDbContext db, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This order is already placed and can't be changed.");
        }

        if (await CheckAsync(request, db, ct) is { } problem)
        {
            return problem;
        }

        var note = request.Note?.Trim();
        order.SupplierId = request.SupplierId;
        order.Note = string.IsNullOrEmpty(note) ? null : note[..Math.Min(note.Length, 200)];
        order.ExpectedOn = request.ExpectedOn;
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        order.Lines = request.Lines!.Select(l => new PurchaseOrderLine { OrderId = order.Id, ProductId = l.ProductId, Quantity = l.Quantity, Rate = l.Rate, Bags = l.Bags }).ToList();
        db.PurchaseOrderLines.AddRange(order.Lines);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, [order], ct))[0]);
    }

    /// <summary>Places a draft: from now it counts as on order. Placing twice is harmless.</summary>
    private static async Task<IResult> PlaceAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status == PurchaseOrderStatus.Draft)
        {
            order.Status = PurchaseOrderStatus.Open;
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok((await ToDtosAsync(db, [order], ct))[0]);
    }

    private static async Task<IResult?> CheckAsync(PurchaseOrderRequest request, AppDbContext db, CancellationToken ct)
    {
        var lines = request.Lines ?? [];
        if (lines.Count is 0 or > 200 || lines.Select(l => l.ProductId).Distinct().Count() != lines.Count
            || lines.Any(l => !Money.IsValidQuantity(l.Quantity) || l.Bags is <= 0 or > 100_000 || l.Rate is < 0 || (l.Rate is { } r && (r > Money.MaxAmount || decimal.Round(r, 2) != r))))
        {
            return Invalid("Lines", "Choose the items, each once, with how many to order.");
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
        {
            return Invalid("SupplierId", "Choose the supplier.");
        }

        var ids = lines.Select(l => l.ProductId).ToList();
        var units = await db.Products.Where(p => ids.Contains(p.Id) && !p.IsService).ToDictionaryAsync(p => p.Id, p => p.Unit, ct);
        if (units.Count != ids.Count)
        {
            return Invalid("Lines", "An item wasn't found.");
        }

        if (lines.Any(l => Catalogue.Units.IsWhole(units[l.ProductId]) && l.Quantity != decimal.Truncate(l.Quantity)))
        {
            return Invalid("Lines", "Pieces, cartons, strips and the like are ordered in whole numbers.");
        }

        return null;
    }

    /// <summary>Closed short (the rest won't come) or cancelled (nothing came). Either way it stops counting as on order.</summary>
    private static async Task<IResult> FinishAsync(Guid id, PurchaseOrderStatus to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status == PurchaseOrderStatus.Draft && to == PurchaseOrderStatus.Cancelled)
        {
            (order.Status, order.ClosedAt) = (to, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }

        if (order.Status != PurchaseOrderStatus.Open)
        {
            return TypedResults.NoContent();
        }

        if (to == PurchaseOrderStatus.Cancelled && order.Lines.Any(l => l.Received > 0))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Some goods already came on this order: close it instead of cancelling.");
        }

        (order.Status, order.ClosedAt) = (to, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Counts a supplier bill's goods against its order, in the bill's transaction: the first line of each item takes them,
    /// and an order with nothing left to come closes itself. A cancelled bill gives them back (<paramref name="sign"/> -1).
    /// </summary>
    internal static async Task<string?> ReceiveAsync(AppDbContext db, Guid orderId, Guid supplierId, IEnumerable<(Guid ProductId, decimal Units)> goods, int sign, TimeProvider clock, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.FromSql($"SELECT * FROM purchase_orders WHERE id = {orderId} FOR UPDATE").Include(o => o.Lines).SingleOrDefaultAsync(ct);
        if (order is null || order.SupplierId != supplierId)
        {
            return "That order isn't this supplier's.";
        }

        if (sign > 0 && order.Status != PurchaseOrderStatus.Open)
        {
            return "That order is already closed.";
        }

        foreach (var (productId, units) in goods)
        {
            if (order.Lines.FirstOrDefault(l => l.ProductId == productId) is { } line)
            {
                line.Received = Math.Max(0, line.Received + (sign * units));
            }
        }

        var allIn = order.Lines.All(l => PendingOf(l) <= 0);
        if (sign > 0 && allIn)
        {
            (order.Status, order.ClosedAt) = (PurchaseOrderStatus.Closed, clock.GetUtcNow());
        }
        else if (sign < 0 && order.Status == PurchaseOrderStatus.Closed && !allIn)
        {
            (order.Status, order.ClosedAt) = (PurchaseOrderStatus.Open, null);
        }

        return null;
    }

    /// <summary>Units still to come, per item, on open orders: "What to order" leaves them out so nothing is ordered twice.</summary>
    internal static async Task<Dictionary<Guid, decimal>> PendingAsync(AppDbContext db, CancellationToken ct) =>
        (await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => db.PurchaseOrders.Any(o => o.Id == l.OrderId && o.Status == PurchaseOrderStatus.Open) && l.Received + l.ShortClosed < l.Quantity)
            .Select(l => new { l.ProductId, Left = l.Quantity - l.Received - l.ShortClosed }).ToListAsync(ct))
        .GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Left));

    /// <summary>
    /// Suppliers from a file. One already here (same name) is left alone, so importing twice changes nothing. A balance goes
    /// in as the supplier's opening balance, never as a bill, so purchases and stock stay true.
    /// </summary>
    private static async Task<IResult> ImportAsync(SupplierImportRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (request.Rows is not { Count: > 0 and <= 2000 } rows)
        {
            return Invalid("Rows", "Send between 1 and 2000 suppliers.");
        }

        var names = (await db.Suppliers.AsNoTracking().Select(s => s.Name).ToListAsync(ct)).Select(n => n.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        var created = new List<(Supplier Supplier, decimal Balance)>();
        var skipped = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = row.Name?.Trim();
            var gstin = string.IsNullOrWhiteSpace(row.Gstin) ? null : row.Gstin.Trim().ToUpperInvariant();
            var phone = string.IsNullOrWhiteSpace(row.Phone) ? null : row.Phone.Trim();
            if (name is not { Length: >= 2 and <= 120 })
            {
                problems.Add($"Row {i + 1}: the name is missing or too long.");
            }
            else if (gstin is not null && !GstExport.IsValidGstin(gstin))
            {
                problems.Add($"Row {i + 1} ({name}): the GSTIN isn't valid.");
            }
            else if (phone?.Length > 20)
            {
                problems.Add($"Row {i + 1} ({name}): the phone number is too long.");
            }
            else if (row.Balance != 0 && (Math.Abs(row.Balance) > Money.MaxAmount || decimal.Round(row.Balance, 2) != row.Balance))
            {
                problems.Add($"Row {i + 1} ({name}): the balance isn't a valid amount.");
            }
            else if (!names.Add(name))
            {
                skipped++;
            }
            else
            {
                created.Add((new Supplier { Id = Guid.CreateVersion7(), Name = name, Phone = phone, Gstin = gstin }, row.Balance));
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Suppliers.AddRange(created.Select(c => c.Supplier));
        await db.SaveChangesAsync(ct);
        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        db.SupplierEntries.AddRange(created.Where(c => c.Balance != 0).Select(c => new SupplierEntry
        {
            SupplierId = c.Supplier.Id, Kind = SupplierEntryKind.Opening, Amount = c.Balance, OccurredAt = now, CreatedByUserId = userId,
        }));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(new SupplierImportResult(created.Count, skipped, problems));
    }

    /// <summary>
    /// The month's supplier bills for the CA, one row per bill and GST rate: the purchase register they check input credit
    /// against. Cancelled bills stay in, marked, so the bill numbers have no unexplained gaps.
    /// </summary>
    private static async Task<IResult> RegisterAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var (year, m) = IndiaTime.ParseMonth(month) ?? (today.Year, today.Month);
        var (start, end) = IndiaTime.Month(year, m);
        var bills = await db.Purchases.AsNoTracking().Include(p => p.Lines).Where(p => p.OccurredAt >= start && p.OccurredAt < end).OrderBy(p => p.OccurredAt).ToListAsync(ct);
        var supplierIds = bills.Select(b => b.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Gstin }, ct);
        static string Cell(string? text) => text is null ? "" : text.Contains(',') || text.Contains('"') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
        static string Num(decimal n) => n.ToString("0.00", CultureInfo.InvariantCulture);
        var composition = await db.Shops.AsNoTracking().Select(s => s.GstComposition).FirstOrDefaultAsync(ct);
        var shopGstin = await db.Shops.AsNoTracking().Select(s => s.Gstin).FirstOrDefaultAsync(ct);
        var csv = new StringBuilder("Date,Bill no,Supplier,Supplier GSTIN,Supplier state,GST rate %,Taxable value,CGST,SGST,IGST,Input tax claimable,Freight and round-off,Bill total,Status\n");
        decimal tTaxable = 0, tCgst = 0, tSgst = 0, tIgst = 0, tExtra = 0, tTotal = 0;
        foreach (var bill in bills)
        {
            var supplier = suppliers.GetValueOrDefault(bill.SupplierId);
            var state = supplier?.Gstin is { Length: 15 } g && GstExport.States.TryGetValue(g[..2], out var name) ? name : "";
            var date = bill.OccurredAt.ToOffset(IndiaTime.Offset).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
            // Input tax only for a regular shop, on a bill from a supplier with a valid GSTIN; another state's supplier charges IGST.
            var claimable = !composition && supplier?.Gstin is { } sg && GstExport.IsValidGstin(sg);
            var interState = supplier?.Gstin is { Length: 15 } sg2 && shopGstin is { Length: 15 } && sg2[..2] != shopGstin[..2];
            var live = bill.CancelledAt is null;
            var byRate = bill.Lines.GroupBy(l => l.GstRate).OrderBy(g => g.Key ?? -1).ToList();
            for (var i = 0; i < byRate.Count; i++)
            {
                var rate = byRate[i];
                var taxable = rate.Sum(l => l.Taxable);
                var gst = rate.Sum(l => l.Gst);
                var (cgst, sgst, igst) = interState ? (0m, 0m, gst) : (decimal.Round(gst / 2, 2), gst - decimal.Round(gst / 2, 2), 0m);
                if (live)
                {
                    (tTaxable, tCgst, tSgst, tIgst) = (tTaxable + taxable, tCgst + cgst, tSgst + sgst, tIgst + igst);
                }

                csv.AppendJoin(',', date, Cell(bill.BillNo), Cell(supplier?.Name), supplier?.Gstin ?? "", Cell(state), rate.Key is { } r ? Num(r) : "",
                    Num(taxable), Num(cgst), Num(sgst), Num(igst), claimable ? "Yes" : "No", i == 0 ? Num(bill.ExtraCharges) : "", i == 0 ? Num(bill.Total) : "",
                    live ? "" : "Cancelled").Append('\n');
            }

            if (live)
            {
                (tExtra, tTotal) = (tExtra + bill.ExtraCharges, tTotal + bill.Total);
            }
        }

        csv.AppendJoin(',', "Total (cancelled bills left out)", "", "", "", "", "", Num(tTaxable), Num(tCgst), Num(tSgst), Num(tIgst), "", Num(tExtra), Num(tTotal), "").Append('\n');
        csv.Append("\nNote: a bill with more than one GST rate has one row per rate; Freight and round-off and Bill total are on its first row only.\n");
        return TypedResults.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", $"purchase-register-{year:D4}-{m:D2}.csv");
    }
}
