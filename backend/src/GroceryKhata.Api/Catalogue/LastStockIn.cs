using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// When each item's stock last came in, how much, and from whom: a supplier's bill or goods receipt names the supplier;
/// stock added by hand or as opening stock names none (<see cref="ByHand"/>), so a count can tell where stock came from.
/// </summary>
public sealed record LastStockIn(Guid ProductId, DateTimeOffset At, decimal Quantity, string? Supplier, bool ByHand);

public static class LastStockInEndpoints
{
    public static IEndpointRouteBuilder MapLastStockIn(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/stock/last-in", GetAsync).WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<IReadOnlyList<LastStockIn>> GetAsync(AppDbContext db, CancellationToken ct)
    {
        // The latest stock-in per item: purchases, goods receipts, opening stock and stock added by hand.
        var latest = await db.StockMovements.AsNoTracking()
            .Where(m => (m.Kind == StockMovementKind.Purchase || m.Kind == StockMovementKind.Opening) && m.Quantity > 0)
            .GroupBy(m => m.ProductId)
            .Select(g => g.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt).First())
            .ToListAsync(ct);

        var purchaseIds = latest.Where(m => m.RefType == "purchase" && m.RefId != null).Select(m => m.RefId!.Value).ToList();
        var receiptIds = latest.Where(m => m.RefType == "receipt" && m.RefId != null).Select(m => m.RefId!.Value).ToList();
        var fromBills = await (from p in db.Purchases.AsNoTracking()
                               join s in db.Suppliers.AsNoTracking() on p.SupplierId equals s.Id
                               where purchaseIds.Contains(p.Id)
                               select new { p.Id, s.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var fromReceipts = await (from r in db.GoodsReceipts.AsNoTracking()
                                  join s in db.Suppliers.AsNoTracking() on r.SupplierId equals s.Id
                                  where receiptIds.Contains(r.Id)
                                  select new { r.Id, s.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return latest.Select(m =>
        {
            var supplier = m.RefId is { } id ? (m.RefType == "purchase" ? fromBills.GetValueOrDefault(id) : m.RefType == "receipt" ? fromReceipts.GetValueOrDefault(id) : null) : null;
            return new LastStockIn(m.ProductId, m.OccurredAt, m.Quantity, supplier, supplier is null);
        }).ToList();
    }
}
