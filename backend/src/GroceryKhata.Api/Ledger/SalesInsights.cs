using System.Globalization;
using System.Text;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>One item's sales in the period, and in the same number of days just before it, to compare.</summary>
public sealed record RangeItem(Guid? ProductId, string Name, Unit Unit, decimal Quantity, decimal Sales, int Bills, decimal PreviousQuantity, decimal PreviousSales);

/// <summary>An item on the shelf that hasn't sold in the period: money sitting still.</summary>
public sealed record UnsoldItem(Guid ProductId, string Name, Unit Unit, decimal Stock, DateTimeOffset? LastSold);

/// <summary>
/// Sales over any days the shop picks (a week by default), against the same number of days before: totals, each day,
/// every item ranked, and what is on the shelf but didn't sell.
/// </summary>
public sealed record SalesRangeDto(
    DateOnly From, DateOnly To, decimal Sales, int Bills, decimal Cash, decimal Digital, decimal PreviousSales, int PreviousBills,
    IReadOnlyList<DailyTotals> Days, IReadOnlyList<RangeItem> Items, IReadOnlyList<UnsoldItem> NotSold, int NamedCustomers, int WalkInBills);

/// <summary>How one customer buys: how much, how often, when last, and what they usually take.</summary>
public sealed record CustomerPattern(
    Guid CustomerId, string Name, string? Phone, int Bills, decimal Spend, DateTimeOffset FirstVisit, DateTimeOffset LastVisit, int? GapDays, int DaysSince,
    CustomerHabit Habit, IReadOnlyList<string> UsualItems);

public enum CustomerHabit
{
    /// <summary>Comes about as often as usual.</summary>
    Regular,
    /// <summary>Past their usual gap: a good time for a call or a message.</summary>
    Due,
    /// <summary>Used to come often and hasn't for a long while.</summary>
    Stopped,
    /// <summary>Too few visits to say.</summary>
    New,
}

public sealed record CustomerPatternsDto(int Days, IReadOnlyList<CustomerPattern> Customers);

public static class SalesInsights
{
    public static IEndpointRouteBuilder MapSalesInsights(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reports").WithTags("Reports");
        // What sold is for everyone who bills; who spends what is the owner's.
        group.MapGet("/range", RangeAsync).RequireAuthorization(AppPolicies.ShopMember).RequireFeature(Features.Reports);
        group.MapGet("/items.csv", ItemsCsvAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Features.Reports);
        group.MapGet("/customers", CustomersAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Features.Reports);
        return app;
    }

    /// <summary>The days asked for: "from" and "to" (India dates); none: this week from Monday to today. At most 92 days.</summary>
    private static (DateOnly From, DateOnly To)? Period(string? from, string? to, TimeProvider clock)
    {
        var today = IndiaTime.Today(clock);
        DateOnly F(string? text, DateOnly fallback) =>
            DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : fallback;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var (f, t) = (F(from, monday), F(to, today));
        return f > t || t > today || t.DayNumber - f.DayNumber > 91 || (!string.IsNullOrEmpty(from) && F(from, DateOnly.MinValue) == DateOnly.MinValue) ? null : (f, t);
    }

