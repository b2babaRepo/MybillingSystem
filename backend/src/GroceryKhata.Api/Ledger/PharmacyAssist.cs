using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A regular patient's medicine that is due again: they buy it about every <see cref="EveryDays"/> days.</summary>
public sealed record RefillDto(Guid CustomerId, string CustomerName, string? Phone, Guid ProductId, string Medicine, decimal LastQuantity, DateOnly LastBought, int EveryDays, DateOnly DueOn, int DaysLate);

/// <summary>One medicine to order: sold <see cref="SoldLast30Days"/> in a month, <see cref="Stock"/> left, so <see cref="Suggested"/> covers a month.</summary>
public sealed record ReorderLineDto(Guid ProductId, string Medicine, decimal Stock, decimal SoldLast30Days, decimal Suggested, decimal? LastRate);

public sealed record ReorderGroupDto(Guid? SupplierId, string? SupplierName, string? Phone, IReadOnlyList<ReorderLineDto> Lines);

/// <summary>
/// Helpers that work out, by plain rules from the shop's own bills, what a medical shop would otherwise keep in its head:
/// which regular patients are due a refill, and what to order from which distributor. They only draft: a reminder or an
/// order goes out when the shop taps send on WhatsApp.
/// </summary>
public static class PharmacyAssist
{
    public static IEndpointRouteBuilder MapPharmacyAssist(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pharmacy").WithTags("Shops");
        group.MapGet("/refills", RefillsAsync).RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/reorder", ReorderAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>
    /// A medicine a khata customer has bought at least three times: its usual gap is the average of the gaps between
    /// buys. Due from two days before that gap runs out, until two weeks late (after that they've likely moved on).
    /// </summary>
    public static IReadOnlyList<RefillDto> Refills(IEnumerable<(Guid CustomerId, Guid ProductId, DateOnly Day, decimal Quantity)> sales, DateOnly today)
    {
        var due = new List<RefillDto>();
        foreach (var g in sales.GroupBy(s => (s.CustomerId, s.ProductId)))
        {
            var days = g.GroupBy(s => s.Day).Select(d => (Day: d.Key, Quantity: d.Sum(x => x.Quantity))).OrderBy(d => d.Day).ToList();
            if (days.Count < 3)
            {
                continue;
            }

            var gaps = days.Zip(days.Skip(1), (a, b) => b.Day.DayNumber - a.Day.DayNumber).ToList();
            var every = (int)Math.Round(gaps.Average());
            if (every is < 5 or > 120)
            {
                continue;
            }

            var last = days[^1];
            var dueOn = last.Day.AddDays(every);
            var late = today.DayNumber - dueOn.DayNumber;
            if (late is >= -2 and <= 14)
            {
                due.Add(new RefillDto(g.Key.CustomerId, "", null, g.Key.ProductId, "", last.Quantity, last.Day, every, dueOn, late));
            }
        }

        return due;
    }

    private static async Task<IReadOnlyList<RefillDto>> RefillsAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var since = new DateTimeOffset(today.AddDays(-240).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rows = await (from l in db.BillLines.AsNoTracking()
                          join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                          where b.CustomerId != null && l.ProductId != null && b.CancelledAt == null && b.OccurredAt >= since
                          select new { CustomerId = b.CustomerId!.Value, ProductId = l.ProductId!.Value, b.OccurredAt, l.Quantity }).ToListAsync(ct);
        var due = Refills(rows.Select(r => (r.CustomerId, r.ProductId, DateOnly.FromDateTime(r.OccurredAt.ToOffset(IndiaTime.Offset).DateTime), r.Quantity)), today);
        var customerIds = due.Select(d => d.CustomerId).Distinct().ToList();
        var productIds = due.Select(d => d.ProductId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id) && p.IsActive && !p.IsService).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return due.Where(d => customers.ContainsKey(d.CustomerId) && products.ContainsKey(d.ProductId))
            .Select(d => d with { CustomerName = customers[d.CustomerId].Name, Phone = customers[d.CustomerId].Phone, Medicine = products[d.ProductId] })
            .OrderByDescending(d => d.DaysLate).ThenBy(d => d.CustomerName).ToList();
    }

    /// <summary>
    /// What to order: anything at or under its reorder level, or with less than two weeks of sales left. The suggestion
    /// covers a month of sales (whole packs), less what's on the shelf. Grouped by the distributor it last came from.
    /// </summary>
    private static async Task<IReadOnlyList<ReorderGroupDto>> ReorderAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-30);
        var sold = await (from l in db.BillLines.AsNoTracking()
                          join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                          where l.ProductId != null && b.CancelledAt == null && b.OccurredAt >= since
                          group l by l.ProductId into g
                          select new { ProductId = g.Key!.Value, Quantity = g.Sum(l => l.Quantity) }).ToDictionaryAsync(x => x.ProductId, x => x.Quantity, ct);
        var products = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsService).Select(p => new { p.Id, p.Name, p.Stock, p.ReorderLevel }).ToListAsync(ct);
        var needed = products
            .Select(p => (p.Id, p.Name, p.Stock, p.ReorderLevel, Sold: sold.GetValueOrDefault(p.Id)))
            .Where(p => p.Stock <= 0 || (p.ReorderLevel is { } level && p.Stock <= level) || (p.Sold > 0 && p.Stock < p.Sold / 2))
            .Select(p => (p.Id, p.Name, p.Stock, p.Sold, Suggested: Math.Max(1, Math.Ceiling(Math.Max(p.Sold, p.ReorderLevel ?? 0) - Math.Max(0, p.Stock)))))
            .ToList();
        var ids = needed.Select(n => n.Id).ToList();
        // The distributor and rate each one last came in at.
        var last = (await (from l in db.PurchaseLines.AsNoTracking()
                           join pu in db.Purchases.AsNoTracking() on l.PurchaseId equals pu.Id
                           where ids.Contains(l.ProductId) && pu.CancelledAt == null
                           select new { l.ProductId, pu.SupplierId, l.UnitCost, pu.OccurredAt }).ToListAsync(ct))
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OccurredAt).First());
        var suppliers = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Phone }, ct);
        return needed
            .GroupBy(n => last.GetValueOrDefault(n.Id)?.SupplierId)
            .Select(g => new ReorderGroupDto(g.Key, g.Key is { } s ? suppliers.GetValueOrDefault(s)?.Name : null, g.Key is { } p ? suppliers.GetValueOrDefault(p)?.Phone : null,
                g.Select(n => new ReorderLineDto(n.Id, n.Name, n.Stock, n.Sold, n.Suggested, last.GetValueOrDefault(n.Id)?.UnitCost)).OrderBy(l => l.Medicine).ToList()))
            .OrderBy(g => g.SupplierName == null).ThenBy(g => g.SupplierName).ToList();
    }
}
