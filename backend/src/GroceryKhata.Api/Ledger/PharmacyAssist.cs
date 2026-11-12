using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A regular patient's medicine that is due again: they buy it about every <see cref="EveryDays"/> days.</summary>
public sealed record RefillDto(Guid CustomerId, string CustomerName, string? Phone, Guid ProductId, string Medicine, decimal LastQuantity, DateOnly LastBought, int EveryDays, DateOnly DueOn, int DaysLate);

/// <summary>One medicine to order: sold <see cref="SoldLast30Days"/> in a month, <see cref="Stock"/> left, so <see cref="Suggested"/> covers a month.</summary>
/// <summary><see cref="OnOrder"/>: still to come on open orders, already taken off <see cref="Suggested"/>.</summary>
public sealed record ReorderLineDto(Guid ProductId, string Medicine, decimal Stock, decimal SoldLast30Days, decimal Suggested, decimal? LastRate, decimal OnOrder = 0,
    // Days the stock lasts at the 30-day sale rate; the supplier's delivery days; and the level at which to order.
    decimal? DaysLeft = null, int LeadDays = 0, decimal? ReorderPoint = null);

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

    /// <summary>The supplier's usual delivery time when nothing is known yet, in days.</summary>
    public const int DefaultLeadDays = 3;

    /// <summary>
    /// What to order: anything out, at or under its reorder level, or whose stock won't last until a new order arrives.
    /// The reorder point is the 30-day average daily sale × the supplier's delivery days (measured from past orders),
    /// plus a safety margin of half that again (at least 2 days of sales). The suggestion covers a month of sales and
    /// the delivery time, less what's on the shelf and on its way. Grouped by the supplier it last came from.
    /// </summary>
    private static async Task<IReadOnlyList<ReorderGroupDto>> ReorderAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var (lines, last) = await NeedsAsync(db, clock, ct);
        var suppliers = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Phone }, ct);
        return lines
            .GroupBy(n => last.TryGetValue(n.ProductId, out var from) ? from.SupplierId : (Guid?)null)
            .Select(g => new ReorderGroupDto(g.Key, g.Key is { } s ? suppliers.GetValueOrDefault(s)?.Name : null, g.Key is { } p ? suppliers.GetValueOrDefault(p)?.Phone : null,
                g.OrderBy(l => l.Medicine).ToList()))
            .OrderBy(g => g.SupplierName == null).ThenBy(g => g.SupplierName).ToList();
    }

    /// <summary>Every item to order now (the current shop), with its days left, delivery days and reorder point.</summary>
    internal static async Task<(List<ReorderLineDto> Lines, Dictionary<Guid, (Guid SupplierId, decimal UnitCost)> Last)> NeedsAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var sold = await Catalogue.Cover.SoldAsync(db, clock, ct);
        var products = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsService && !p.Uncounted).Select(p => new { p.Id, p.Name, p.Stock, p.Reserved, p.ReorderLevel }).ToListAsync(ct);
        // The supplier and rate each item last came in at.
        var last = (await (from l in db.PurchaseLines.AsNoTracking()
                           join pu in db.Purchases.AsNoTracking() on l.PurchaseId equals pu.Id
                           where pu.CancelledAt == null
                           select new { l.ProductId, pu.SupplierId, l.UnitCost, pu.OccurredAt }).ToListAsync(ct))
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => { var x = g.OrderByDescending(y => y.OccurredAt).First(); return (x.SupplierId, x.UnitCost); });
        var lead = await LeadDaysAsync(db, ct);
        var pending = await PurchaseOrders.PendingAsync(db, ct);
        // Stock promised on open sale orders isn't free to sell: it counts as already gone.
        var promised = await SaleOrders.PromisedAsync(db, ct);

        var lines = new List<ReorderLineDto>();
        foreach (var p in products)
        {
            var soldMonth = sold.GetValueOrDefault(p.Id);
            var daily = soldMonth / Catalogue.Cover.Days;
            var leadDays = last.TryGetValue(p.Id, out var from) && lead.TryGetValue(from.SupplierId, out var days) ? days : DefaultLeadDays;
            decimal? point = daily > 0 ? Math.Ceiling((daily * leadDays) + Math.Max(daily * 2, daily * leadDays / 2)) : null;
            var onOrder = pending.GetValueOrDefault(p.Id);
            // Held for online orders (Reserved) and promised on sale orders: neither is free to sell.
            var free = p.Stock - p.Reserved - promised.GetValueOrDefault(p.Id);
            var due = free <= 0 || (p.ReorderLevel is { } level && p.Stock <= level) || (point is { } rop && free + onOrder <= rop);
            if (!due)
            {
                continue;
            }

            // Cover a month and the wait, less the shelf and what's coming; at least the reorder level.
            var target = Math.Max((daily * (30 + leadDays)) + (point is { } r ? r - (daily * leadDays) : 0), p.ReorderLevel ?? 0);
            // Out of stock: at least one, even with no sales yet to go by.
            var suggested = Math.Max(free <= 0 && onOrder <= 0 ? 1 : 0, Math.Ceiling(target - Math.Max(0, free) - onOrder));
            if (suggested <= 0)
            {
                continue;
            }

            lines.Add(new ReorderLineDto(p.Id, p.Name, p.Stock, soldMonth, suggested, last.TryGetValue(p.Id, out var l) ? l.UnitCost : null, onOrder,
                daily > 0 ? decimal.Round(Math.Max(0, p.Stock) / daily, 1) : null, leadDays, point));
        }

        return (lines, last);
    }

    /// <summary>
    /// Each supplier's delivery time: days from a purchase order to the first goods received against it (a receipt or
    /// a bill), averaged over their last ten orders, between 1 and 30 days.
    /// </summary>
    internal static async Task<Dictionary<Guid, int>> LeadDaysAsync(AppDbContext db, CancellationToken ct)
    {
        var orders = await db.PurchaseOrders.AsNoTracking().Select(o => new { o.Id, o.SupplierId, o.CreatedAt }).ToListAsync(ct);
        var receipts = await db.GoodsReceipts.AsNoTracking().Where(r => r.PurchaseOrderId != null && r.CancelledAt == null)
            .Select(r => new { OrderId = r.PurchaseOrderId!.Value, At = r.ReceivedAt }).ToListAsync(ct);
        var bills = await db.Purchases.AsNoTracking().Where(x => x.PurchaseOrderId != null && x.CancelledAt == null)
            .Select(x => new { OrderId = x.PurchaseOrderId!.Value, At = x.OccurredAt }).ToListAsync(ct);
        var firstIn = receipts.Concat(bills).GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => g.Min(x => x.At));
        return orders.Where(o => firstIn.ContainsKey(o.Id))
            .GroupBy(o => o.SupplierId)
            .ToDictionary(g => g.Key, g =>
            {
                var recent = g.OrderByDescending(o => o.CreatedAt).Take(10).Select(o => (firstIn[o.Id] - o.CreatedAt).TotalDays).ToList();
                return (int)Math.Clamp(Math.Round(recent.Average()), 1, 30);
            });
    }
}