    private static async Task<IResult> RangeAsync(string? from, string? to, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        Period(from, to, clock) is { } p ? TypedResults.Ok(await BuildRangeAsync(db, p.From, p.To, ct)) : Invalid("from", "Choose dates up to today, at most 92 days apart.");

    public static async Task<SalesRangeDto> BuildRangeAsync(AppDbContext db, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var length = to.DayNumber - from.DayNumber + 1;
        var (start, _) = IndiaTime.Day(from);
        var (_, end) = IndiaTime.Day(to);
        var (previousStart, _) = IndiaTime.Day(from.AddDays(-length));
        var bills = db.Bills.AsNoTracking().Where(b => b.CancelledAt == null);

        // Totals and each day, summed in the database.
        var now = await bills.Where(b => b.OccurredAt >= start && b.OccurredAt < end)
            .Select(b => new { b.OccurredAt, b.Total, b.PaidAmount, b.PaymentMode, b.Payments, b.CustomerId }).ToListAsync(ct);
        var previous = await bills.Where(b => b.OccurredAt >= previousStart && b.OccurredAt < start)
            .GroupBy(b => 1).Select(g => new { Sales = g.Sum(b => b.Total), Count = g.Count() }).SingleOrDefaultAsync(ct);
        var days = Enumerable.Range(0, length).Select(i =>
        {
            var date = from.AddDays(i);
            var (ds, de) = IndiaTime.Day(date);
            var day = now.Where(b => b.OccurredAt >= ds && b.OccurredAt < de).ToList();
            return new DailyTotals(date, day.Sum(b => b.Total), day.Sum(b => b.PaidAmount));
        }).ToList();

        // Each item, this period and the one before; items typed by name group by their name.
        var lines = from l in db.BillLines.AsNoTracking()
                    join b in bills on l.BillId equals b.Id
                    where b.OccurredAt >= previousStart && b.OccurredAt < end
                    select new { l.ProductId, l.Name, l.Unit, l.Quantity, l.Amount, l.BillId, Current = b.OccurredAt >= start };
        var grouped = await lines
            .GroupBy(l => new { l.ProductId, Name = l.ProductId == null ? l.Name.ToLower() : null, l.Unit })
            .Select(g => new
            {
                g.Key.ProductId, g.Key.Unit, Name = g.Max(l => l.Name),
                Quantity = g.Where(l => l.Current).Sum(l => l.Quantity), Sales = g.Where(l => l.Current).Sum(l => l.Amount),
                Bills = g.Where(l => l.Current).Select(l => l.BillId).Distinct().Count(),
                PreviousQuantity = g.Where(l => !l.Current).Sum(l => l.Quantity), PreviousSales = g.Where(l => !l.Current).Sum(l => l.Amount),
            }).ToListAsync(ct);
        var names = await db.Products.AsNoTracking().Where(p => grouped.Select(g => g.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var items = grouped.Where(g => g.Quantity > 0 || g.Sales > 0)
            .Select(g => new RangeItem(g.ProductId, g.ProductId is { } id && names.TryGetValue(id, out var n) ? n : g.Name, g.Unit, g.Quantity, g.Sales, g.Bills, g.PreviousQuantity, g.PreviousSales))
            .OrderByDescending(i => i.Sales).ToList();

        // On the shelf, counted, and not sold in the period: biggest stock first.
        var soldIds = items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).ToHashSet();
        var shelf = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsService && !p.Uncounted && p.Stock > 0)
            .Select(p => new { p.Id, p.Name, p.Unit, p.Stock }).ToListAsync(ct);
        var unsoldIds = shelf.Where(p => !soldIds.Contains(p.Id)).Select(p => p.Id).ToList();
        var lastSold = await (from l in db.BillLines.AsNoTracking()
                              join b in bills on l.BillId equals b.Id
                              where l.ProductId != null && unsoldIds.Contains(l.ProductId.Value)
                              group b by l.ProductId into g
                              select new { Id = g.Key!.Value, Last = g.Max(b => b.OccurredAt) }).ToDictionaryAsync(x => x.Id, x => x.Last, ct);
        var notSold = shelf.Where(p => !soldIds.Contains(p.Id))
            .Select(p => new UnsoldItem(p.Id, p.Name, p.Unit, p.Stock, lastSold.TryGetValue(p.Id, out var last) ? last : null))
            .OrderBy(u => u.LastSold ?? DateTimeOffset.MinValue).ThenByDescending(u => u.Stock).Take(50).ToList();

        return new SalesRangeDto(
            from, to, now.Sum(b => b.Total), now.Count,
            now.Sum(b => BillPayments.PaidBy(b.PaymentMode, b.PaidAmount, b.Payments, PaymentMode.Cash)), now.Sum(b => b.PaidAmount - BillPayments.PaidBy(b.PaymentMode, b.PaidAmount, b.Payments, PaymentMode.Cash)),
            previous?.Sales ?? 0, previous?.Count ?? 0, days, items, notSold,
            now.Where(b => b.CustomerId != null).Select(b => b.CustomerId).Distinct().Count(), now.Count(b => b.CustomerId == null));
    }

    /// <summary>Every item sold in the days asked for, for the CA or a spreadsheet.</summary>
    private static async Task<IResult> ItemsCsvAsync(string? from, string? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (Period(from, to, clock) is not { } p)
        {
            return Invalid("from", "Choose dates up to today, at most 92 days apart.");
        }

        var report = await BuildRangeAsync(db, p.From, p.To, ct);
        static string Cell(string text) => text.Contains(',') || text.Contains('"') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
        static string Num(decimal n) => n.ToString("0.##", CultureInfo.InvariantCulture);
        var csv = new StringBuilder("Item,Unit,Quantity,Sales,Bills,Quantity before,Sales before\n");
        foreach (var i in report.Items)
        {
            csv.AppendJoin(',', Cell(i.Name), i.Unit.ToString().ToLowerInvariant(), Num(i.Quantity), Num(i.Sales), i.Bills, Num(i.PreviousQuantity), Num(i.PreviousSales)).Append('\n');
        }

        csv.Append($"\nPeriod,{p.From:yyyy-MM-dd} to {p.To:yyyy-MM-dd}\n\"Before\" is the same number of days just before.\n");
        return TypedResults.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", $"items-{p.From:yyyy-MM-dd}-to-{p.To:yyyy-MM-dd}.csv");
    }

    /// <summary>
    /// How each named customer buys over the last <paramref name="days"/> (default 90): spend, visits, the usual gap,
    /// days since the last visit, and their usual items. Walk-in bills have no name, so they aren't here.
    /// </summary>
    private static async Task<IResult> CustomersAsync(int? days, Guid? customerId, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var window = days ?? 90;
        if (window is < 7 or > 365)
        {
            return Invalid("days", "Look back between 7 and 365 days.");
        }

        var nowAt = clock.GetUtcNow();
        var since = nowAt.AddDays(-window);
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.CancelledAt == null && b.CustomerId != null && b.OccurredAt >= since && (customerId == null || b.CustomerId == customerId))
            .Select(b => new { b.Id, CustomerId = b.CustomerId!.Value, b.OccurredAt, b.Total }).ToListAsync(ct);
        var ids = bills.Select(b => b.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        var billIds = bills.Select(b => b.Id).ToList();
        var lines = await db.BillLines.AsNoTracking().Where(l => billIds.Contains(l.BillId)).Select(l => new { l.BillId, l.ProductId, l.Name }).ToListAsync(ct);
        var billOwner = bills.ToDictionary(b => b.Id, b => b.CustomerId);
        var usual = lines.GroupBy(l => billOwner[l.BillId])
            .ToDictionary(g => g.Key, g => g.GroupBy(l => l.ProductId?.ToString() ?? l.Name.ToLowerInvariant())
                .Select(x => (Name: x.First().Name, Bills: x.Select(l => l.BillId).Distinct().Count()))
                .Where(x => x.Bills >= 2).OrderByDescending(x => x.Bills).Take(3).Select(x => x.Name).ToList());

        var list = bills.GroupBy(b => b.CustomerId).Where(g => customers.ContainsKey(g.Key)).Select(g =>
        {
            // Visits are days with a bill: two bills on one day are one visit.
            var visits = g.Select(b => DateOnly.FromDateTime(b.OccurredAt.ToOffset(IndiaTime.Offset).DateTime)).Distinct().Order().ToList();
            var first = g.Min(b => b.OccurredAt);
            var last = g.Max(b => b.OccurredAt);
            int? gap = visits.Count >= 2 ? (int)Math.Round((double)(visits[^1].DayNumber - visits[0].DayNumber) / (visits.Count - 1)) : null;
            var since = IndiaTime.Today(clock).DayNumber - visits[^1].DayNumber;
            var habit = gap is not { } usualGap || visits.Count < 3 ? CustomerHabit.New
                : since > Math.Max(3 * usualGap, 21) ? CustomerHabit.Stopped
                : since > Math.Max((int)(1.5 * usualGap), usualGap + 2) ? CustomerHabit.Due
                : CustomerHabit.Regular;
            var who = customers[g.Key];
            return new CustomerPattern(g.Key, who.Name, who.Phone, visits.Count, g.Sum(b => b.Total), first, last, gap, since, habit, usual.GetValueOrDefault(g.Key) ?? []);
        }).OrderByDescending(c => c.Spend).Take(300).ToList();
        return TypedResults.Ok(new CustomerPatternsDto(window, list));
    }
}
