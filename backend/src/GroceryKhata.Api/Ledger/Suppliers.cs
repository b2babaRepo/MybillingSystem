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

    /// <summary>How the lot's charges were spread over its items; null when there were none to spread.</summary>
    public CostSplit? CostSplit { get; set; }

    /// <summary>The bill's own extra (freight on the bill) was spread into the items' cost.</summary>
    public bool ExtraInCost { get; set; }

    /// <summary>Charges paid to others to bring this lot in (transporter, hamali, commission), spread into cost.</summary>
    public List<PurchaseCharge> Charges { get; set; } = [];

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

    /// <summary>What one unit really cost on the shelf: the bill rate before GST plus its share of the lot's charges.</summary>
    public decimal? LandedUnitCost { get; set; }

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

    /// <summary>On a credit note: the GST inside it, which takes back the input tax claimed on those goods.</summary>
    public decimal? Gst { get; set; }
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
    IReadOnlyList<Guid>? ReceiptIds = null, IReadOnlyList<LandedChargeRequest>? Charges = null, bool ExtraInCost = false, CostSplit Split = CostSplit.Value, Guid? GodownId = null);

/// <summary>How a lot's charges are shared over its items: by value (default), by units, or by bags.</summary>
public enum CostSplit
{
    Value,
    Quantity,
    Bags,

    /// <summary>By kilograms: a 50 kg bag carries twice a 25 kg bag's freight. Lines whose weight isn't known carry none.</summary>
    Weight,
}

/// <summary>
/// A charge to bring the goods in that isn't on the supplier's bill: lorry freight, hamali (loading), mandi commission.
/// <see cref="PaidBy"/> cash or UPI writes it in the kharcha as stock bought (out of profit, since it is in the goods' cost);
/// null when it is paid some other way or later.
/// </summary>
public sealed record LandedChargeRequest(string? Label, decimal Amount, PaymentMode? PaidBy = null);

public sealed class PurchaseCharge : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid PurchaseId { get; set; }

    public required string Label { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The kharcha line it was paid from, struck if the bill is cancelled.</summary>
    public Guid? ExpenseId { get; set; }
}

/// <summary>
/// <see cref="Sold"/>, <see cref="Revenue"/> and <see cref="Profit"/>: what of this lot has really sold (first in, first
/// out), what it fetched before GST, and that less its landed cost. <see cref="Left"/>: units of it not yet gone.
/// </summary>
public sealed record LotLineDto(Guid ProductId, string Name, decimal Units, decimal BillRate, decimal ChargePerUnit, decimal LandedCost, decimal? SellingPrice, decimal? MarginPerUnit, decimal? MarginPercent, decimal? LotMargin,
    decimal Sold = 0, decimal Revenue = 0, decimal Profit = 0, decimal Left = 0);

/// <summary>One supplier bill seen as a lot: what its goods really cost landed, and the margin at today's selling prices.</summary>
public sealed record LotDto(Guid Id, string? BillNo, Guid SupplierId, string Supplier, DateTimeOffset OccurredAt, bool Cancelled, CostSplit? Split, decimal GoodsValue,
    decimal Charges, IReadOnlyList<LandedChargeDto> ChargeLines, decimal LandedTotal, decimal? ExpectedMargin, IReadOnlyList<LotLineDto> Lines,
    decimal Revenue = 0, decimal Profit = 0);

public sealed record LandedChargeDto(string Label, decimal Amount, bool Paid);

