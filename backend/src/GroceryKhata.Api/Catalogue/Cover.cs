using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// The one sales rate every screen uses for "days left": units sold on bills that are not cancelled, over the last
/// <see cref="Days"/> days, per day. The item page, the reorder list and stock health all read it, so the same item
/// shows the same days of stock everywhere.
/// </summary>
public static class Cover
{
    public const int Days = 30;

    /// <summary>Units sold per item in the window (not per day).</summary>
    public static Task<Dictionary<Guid, decimal>> SoldAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-Days);
        return (from l in db.BillLines.AsNoTracking()
                join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                where l.ProductId != null && b.CancelledAt == null && b.OccurredAt >= since
                group l by l.ProductId into g
                select new { ProductId = g.Key!.Value, Quantity = g.Sum(l => l.Quantity) }).ToDictionaryAsync(x => x.ProductId, x => x.Quantity, ct);
    }

    /// <summary>Days the stock lasts at that rate, or null when nothing sold.</summary>
    public static decimal? DaysLeft(decimal stock, decimal soldInWindow) =>
        stock > 0 && soldInWindow > 0 ? decimal.Round(stock / (soldInWindow / Days), 0) : null;
}
