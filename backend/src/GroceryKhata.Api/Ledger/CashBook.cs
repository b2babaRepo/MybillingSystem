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
}

/// <summary>
/// The cash book for one shop day: what came in by cash and by UPI, and from where. What the drawer should
/// hold at closing is <see cref="Cash"/> plus whatever was in it at opening.
/// </summary>
public sealed record CashBookDto(DateOnly Date, decimal Cash, decimal Upi, decimal Total, decimal SalesTotal, decimal UdhaarGiven, IReadOnlyList<CashBookLine> Lines);

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

        var (start, end) = IndiaTime.Day(day);
        var bills = await (from b in db.Bills.AsNoTracking()
                           where b.OccurredAt >= start && b.OccurredAt < end
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
        lines.Sort((a, b) => a.OccurredAt.CompareTo(b.OccurredAt));

        var cash = lines.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount);
        var upi = lines.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount);
        var udhaar = entries.Where(e => e.PaymentMode is null).Sum(e => e.Amount)
            + entries.Where(e => e.PaymentMode is not null && e.BillId is not null).Sum(e => e.Amount);
        return TypedResults.Ok(new CashBookDto(day, cash, upi, cash + upi, bills.Sum(b => b.Total), udhaar, lines));
    }
}
