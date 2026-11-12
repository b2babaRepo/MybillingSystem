using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>Someone the shop buys from.</summary>
public sealed class Supplier : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public string? Phone { get; set; }

    public string? Gstin { get; set; }

    public string? Address { get; set; }

    /// <summary>Days the supplier gives to pay a bill. Null: no terms agreed.</summary>
    public int? PaymentDays { get; set; }

    /// <summary>A supplier with history is never deleted: it is put away, and its khata stays.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>A supplier's bill: goods in, on credit for whatever wasn't paid at once.</summary>
public sealed class Purchase : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>The number on the supplier's own bill.</summary>
    public string? BillNo { get; set; }

    public decimal Total { get; set; }

    /// <summary>Freight and round-off on the printed bill (minus for a round-down), in the total but on no item.</summary>
    public decimal ExtraCharges { get; set; }

    /// <summary>The date printed on the supplier's bill, not the day it was entered.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>The order these goods came against, if any.</summary>
    public Guid? PurchaseOrderId { get; set; }

    /// <summary>Set when the bill was entered by mistake: stock and the amount owed are taken back.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    public List<PurchaseLine> Lines { get; set; } = [];
}

public sealed class PurchaseLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid PurchaseId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    /// <summary>Free units on a scheme (10+1): they come into stock at no cost, lowering each unit's real cost.</summary>
    public decimal FreeQuantity { get; set; }

    /// <summary>Trade discount % off the rate.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Medicines: the batch these units went into.</summary>
    public Guid? BatchId { get; set; }

    /// <summary>GST % the supplier charged on top of the rate; its tax is the shop's input credit. Null: no GST on the bill.</summary>
    public decimal? GstRate { get; set; }

    /// <summary>The line's value before GST (after discount), and the GST on it.</summary>
    public decimal Taxable { get; set; }

    public decimal Gst { get; set; }

    /// <summary>Units of this line already counted in on a goods receipt: the bill added no stock for them.</summary>
    public decimal FromReceipt { get; set; }

    /// <summary>Bags on this line (paid and free together), when the goods come in bags.</summary>
    public decimal? Bags { get; set; }
}

/// <summary>What a line of a supplier's khata is. Other: lines from before kinds were kept, whose kind can't be told.</summary>
public enum SupplierEntryKind
{
    Other,
    Opening,
    Bill,
    Payment,
    CreditNote,
    BillCancelled,
}

