using System.Globalization;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>One bill in the day's register: what it was for, how it was paid, and what went on khata.</summary>
public sealed record DayBill(
    Guid Id, string? BillNo, DateTimeOffset At, Guid? CustomerId, string? Customer, int Items, string ItemsText, decimal Total, decimal Paid,
    PaymentMode? Mode, decimal OnKhata, string? By, DateTimeOffset? CancelledAt, string? CancelReason, IReadOnlyList<BillPaymentPart>? Parts = null, string? Counter = null);

/// <summary>Today's money by how it came: cash, UPI, bank, and what was left on khata.</summary>
public sealed record DayTotals(decimal Sales, int Bills, decimal Cash, decimal Upi, decimal Bank, decimal OnKhata, decimal KhataCollected, decimal Spent, decimal Card = 0);

/// <summary>
/// The day book for one date: every bill (cancelled ones apart, never hidden), the money by mode, against the day
/// before, the items sold, and the drawer's close.
/// </summary>
public sealed record DayReportDto(
    DateOnly Date, DayTotals Totals, DayTotals Yesterday, IReadOnlyList<DayBill> Bills, IReadOnlyList<DayBill> Cancelled,
    IReadOnlyList<RangeItem> Items, CashCountDto? Galla, IReadOnlyList<TillDto>? Tills = null);

/// <summary>One counter and cashier's day: bills, sales and the money by how it came, and what they cancelled.</summary>
public sealed record TillDto(string? Counter, string? By, int Bills, decimal Sales, decimal Cash, decimal Upi, decimal Card, decimal Bank, decimal OnKhata, int Cancelled, decimal CancelledValue);

public static class DayReport
{
    public static IEndpointRouteBuilder MapDayReport(this IEndpointRouteBuilder app)
    {
        // Everyone who bills can see the day's bills; the drawer close is already shared with helpers.
        app.MapGet("/api/v1/reports/day", GetAsync).WithTags("Reports").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<IResult> GetAsync(string? date, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        DateOnly day = today;
        if (!string.IsNullOrEmpty(date) && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return Invalid("date", "Use a date like 2026-11-09.");
        }

        if (day > today)
        {
            return Invalid("date", "The day can't be in the future.");
        }

        var (bills, cancelled) = await BillsAsync(db, day, ct);
        var yesterday = await TotalsAsync(db, day.AddDays(-1), (await BillsAsync(db, day.AddDays(-1), ct)).Bills, ct);
        var items = (await SalesInsights.BuildRangeAsync(db, day, day, ct)).Items;
        var galla = await CashCountEndpoints.SummaryForAsync(db, day, ct);
        // Each counter (the device it was billed on) and cashier apart, so two phones billing at once can close separately.
        var tills = bills.Concat(cancelled).GroupBy(b => (b.Counter, b.By)).Select(g =>
        {
            var live = g.Where(b => b.CancelledAt is null).ToList();
            decimal Paid(PaymentMode mode) => live.Sum(b => (b.Parts ?? []).Where(p => p.Mode == mode).Sum(p => p.Amount));
            var gone = g.Where(b => b.CancelledAt is not null).ToList();
            return new TillDto(g.Key.Counter, g.Key.By, live.Count, live.Sum(b => b.Total), Paid(PaymentMode.Cash), Paid(PaymentMode.Upi), Paid(PaymentMode.Card), Paid(PaymentMode.Bank),
                live.Sum(b => b.OnKhata), gone.Count, gone.Sum(b => b.Total));
        }).OrderByDescending(t => t.Sales).ToList();
        return TypedResults.Ok(new DayReportDto(day, await TotalsAsync(db, day, bills, ct), yesterday, bills, cancelled, items, galla, tills));
    }

    /// <summary>The day's bills, newest first: those that stand, and those cancelled since (kept on the day they were made).</summary>
    private static async Task<(List<DayBill> Bills, List<DayBill> Cancelled)> BillsAsync(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Day(day);
        var rows = await (from b in db.Bills.AsNoTracking().Include(b => b.Lines)
                          where b.OccurredAt >= start && b.OccurredAt < end
                          join c in db.Customers.AsNoTracking() on b.CustomerId equals c.Id into customers
                          from c in customers.DefaultIfEmpty()
                          join u in db.Users.AsNoTracking() on b.CreatedByUserId equals u.Id into users
                          from u in users.DefaultIfEmpty()
                          orderby b.OccurredAt descending
                          select new { b, Customer = c == null ? null : c.Name, By = u == null ? null : u.FullName }).ToListAsync(ct);
        var list = rows.Select(r => new DayBill(
            r.b.Id, r.b.BillNo, r.b.OccurredAt, r.b.CustomerId, r.Customer, r.b.Lines.Count,
            string.Join(", ", r.b.Lines.OrderBy(l => l.LineNo).Take(4).Select(l => $"{l.Name} × {l.Quantity.ToString("0.###", CultureInfo.InvariantCulture)}")) + (r.b.Lines.Count > 4 ? $" +{r.b.Lines.Count - 4}" : ""),
            r.b.Total, r.b.PaidAmount, r.b.PaymentMode, r.b.CustomerId is null ? 0 : Math.Max(0, r.b.Total - r.b.PaidAmount - r.b.ExchangeCredit), r.By,
            r.b.CancelledAt, r.b.CancelReason, r.b.Parts(), r.b.Counter)).ToList();
        return (list.Where(b => b.CancelledAt is null).ToList(), list.Where(b => b.CancelledAt is not null).ToList());
    }

    private static async Task<DayTotals> TotalsAsync(AppDbContext db, DateOnly day, List<DayBill> bills, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Day(day);
        // Khata payments not tied to a bill (negative amounts on the khata), less any reversed later.
        var collected = -await db.LedgerEntries.AsNoTracking()
            .Where(e => (e.OccurredAt >= start && e.OccurredAt < end) && e.PaymentMode != null && e.BillId == null)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var spent = await db.Expenses.AsNoTracking().Where(e => e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        // Money by mode as the drawer saw it, the cash-book way: a bill's money on the day it was paid, and money handed
        // back on the day a bill was cancelled. So a later cancel changes the day it happened, not the day already closed.
        var book = await CashBookEndpoints.DayAsync(db, day, ct);
        decimal Paid(PaymentMode mode) => book.Lines.Where(l => l.Source is CashBookSource.Bill or CashBookSource.BillCancelled && l.Mode == mode).Sum(l => l.Amount);
        return new DayTotals(bills.Sum(b => b.Total), bills.Count, Paid(PaymentMode.Cash), Paid(PaymentMode.Upi), Paid(PaymentMode.Bank), bills.Sum(b => b.OnKhata), collected, spent, Paid(PaymentMode.Card));
    }
}
