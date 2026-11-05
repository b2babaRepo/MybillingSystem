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
}

/// <summary>A supplier's bill: goods in, on credit for whatever wasn't paid at once.</summary>
public sealed class Purchase : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>The number on the supplier's own bill.</summary>
    public string? BillNo { get; set; }

    public decimal Total { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

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
}

/// <summary>The supplier's khata, append-only: + a bill, − a payment. What the shop owes is the sum.</summary>
public sealed class SupplierEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid SupplierId { get; set; }

    public decimal Amount { get; set; }

    public Guid? PurchaseId { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record SupplierRequest(Guid Id, string? Name, string? Phone);

/// <summary>
/// A supplier and what the shop owes them, split by how old the unpaid bills are: payments clear the oldest bills
/// first, so what is still owed sits on the newest ones. <see cref="OldestDays"/>: the oldest unpaid bill's age.
/// </summary>
public sealed record SupplierDto(Guid Id, string Name, string? Phone, decimal Due,
    decimal Days0To15 = 0, decimal Days16To30 = 0, decimal Over30 = 0, int OldestDays = 0);

public sealed record SupplierEntryDto(Guid Id, decimal Amount, Guid? PurchaseId, string? Note, DateTimeOffset OccurredAt, bool Cancelled = false);

public sealed record SupplierDetail(SupplierDto Supplier, IReadOnlyList<SupplierEntryDto> Entries);

/// <summary>
/// <see cref="UnitCost"/>: the rate (PTR) per unit. Medicines also send the batch, expiry month ("2027-05") and MRP;
/// <see cref="FreeQuantity"/> and <see cref="DiscountPercent"/> are the distributor's scheme and trade discount.
/// </summary>
public sealed record PurchaseLineRequest(
    Guid ProductId, decimal Quantity, decimal UnitCost, decimal FreeQuantity = 0, decimal DiscountPercent = 0, string? BatchNo = null, string? ExpiryMonth = null, decimal? Mrp = null,
    decimal? GstRate = null);

/// <summary><see cref="Id"/> is made by the app, so a repeated tap saves once.</summary>
public sealed record PurchaseRequest(Guid Id, Guid SupplierId, string? BillNo, IReadOnlyList<PurchaseLineRequest>? Lines, decimal PaidAmount, PaymentMode? PaymentMode);

public sealed record SupplierPaymentRequest(Guid Id, decimal Amount, PaymentMode? PaymentMode);

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
        group.MapPost("/purchases", PurchaseAsync);
        group.MapPut("/{id:guid}", ChangeAsync);
        group.MapPost("/purchases/{id:guid}/cancel", CancelPurchaseAsync);
        return app;
    }

    private static async Task<IReadOnlyList<SupplierDto>> ListAsync(AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        await SupplierDuesAsync(db, clock, ct);

    public static async Task<IReadOnlyList<SupplierDto>> SupplierDuesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var suppliers = await db.Suppliers.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Phone }).ToListAsync(ct);
        var entries = await db.SupplierEntries.AsNoTracking().Select(e => new { e.SupplierId, e.Amount, e.OccurredAt }).ToListAsync(ct);
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
            foreach (var bill in mine.Where(e => e.Amount > 0).OrderByDescending(e => e.OccurredAt))
            {
                if (left <= 0) break;
                var part = Math.Min(bill.Amount, left);
                left -= part;
                var days = (int)(now - bill.OccurredAt).TotalDays;
                oldest = Math.Max(oldest, days);
                if (days <= 15) d0 += part; else if (days <= 30) d16 += part; else d30 += part;
            }

            return new SupplierDto(x.Id, x.Name, x.Phone, due, d0, d16, d30, oldest);
        }).ToList();
    }

    private static async Task<IResult> AddAsync(SupplierRequest request, AppDbContext db, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (request.Id == Guid.Empty || name is not { Length: >= 2 and <= 120 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Enter the supplier's name."] });
        }

        if (!await db.Suppliers.AnyAsync(s => s.Id == request.Id, ct))
        {
            db.Suppliers.Add(new Supplier { Id = request.Id, Name = name, Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim() });
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

        var entries = await db.SupplierEntries.AsNoTracking().Where(e => e.SupplierId == id).OrderByDescending(e => e.OccurredAt)
            .Select(e => new SupplierEntryDto(e.Id, e.Amount, e.PurchaseId, e.Note, e.OccurredAt,
                e.Amount > 0 && db.Purchases.Any(p => p.Id == e.PurchaseId && p.CancelledAt != null))).ToListAsync(ct);
        return TypedResults.Ok(new SupplierDetail(new SupplierDto(id, supplier.Name, supplier.Phone, entries.Sum(e => e.Amount)), entries));
    }

    private static async Task<IResult> PurchaseAsync(PurchaseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Purchases.AnyAsync(p => p.Id == request.Id, ct))
        {
            return TypedResults.Ok(new { request.Id });
        }

        var lines = request.Lines ?? [];
        var products = await db.Products.Where(p => lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var total = lines.Sum(LineTotal);
        // A product can come in two batches on one bill, but not the same batch twice.
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200 || lines.Select(l => (l.ProductId, l.BatchNo?.Trim().ToUpperInvariant())).Distinct().Count() != lines.Count
            || lines.Any(l => !products.ContainsKey(l.ProductId) || !Money.IsValidQuantity(l.Quantity) || l.UnitCost < 0 || l.UnitCost > Money.MaxAmount || decimal.Round(l.UnitCost, 2) != l.UnitCost
                || l.FreeQuantity < 0 || (l.FreeQuantity > 0 && !Money.IsValidQuantity(l.FreeQuantity)) || l.DiscountPercent is < 0 or >= 100 || l.GstRate is < 0 or > 40)
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

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var billNo = request.BillNo?.Trim();
        db.Purchases.Add(new Purchase
        {
            Id = request.Id, SupplierId = request.SupplierId, BillNo = string.IsNullOrEmpty(billNo) ? null : billNo[..Math.Min(billNo.Length, 40)], Total = total, OccurredAt = now, CreatedByUserId = userId,
        });
        db.SupplierEntries.Add(new SupplierEntry { SupplierId = request.SupplierId, Amount = total, PurchaseId = request.Id, Note = billNo, OccurredAt = now, CreatedByUserId = userId });
        var purchase = db.Purchases.Local.Single(p => p.Id == request.Id);
        var added = new Dictionary<Guid, decimal>();
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var units = line.Quantity + line.FreeQuantity;
            // What one unit really cost: the line's value before GST (the GST comes back as input credit) over paid and free units together.
            var unitCost = decimal.Round(Taxable(line) / units, 2, MidpointRounding.AwayFromZero);
            var held = Math.Max(0, product.Stock + added.GetValueOrDefault(line.ProductId));
            product.CostPrice = product.CostPrice is { } cost && held > 0
                ? decimal.Round(((held * cost) + (units * unitCost)) / (held + units), 2)
                : unitCost;
            ProductBatch? batch = null;
            if (!string.IsNullOrWhiteSpace(line.BatchNo))
            {
                batch = await Batches.AddToBatchAsync(db, line.ProductId, line.BatchNo.Trim().ToUpperInvariant(), Batches.ParseExpiry(line.ExpiryMonth)!.Value, line.Mrp!.Value, unitCost, units, now, ct);
                product.SellingPrice = line.Mrp!.Value;
            }

            purchase.Lines.Add(new PurchaseLine
            {
                ProductId = line.ProductId, Quantity = line.Quantity, UnitCost = line.UnitCost, FreeQuantity = line.FreeQuantity, DiscountPercent = line.DiscountPercent, BatchId = batch?.Id,
                GstRate = line.GstRate, Taxable = Taxable(line), Gst = LineTotal(line) - Taxable(line),
            });
            db.StockMovements.Add(new StockMovement
            {
                ProductId = line.ProductId, Kind = StockMovementKind.Purchase, Quantity = units, CostPrice = unitCost,
                OccurredAt = now, CreatedByUserId = userId, Note = batch is null ? "Supplier bill" : $"Supplier bill · batch {batch.BatchNo}",
            });
            added[line.ProductId] = added.GetValueOrDefault(line.ProductId) + units;
        }

        if (request.PaidAmount > 0)
        {
            AddPayment(db, request.SupplierId, request.PaidAmount, request.PaymentMode ?? PaymentMode.Cash, request.Id, now, userId, Guid.CreateVersion7());
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

        db.SupplierEntries.Add(new SupplierEntry { SupplierId = purchase.SupplierId, Amount = -purchase.Total, PurchaseId = id, Note = "Bill cancelled", OccurredAt = now, CreatedByUserId = userId });
        foreach (var line in purchase.Lines)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = line.ProductId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Other, Quantity = -(line.Quantity + line.FreeQuantity), OccurredAt = now,
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
        var quantities = byProduct.Select(g => g.Sum(l => l.Quantity + l.FreeQuantity)).ToArray();
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

        AddPayment(db, id, request.Amount, request.PaymentMode ?? PaymentMode.Cash, null, clock.GetUtcNow(), user.UserId()!.Value, request.Id);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Money out: on the supplier's khata, and as stock bought in the kharcha so the cash book shows it.</summary>
    private static void AddPayment(AppDbContext db, Guid supplierId, decimal amount, PaymentMode mode, Guid? purchaseId, DateTimeOffset at, Guid userId, Guid entryId)
    {
        db.SupplierEntries.Add(new SupplierEntry { Id = entryId, SupplierId = supplierId, Amount = -amount, PurchaseId = purchaseId, OccurredAt = at, CreatedByUserId = userId });
        db.Expenses.Add(new Expense { Amount = amount, Category = ExpenseCategory.Purchase, Mode = mode, Note = "Supplier payment", OccurredAt = at, CreatedByUserId = userId });
    }
}
