using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

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

public sealed record SupplierDto(Guid Id, string Name, string? Phone, decimal Due);

public sealed record SupplierEntryDto(Guid Id, decimal Amount, Guid? PurchaseId, string? Note, DateTimeOffset OccurredAt, bool Cancelled = false);

public sealed record SupplierDetail(SupplierDto Supplier, IReadOnlyList<SupplierEntryDto> Entries);

public sealed record PurchaseLineRequest(Guid ProductId, decimal Quantity, decimal UnitCost);

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

    private static async Task<IReadOnlyList<SupplierDto>> ListAsync(AppDbContext db, CancellationToken ct) =>
        await db.Suppliers.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone, db.SupplierEntries.Where(e => e.SupplierId == s.Id).Sum(e => (decimal?)e.Amount) ?? 0m))
            .ToListAsync(ct);

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
        var total = lines.Sum(l => Money.LineAmount(l.Quantity, l.UnitCost));
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200 || lines.Select(l => l.ProductId).Distinct().Count() != lines.Count
            || lines.Any(l => !products.ContainsKey(l.ProductId) || !Money.IsValidQuantity(l.Quantity) || l.UnitCost < 0 || l.UnitCost > Money.MaxAmount || decimal.Round(l.UnitCost, 2) != l.UnitCost)
            || total <= 0 || request.PaidAmount < 0 || request.PaidAmount > total || !await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = ["Check the supplier, items, quantities, costs and amount paid."] });
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var billNo = request.BillNo?.Trim();
        db.Purchases.Add(new Purchase
        {
            Id = request.Id, SupplierId = request.SupplierId, BillNo = string.IsNullOrEmpty(billNo) ? null : billNo[..Math.Min(billNo.Length, 40)], Total = total, OccurredAt = now, CreatedByUserId = userId,
            Lines = lines.Select(l => new PurchaseLine { ProductId = l.ProductId, Quantity = l.Quantity, UnitCost = l.UnitCost }).ToList(),
        });
        db.SupplierEntries.Add(new SupplierEntry { SupplierId = request.SupplierId, Amount = total, PurchaseId = request.Id, Note = billNo, OccurredAt = now, CreatedByUserId = userId });
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var held = Math.Max(0, product.Stock);
            product.CostPrice = product.CostPrice is { } cost && held > 0
                ? decimal.Round(((held * cost) + (line.Quantity * line.UnitCost)) / (held + line.Quantity), 2)
                : line.UnitCost;
            db.StockMovements.Add(new StockMovement
            {
                ProductId = line.ProductId, Kind = StockMovementKind.Purchase, Quantity = line.Quantity, CostPrice = line.UnitCost,
                OccurredAt = now, CreatedByUserId = userId, Note = "Supplier bill",
            });
        }

        if (request.PaidAmount > 0)
        {
            AddPayment(db, request.SupplierId, request.PaidAmount, request.PaymentMode ?? PaymentMode.Cash, request.Id, now, userId, Guid.CreateVersion7());
        }

        await db.SaveChangesAsync(ct);
        var ids = lines.OrderBy(l => l.ProductId).Select(l => l.ProductId).ToArray();
        var quantities = lines.OrderBy(l => l.ProductId).Select(l => l.Quantity).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock + v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(new { request.Id });
    }

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
                ProductId = line.ProductId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Other, Quantity = -line.Quantity, OccurredAt = now,
                CreatedByUserId = userId, Note = "Supplier bill cancelled",
            });
        }

        await db.SaveChangesAsync(ct);
        var lines = purchase.Lines.OrderBy(l => l.ProductId).ToList();
        var ids = lines.Select(l => l.ProductId).ToArray();
        var quantities = lines.Select(l => l.Quantity).ToArray();
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
