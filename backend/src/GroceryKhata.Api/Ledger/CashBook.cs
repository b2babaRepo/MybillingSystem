using System.Globalization;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>One line of the day book: money that came in (or, for a reversed payment, went back).</summary>
public sealed record CashBookLine(DateTimeOffset OccurredAt, CashBookSource Source, string Who, PaymentMode Mode, decimal Amount, Guid? BillId);

public enum CashBookSource
{
    /// <summary>Paid at the counter on a bill.</summary>
    Bill,

    /// <summary>A khata payment (jama).</summary>
    KhataPayment,

    /// <summary>A khata payment taken back by a reversal (negative).</summary>
    Reversal,

    /// <summary>Money spent from the kharcha page (negative). Who holds the heading ("Tea", "Rent").</summary>
    Expense,
}

/// <summary>
/// The cash book for one shop day: what came in by cash and by UPI, and from where. What the drawer should
/// hold at closing is <see cref="Cash"/> plus whatever was in it at opening.
/// </summary>
public sealed record CashBookDto(
    DateOnly Date, decimal Cash, decimal Upi, decimal Total, decimal SalesTotal, decimal UdhaarGiven, IReadOnlyList<CashBookLine> Lines,
    decimal SpentCash = 0, decimal SpentUpi = 0)
{
    /// <summary>What the drawer gained today: cash in less cash spent (add the opening cash for what it should hold).</summary>
    public decimal NetCash => Cash - SpentCash;
}

public static class CashBookEndpoints
{
    public static IEndpointRouteBuilder MapCashBook(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/cash-book", GetAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<IResult> GetAsync(string? date, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        DateOnly day;
        if (string.IsNullOrEmpty(date))
        {
            day = IndiaTime.Today(clock);
        }
        else if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use a date like 2026-10-27."] });
        }

        return TypedResults.Ok(await DayAsync(db, day, ct));
    }

    /// <summary>One day's money in and out, by cash and UPI (also used by the evening cash count).</summary>
    internal static async Task<CashBookDto> DayAsync(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Day(day);
        var bills = await (from b in db.Bills.AsNoTracking()
                           where b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null
                           join c in db.Customers.AsNoTracking() on b.CustomerId equals c.Id into customers
                           from c in customers.DefaultIfEmpty()
                           select new { b.Id, b.OccurredAt, b.Total, b.PaidAmount, b.PaymentMode, Name = c == null ? null : c.Name })
            .ToListAsync(ct);
        var entries = await (from e in db.LedgerEntries.AsNoTracking()
                             where e.OccurredAt >= start && e.OccurredAt < end
                             join c in db.Customers.AsNoTracking() on e.CustomerId equals c.Id
                             select new { e.OccurredAt, e.Kind, e.Amount, e.PaymentMode, e.BillId, c.Name })
            .ToListAsync(ct);

        var lines = new List<CashBookLine>();
        lines.AddRange(bills.Where(b => b.PaidAmount > 0 && b.PaymentMode is not null)
            .Select(b => new CashBookLine(b.OccurredAt, CashBookSource.Bill, b.Name ?? "", b.PaymentMode!.Value, b.PaidAmount, b.Id)));
        // Khata payments not tied to a bill (a bill's own payment is already counted on the bill line above).
        lines.AddRange(entries.Where(e => e.PaymentMode is not null && e.BillId is null)
            .Select(e => new CashBookLine(e.OccurredAt, e.Kind == LedgerEntryKind.Reversal ? CashBookSource.Reversal : CashBookSource.KhataPayment,
                e.Name, e.PaymentMode!.Value, -e.Amount, null)));
        // Money out: the day's kharcha (struck lines don't count).
        lines.AddRange(await db.Expenses.AsNoTracking()
            .Where(e => e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null)
            .Select(e => new CashBookLine(e.OccurredAt, CashBookSource.Expense, e.Note ?? e.Category.ToString(), e.Mode, -e.Amount, null))
            .ToListAsync(ct));
        lines.Sort((a, b) => a.OccurredAt.CompareTo(b.OccurredAt));

        var money = lines.Where(l => l.Source != CashBookSource.Expense).ToList();
        var cash = money.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount);
        var upi = money.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount);
        var spent = lines.Where(l => l.Source == CashBookSource.Expense).ToList();
        var udhaar = entries.Where(e => e.PaymentMode is null).Sum(e => e.Amount)
            + entries.Where(e => e.PaymentMode is not null && e.BillId is not null).Sum(e => e.Amount);
        return new CashBookDto(day, cash, upi, cash + upi, bills.Sum(b => b.Total), udhaar, lines,
            -spent.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount), -spent.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount));
    }
}