/// <summary>The supplier's khata, append-only: + a bill, − a payment. What the shop owes is the sum.</summary>
public sealed class SupplierEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid SupplierId { get; set; }

    public SupplierEntryKind Kind { get; set; }

    public decimal Amount { get; set; }

    public Guid? PurchaseId { get; set; }

    public string? Note { get; set; }

    /// <summary>How a payment went, and its cheque or transfer number.</summary>
    public PaymentMode? Mode { get; set; }

    public string? Reference { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

/// <summary>On a change, null keeps a detail and an empty value clears it.</summary>
public sealed record SupplierRequest(Guid Id, string? Name, string? Phone, string? Gstin = null, string? Address = null, int? PaymentDays = null, bool? IsActive = null);

/// <summary>
/// A supplier and what the shop owes them, split by how old the unpaid bills are: payments clear the oldest bills
/// first, so what is still owed sits on the newest ones. <see cref="OldestDays"/>: the oldest unpaid bill's age.
/// </summary>
public sealed record SupplierDto(Guid Id, string Name, string? Phone, decimal Due,
    decimal Days0To15 = 0, decimal Days16To30 = 0, decimal Over30 = 0, int OldestDays = 0,
    string? Gstin = null, string? Address = null, int? PaymentDays = null, bool IsActive = true);

/// <summary><see cref="Balance"/>: what was owed after this line, oldest line first.</summary>
public sealed record SupplierEntryDto(
    Guid Id, decimal Amount, Guid? PurchaseId, string? Note, DateTimeOffset OccurredAt, bool Cancelled = false,
    SupplierEntryKind Kind = SupplierEntryKind.Other, PaymentMode? Mode = null, string? Reference = null, decimal Balance = 0, DateOnly? DueOn = null);

/// <summary><see cref="Amount"/>: what was owed when the shop started here; minus when the supplier owed the shop.</summary>
public sealed record SupplierOpeningRequest(Guid Id, decimal Amount, DateOnly? AsOf = null);

public sealed record PurchaseLineDto(string Name, decimal Quantity, decimal FreeQuantity, decimal UnitCost, decimal DiscountPercent, decimal? GstRate, decimal Taxable, decimal Gst);

/// <summary>A supplier's bill as it was entered, line by line.</summary>
public sealed record PurchaseDetailDto(Guid Id, string? BillNo, decimal Total, decimal Extra, DateTimeOffset OccurredAt, bool Cancelled, IReadOnlyList<PurchaseLineDto> Lines);

public sealed record SupplierDetail(SupplierDto Supplier, IReadOnlyList<SupplierEntryDto> Entries);

/// <summary>
/// <see cref="UnitCost"/>: the rate (PTR) per unit. Medicines also send the batch, expiry month ("2027-05") and MRP;
/// <see cref="FreeQuantity"/> and <see cref="DiscountPercent"/> are the distributor's scheme and trade discount.
/// </summary>
public sealed record PurchaseLineRequest(
    Guid ProductId, decimal Quantity, decimal UnitCost, decimal FreeQuantity = 0, decimal DiscountPercent = 0, string? BatchNo = null, string? ExpiryMonth = null, decimal? Mrp = null,
    decimal? GstRate = null, decimal? Bags = null);

/// <summary><see cref="Id"/> is made by the app, so a repeated tap saves once.</summary>
public sealed record PurchaseRequest(Guid Id, Guid SupplierId, string? BillNo, IReadOnlyList<PurchaseLineRequest>? Lines, decimal PaidAmount, PaymentMode? PaymentMode, DateOnly? BillDate = null, decimal Extra = 0, Guid? OrderId = null,
    IReadOnlyList<Guid>? ReceiptIds = null);

public sealed record SupplierPaymentRequest(Guid Id, decimal Amount, PaymentMode? PaymentMode, string? Reference = null, DateOnly? PaidOn = null);

/// <summary>
/// Buying: a supplier's bill adds stock and moves each item's cost to the moving average; money paid is also a
/// "stock bought" line in the kharcha, so the cash book stays right. Owner only: it shows costs.
/// </summary>
public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSuppliers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/suppliers").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/{id:guid}/pay", PayAsync);
        group.MapPost("/{id:guid}/opening", OpeningAsync);
        group.MapPost("/purchases", PurchaseAsync);
        group.MapGet("/purchases/{id:guid}", GetPurchaseAsync);
        group.MapPut("/{id:guid}", ChangeAsync);
        group.MapPost("/purchases/{id:guid}/cancel", CancelPurchaseAsync);
        return app;
    }

    private static async Task<IReadOnlyList<SupplierDto>> ListAsync(AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        await SupplierDuesAsync(db, clock, ct);

    public static async Task<IReadOnlyList<SupplierDto>> SupplierDuesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var suppliers = await db.Suppliers.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Phone, x.Gstin, x.Address, x.PaymentDays, x.IsActive }).ToListAsync(ct);
        // A cancelled bill isn't owed, so it can't be the old bill that makes a supplier look overdue.
        var cancelled = (await db.Purchases.AsNoTracking().Where(p => p.CancelledAt != null).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        var entries = await db.SupplierEntries.AsNoTracking().Select(e => new { e.SupplierId, e.Amount, e.OccurredAt, e.PurchaseId }).ToListAsync(ct);
        var byId = entries.ToLookup(e => e.SupplierId);
        var now = clock.GetUtcNow();
        return suppliers.Select(x =>
        {
            var mine = byId[x.Id].ToList();
            var due = mine.Sum(e => e.Amount);
            decimal d0 = 0, d16 = 0, d30 = 0;
            var oldest = 0;
            // Walk the bills from newest back until what is owed is covered: those are the unpaid ones.
            var left = due;
            foreach (var bill in mine.Where(e => e.Amount > 0 && !(e.PurchaseId is { } billId && cancelled.Contains(billId))).OrderByDescending(e => e.OccurredAt))
            {
                if (left <= 0) break;
                var part = Math.Min(bill.Amount, left);
                left -= part;
                var days = (int)(now - bill.OccurredAt).TotalDays;
                oldest = Math.Max(oldest, days);
                if (days <= 15) d0 += part; else if (days <= 30) d16 += part; else d30 += part;
            }

            return new SupplierDto(x.Id, x.Name, x.Phone, due, d0, d16, d30, oldest, x.Gstin, x.Address, x.PaymentDays, x.IsActive);
        }).ToList();
    }

    private static async Task<IResult> AddAsync(SupplierRequest request, AppDbContext db, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (request.Id == Guid.Empty || name is not { Length: >= 2 and <= 120 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Enter the supplier's name."] });
        }

        if (DetailsProblem(request) is { } problem)
        {
            return problem;
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.Id, ct))
        {
            var supplier = new Supplier { Id = request.Id, Name = name, Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim() };
            ApplyDetails(supplier, request);
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(new SupplierDto(request.Id, name, request.Phone, 0));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
        {
            return TypedResults.NotFound();
        }

        // The opening balance is where the khata starts, whatever date it carries or bills dated before it.
        var rows = await db.SupplierEntries.AsNoTracking().Where(e => e.SupplierId == id).OrderBy(e => e.Kind == SupplierEntryKind.Opening ? 0 : 1).ThenBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt)
            .Select(e => new { Entry = e, Cancelled = e.Amount > 0 && db.Purchases.Any(p => p.Id == e.PurchaseId && p.CancelledAt != null) }).ToListAsync(ct);
        var running = 0m;
        var entries = rows.Select(r =>
        {
            var e = r.Entry;
            running += e.Amount;
            var due = e.Kind == SupplierEntryKind.Bill && !r.Cancelled && supplier.PaymentDays is { } days
                ? DateOnly.FromDateTime(e.OccurredAt.ToOffset(IndiaTime.Offset).DateTime).AddDays(days) : (DateOnly?)null;
            return new SupplierEntryDto(e.Id, e.Amount, e.PurchaseId, e.Note, e.OccurredAt, r.Cancelled, e.Kind, e.Mode, e.Reference, running, due);
        }).Reverse().ToList();
        return TypedResults.Ok(new SupplierDetail(new SupplierDto(id, supplier.Name, supplier.Phone, running, Gstin: supplier.Gstin, Address: supplier.Address, PaymentDays: supplier.PaymentDays, IsActive: supplier.IsActive), entries));
    }

    /// <summary>A bill as it was entered, so it can be opened and checked later.</summary>
    private static async Task<IResult> GetPurchaseAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var purchase = await db.Purchases.AsNoTracking().Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null)
        {
            return TypedResults.NotFound();
        }

        var ids = purchase.Lines.Select(l => l.ProductId).Distinct().ToList();
        var names = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return TypedResults.Ok(new PurchaseDetailDto(purchase.Id, purchase.BillNo, purchase.Total, purchase.ExtraCharges, purchase.OccurredAt, purchase.CancelledAt is not null,
            purchase.Lines.Select(l => new PurchaseLineDto(names.GetValueOrDefault(l.ProductId) ?? "", l.Quantity, l.FreeQuantity, l.UnitCost, l.DiscountPercent, l.GstRate, l.Taxable, l.Gst)).ToList()));
    }

    /// <summary>What was owed when the shop started using the app: a line of its own, never a bill, so purchases and stock stay true. Once per supplier.</summary>
    private static async Task<IResult> OpeningAsync(Guid id, SupplierOpeningRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.SupplierEntries.AnyAsync(e => e.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        if (request.Id == Guid.Empty || request.Amount == 0 || Math.Abs(request.Amount) > Money.MaxAmount || decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Invalid("Amount", "Enter what was owed to this supplier when you started.");
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        if (await db.SupplierEntries.AnyAsync(e => e.SupplierId == id && e.Kind == SupplierEntryKind.Opening, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This supplier already has an opening balance.");
        }

        if (OnDate(request.AsOf, clock) is not { } at)
        {
            return Invalid("AsOf", "The date can't be in the future.");
        }

        // It comes before everything else, or the running balance would start in the wrong place.
        var first = await db.SupplierEntries.Where(e => e.SupplierId == id).MinAsync(e => (DateTimeOffset?)e.OccurredAt, ct);
        var when = first is { } earliest && earliest <= at ? earliest.AddSeconds(-1) : at;
        db.SupplierEntries.Add(new SupplierEntry { Id = request.Id, SupplierId = id, Kind = SupplierEntryKind.Opening, Amount = request.Amount, OccurredAt = when, CreatedByUserId = user.UserId()!.Value });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>A date on a supplier's paper, as a moment; today is now. Null when it's in the future.</summary>
    private static DateTimeOffset? OnDate(DateOnly? date, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var today = IndiaTime.Today(clock);
        if (date is not { } day || day == today)
        {
            return now;
        }

        return day > today ? null : new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), IndiaTime.Offset).ToUniversalTime();
    }

    private static IResult? DetailsProblem(SupplierRequest request)
    {
        var gstin = request.Gstin?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(gstin) && !GstExport.IsValidGstin(gstin))
        {
            return Invalid("Gstin", "This GSTIN isn't valid: check each letter and digit (the last one is a check digit).");
        }

        if (request.Address?.Trim().Length > 200)
        {
            return Invalid("Address", "Keep the address under 200 letters.");
        }

        return request.PaymentDays is < 0 or > 365 ? Invalid("PaymentDays", "Enter the days to pay, from 0 to 365.") : null;
    }

    private static void ApplyDetails(Supplier supplier, SupplierRequest request)
    {
        static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        supplier.Gstin = request.Gstin is null ? supplier.Gstin : Clean(request.Gstin)?.ToUpperInvariant();
        supplier.Address = request.Address is null ? supplier.Address : Clean(request.Address);
        supplier.PaymentDays = request.PaymentDays is null ? supplier.PaymentDays : request.PaymentDays == 0 ? null : request.PaymentDays;
        supplier.IsActive = request.IsActive ?? supplier.IsActive;
    }

    private static async Task<IResult> PurchaseAsync(PurchaseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Purchases.AnyAsync(p => p.Id == request.Id, ct))
        {
            return TypedResults.Ok(new { request.Id });
        }

        var lines = request.Lines ?? [];
        var products = await db.Products.Where(p => lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var total = lines.Sum(LineTotal) + request.Extra;
        // A product can come in two batches on one bill, but not the same batch twice.
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200 || lines.Select(l => (l.ProductId, l.BatchNo?.Trim().ToUpperInvariant())).Distinct().Count() != lines.Count
            || lines.Any(l => !products.ContainsKey(l.ProductId) || !Money.IsValidQuantity(l.Quantity) || l.UnitCost < 0 || l.UnitCost > Money.MaxAmount || decimal.Round(l.UnitCost, 2) != l.UnitCost
                || l.FreeQuantity < 0 || (l.FreeQuantity > 0 && !Money.IsValidQuantity(l.FreeQuantity)) || l.DiscountPercent is < 0 or >= 100 || l.GstRate is < 0 or > 40)
            || Math.Abs(request.Extra) > Money.MaxAmount || decimal.Round(request.Extra, 2) != request.Extra
            || total <= 0 || request.PaidAmount < 0 || request.PaidAmount > total || !await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = ["Check the supplier, items, quantities, costs and amount paid."] });
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        foreach (var l in lines.Where(l => !string.IsNullOrWhiteSpace(l.BatchNo)))
        {
            var name = products[l.ProductId].Name;
            if (l.BatchNo!.Trim().Length > 30) return Invalid("Lines", $"{name}: the batch number is too long.");
            if (Batches.ParseExpiry(l.ExpiryMonth) is not { } expiry || expiry < today) return Invalid("Lines", $"{name}: enter an expiry month that hasn't passed.");
            if (l.Mrp is not { } mrp || mrp <= 0 || !Money.IsValidAmount(mrp)) return Invalid("Lines", $"{name}: enter the MRP for batch {l.BatchNo.Trim()}.");
        }

        if (OnDate(request.BillDate, clock) is not { } billDate)
        {
            return Invalid("BillDate", "The bill date can't be in the future.");
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var billNo = request.BillNo?.Trim();
        db.Purchases.Add(new Purchase
        {
            Id = request.Id, SupplierId = request.SupplierId, BillNo = string.IsNullOrEmpty(billNo) ? null : billNo[..Math.Min(billNo.Length, 40)], Total = total, ExtraCharges = request.Extra, OccurredAt = billDate, CreatedByUserId = userId,
            PurchaseOrderId = request.OrderId,
        });
        db.SupplierEntries.Add(new SupplierEntry { SupplierId = request.SupplierId, Kind = SupplierEntryKind.Bill, Amount = total, PurchaseId = request.Id, Note = billNo?[..Math.Min(billNo.Length, 40)], OccurredAt = billDate, CreatedByUserId = userId });
        var purchase = db.Purchases.Local.Single(p => p.Id == request.Id);
        var added = new Dictionary<Guid, decimal>();
        // Goods already counted in on receipts of this supplier: the bill takes them over and adds only what is more.
        var receiptIds = request.ReceiptIds?.Distinct().ToList() ?? [];
        var receipts = receiptIds.Count == 0 ? [] : await db.GoodsReceipts.Where(r => receiptIds.Contains(r.Id)).ToListAsync(ct);
        if (receipts.Count != receiptIds.Count || receipts.Any(r => r.SupplierId != request.SupplierId || r.PurchaseId is not null || r.CancelledAt is not null))
        {
            return Invalid("ReceiptIds", "Those goods receipts aren't this supplier's open ones.");
        }

        var alreadyIn = receipts.SelectMany(GoodsReceipts.LinesOf).GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        // A receipt closes with its bill, so every item on it must be on the bill: otherwise those goods would never be costed.
        if (alreadyIn.Keys.FirstOrDefault(p => lines.All(l => l.ProductId != p)) is { } missing && missing != Guid.Empty)
        {
            var name = receipts.SelectMany(GoodsReceipts.LinesOf).First(l => l.ProductId == missing).Name;
            return Invalid("ReceiptIds", $"{name} came on the goods receipt but isn't on this bill. Add it, or bill it on its own receipt.");
        }
        foreach (var receipt in receipts)
        {
            receipt.PurchaseId = request.Id;
        }
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var units = line.Quantity + line.FreeQuantity;
            var fromReceipt = Math.Min(units, alreadyIn.GetValueOrDefault(line.ProductId));
            alreadyIn[line.ProductId] = alreadyIn.GetValueOrDefault(line.ProductId) - fromReceipt;
            // What one unit really cost: the line's value before GST (the GST comes back as input credit) over paid and free units together.
            var unitCost = decimal.Round(Taxable(line) / units, 2, MidpointRounding.AwayFromZero);
            // Units from a receipt are already on the shelf but weren't costed: they take this bill's cost, not the old one.
            var held = Math.Max(0, product.Stock + added.GetValueOrDefault(line.ProductId) - fromReceipt - alreadyIn.GetValueOrDefault(line.ProductId));
            product.CostPrice = product.CostPrice is { } cost && held > 0
                ? decimal.Round(((held * cost) + (units * unitCost)) / (held + units), 2)
                : unitCost;
            ProductBatch? batch = null;
            if (!string.IsNullOrWhiteSpace(line.BatchNo))
            {
                await Batches.FollowMrpAsync(db, product, line.Mrp!.Value, today, ct);
                batch = await Batches.AddToBatchAsync(db, line.ProductId, line.BatchNo.Trim().ToUpperInvariant(), Batches.ParseExpiry(line.ExpiryMonth)!.Value, line.Mrp!.Value, unitCost, units, now, ct);
            }

            purchase.Lines.Add(new PurchaseLine
            {
                ProductId = line.ProductId, Quantity = line.Quantity, UnitCost = line.UnitCost, FreeQuantity = line.FreeQuantity, DiscountPercent = line.DiscountPercent, BatchId = batch?.Id,
                GstRate = line.GstRate, Taxable = Taxable(line), Gst = LineTotal(line) - Taxable(line), FromReceipt = fromReceipt,
                Bags = line.Bags is > 0 and <= 100_000 ? line.Bags : null,
            });
            if (units - fromReceipt > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    RefType = "purchase", RefId = purchase.Id, ProductId = line.ProductId, Kind = StockMovementKind.Purchase, Quantity = units - fromReceipt, CostPrice = unitCost,
                    // Bags already counted in on a receipt aren't counted again: only the share that came with this bill.
                    Bags = line.Bags is > 0 and <= 100_000 ? decimal.Round(line.Bags.Value * (units - fromReceipt) / units, 3) : null,
                    OccurredAt = now, CreatedByUserId = userId, Note = batch is null ? "Supplier bill" : $"Supplier bill · batch {batch.BatchNo}",
                });
            }

            added[line.ProductId] = added.GetValueOrDefault(line.ProductId) + units - fromReceipt;
        }

        // Goods against an order: what came counts off what is still to come.
        // Every arrival is one row: the bill's own share (beyond its receipts) arrives now; receipt rows get billed.
        await Arrivals.AddAsync(db, request.SupplierId, null, request.Id, request.OrderId,
            lines.GroupBy(l => l.ProductId).Select(g =>
            {
                var units = g.Sum(l => l.Quantity + l.FreeQuantity);
                var own = Math.Max(0, added.GetValueOrDefault(g.Key));
                var bags = g.Sum(l => l.Bags ?? 0);
                return (g.Key, own, 0m, bags > 0 && units > 0 ? decimal.Round(bags * own / units, 3) : (decimal?)null, (decimal?)g.First().UnitCost);
            }), now, ct);
        if (receipts.Count > 0)
        {
            var rates = lines.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.First().UnitCost);
            var rows = await db.ArrivalLines.Where(a => a.ReceiptId != null && receiptIds.Contains(a.ReceiptId.Value)).ToListAsync(ct);
            foreach (var row in rows)
            {
                (row.PurchaseId, row.Rate) = (request.Id, rates.TryGetValue(row.ProductId, out var rate) ? rate : null);
            }
        }

        // Receipts already counted against this order are not counted twice; the bill adds only what is more.
        if (request.OrderId is { } orderId)
        {
            var counted = receipts.Where(r => r.PurchaseOrderId == orderId).SelectMany(GoodsReceipts.LinesOf).GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            var goods = lines.GroupBy(l => l.ProductId).Select(g => (ProductId: g.Key, Units: Math.Max(0, g.Sum(l => l.Quantity + l.FreeQuantity) - counted.GetValueOrDefault(g.Key)))).Where(g => g.Units > 0).ToList();
            if (goods.Count > 0 && await PurchaseOrders.ReceiveAsync(db, orderId, request.SupplierId, goods, 1, clock, ct) is { } orderProblem)
            {
                return Invalid("OrderId", orderProblem);
            }
        }

        if (request.PaidAmount > 0)
        {
            AddPayment(db, request.SupplierId, request.PaidAmount, request.PaymentMode ?? PaymentMode.Cash, null, request.Id, now, userId, Guid.CreateVersion7());
        }

        await db.SaveChangesAsync(ct);
        var ids = added.Keys.Order().ToArray();
        var quantities = ids.Select(i => added[i]).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock + v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(new { request.Id });
    }

    /// <summary>The line's value before GST: paid units at the rate, less the trade discount. Free units cost nothing.</summary>
    private static decimal Taxable(PurchaseLineRequest l) =>
        decimal.Round(l.Quantity * l.UnitCost * (100 - l.DiscountPercent) / 100, 2, MidpointRounding.AwayFromZero);

    /// <summary>What the line costs on the bill: its value plus the GST charged on it.</summary>
    private static decimal LineTotal(PurchaseLineRequest l) =>
        Taxable(l) + (l.GstRate is { } rate ? decimal.Round(Taxable(l) * rate / 100, 2, MidpointRounding.AwayFromZero) : 0);

    private static async Task<IResult> ChangeAsync(Guid id, SupplierRequest request, AppDbContext db, CancellationToken ct)
    {
        if (DetailsProblem(request) is { } problem)
        {
            return problem;
        }

        var name = request.Name?.Trim();
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
        {
            return TypedResults.NotFound();
        }

        if (name is not { Length: >= 2 and <= 120 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Enter the supplier's name."] });
        }

        (supplier.Name, supplier.Phone) = (name, string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim());
        ApplyDetails(supplier, request);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Takes back a bill entered by mistake: its stock goes out again and the amount owed comes off. Money already paid
    /// stays paid (the supplier owes it back, shown as a credit). Item costs keep their average. Safe to repeat.
    /// </summary>
    private static async Task<IResult> CancelPurchaseAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var purchase = await db.Purchases.AsNoTracking().Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null)
        {
            return TypedResults.NotFound();
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Purchases.Where(p => p.Id == id && p.CancelledAt == null).ExecuteUpdateAsync(s => s.SetProperty(p => p.CancelledAt, now), ct) == 0)
        {
            return TypedResults.NoContent();
        }

        if (purchase.PurchaseOrderId is { } orderId)
        {
            // Receipts stay counted on the order (they are still in stock); only the bill's own share goes back.
            var onReceipts = (await db.GoodsReceipts.AsNoTracking().Where(r => r.PurchaseId == id && r.PurchaseOrderId == orderId).ToListAsync(ct))
                .SelectMany(GoodsReceipts.LinesOf).GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            var back = purchase.Lines.GroupBy(l => l.ProductId).Select(g => (g.Key, Math.Max(0, g.Sum(l => l.Quantity + l.FreeQuantity) - onReceipts.GetValueOrDefault(g.Key)))).ToList();
            await PurchaseOrders.ReceiveAsync(db, orderId, purchase.SupplierId, back, -1, clock, ct);
        }

await db.GoodsReceipts.Where(r => r.PurchaseId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.PurchaseId, (Guid?)null), ct);
        // The bill's own arrivals are undone; goods that came on receipts are still here, waiting for a bill again.
        await db.ArrivalLines.Where(a => a.PurchaseId == id && a.ReceiptId == null).ExecuteUpdateAsync(s => s.SetProperty(a => a.CancelledAt, now), ct);
        await db.ArrivalLines.Where(a => a.PurchaseId == id && a.ReceiptId != null).ExecuteUpdateAsync(s => s.SetProperty(a => a.PurchaseId, (Guid?)null).SetProperty(a => a.Rate, (decimal?)null), ct);
        db.SupplierEntries.Add(new SupplierEntry { SupplierId = purchase.SupplierId, Kind = SupplierEntryKind.BillCancelled, Amount = -purchase.Total, PurchaseId = id, Note = "Bill cancelled", OccurredAt = now, CreatedByUserId = userId });
        foreach (var line in purchase.Lines)
        {
            db.StockMovements.Add(new StockMovement
            {
                RefType = "purchase", RefId = id, ProductId = line.ProductId, Kind = StockMovementKind.Purchase, CostPrice = line.UnitCost, Quantity = -(line.Quantity + line.FreeQuantity - line.FromReceipt), OccurredAt = now,
                Bags = line.Bags is { } b && line.Quantity + line.FreeQuantity > 0 ? -decimal.Round(b * (line.Quantity + line.FreeQuantity - line.FromReceipt) / (line.Quantity + line.FreeQuantity), 3) : null,
                CreatedByUserId = userId, Note = "Supplier bill cancelled",
            });
            if (line.BatchId is { } batchId)
            {
                // Whatever of the batch is left goes back; units already sold stay sold.
                var units = line.Quantity + line.FreeQuantity;
                await db.ProductBatches.Where(b => b.Id == batchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.Quantity, b => b.Quantity > units ? b.Quantity - units : 0), ct);
            }
        }

        await db.SaveChangesAsync(ct);
        var byProduct = purchase.Lines.GroupBy(l => l.ProductId).OrderBy(g => g.Key).ToList();
        var ids = byProduct.Select(g => g.Key).ToArray();
        var quantities = byProduct.Select(g => g.Sum(l => l.Quantity + l.FreeQuantity - l.FromReceipt)).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock - v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> PayAsync(Guid id, SupplierPaymentRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.SupplierEntries.AnyAsync(e => e.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        if (request.Id == Guid.Empty || request.Amount <= 0 || request.Amount > Money.MaxAmount || decimal.Round(request.Amount, 2) != request.Amount || !await db.Suppliers.AnyAsync(s => s.Id == id, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = ["Enter the amount paid."] });
        }

        if (OnDate(request.PaidOn, clock) is not { } paidOn)
        {
            return Invalid("PaidOn", "The payment date can't be in the future.");
        }

        var reference = request.Reference?.Trim();
        AddPayment(db, id, request.Amount, request.PaymentMode ?? PaymentMode.Cash, string.IsNullOrEmpty(reference) ? null : reference[..Math.Min(reference.Length, 40)], null, paidOn, user.UserId()!.Value, request.Id);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Money out: on the supplier's khata, and as stock bought in the kharcha so the cash book shows it.</summary>
    private static void AddPayment(AppDbContext db, Guid supplierId, decimal amount, PaymentMode mode, string? reference, Guid? purchaseId, DateTimeOffset at, Guid userId, Guid entryId)
    {
        db.SupplierEntries.Add(new SupplierEntry { Id = entryId, SupplierId = supplierId, Kind = SupplierEntryKind.Payment, Amount = -amount, Mode = mode, Reference = reference, PurchaseId = purchaseId, OccurredAt = at, CreatedByUserId = userId });
        db.Expenses.Add(new Expense { Amount = amount, Category = ExpenseCategory.Purchase, Mode = mode, Note = "Supplier payment", OccurredAt = at, CreatedByUserId = userId });
    }
}
