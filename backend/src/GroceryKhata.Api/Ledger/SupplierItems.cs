using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// What the shop has decided about buying one item from one supplier: whether this is the supplier to order it from
/// (preferred), and their code for it. Everything else (rates, how much came, how fast) is worked out from the
/// arrival rows, so it can never drift from what really came.
/// </summary>
public sealed class SupplierItem : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid SupplierId { get; set; }

    public Guid ProductId { get; set; }

    public bool IsPreferred { get; set; }

    public string? SupplierCode { get; set; }
}

/// <summary>One supplier of an item: what was ordered from them, what came, what is still to come, and the last rate.</summary>
public sealed record ItemSupplierDto(Guid SupplierId, string Name, string? Phone, decimal Ordered, decimal Received, decimal Pending, decimal Damaged,
    decimal? LastRate, DateTimeOffset? LastAt, int Deliveries, int? LeadDays, decimal? FillRate, bool Preferred, bool BestRate);

/// <summary>One item bought from a supplier (the supplier page's "Items from this supplier").</summary>
public sealed record SupplierProductDto(Guid ProductId, string Name, string? Category, Catalogue.Unit Unit, decimal Received, decimal Pending, decimal? LastRate, DateTimeOffset? LastAt, int Deliveries, bool Preferred);

public sealed record ReceivedCell(Guid SupplierId, decimal Ordered, decimal Received);

public sealed record ReceivedRow(Guid ProductId, string Name, string? Category, Catalogue.Unit Unit, IReadOnlyList<ReceivedCell> Cells, decimal Received, decimal Pending);

public sealed record ReceivedBySupplier(DateOnly From, DateOnly To, IReadOnlyList<SupplierRef> Suppliers, IReadOnlyList<ReceivedRow> Rows);

public sealed record SupplierRef(Guid Id, string Name);

/// <summary>
/// A supplier's last 90 days. Fill rate: units received ÷ units ordered, on orders placed in the window. On time: order
/// lines due in the window that were fully in by their expected date ÷ those due. Damage: damaged ÷ received. Rate
/// change: bill rate against order rate, (bill − order) ÷ order, averaged over billed lines that had an order rate.
/// </summary>
public sealed record ScorecardDto(Guid SupplierId, string Name, int Orders, decimal Ordered, decimal Received, decimal? FillRate, int LinesDue, decimal? OnTimeRate,
    decimal? DamageRate, int? LeadDays, decimal? RateChange, decimal ValueReceived, int Deliveries);

public sealed record PreferredRequest(bool Preferred);

public static class SupplierItems
{
    public static IEndpointRouteBuilder MapSupplierItems(this IEndpointRouteBuilder app)
    {
        var owner = app.MapGroup("/api/v1").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        owner.MapGet("/products/{id:guid}/suppliers", ItemSuppliersAsync);
        owner.MapPut("/products/{id:guid}/suppliers/{supplierId:guid}/preferred", PreferAsync);
        owner.MapGet("/suppliers/{id:guid}/items", SupplierProductsAsync);
        owner.MapGet("/reports/received-by-supplier", ReceivedAsync);
        owner.MapGet("/reports/supplier-scorecard", ScorecardAsync);
        return app;
    }

    private sealed record Stat(Guid SupplierId, Guid ProductId, decimal Received, decimal Damaged, decimal? LastRate, DateTimeOffset? LastAt, int Deliveries);

    /// <summary>Arrivals (not cancelled) since a date, per supplier and item, with the last billed rate.</summary>
    private static async Task<List<Stat>> StatsAsync(AppDbContext db, DateTimeOffset since, Guid? productId, Guid? supplierId, CancellationToken ct)
    {
        var rows = await db.ArrivalLines.AsNoTracking()
            .Where(a => a.CancelledAt == null && a.OccurredAt >= since && (productId == null || a.ProductId == productId) && (supplierId == null || a.SupplierId == supplierId))
            .Select(a => new { a.SupplierId, a.ProductId, a.Received, a.Damaged, a.Rate, a.OccurredAt }).ToListAsync(ct);
        return rows.GroupBy(r => (r.SupplierId, r.ProductId)).Select(g =>
        {
            var rated = g.Where(r => r.Rate != null).OrderByDescending(r => r.OccurredAt).FirstOrDefault();
            return new Stat(g.Key.SupplierId, g.Key.ProductId, g.Sum(r => r.Received), g.Sum(r => r.Damaged), rated?.Rate, g.Max(r => r.OccurredAt), g.Count());
        }).ToList();
    }

