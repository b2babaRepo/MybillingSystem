using GroceryKhata.Api.Billing;
using System.Globalization;
using System.Text;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// One month of the year report. <see cref="Turnover"/> is sales without the GST inside them; <see cref="NoRate"/> is
/// sales on GST bills with no rate set, counted in turnover as they are, so a CA can check them.
/// </summary>
public sealed record YearMonthRow(string Month, int Bills, decimal Sales, decimal Gst, decimal Turnover, decimal NoRate, decimal Cash, decimal Digital);

/// <summary>A financial year (April to March) for the owner and their CA. A working aid: not a return.</summary>
public sealed record YearReport(string FinancialYear, IReadOnlyList<YearMonthRow> Months, YearMonthRow Total);

/// <summary>One customer's dues by age. The three buckets always add up to <see cref="Balance"/>.</summary>
public sealed record DueDto(
    Guid CustomerId,
    string Name,
    string? Phone,
    decimal Balance,
    decimal Days0To7,
    decimal Days8To30,
    decimal Over30,
    int OldestDays,
    DateTimeOffset? LastPaymentAt,
    DateTimeOffset? LastRemindedAt);

public sealed record DuesReport(decimal Total, decimal Days0To7, decimal Days8To30, decimal Over30, IReadOnlyList<DueDto> Customers);

public sealed record DailyTotals(DateOnly Date, decimal Sales, decimal Received);

public sealed record TopItem(string Name, decimal Quantity, Catalogue.Unit Unit, decimal Amount);

/// <summary>
/// One month at a glance. <see cref="EstimatedProfit"/> counts only bill lines whose item has a cost price;
/// <see cref="ProfitCoverage"/> says what share of sales that is, so a low number reads as "add cost prices".
/// </summary>
public sealed record MonthReport(
    string Month,
    decimal Sales,
    int Bills,
    decimal Cash,
    decimal Upi,
    decimal UdhaarGiven,
    decimal KhataCollected,
    decimal Outstanding,
    decimal EstimatedProfit,
    decimal ProfitCoverage,
    IReadOnlyList<TopItem> TopItems,
    IReadOnlyList<DailyTotals> Days,
    decimal Expenses = 0,
    decimal StockPurchases = 0,
    decimal TakenHome = 0,
    decimal Returns = 0,
    decimal StockLoss = 0,
    decimal StockValue = 0,
    decimal SupplierDue = 0)
{
    /// <summary>Estimated profit less running costs. Stock bought isn't taken off (sales already count goods at cost); money taken home isn't a cost.</summary>
    public decimal NetProfit => EstimatedProfit - Expenses;
}

/// <summary>
/// Ageing by first-in, first-out: every payment (and every reversal of udhaar) pays off the oldest udhaar first,
/// so what is still unpaid is the newest udhaar, aged by the day it was given.
/// </summary>
public static class Ageing
{
    public sealed record Entry(DateTimeOffset At, decimal Amount);

