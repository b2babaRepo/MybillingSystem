using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// The one sales rate every screen uses for "days left": units sold on bills that are not cancelled over the last
/// <see cref="Days"/> days, per day the item was on the shelf. The item page, the reorder list and stock health all
/// read it, so the same item shows the same days of stock everywhere.
/// </summary>
public static class Cover
{
    public const int Days = 30;

    /// <summary>The short window: the last week, to catch an item that has started selling faster.</summary>
    public const int RecentDays = 7;

    /// <summary>The fewest shelf days the week's rate is spread over, as for the month's.</summary>
    public const int MinRecentShelfDays = 4;

    /// <summary>
    /// The fewest shelf days a rate is spread over. An item back in stock for a day or two would otherwise get a rate from
    /// one lucky (or one bulk) sale.
    /// </summary>
    public const int MinShelfDays = 7;

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

    /// <summary>
    /// One item's sales: what sold in the month and on how many shelf days; <see cref="Daily"/>, the month's rate per shelf
    /// day; <see cref="Recent"/>, the last week's, when it sold on at least two days of it (one bulk order is not a trend).
    /// </summary>
    public sealed record Rate(decimal Sold, int ShelfDays, decimal Daily, decimal? Recent = null)
    {
        /// <summary>
        /// The faster of the two: what "days left" and when to order go by, as running out early costs a sale while
        /// ordering a few days sooner costs little. How much to order goes by <see cref="Daily"/>, so one busy week
        /// doesn't fill the shelf for two months.
        /// </summary>
        public decimal Timing => Math.Max(Daily, Recent ?? 0);
    }

    /// <summary>
    /// Each selling item's rate per day. Days with no stock are left out: an item that sold 30 in the 15 days it was on
    /// the shelf sells 2 a day, not 1, or it would be ordered short every time it runs out. A day counts as on the shelf
    /// when there was stock at its start or end, or a sale that day (a shop can sell what it hasn't entered yet). An
    /// uncounted item is always on the shelf.
    /// </summary>
    public static async Task<Dictionary<Guid, Rate>> RatesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var sold = await SoldAsync(db, clock, ct);
        var selling = sold.Where(s => s.Value > 0).Select(s => s.Key).ToList();
        if (selling.Count == 0)
        {
            return [];
        }

        // The window's India days, oldest first, ending today.
        var today = IndiaTime.Today(clock);
        var first = today.AddDays(1 - Days);
        var start = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime();
        var moves = await db.StockMovements.AsNoTracking().Where(m => m.OccurredAt >= start && selling.Contains(m.ProductId))
            .Select(m => new { m.ProductId, m.Quantity, m.OccurredAt, Sale = m.Kind == StockMovementKind.Sale }).ToListAsync(ct);
        // The last week's sales, by bill (not cancelled), and on which days.
        var weekFirst = today.AddDays(1 - RecentDays);
        var weekStart = new DateTimeOffset(weekFirst.ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime();
        var week = (await (from l in db.BillLines.AsNoTracking()
                           join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                           where l.ProductId != null && b.CancelledAt == null && b.OccurredAt >= weekStart && selling.Contains(l.ProductId!.Value)
                           select new { ProductId = l.ProductId!.Value, l.Quantity, b.OccurredAt }).ToListAsync(ct))
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => (Sold: g.Sum(l => l.Quantity), Days: g.Select(l => DateOnly.FromDateTime(l.OccurredAt.ToOffset(IndiaTime.Offset).DateTime)).Distinct().Count()));
        var products = await db.Products.AsNoTracking().Where(p => selling.Contains(p.Id))
            .Select(p => new { p.Id, p.Stock, p.Uncounted }).ToDictionaryAsync(p => p.Id, ct);

        var rates = new Dictionary<Guid, Rate>();
        var byProduct = moves.GroupBy(m => m.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var id in selling)
        {
            if (!products.TryGetValue(id, out var product))
            {
                continue;
            }

            var shelfDays = Days;
            var weekShelfDays = RecentDays;
            if (!product.Uncounted)
            {
                var own = byProduct.GetValueOrDefault(id) ?? [];
                var days = own.GroupBy(m => DateOnly.FromDateTime(m.OccurredAt.ToOffset(IndiaTime.Offset).DateTime))
                    .ToDictionary(g => g.Key, g => (Net: g.Sum(m => m.Quantity), Sale: g.Any(m => m.Sale)));
                // Stock when the window opened: today's stock less everything that moved since.
                var level = product.Stock - own.Sum(m => m.Quantity);
                shelfDays = 0;
                weekShelfDays = 0;
                for (var day = first; day <= today; day = day.AddDays(1))
                {
                    var (net, sale) = days.GetValueOrDefault(day);
                    var end = level + net;
                    if (level > 0 || end > 0 || sale)
                    {
                        shelfDays++;
                        if (day >= weekFirst)
                        {
                            weekShelfDays++;
                        }
                    }

                    level = end;
                }
            }

            var units = sold[id];
            decimal? recent = week.TryGetValue(id, out var w) && w.Days >= 2 ? w.Sold / Math.Clamp(weekShelfDays, MinRecentShelfDays, RecentDays) : null;
            rates[id] = new Rate(units, shelfDays, units / Math.Clamp(shelfDays, MinShelfDays, Days), recent);
        }

        return rates;
    }

    /// <summary>The rate per item that "days left" goes by (<see cref="Rate.Timing"/>), for screens that need only that.</summary>
    public static async Task<Dictionary<Guid, decimal>> DailyAsync(AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        (await RatesAsync(db, clock, ct)).ToDictionary(r => r.Key, r => r.Value.Timing);

    /// <summary>Days the stock lasts at that daily rate, or null when nothing sold.</summary>
    public static decimal? DaysLeft(decimal stock, decimal daily) =>
        stock > 0 && daily > 0 ? decimal.Round(stock / daily, 0) : null;
}