    /// <summary>Ordered (orders placed since the date, not cancelled) and still to come (open orders, any date), per supplier and item.</summary>
    private static async Task<(Dictionary<(Guid, Guid), decimal> Ordered, Dictionary<(Guid, Guid), decimal> Pending)> OrdersAsync(
        AppDbContext db, DateTimeOffset since, DateTimeOffset? until, Guid? productId, Guid? supplierId, CancellationToken ct)
    {
        var lines = await (from l in db.PurchaseOrderLines.AsNoTracking()
                           join o in db.PurchaseOrders.AsNoTracking() on l.OrderId equals o.Id
                           where o.Status != PurchaseOrderStatus.Cancelled && (productId == null || l.ProductId == productId) && (supplierId == null || o.SupplierId == supplierId)
                           select new { o.SupplierId, l.ProductId, l.Quantity, l.Received, l.ShortClosed, o.Status, o.CreatedAt }).ToListAsync(ct);
        var ordered = lines.Where(l => l.CreatedAt >= since && (until == null || l.CreatedAt < until)).GroupBy(l => (l.SupplierId, l.ProductId)).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var pending = lines.Where(l => l.Status == PurchaseOrderStatus.Open).GroupBy(l => (l.SupplierId, l.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.Quantity - l.Received - l.ShortClosed)));
        return (ordered, pending);
    }

    /// <summary>
    /// Who to order an item from: the preferred supplier; else, of those who delivered it in the last 180 days, the one
    /// with the lowest last rate; else whoever delivered it last.
    /// </summary>
    internal static async Task<Dictionary<Guid, (Guid SupplierId, decimal? UnitCost)>> DefaultsAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var preferred = await db.SupplierItems.AsNoTracking().Where(s => s.IsPreferred).ToDictionaryAsync(s => s.ProductId, s => s.SupplierId, ct);
        var stats = await StatsAsync(db, DateTimeOffset.MinValue, null, null, ct);
        var recent = clock.GetUtcNow().AddDays(-180);
        return stats.GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g =>
        {
            if (preferred.TryGetValue(g.Key, out var p))
            {
                return (p, g.FirstOrDefault(s => s.SupplierId == p)?.LastRate);
            }

            var pick = g.Where(s => s.LastAt >= recent && s.LastRate != null).OrderBy(s => s.LastRate).FirstOrDefault()
                ?? g.OrderByDescending(s => s.LastAt).First();
            return (pick.SupplierId, pick.LastRate);
        }).Concat(preferred.Where(p => !stats.Any(s => s.ProductId == p.Key)).Select(p => KeyValuePair.Create(p.Key, (p.Value, (decimal?)null))))
        .ToDictionary(x => x.Key, x => x.Value);
    }

    /// <summary>Fill rate per supplier over 90 days: units received on their order lines ÷ units ordered.</summary>
    internal static async Task<Dictionary<Guid, decimal>> FillRatesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-90);
        var lines = await (from l in db.PurchaseOrderLines.AsNoTracking()
                           join o in db.PurchaseOrders.AsNoTracking() on l.OrderId equals o.Id
                           where o.Status != PurchaseOrderStatus.Cancelled && o.CreatedAt >= since
                           select new { o.SupplierId, l.Quantity, l.Received }).ToListAsync(ct);
        return lines.GroupBy(l => l.SupplierId).Where(g => g.Sum(l => l.Quantity) > 0)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(l => Math.Min(l.Received, l.Quantity)) / g.Sum(l => l.Quantity), 3));
    }

    private static async Task<IResult> ItemSuppliersAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        var stats = (await StatsAsync(db, DateTimeOffset.MinValue, id, null, ct)).ToDictionary(s => s.SupplierId);
        var (ordered, pending) = await OrdersAsync(db, DateTimeOffset.MinValue, null, id, null, ct);
        var preferred = await db.SupplierItems.AsNoTracking().Where(s => s.ProductId == id && s.IsPreferred).Select(s => s.SupplierId).ToListAsync(ct);
        var ids = stats.Keys.Concat(ordered.Keys.Select(k => k.Item1)).Concat(pending.Keys.Select(k => k.Item1)).Concat(preferred).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var lead = await PharmacyAssist.LeadDaysAsync(db, ct);
        var fill = await FillRatesAsync(db, clock, ct);
        var recent = clock.GetUtcNow().AddDays(-180);
        var best = stats.Values.Where(s => s.LastRate != null && s.LastAt >= recent).OrderBy(s => s.LastRate).FirstOrDefault()?.SupplierId;
        return TypedResults.Ok(ids.Where(suppliers.ContainsKey).Select(s =>
        {
            var st = stats.GetValueOrDefault(s);
            return new ItemSupplierDto(s, suppliers[s].Name, suppliers[s].Phone, ordered.GetValueOrDefault((s, id)), st?.Received ?? 0, pending.GetValueOrDefault((s, id)), st?.Damaged ?? 0,
                st?.LastRate, st?.LastAt, st?.Deliveries ?? 0, lead.TryGetValue(s, out var d) ? d : null, fill.TryGetValue(s, out var f) ? f : null, preferred.Contains(s), best == s);
        }).OrderByDescending(x => x.Preferred).ThenByDescending(x => x.LastAt).ToList());
    }

    /// <summary>Marks (or unmarks) the supplier to order this item from; only one per item.</summary>
    private static async Task<IResult> PreferAsync(Guid id, Guid supplierId, PreferredRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == id, ct) || !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
        {
            return Invalid("SupplierId", "Pick a supplier and an item.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (request.Preferred)
        {
            await db.SupplierItems.Where(s => s.ProductId == id && s.SupplierId != supplierId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPreferred, false), ct);
        }

        var row = await db.SupplierItems.SingleOrDefaultAsync(s => s.ProductId == id && s.SupplierId == supplierId, ct);
        if (row is null)
        {
            db.SupplierItems.Add(new SupplierItem { ProductId = id, SupplierId = supplierId, IsPreferred = request.Preferred });
        }
        else
        {
            row.IsPreferred = request.Preferred;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SupplierProductsAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var stats = (await StatsAsync(db, DateTimeOffset.MinValue, null, id, ct)).ToDictionary(s => s.ProductId);
        var (_, pending) = await OrdersAsync(db, DateTimeOffset.MinValue, null, null, id, ct);
        var preferred = await db.SupplierItems.AsNoTracking().Where(s => s.SupplierId == id && s.IsPreferred).Select(s => s.ProductId).ToListAsync(ct);
        var ids = stats.Keys.Concat(pending.Keys.Select(k => k.Item2)).Concat(preferred).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Category, p.Unit }).ToDictionaryAsync(p => p.Id, ct);
        return TypedResults.Ok(ids.Where(products.ContainsKey).Select(p =>
        {
            var st = stats.GetValueOrDefault(p);
            return new SupplierProductDto(p, products[p].Name, products[p].Category, products[p].Unit, st?.Received ?? 0, pending.GetValueOrDefault((id, p)), st?.LastRate, st?.LastAt, st?.Deliveries ?? 0, preferred.Contains(p));
        }).OrderByDescending(x => x.LastAt).ThenBy(x => x.Name).ToList());
    }

    /// <summary>Items down, suppliers across: what came from whom in the dates (and what was ordered), and what is still to come.</summary>
    private static async Task<IResult> ReceivedAsync(DateOnly? from, DateOnly? to, string? category, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var (f, t) = (from ?? today.AddDays(1 - today.Day), to ?? today);
        if (t < f || t.DayNumber - f.DayNumber > 366)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["From"] = ["Pick dates in order, at most a year apart."] });
        }

        var (start, end) = (IndiaTime.Day(f).Start, IndiaTime.Day(t).End);
        var arrivals = await db.ArrivalLines.AsNoTracking().Where(a => a.CancelledAt == null && a.OccurredAt >= start && a.OccurredAt < end)
            .Select(a => new { a.SupplierId, a.ProductId, a.Received }).ToListAsync(ct);
        var (ordered, pending) = await OrdersAsync(db, start, end, null, null, ct);
        var productIds = arrivals.Select(a => a.ProductId).Concat(ordered.Keys.Select(k => k.Item2)).Concat(pending.Keys.Select(k => k.Item2)).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Category, p.Unit }).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(category))
        {
            products = products.Where(p => Catalogue.Categories.Normalise(p.Category ?? "") == Catalogue.Categories.Normalise(category)).ToList();
        }

        var came = arrivals.GroupBy(a => (a.SupplierId, a.ProductId)).ToDictionary(g => g.Key, g => g.Sum(a => a.Received));
        var rows = products.Select(p =>
        {
            var suppliers = came.Keys.Concat(ordered.Keys).Where(k => k.Item2 == p.Id).Select(k => k.Item1).Distinct();
            var cells = suppliers.Select(s => new ReceivedCell(s, ordered.GetValueOrDefault((s, p.Id)), came.GetValueOrDefault((s, p.Id)))).ToList();
            return new ReceivedRow(p.Id, p.Name, p.Category, p.Unit, cells, cells.Sum(c => c.Received), pending.Where(x => x.Key.Item2 == p.Id).Sum(x => x.Value));
        }).Where(r => r.Cells.Count > 0 || r.Pending > 0).OrderBy(r => r.Category ?? "~").ThenBy(r => r.Name).ToList();
        var supplierIds = rows.SelectMany(r => r.Cells).Select(c => c.SupplierId).Distinct().ToList();
        var names = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).OrderBy(s => s.Name).Select(s => new SupplierRef(s.Id, s.Name)).ToListAsync(ct);
        return TypedResults.Ok(new ReceivedBySupplier(f, t, names, rows));
    }

    private static async Task<IReadOnlyList<ScorecardDto>> ScorecardAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var since = now.AddDays(-90);
        var today = IndiaTime.Today(clock);
        var orderLines = await (from l in db.PurchaseOrderLines.AsNoTracking()
                                join o in db.PurchaseOrders.AsNoTracking() on l.OrderId equals o.Id
                                where o.Status != PurchaseOrderStatus.Cancelled && (o.CreatedAt >= since || (o.ExpectedOn != null && o.ExpectedOn >= DateOnly.FromDateTime(since.UtcDateTime)))
                                select new { LineId = l.Id, o.Id, o.SupplierId, o.CreatedAt, o.ExpectedOn, l.Quantity, l.Received, l.ShortClosed, l.Rate }).ToListAsync(ct);
        var lineIds = orderLines.Select(l => l.LineId).ToList();
        var lineArrivals = await db.ArrivalLines.AsNoTracking().Where(a => a.CancelledAt == null && a.OrderLineId != null && lineIds.Contains(a.OrderLineId.Value))
            .Select(a => new { LineId = a.OrderLineId!.Value, a.Received, a.OccurredAt, a.Rate }).ToListAsync(ct);
        var byLine = lineArrivals.ToLookup(a => a.LineId);
        var arrivals = await db.ArrivalLines.AsNoTracking().Where(a => a.CancelledAt == null && a.OccurredAt >= since)
            .Select(a => new { a.SupplierId, a.Received, a.Damaged, a.Rate }).ToListAsync(ct);
        var lead = await PharmacyAssist.LeadDaysAsync(db, ct);
        var supplierIds = orderLines.Select(l => l.SupplierId).Concat(arrivals.Select(a => a.SupplierId)).Distinct().ToList();
        var names = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return supplierIds.Where(names.ContainsKey).Select(s =>
        {
            var placed = orderLines.Where(l => l.SupplierId == s && l.CreatedAt >= since).ToList();
            var orderedQty = placed.Sum(l => l.Quantity);
            var receivedQty = placed.Sum(l => Math.Min(l.Received, l.Quantity));
            // Due: lines whose expected date has passed within the window. On time: all of it in by the end of that day.
            var due = orderLines.Where(l => l.SupplierId == s && l.ExpectedOn is { } e && e < today && e >= DateOnly.FromDateTime(since.UtcDateTime)).ToList();
            var onTime = due.Count(l => byLine[l.LineId].Where(a => a.OccurredAt < IndiaTime.Day(l.ExpectedOn!.Value).End).Sum(a => a.Received) >= l.Quantity);
            var came = arrivals.Where(a => a.SupplierId == s).ToList();
            var variance = placed.Where(l => l.Rate is > 0).SelectMany(l => byLine[l.LineId].Where(a => a.Rate != null).Select(a => (a.Rate!.Value - l.Rate!.Value) / l.Rate!.Value)).ToList();
            return new ScorecardDto(s, names[s], placed.Select(l => l.Id).Distinct().Count(), orderedQty, receivedQty,
                orderedQty > 0 ? decimal.Round(receivedQty / orderedQty, 3) : null, due.Count, due.Count > 0 ? decimal.Round((decimal)onTime / due.Count, 3) : null,
                came.Sum(a => a.Received) > 0 ? decimal.Round(came.Sum(a => a.Damaged) / came.Sum(a => a.Received), 3) : null,
                lead.TryGetValue(s, out var d) ? d : null, variance.Count > 0 ? decimal.Round(variance.Average(), 3) : null,
                decimal.Round(came.Sum(a => a.Received * (a.Rate ?? 0)), 2), came.Count);
        }).OrderByDescending(x => x.ValueReceived).ThenBy(x => x.Name).ToList();
    }
}