    /// <summary>The unpaid pieces of udhaar, each with the day it was given. Their sum is the balance (when positive).</summary>
    public static IReadOnlyList<Entry> Unpaid(IEnumerable<Entry> entries)
    {
        var open = new LinkedList<Entry>();
        var credit = 0m; // payments made before there was udhaar to pay: an advance, used up by later udhaar
        foreach (var entry in entries.OrderBy(e => e.At))
        {
            if (entry.Amount > 0)
            {
                var amount = entry.Amount;
                var fromAdvance = Math.Min(credit, amount);
                credit -= fromAdvance;
                amount -= fromAdvance;
                if (amount > 0)
                {
                    open.AddLast(entry with { Amount = amount });
                }
            }
            else
            {
                var paying = -entry.Amount;
                while (paying > 0 && open.First is { } oldest)
                {
                    var used = Math.Min(paying, oldest.Value.Amount);
                    paying -= used;
                    if (used == oldest.Value.Amount)
                    {
                        open.RemoveFirst();
                    }
                    else
                    {
                        oldest.Value = oldest.Value with { Amount = oldest.Value.Amount - used };
                    }
                }

                credit += paying;
            }
        }

        return [.. open];
    }
}

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Reports");
        group.MapGet("/reports/dues", (AppDbContext db, TimeProvider clock, CancellationToken ct) => DuesAsync(db, clock, ct))
            .RequireAuthorization(AppPolicies.ShopMember);
        // The month report and downloads are in Basic and Pro; the dues list (collecting money owed) is in every plan.
        group.MapGet("/reports/month", MonthAsync).RequireAuthorization(AppPolicies.ShopMember).RequireFeature(Billing.Features.Reports);
        group.MapPost("/customers/{id:guid}/reminded", RemindedAsync).RequireAuthorization(AppPolicies.ShopMember);
        // Downloads hold every customer's dues or the month's figures: the owner's data to take away.
        group.MapGet("/reports/dues.csv", DuesCsvAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/reports/year", YearAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/year.csv", YearCsvAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/month.csv", MonthCsvAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        return app;
    }

    public static async Task<DuesReport> DuesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking().Where(c => c.Balance > 0).ToListAsync(ct);
        var ids = customers.Select(c => c.Id).ToList();
        var entries = (await db.LedgerEntries.AsNoTracking().Where(e => ids.Contains(e.CustomerId))
                .Select(e => new { e.CustomerId, e.OccurredAt, e.Amount, e.PaymentMode }).ToListAsync(ct))
            .ToLookup(e => e.CustomerId);
        var today = IndiaTime.Today(clock);

        var dues = customers.Select(c =>
        {
            var own = entries[c.Id].ToList();
            var unpaid = Ageing.Unpaid(own.Select(e => new Ageing.Entry(e.OccurredAt, e.Amount)));
            int Age(Ageing.Entry e) => today.DayNumber - DateOnly.FromDateTime(e.At.ToOffset(IndiaTime.Offset).DateTime).DayNumber;
            var lastPayment = own.Where(e => e.Amount < 0 && e.PaymentMode != null).Select(e => (DateTimeOffset?)e.OccurredAt).Max();
            return new DueDto(
                c.Id, c.Name, c.Phone, c.Balance,
                unpaid.Where(e => Age(e) <= 7).Sum(e => e.Amount),
                unpaid.Where(e => Age(e) is > 7 and <= 30).Sum(e => e.Amount),
                unpaid.Where(e => Age(e) > 30).Sum(e => e.Amount),
                unpaid.Count == 0 ? 0 : Age(unpaid[0]),
                lastPayment,
                c.LastRemindedAt);
        })
        .OrderByDescending(d => d.Over30).ThenByDescending(d => d.Days8To30).ThenByDescending(d => d.Balance)
        .ToList();

        return new DuesReport(dues.Sum(d => d.Balance), dues.Sum(d => d.Days0To7), dues.Sum(d => d.Days8To30), dues.Sum(d => d.Over30), dues);
    }

    private static async Task<IResult> RemindedAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // A direct update: a reminder is bookkeeping, not an edit to audit.
        var updated = await db.Customers.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.LastRemindedAt, now), ct);
        return updated == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static async Task<IResult> MonthAsync(string? month, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        ParseOrThisMonth(month, clock) is { } period
            // Profit shows what goods cost: for the owner, not helpers.
            ? TypedResults.Ok(await BuildMonthAsync(db, period, ct) is var report && user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner))
                ? report
                : report with { EstimatedProfit = 0, ProfitCoverage = 0, Expenses = 0, StockPurchases = 0, TakenHome = 0, Returns = 0, StockLoss = 0, StockValue = 0, SupplierDue = 0 })
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });

    public static async Task<MonthReport> BuildMonthAsync(AppDbContext db, (int Year, int Month) period, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines)
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null)
            .ToListAsync(ct);
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.OccurredAt >= start && e.OccurredAt < end)
            .Select(e => new { e.OccurredAt, e.Amount, e.PaymentMode, e.BillId })
            .ToListAsync(ct);
        var productIds = bills.SelectMany(b => b.Lines).Where(l => l.ProductId != null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var costs = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id) && p.CostPrice != null)
            .ToDictionaryAsync(p => p.Id, p => p.CostPrice!.Value, ct);

        // Money in, the cash-book way: bills paid at the counter plus khata payments (and their reversals) not tied to a bill.
        decimal Received(PaymentMode mode) =>
            bills.Where(b => b.PaymentMode == mode).Sum(b => b.PaidAmount)
            - entries.Where(e => e.PaymentMode == mode && e.BillId == null).Sum(e => e.Amount);
        var khataCollected = -entries.Where(e => e.PaymentMode != null && e.BillId == null).Sum(e => e.Amount);
        var udhaar = entries.Where(e => e.PaymentMode == null).Sum(e => e.Amount)
            + entries.Where(e => e.PaymentMode != null && e.BillId != null).Sum(e => e.Amount);

        var lines = bills.SelectMany(b => b.Lines).ToList();
        // The cost when sold; bills from before that was kept use today's. Lines with neither aren't counted.
        decimal? Cost(BillLine l) => l.UnitCost ?? (l.ProductId is { } id && costs.TryGetValue(id, out var cost) ? cost : null);
        var costed = lines.Where(l => Cost(l) is not null).ToList();
        var taxed = bills.Where(b => b.Gstin != null).Select(b => b.Id).ToHashSet();
        // GST inside the price belongs to the government, not the shop.
        decimal WithoutGst(BillLine l) => taxed.Contains(l.BillId) && l.GstRate is > 0 ? decimal.Round(l.Amount / (1 + (l.GstRate.Value / 100)), 2) : l.Amount;
        var returns = -await db.LedgerEntries.AsNoTracking().Where(e => e.Kind == LedgerEntryKind.Return && e.OccurredAt >= start && e.OccurredAt < end).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        StockReason[] lossReasons = [StockReason.Damage, StockReason.Wastage, StockReason.CountCorrection];
        var moved = await (from m in db.StockMovements.AsNoTracking()
                           where m.Kind == StockMovementKind.Adjustment && m.OccurredAt >= start && m.OccurredAt < end && m.Reason != null && lossReasons.Contains(m.Reason.Value)
                           join p in db.Products.AsNoTracking() on m.ProductId equals p.Id
                           select new { m.Quantity, Cost = m.CostPrice ?? p.CostPrice }).ToListAsync(ct);
        var stockLoss = moved.Where(m => m.Cost != null).Sum(m => decimal.Round(-m.Quantity * m.Cost!.Value, 2));
        var profit = costed.Sum(l => WithoutGst(l) - Money.LineAmount(l.Quantity, Cost(l)!.Value)) - returns - stockLoss;
        var sales = bills.Sum(b => b.Total);

        var days = Enumerable.Range(0, DateTime.DaysInMonth(period.Year, period.Month)).Select(i =>
        {
            var date = new DateOnly(period.Year, period.Month, 1).AddDays(i);
            var (dayStart, dayEnd) = IndiaTime.Day(date);
            var dayBills = bills.Where(b => b.OccurredAt >= dayStart && b.OccurredAt < dayEnd).ToList();
            var received = dayBills.Sum(b => b.PaidAmount)
                - entries.Where(e => e.PaymentMode != null && e.BillId == null && e.OccurredAt >= dayStart && e.OccurredAt < dayEnd).Sum(e => e.Amount);
            return new DailyTotals(date, dayBills.Sum(b => b.Total), received);
        }).ToList();

        var top = lines.GroupBy(l => (Key: l.ProductId?.ToString() ?? l.Name.ToLowerInvariant(), l.Unit))
            .Select(g => new TopItem(g.First().Name, g.Sum(l => l.Quantity), g.Key.Unit, g.Sum(l => l.Amount)))
            .OrderByDescending(t => t.Amount).Take(10).ToList();

        var outstanding = await db.Customers.AsNoTracking().Where(c => c.Balance > 0).SumAsync(c => (decimal?)c.Balance, ct) ?? 0m;
        var spent = await db.Expenses.AsNoTracking()
            .Where(e => e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null)
            .GroupBy(e => e.Category).Select(g => new { g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(g => g.Key, g => g.Total, ct);
        decimal Spent(ExpenseCategory c) => spent.GetValueOrDefault(c);
        // Today's figures, whichever month is open: stock at cost (items with a count and a cost) and what suppliers are owed.
        var stockValue = await db.Products.AsNoTracking().Where(p => p.IsActive && p.Stock > 0 && p.CostPrice != null).SumAsync(p => (decimal?)(p.Stock * p.CostPrice!.Value), ct) ?? 0m;
        var supplierDue = await db.SupplierEntries.AsNoTracking().SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        return new MonthReport(
            $"{period.Year:D4}-{period.Month:D2}", sales, bills.Count, Received(PaymentMode.Cash), Received(PaymentMode.Upi), udhaar, khataCollected,
            outstanding, profit, sales == 0 ? 0 : decimal.Round(costed.Sum(l => l.Amount) / sales, 2), top, days,
            spent.Values.Sum() - Spent(ExpenseCategory.Purchase) - Spent(ExpenseCategory.Home), Spent(ExpenseCategory.Purchase), Spent(ExpenseCategory.Home), returns, stockLoss, stockValue, supplierDue);
    }

    private static async Task<IResult> DuesCsvAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var report = await DuesAsync(db, clock, ct);
        var csv = new StringBuilder("Name,Phone,Balance,0-7 days,8-30 days,30+ days,Oldest due (days),Last payment,Last reminded\n");
        foreach (var d in report.Customers)
        {
            csv.AppendJoin(',', Cell(d.Name), Cell(d.Phone), Num(d.Balance), Num(d.Days0To7), Num(d.Days8To30), Num(d.Over30), d.OldestDays,
                Day(d.LastPaymentAt), Day(d.LastRemindedAt)).Append('\n');
        }

        return Csv(csv, $"dues-{IndiaTime.Today(clock):yyyy-MM-dd}.csv");
    }

    private static async Task<IResult> YearAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await BuildYearAsync(db, fy ?? ThisFinancialYear(clock), ct));

    private static async Task<IResult> YearCsvAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var report = await BuildYearAsync(db, fy ?? ThisFinancialYear(clock), ct);
        var csv = new StringBuilder("Month,Bills,Sales with GST,GST,Turnover without GST,GST rate not set,Cash received,Digital received\n");
        foreach (var m in report.Months.Append(report.Total))
        {
            csv.AppendJoin(',', m.Month, m.Bills.ToString(CultureInfo.InvariantCulture), Num(m.Sales), Num(m.Gst), Num(m.Turnover), Num(m.NoRate), Num(m.Cash), Num(m.Digital)).Append('\n');
        }

        return Csv(csv, $"year-{report.FinancialYear}.csv");
    }

    private static int ThisFinancialYear(TimeProvider clock)
    {
        var today = IndiaTime.Today(clock);
        return today.Month >= 4 ? today.Year : today.Year - 1;
    }

    /// <summary>The financial year starting 1 April of <paramref name="startYear"/>. Cancelled bills don't count.</summary>
    public static async Task<YearReport> BuildYearAsync(AppDbContext db, int startYear, CancellationToken ct)
    {
        var (start, _) = IndiaTime.Month(startYear, 4);
        var (end, _) = IndiaTime.Month(startYear + 1, 4);
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines)
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null).ToListAsync(ct);
        var khata = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.OccurredAt >= start && e.OccurredAt < end && e.BillId == null && e.PaymentMode != null)
            .Select(e => new { e.OccurredAt, e.Amount, e.PaymentMode }).ToListAsync(ct);

        var rows = new List<YearMonthRow>();
        for (var i = 0; i < 12; i++)
        {
            var (year, month) = i < 9 ? (startYear, i + 4) : (startYear + 1, i - 8);
            var (from, to) = IndiaTime.Month(year, month);
            var own = bills.Where(b => b.OccurredAt >= from && b.OccurredAt < to).ToList();
            var ownKhata = khata.Where(e => e.OccurredAt >= from && e.OccurredAt < to).ToList();
            // GST exists only on bills of a registered shop; prices include it.
            var gstLines = own.Where(b => b.Gstin != null).SelectMany(b => b.Lines).ToList();
            var gst = gstLines.Where(l => l.GstRate is > 0).GroupBy(l => l.GstRate!.Value)
                .Sum(g => { var amount = g.Sum(l => l.Amount); return amount - decimal.Round(amount / (1 + (g.Key / 100)), 2); });
            decimal Received(PaymentMode mode) => own.Where(b => b.PaymentMode == mode).Sum(b => b.PaidAmount) - ownKhata.Where(e => e.PaymentMode == mode).Sum(e => e.Amount);
            var sales = own.Sum(b => b.Total);
            rows.Add(new YearMonthRow($"{year:D4}-{month:D2}", own.Count, sales, gst, sales - gst, gstLines.Where(l => l.GstRate == null).Sum(l => l.Amount), Received(PaymentMode.Cash), Received(PaymentMode.Upi)));
        }

        var total = new YearMonthRow("Total", rows.Sum(r => r.Bills), rows.Sum(r => r.Sales), rows.Sum(r => r.Gst), rows.Sum(r => r.Turnover), rows.Sum(r => r.NoRate), rows.Sum(r => r.Cash), rows.Sum(r => r.Digital));
        return new YearReport($"{startYear}-{(startYear + 1) % 100:D2}", rows, total);
    }

    private static async Task<IResult> MonthCsvAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ParseOrThisMonth(month, clock) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var report = await BuildMonthAsync(db, period, ct);
        var csv = new StringBuilder("Date,Sales,Received\n");
        foreach (var day in report.Days)
        {
            csv.AppendJoin(',', day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Num(day.Sales), Num(day.Received)).Append('\n');
        }

        csv.Append('\n').AppendJoin(',', "Total", Num(report.Sales), Num(report.Cash + report.Upi)).Append('\n');
        return Csv(csv, $"month-{report.Month}.csv");
    }

    private static (int Year, int Month)? ParseOrThisMonth(string? month, TimeProvider clock)
    {
        if (string.IsNullOrEmpty(month))
        {
            var today = IndiaTime.Today(clock);
            return (today.Year, today.Month);
        }

        return IndiaTime.ParseMonth(month);
    }

    /// <summary>UTF-8 with a byte-order mark, so Excel shows Hindi names correctly when the file is opened.</summary>
    private static IResult Csv(StringBuilder csv, string fileName) =>
        TypedResults.File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())], "text/csv; charset=utf-8", fileName);

    private static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Day(DateTimeOffset? at) => at?.ToOffset(IndiaTime.Offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    /// <summary>Quotes a text cell, and defuses values Excel would run as a formula (= + - @).</summary>
    private static string Cell(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var safe = text[0] is '=' or '+' or '-' or '@' ? "'" + text : text;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