/// <summary>Charges that came to light after the bill was saved (the transporter's bill came later): added to the lot's cost.</summary>
public sealed record AddChargesRequest(IReadOnlyList<LandedChargeRequest>? Charges, CostSplit Split = CostSplit.Value);

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
        group.MapGet("/purchases/{id:guid}/landed", LotAsync);
        group.MapPost("/purchases/{id:guid}/charges", AddChargesAsync);
        group.MapGet("/lots", LotsAsync);
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

    /// <summary>
    /// Adds charges to a saved bill (an older one too). Each item's landed cost goes up by its share, and so does the cost
    /// of what is still in stock: its average cost rises by the share on the units still held. The stock ledger is
    /// append-only and is left as it was; both changes are kept in the audit log.
    /// </summary>
    private static async Task<IResult> AddChargesAsync(Guid id, AddChargesRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var charges = request.Charges ?? [];
        if (charges.Count is 0 or > 10 || charges.Any(c => c.Amount <= 0 || c.Amount > Money.MaxAmount || decimal.Round(c.Amount, 2) != c.Amount || string.IsNullOrWhiteSpace(c.Label)
            || c.PaidBy is not (null or PaymentMode.Cash or PaymentMode.Upi)) || !Enum.IsDefined(request.Split))
        {
            return Invalid("Charges", "Give each charge a name and an amount, paid by cash, UPI or not yet.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var purchase = await db.Purchases.FromSql($"SELECT * FROM purchases WHERE id = {id} FOR UPDATE").Include(p => p.Lines).SingleOrDefaultAsync(ct);
        if (purchase is null)
        {
            return TypedResults.NotFound();
        }

        if (purchase.CancelledAt is not null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This bill is cancelled.");
        }

        // A bill in a closed month keeps its cost as filed: a late charge for it goes in the kharcha of the open month.
        await Books.EnsureOpenAsync(db, tenant, purchase.OccurredAt, ct);

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        var pool = charges.Sum(c => c.Amount);
        decimal Units(PurchaseLine l) => l.Quantity + l.FreeQuantity;
        var lineIds = purchase.Lines.Select(l => l.ProductId).Distinct().ToList();
        var kinds = await db.Products.AsNoTracking().Where(p => lineIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Unit, p.WeightKg }, ct);
        if (request.Split == CostSplit.Quantity && purchase.Lines.Select(l => kinds[l.ProductId].Unit).Distinct().Count() > 1)
        {
            return Invalid("Split", "These items are in different units (kg and pieces): share by weight, bags or value.");
        }

        decimal Weight(PurchaseLine l) => request.Split switch
        {
            CostSplit.Quantity => Units(l),
            CostSplit.Weight => Catalogue.Units.Kg(kinds[l.ProductId].Unit, kinds[l.ProductId].WeightKg, Units(l)) ?? 0,
            CostSplit.Bags when purchase.Lines.Any(x => x.Bags > 0) => l.Bags ?? 0,
            _ => l.Taxable,
        };
        var weights = purchase.Lines.Sum(Weight);
        if (weights <= 0)
        {
            return Invalid("Split", "Nothing on this bill to share the charges over that way.");
        }

        var ids = purchase.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var line in purchase.Lines.Where(l => Units(l) > 0))
        {
            var perUnit = pool * Weight(line) / weights / Units(line);
            line.LandedUnitCost = decimal.Round((line.LandedUnitCost ?? line.Taxable / Units(line)) + perUnit, 2);
            // Only the units still on hand carry the extra into today's cost; what was sold already went at the old cost.
            var product = products[line.ProductId];
            if (product.CostPrice is { } cost && product.Stock > 0)
            {
                product.CostPrice = decimal.Round(cost + (perUnit * Math.Min(product.Stock, Units(line)) / product.Stock), 2);
            }
        }

        purchase.CostSplit ??= request.Split;
        foreach (var charge in charges)
        {
            var label = charge.Label!.Trim()[..Math.Min(60, charge.Label.Trim().Length)];
            Guid? expenseId = null;
            if (charge.PaidBy is { } mode)
            {
                expenseId = Guid.CreateVersion7();
                db.Expenses.Add(new Expense
                {
                    Id = expenseId.Value, Amount = charge.Amount, Category = ExpenseCategory.Purchase, Mode = mode, OccurredAt = now, CreatedByUserId = userId,
                    Note = purchase.BillNo is { } no ? $"{label} · bill {no}" : label,
                });
            }

            db.PurchaseCharges.Add(new PurchaseCharge { PurchaseId = id, Label = label, Amount = charge.Amount, ExpenseId = expenseId });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>The first item on a supplier bill no longer all in stock (or whose batch has been drawn on), or null.</summary>
    private static async Task<string?> GoneAsync(AppDbContext db, Purchase purchase, CancellationToken ct)
    {
        var ids = purchase.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Stock }, ct);
        foreach (var g in purchase.Lines.GroupBy(l => l.ProductId))
        {
            var own = g.Sum(l => l.Quantity + l.FreeQuantity - l.FromReceipt);
            if (products.TryGetValue(g.Key, out var p) && own > 0 && p.Stock < own)
            {
                return p.Name;
            }
        }

        var batchIds = purchase.Lines.Where(l => l.BatchId != null).Select(l => l.BatchId!.Value).ToList();
        var batches = await db.ProductBatches.AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Quantity, ct);
        foreach (var line in purchase.Lines.Where(l => l.BatchId != null))
        {
            if (batches.GetValueOrDefault(line.BatchId!.Value) < line.Quantity + line.FreeQuantity)
            {
                return products.GetValueOrDefault(line.ProductId)?.Name ?? "An item";
            }
        }

        return null;
    }

    /// <summary>One bill as a lot: each item's bill rate, its share of the charges, what it landed at, and the margin at today's price.</summary>
    private static async Task<IResult> LotAsync(Guid id, AppDbContext db, CancellationToken ct) =>
        (await LotsOfAsync(db, db.Purchases.AsNoTracking().Where(p => p.Id == id), ct)).SingleOrDefault() is { } lot ? TypedResults.Ok(lot) : TypedResults.NotFound();

    /// <summary>Recent lots, newest first, for one item or supplier or all: to compare what goods really cost landed.</summary>
    private static async Task<IReadOnlyList<LotDto>> LotsAsync(Guid? productId, Guid? supplierId, AppDbContext db, CancellationToken ct) =>
        await LotsOfAsync(db, db.Purchases.AsNoTracking()
            .Where(p => p.CancelledAt == null && (supplierId == null || p.SupplierId == supplierId) && (productId == null || p.Lines.Any(l => l.ProductId == productId)))
            .OrderByDescending(p => p.OccurredAt).Take(50), ct, productId);

    private static async Task<List<LotDto>> LotsOfAsync(AppDbContext db, IQueryable<Purchase> query, CancellationToken ct, Guid? onlyProduct = null)
    {
        var purchases = await query.Include(p => p.Lines).Include(p => p.Charges).AsSplitQuery().ToListAsync(ct);
        var ids = purchases.SelectMany(p => p.Lines).Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Name, p.SellingPrice }, ct);
        var supplierIds = purchases.Select(p => p.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var fifo = await FifoAsync(db, ids, ct);
        return purchases.OrderByDescending(p => p.OccurredAt).Select(p =>
        {
            var lines = p.Lines.Where(l => onlyProduct == null || l.ProductId == onlyProduct).Select(l =>
            {
                var units = l.Quantity + l.FreeQuantity;
                var billRate = units > 0 ? decimal.Round(l.Taxable / units, 2) : 0;
                var landed = l.LandedUnitCost ?? billRate;
                var item = products.GetValueOrDefault(l.ProductId);
                decimal? price = item?.SellingPrice is > 0 ? item.SellingPrice : null;
                decimal? margin = price is { } sp ? sp - landed : null;
                // A product twice on one bill (two batches) shares the lot's figures by units.
                var lotUnits = p.Lines.Where(x => x.ProductId == l.ProductId).Sum(x => x.Quantity + x.FreeQuantity);
                var part = lotUnits > 0 ? units / lotUnits : 0;
                var used = fifo.GetValueOrDefault((p.Id, l.ProductId));
                var (sold, revenue) = (decimal.Round(used.Sold * part, 3), decimal.Round(used.Revenue * part, 2));
                return new LotLineDto(l.ProductId, item?.Name ?? "", units, billRate, landed - billRate, landed, price, margin,
                    margin is { } m && price is { } s2 ? decimal.Round(m * 100 / s2, 1) : null, margin is { } m2 ? decimal.Round(m2 * units, 2) : null,
                    sold, revenue, decimal.Round(revenue - (landed * sold), 2), p.CancelledAt is null ? Math.Max(0, decimal.Round(units - (used.Gone * part), 3)) : 0);
            }).ToList();
            var charges = p.Charges.Sum(c => c.Amount) + (p.ExtraInCost ? p.ExtraCharges : 0);
            var chargeLines = p.Charges.Select(c => new LandedChargeDto(c.Label, c.Amount, c.ExpenseId != null)).ToList();
            if (p.ExtraInCost)
            {
                chargeLines.Insert(0, new LandedChargeDto("On the bill", p.ExtraCharges, false));
            }

            return new LotDto(p.Id, p.BillNo, p.SupplierId, suppliers.GetValueOrDefault(p.SupplierId) ?? "", p.OccurredAt, p.CancelledAt != null, p.CostSplit,
                p.Lines.Sum(l => l.Taxable), charges, chargeLines, decimal.Round(lines.Sum(l => l.LandedCost * l.Units), 2),
                lines.All(l => l.LotMargin is not null) ? lines.Sum(l => l.LotMargin!.Value) : null, lines, lines.Sum(l => l.Revenue), lines.Sum(l => l.Profit));
        }).ToList();
    }

    /// <summary>
    /// Matches what left each item to the lots it came in, oldest first (first in, first out). A lot is a supplier bill
    /// (goods counted in on a receipt belong to the bill that took the receipt); opening stock and stock found are lots
    /// of their own. Sales bring their bill's price before GST; damage and other losses use up stock and bring nothing.
    /// </summary>
    private static async Task<Dictionary<(Guid Purchase, Guid Product), (decimal Sold, decimal Revenue, decimal Gone)>> FifoAsync(AppDbContext db, List<Guid> productIds, CancellationToken ct)
    {
        var moves = await db.StockMovements.AsNoTracking().Where(m => productIds.Contains(m.ProductId))
            .Select(m => new { m.Id, m.ProductId, m.Kind, m.Quantity, m.RefType, m.RefId, m.BillId, m.OccurredAt }).ToListAsync(ct);
        var receiptIds = moves.Where(m => m.RefType == "receipt" && m.RefId != null).Select(m => m.RefId!.Value).Distinct().ToList();
        var billedOn = await db.GoodsReceipts.AsNoTracking().Where(r => receiptIds.Contains(r.Id) && r.PurchaseId != null).ToDictionaryAsync(r => r.Id, r => r.PurchaseId!.Value, ct);
        var billIds = moves.Where(m => m.BillId != null && m.Kind == StockMovementKind.Sale).Select(m => m.BillId!.Value).Distinct().ToList();
        var takings = (await db.BillLines.AsNoTracking().Where(l => billIds.Contains(l.BillId) && l.ProductId != null && productIds.Contains(l.ProductId!.Value))
                .Select(l => new { l.BillId, l.ProductId, Value = l.TaxableValue ?? l.Amount }).ToListAsync(ct))
            .GroupBy(l => (l.BillId, l.ProductId!.Value)).ToDictionary(g => g.Key, g => g.Sum(l => l.Value));

        var result = new Dictionary<(Guid, Guid), (decimal Sold, decimal Revenue, decimal Gone)>();
        foreach (var item in moves.GroupBy(m => m.ProductId))
        {
            // Lots in: each bill's net units (a cancelled bill nets to nothing), at its first arrival.
            Guid? LotOf(string? refType, Guid? refId) => refType == "purchase" ? refId : refType == "receipt" && refId is { } r && billedOn.TryGetValue(r, out var bill) ? bill : null;
            var ins = item.Where(m => m.Kind == StockMovementKind.Purchase).GroupBy(m => LotOf(m.RefType, m.RefId) ?? m.RefId ?? m.Id)
                .Select(g => (Lot: (Guid?)(LotOf(g.First().RefType, g.First().RefId)), Units: g.Sum(m => m.Quantity), At: g.Min(m => m.OccurredAt)))
                .Concat(item.Where(m => m.Kind != StockMovementKind.Purchase && m.Kind != StockMovementKind.Sale && m.Quantity > 0).Select(m => (Lot: (Guid?)null, Units: m.Quantity, At: m.OccurredAt)))
                .Where(x => x.Units > 0).OrderBy(x => x.At).ToList();
            // Out: each bill's net sale (returns and cancels net off), and every other loss.
            var outs = item.Where(m => m.Kind == StockMovementKind.Sale && m.BillId != null).GroupBy(m => m.BillId!.Value)
                .Select(g => (Bill: (Guid?)g.Key, Units: -g.Sum(m => m.Quantity), At: g.Min(m => m.OccurredAt)))
                .Concat(item.Where(m => m.Kind != StockMovementKind.Purchase && m.Quantity < 0 && !(m.Kind == StockMovementKind.Sale && m.BillId != null)).Select(m => (Bill: (Guid?)null, Units: -m.Quantity, At: m.OccurredAt)))
                .Where(x => x.Units > 0).OrderBy(x => x.At).ToList();
            var queue = new Queue<(Guid? Lot, decimal Left)>(ins.Select(x => (x.Lot, x.Units)));
            var head = queue.Count > 0 ? queue.Dequeue() : ((Guid?)null, 0m);
            foreach (var o in outs)
            {
                var need = o.Units;
                var price = o.Bill is { } b ? takings.GetValueOrDefault((b, item.Key)) / o.Units : 0;
                while (need > 0 && (head.Item2 > 0 || queue.Count > 0))
                {
                    if (head.Item2 <= 0)
                    {
                        head = queue.Dequeue();
                        continue;
                    }

                    var take = Math.Min(need, head.Item2);
                    if (head.Item1 is { } lot)
                    {
                        var was = result.GetValueOrDefault((lot, item.Key));
                        result[(lot, item.Key)] = o.Bill is null
                            ? (was.Sold, was.Revenue, was.Gone + take)
                            : (was.Sold + take, was.Revenue + (take * price), was.Gone + take);
                    }

                    head = (head.Item1, head.Item2 - take);
                    need -= take;
                }
            }
        }

        return result;
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
        // An item kept by batch (it has batches already, or is a scheduled medicine) needs its batch on every bill:
        // stock without a batch could never be sold, since medicines sell from batches.
        var batchKept = (await db.ProductBatches.AsNoTracking().Where(b => lines.Select(l => l.ProductId).Contains(b.ProductId)).Select(b => b.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        if (lines.FirstOrDefault(l => string.IsNullOrWhiteSpace(l.BatchNo) && (batchKept.Contains(l.ProductId) || products[l.ProductId].Schedule is not null)) is { } loose)
        {
            return Invalid("Lines", $"{products[loose.ProductId].Name} is kept by batch: enter its batch number, expiry month and MRP.");
        }

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

        var charges = request.Charges ?? [];
        if (charges.Count > 10 || charges.Any(c => c.Amount <= 0 || c.Amount > Money.MaxAmount || decimal.Round(c.Amount, 2) != c.Amount || string.IsNullOrWhiteSpace(c.Label)
            || c.PaidBy is not (null or PaymentMode.Cash or PaymentMode.Upi)) || !Enum.IsDefined(request.Split))
        {
            return Invalid("Charges", "Give each charge a name and an amount, paid by cash, UPI or not yet.");
        }

        // The lot's charges, spread over its items: by value, units or bags (bags falls back to value when no line has bags).
        var pool = charges.Sum(c => c.Amount) + (request.ExtraInCost ? request.Extra : 0);
        if (request.Split == CostSplit.Quantity && pool > 0 && lines.Select(l => products[l.ProductId].Unit).Distinct().Count() > 1)
        {
            return Invalid("Split", "These items are in different units (kg and pieces): share by weight, bags or value.");
        }

        decimal Weight(PurchaseLineRequest l) => request.Split switch
        {
            CostSplit.Quantity => l.Quantity + l.FreeQuantity,
            CostSplit.Weight => Catalogue.Units.Kg(products[l.ProductId].Unit, products[l.ProductId].WeightKg, l.Quantity + l.FreeQuantity) ?? 0,
            CostSplit.Bags when lines.Any(x => x.Bags > 0) => l.Bags ?? 0,
            _ => Taxable(l),
        };
        var weights = lines.Sum(Weight);
        decimal ShareOf(PurchaseLineRequest l) => pool == 0 || weights <= 0 ? 0 : pool * Weight(l) / weights;

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var billNo = request.BillNo?.Trim();
        db.Purchases.Add(new Purchase
        {
            Id = request.Id, SupplierId = request.SupplierId, BillNo = string.IsNullOrEmpty(billNo) ? null : billNo[..Math.Min(billNo.Length, 40)], Total = total,             ExtraCharges = request.Extra, OccurredAt = billDate, CreatedByUserId = userId,
            PurchaseOrderId = request.OrderId, CostSplit = pool == 0 ? null : request.Split, ExtraInCost = request.ExtraInCost && request.Extra != 0,
        });
        foreach (var charge in charges)
        {
            var label = charge.Label!.Trim()[..Math.Min(60, charge.Label.Trim().Length)];
            Guid? expenseId = null;
            if (charge.PaidBy is { } mode)
            {
                // Paid now: out of the drawer as stock bought, which the profit report leaves out (it is in the goods' cost).
                expenseId = Guid.CreateVersion7();
                db.Expenses.Add(new Expense
                {
                    Id = expenseId.Value, Amount = charge.Amount, Category = ExpenseCategory.Purchase, Mode = mode, OccurredAt = now, CreatedByUserId = userId,
                    Note = string.IsNullOrEmpty(billNo) ? label : $"{label} · bill {billNo[..Math.Min(billNo.Length, 40)]}",
                });
            }

            db.PurchaseCharges.Add(new PurchaseCharge { PurchaseId = request.Id, Label = label, Amount = charge.Amount, ExpenseId = expenseId });
        }
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

        var receiptLines = await GoodsReceipts.LinesAsync(db, receiptIds, ct);
        var alreadyIn = receipts.SelectMany(r => receiptLines[r.Id]).GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        // A receipt closes with its bill, so every item on it must be on the bill: otherwise those goods would never be costed.
        if (alreadyIn.Keys.FirstOrDefault(p => lines.All(l => l.ProductId != p)) is { } missing && missing != Guid.Empty)
        {
            var name = receipts.SelectMany(r => receiptLines[r.Id]).First(l => l.ProductId == missing).Name;
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
            var unitCost = decimal.Round((Taxable(line) + ShareOf(line)) / units, 2, MidpointRounding.AwayFromZero);
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
                GstRate = line.GstRate, Taxable = Taxable(line), Gst = LineTotal(line) - Taxable(line), FromReceipt = fromReceipt, LandedUnitCost = unitCost,
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
            var counted = receipts.Where(r => r.PurchaseOrderId == orderId).SelectMany(r => receiptLines[r.Id]).GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
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

        if (request.GodownId is { } godown)
        {
            // The bill's own goods (not those already counted in on receipts) were unloaded at the godown.
            if (!await Godowns.IsActiveAsync(db, godown, ct))
            {
                return Invalid("GodownId", "That godown wasn't found.");
            }

            await Godowns.MoveForAsync(db, request.Id, null, godown, added.Select(a => (a.Key, a.Value)), $"Supplier bill {billNo}".Trim(), userId, now, ct);
        }

        await Bardana.CameWithGoodsAsync(db, request.Id, request.SupplierId,
            db.StockMovements.Local.Where(m => m.RefId == request.Id && m.Kind == StockMovementKind.Purchase).Sum(m => m.Bags ?? 0), userId, now, ct);
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
    private static async Task<IResult> CancelPurchaseAsync(Guid id, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var purchase = await db.Purchases.AsNoTracking().Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null)
        {
            return TypedResults.NotFound();
        }

        // A supplier bill in a closed month stays as filed: a supplier credit note in the open month puts it right.
        await Books.EnsureOpenAsync(db, tenant, purchase.OccurredAt, ct);

        // A bill can be cancelled only while all its goods are still here (and, for medicines, each batch whole).
        // Once some were sold, returned or written off, taking the whole bill off would push stock below zero and
        // leave the supplier's balance wrong; the goods still here go back with a supplier return and credit note.
        if (purchase.CancelledAt is null && await GoneAsync(db, purchase, ct) is { } gone)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"{gone} from this bill was already sold, returned or written off, so the bill can't be cancelled. Send back what is left as a supplier return, and record the supplier's credit note.");
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
            var onReceiptIds = await db.GoodsReceipts.AsNoTracking().Where(r => r.PurchaseId == id && r.PurchaseOrderId == orderId).Select(r => r.Id).ToListAsync(ct);
            var onReceipts = (await GoodsReceipts.LinesAsync(db, onReceiptIds, ct)).SelectMany(g => g).GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            var back = purchase.Lines.GroupBy(l => l.ProductId).Select(g => (g.Key, Math.Max(0, g.Sum(l => l.Quantity + l.FreeQuantity) - onReceipts.GetValueOrDefault(g.Key)))).ToList();
            await PurchaseOrders.ReceiveAsync(db, orderId, purchase.SupplierId, back, -1, clock, ct);
        }

await db.GoodsReceipts.Where(r => r.PurchaseId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.PurchaseId, (Guid?)null), ct);
        // The bill's own arrivals are undone; goods that came on receipts are still here, waiting for a bill again.
        await db.ArrivalLines.Where(a => a.PurchaseId == id && a.ReceiptId == null).ExecuteUpdateAsync(s => s.SetProperty(a => a.CancelledAt, now), ct);
        await db.ArrivalLines.Where(a => a.PurchaseId == id && a.ReceiptId != null).ExecuteUpdateAsync(s => s.SetProperty(a => a.PurchaseId, (Guid?)null).SetProperty(a => a.Rate, (decimal?)null), ct);
        await Godowns.CancelForAsync(db, id, now, ct);
        await Bardana.StrikeForAsync(db, id, now, ct);
        // Charges paid from the drawer for this lot come off the kharcha too.
        var paidCharges = await db.PurchaseCharges.Where(c => c.PurchaseId == id && c.ExpenseId != null).Select(c => c.ExpenseId!.Value).ToListAsync(ct);
        await db.Expenses.Where(e => paidCharges.Contains(e.Id) && e.StruckAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.StruckAt, now).SetProperty(e => e.StruckByUserId, userId), ct);
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
