using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// One product that arrived from a supplier on one delivery: the single record of every arrival, whether it came on a
/// goods receipt (bill later) or straight on a supplier bill. Reports such as "what came from whom, against which order"
/// are sums over these rows. <see cref="Received"/> is what was counted in (damaged included); <see cref="Damaged"/>
/// went straight out again as damage, so it can go back to the supplier.
/// </summary>
public sealed class ArrivalLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>The goods receipt it came on, or null when it came straight on a bill.</summary>
    public Guid? ReceiptId { get; set; }

    /// <summary>The supplier bill it was billed on: set at once for a direct bill, later for a receipt.</summary>
    public Guid? PurchaseId { get; set; }

    public Guid? OrderId { get; set; }

    /// <summary>The order line it fills. Null on an order delivery: an extra item that wasn't ordered.</summary>
    public Guid? OrderLineId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Received { get; set; }

    public decimal Damaged { get; set; }

    public decimal? Bags { get; set; }

    /// <summary>The bill rate, once billed.</summary>
    public decimal? Rate { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}

/// <summary>Why the rest of an order line is not coming.</summary>
public enum ShortReason
{
    NotSent,
    OutOfStock,
    Damaged,
    WrongItem,
    RateDispute,
    Other,
}

public sealed record CloseShortRequest(Guid ProductId, ShortReason Reason);

/// <summary>An open order as the person unloading the truck sees it: what to expect, without rates.</summary>
public sealed record CheckLine(Guid OrderLineId, Guid ProductId, string Name, string? Category, Catalogue.Unit Unit, decimal Ordered, decimal Received, decimal ShortClosed, decimal Pending, string? Barcode);

public sealed record CheckOrder(Guid Id, int Number, Guid SupplierId, string SupplierName, string? SupplierPhone, DateOnly? ExpectedOn, string? Note, IReadOnlyList<CheckLine> Lines);

/// <summary>One delivery of one item, for the "what came from whom" lists. Extra: came on an order delivery without being on the order.</summary>
public sealed record ArrivalDto(Guid Id, DateTimeOffset At, Guid SupplierId, string Supplier, Guid ProductId, string Product, decimal Received, decimal Damaged, decimal? Bags, decimal? Rate,
    Guid? ReceiptId, int? ReceiptNo, Guid? PurchaseId, string? BillNo, Guid? OrderId, bool Extra);

public static class Arrivals
{
    public static IEndpointRouteBuilder MapArrivals(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/goods-in/orders", OpenOrdersAsync).WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopMember);
        app.MapGet("/api/v1/goods-in/arrivals", ListAsync).WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        app.MapPost("/api/v1/suppliers/orders/{id:guid}/close-short", CloseShortAsync).WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>Records one arrival per product; the order line is looked up by product. Call inside the caller's transaction.</summary>
    internal static async Task AddAsync(AppDbContext db, Guid supplierId, Guid? receiptId, Guid? purchaseId, Guid? orderId,
        IEnumerable<(Guid ProductId, decimal Received, decimal Damaged, decimal? Bags, decimal? Rate)> goods, DateTimeOffset at, CancellationToken ct)
    {
        var orderLines = orderId is { } o
            ? await db.PurchaseOrderLines.AsNoTracking().Where(l => l.OrderId == o).ToDictionaryAsync(l => l.ProductId, l => l.Id, ct)
            : [];
        foreach (var g in goods.Where(g => g.Received > 0))
        {
            db.ArrivalLines.Add(new ArrivalLine
            {
                SupplierId = supplierId, ReceiptId = receiptId, PurchaseId = purchaseId, OrderId = orderId,
                OrderLineId = orderLines.TryGetValue(g.ProductId, out var lineId) ? lineId : null,
                ProductId = g.ProductId, Received = g.Received, Damaged = g.Damaged, Bags = g.Bags, Rate = g.Rate, OccurredAt = at,
            });
        }
    }

    /// <summary>Deliveries (not cancelled), newest first, for an item, a supplier or an order.</summary>
    private static async Task<IReadOnlyList<ArrivalDto>> ListAsync(Guid? productId, Guid? supplierId, Guid? orderId, AppDbContext db, CancellationToken ct)
    {
        var rows = await (from a in db.ArrivalLines.AsNoTracking()
                          where a.CancelledAt == null && (productId == null || a.ProductId == productId) && (supplierId == null || a.SupplierId == supplierId) && (orderId == null || a.OrderId == orderId)
                          join s in db.Suppliers.AsNoTracking() on a.SupplierId equals s.Id
                          join p in db.Products.AsNoTracking() on a.ProductId equals p.Id
                          join r in db.GoodsReceipts.AsNoTracking() on a.ReceiptId equals r.Id into rs
                          from r in rs.DefaultIfEmpty()
                          join b in db.Purchases.AsNoTracking() on a.PurchaseId equals b.Id into bs
                          from b in bs.DefaultIfEmpty()
                          orderby a.OccurredAt descending
                          select new ArrivalDto(a.Id, a.OccurredAt, a.SupplierId, s.Name, a.ProductId, p.Name, a.Received, a.Damaged, a.Bags, a.Rate,
                              a.ReceiptId, r == null ? null : r.Number, a.PurchaseId, b == null ? null : b.BillNo, a.OrderId, a.OrderId != null && a.OrderLineId == null)).Take(500).ToListAsync(ct);
        return rows;
    }

    private static async Task<IReadOnlyList<CheckOrder>> OpenOrdersAsync(Guid? supplierId, AppDbContext db, CancellationToken ct)
    {
        var orders = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.Status == PurchaseOrderStatus.Open && (supplierId == null || o.SupplierId == supplierId))
            .OrderBy(o => o.Number).Take(100).ToListAsync(ct);
        var productIds = orders.SelectMany(o => o.Lines).Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Category, p.Unit, p.Barcode }).ToDictionaryAsync(p => p.Id, ct);
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Phone }, ct);
        return orders.Select(o => new CheckOrder(o.Id, o.Number, o.SupplierId, suppliers.GetValueOrDefault(o.SupplierId)?.Name ?? "", suppliers.GetValueOrDefault(o.SupplierId)?.Phone, o.ExpectedOn, o.Note,
            o.Lines.Where(l => products.ContainsKey(l.ProductId)).Select(l =>
            {
                var p = products[l.ProductId];
                return new CheckLine(l.Id, l.ProductId, p.Name, p.Category, p.Unit, l.Quantity, l.Received, l.ShortClosed, PurchaseOrders.PendingOf(l), p.Barcode);
            }).ToList())).ToList();
    }

    /// <summary>The rest of one order line won't come: it stops counting as on order, with the reason kept.</summary>
    private static async Task<IResult> CloseShortAsync(Guid id, CloseShortRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await db.PurchaseOrders.FromSql($"SELECT * FROM purchase_orders WHERE id = {id} FOR UPDATE").Include(o => o.Lines).SingleOrDefaultAsync(ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Lines.FirstOrDefault(l => l.ProductId == request.ProductId) is not { } line || !Enum.IsDefined(request.Reason))
        {
            return Invalid("ProductId", "That item isn't on this order.");
        }

        if (order.Status != PurchaseOrderStatus.Open || PurchaseOrders.PendingOf(line) <= 0)
        {
            return TypedResults.NoContent();
        }

        line.ShortClosed += PurchaseOrders.PendingOf(line);
        line.ShortReason = request.Reason;
        if (order.Lines.All(l => PurchaseOrders.PendingOf(l) <= 0))
        {
            (order.Status, order.ClosedAt) = (PurchaseOrderStatus.Closed, clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
