using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One item's health: what it is worth on the shelf, how long since it last sold and last came in, how it moves
/// (fast, slow, dead) and its A/B/C rank by sales.
/// </summary>
public sealed record StockHealthItem(
    Guid ProductId, string Name, string? Category, Unit Unit, decimal Stock, decimal? Value, decimal Sales90, decimal Sold90,
    int? DaysSinceSale, int? DaysSinceIn, string Movement, string Rank, decimal? DaysOfCover);

/// <summary>Stock value by how long ago it came in (0–30, 31–60, 61–90, 90+ days).</summary>
public sealed record AgeBucket(string Label, decimal Value, int Items);

public sealed record StockHealthDto(decimal Value, decimal DeadValue, int DeadItems, decimal SlowValue, int SlowItems, IReadOnlyList<AgeBucket> Ageing,
    int RankA, int RankB, int RankC, decimal Turnover, IReadOnlyList<StockHealthItem> Items);

/// <summary>
/// Where the shop's money sits on the shelf. Movement over the last 90 days: dead = nothing sold (and held more
/// than 30 days), slow = less than a month's cover sold in 90 days relative to stock (over 90 days of cover), fast
/// otherwise. Rank: A = the items making the first 80% of 90-day sales, B = the next 15%, C = the rest. Turnover =
/// cost of goods sold in 90 days, a year's worth, over stock value today.
/// </summary>
public static class StockHealth
{
    public static IEndpointRouteBuilder MapStockHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/stock-health", HealthAsync).WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<StockHealthDto> HealthAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var since = now.AddDays(-90);
        var products = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsService)
            .Select(p => new { p.Id, p.Name, p.Category, p.Unit, p.Stock, p.CostPrice, p.CreatedAt, SlowAfter = db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.SlowAfterDays).FirstOrDefault() }).ToListAsync(ct);
        var sales = await (from l in db.BillLines.AsNoTracking()
                           join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                           where l.ProductId != null && b.CancelledAt == null && b.OccurredAt >= since
                           group new { l.Amount, l.Quantity, Cost = l.Quantity * (l.UnitCost ?? 0) } by l.ProductId into g
                           select new { Id = g.Key!.Value, Sales = g.Sum(x => x.Amount), Sold = g.Sum(x => x.Quantity), Cost = g.Sum(x => x.Cost) }).ToDictionaryAsync(x => x.Id, ct);
        var lastSale = await db.StockMovements.AsNoTracking().Where(m => m.Kind == StockMovementKind.Sale)
            .GroupBy(m => m.ProductId).Select(g => new { g.Key, At = g.Max(m => m.OccurredAt) }).ToDictionaryAsync(x => x.Key, x => x.At, ct);
        var recent = await Cover.SoldAsync(db, clock, ct);
        var lastIn = await db.StockMovements.AsNoTracking().Where(m => (m.Kind == StockMovementKind.Purchase || m.Kind == StockMovementKind.Opening) && m.Quantity > 0)
            .GroupBy(m => m.ProductId).Select(g => new { g.Key, At = g.Max(m => m.OccurredAt) }).ToDictionaryAsync(x => x.Key, x => x.At, ct);

        // A/B/C by 90-day sales value, biggest first.
        var total = sales.Values.Sum(s => s.Sales);
        var rank = new Dictionary<Guid, string>();
        decimal running = 0;
        foreach (var s in sales.Values.Where(s => s.Sales > 0).OrderByDescending(s => s.Sales))
        {
            var share = total > 0 ? running / total : 1;
            rank[s.Id] = share < 0.80m ? "A" : share < 0.95m ? "B" : "C";
            running += s.Sales;
        }

        int? Days(DateTimeOffset? at) => at is { } a ? (int)Math.Floor((now - a).TotalDays) : null;
        var items = products.Select(p =>
        {
            var s = sales.GetValueOrDefault(p.Id);
            var held = Math.Max(0, p.Stock);
            decimal? value = p.CostPrice is { } c && held > 0 ? decimal.Round(held * c, 2) : held > 0 ? null : 0;
            var sinceIn = Days(lastIn.TryGetValue(p.Id, out var i) ? i : p.CreatedAt);
            // Days left uses the shared 30-day rate (same as the item page); slow is judged on 90 days so one quiet month isn't "slow".
            var cover = Cover.DaysLeft(held, recent.GetValueOrDefault(p.Id));
            var slow90 = held > 0 && (s?.Sold ?? 0) > 0 && held / ((s?.Sold ?? 0) / 90m) > 90;
            var movement = held <= 0 ? "none"
                : (s?.Sold ?? 0) <= 0 ? (sinceIn > (p.SlowAfter ?? Categories.DefaultSlowAfterDays) ? "dead" : "new")
                : slow90 ? "slow" : "fast";
            return new StockHealthItem(p.Id, p.Name, p.Category, p.Unit, p.Stock, value, s?.Sales ?? 0, s?.Sold ?? 0,
                Days(lastSale.TryGetValue(p.Id, out var ls) ? ls : null), sinceIn, movement, rank.GetValueOrDefault(p.Id, "C"), cover);
        }).ToList();

        var held = items.Where(i => i.Stock > 0).ToList();
        AgeBucket Bucket(string label, Func<int, bool> within)
        {
            var these = held.Where(i => within(i.DaysSinceIn ?? 0)).ToList();
            return new AgeBucket(label, these.Sum(i => i.Value ?? 0), these.Count);
        }

        var value = held.Sum(i => i.Value ?? 0);
        var cogsYear = sales.Values.Sum(s => s.Cost) * 365m / 90m;
        return new StockHealthDto(
            value,
            held.Where(i => i.Movement == "dead").Sum(i => i.Value ?? 0), held.Count(i => i.Movement == "dead"),
            held.Where(i => i.Movement == "slow").Sum(i => i.Value ?? 0), held.Count(i => i.Movement == "slow"),
            [Bucket("0-30", d => d <= 30), Bucket("31-60", d => d is > 30 and <= 60), Bucket("61-90", d => d is > 60 and <= 90), Bucket("90+", d => d > 90)],
            rank.Count(r => r.Value == "A"), rank.Count(r => r.Value == "B"), items.Count(i => i.Rank == "C"),
            value > 0 ? decimal.Round(cogsYear / value, 1) : 0,
            // Most money stuck first: dead, then slow, by value.
            items.OrderBy(i => i.Movement switch { "dead" => 0, "slow" => 1, "new" => 2, "fast" => 3, _ => 4 }).ThenByDescending(i => i.Value ?? 0).ToList());
    }
}
