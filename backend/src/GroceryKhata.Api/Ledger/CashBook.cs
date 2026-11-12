using System.Globalization;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>One line of the day book: money that came in (or, for a reversed payment, went back).</summary>
public sealed record CashBookLine(DateTimeOffset OccurredAt, CashBookSource Source, string Who, PaymentMode Mode, decimal Amount, Guid? BillId, Guid? MoveId = null);

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

    /// <summary>Cash given to a technician for a field trip (negative), or handed back when it is settled.</summary>
    Technician,

    /// <summary>
    /// A bill cancelled today (negative: what was paid on it goes back). The bill itself stays on the day it was made,
    /// so a day whose drawer was counted never changes afterwards.
    /// </summary>
    BillCancelled,

    /// <summary>Cash put in or taken out by hand, with a reason.</summary>
    CashAdded,
    CashRemoved,

    /// <summary>Cash paid into the bank (out of the drawer) or drawn from it (into the drawer).</summary>
    ToBank,
    FromBank,

    /// <summary>Empty bags sold to the bardana dealer (in), or bought (out).</summary>
    BagsSold,
    BagsBought,
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
        // Bills made today, cancelled or not (a later cancel shows on its own day), and bills cancelled today.
        var touched = await (from b in db.Bills.AsNoTracking()
                             where (b.OccurredAt >= start && b.OccurredAt < end) || (b.CancelledAt >= start && b.CancelledAt < end)
                             join c in db.Customers.AsNoTracking() on b.CustomerId equals c.Id into customers
                             from c in customers.DefaultIfEmpty()
                             select new { b.Id, b.OccurredAt, b.Total, b.PaidAmount, b.PaymentMode, b.Payments, b.CancelledAt, Name = c == null ? null : c.Name })
            .ToListAsync(ct);
        var bills = touched.Where(b => b.OccurredAt >= start && b.OccurredAt < end).ToList();
        var entries = await (from e in db.LedgerEntries.AsNoTracking()
                             where (e.PaymentMode == PaymentMode.Cheque ? e.ChequeStatus == ChequeStatus.Cleared && (e.ReversesEntryId == null ? e.ChequeSettledAt >= start && e.ChequeSettledAt < end : e.OccurredAt >= start && e.OccurredAt < end) : e.OccurredAt >= start && e.OccurredAt < end) && e.TripId == null
                             join c in db.Customers.AsNoTracking() on e.CustomerId equals c.Id
                             select new { OccurredAt = e.PaymentMode == PaymentMode.Cheque && e.ReversesEntryId == null ? e.ChequeSettledAt ?? e.OccurredAt : e.OccurredAt, e.Kind, e.Amount, e.PaymentMode, e.BillId, c.Name })
            .ToListAsync(ct);

        var lines = new List<CashBookLine>();
        // One line per way a bill was paid: a ₹500 cash + ₹300 UPI bill puts ₹500 in the drawer and ₹300 under UPI.
        lines.AddRange(bills.SelectMany(b => BillPayments.Parts(b.PaymentMode, b.PaidAmount, b.Payments)
            .Select(p => new CashBookLine(b.OccurredAt, CashBookSource.Bill, b.Name ?? "", p.Mode, p.Amount, b.Id))));
        lines.AddRange(touched.Where(b => b.CancelledAt >= start && b.CancelledAt < end).SelectMany(b => BillPayments.Parts(b.PaymentMode, b.PaidAmount, b.Payments)
            .Select(p => new CashBookLine(b.CancelledAt!.Value, CashBookSource.BillCancelled, b.Name ?? "", p.Mode, -p.Amount, b.Id))));
        // Khata payments not tied to a bill (a bill's own payment is already counted on the bill line above).
        lines.AddRange(entries.Where(e => e.PaymentMode is not null && e.BillId is null)
            .Select(e => new CashBookLine(e.OccurredAt, e.Kind == LedgerEntryKind.Reversal ? CashBookSource.Reversal : CashBookSource.KhataPayment,
                e.Name, e.PaymentMode!.Value, -e.Amount, null)));
        // Money out: the day's kharcha (struck lines don't count).
        lines.AddRange(await db.Expenses.AsNoTracking()
            .Where(e => e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null && e.TripId == null)
            .Select(e => new CashBookLine(e.OccurredAt, CashBookSource.Expense, e.Note ?? e.Category.ToString(), e.Mode, -e.Amount, null))
            .ToListAsync(ct));
        // Technicians' trip cash: the advance leaves the drawer the day it is given; what they hand back comes in at settlement.
        lines.AddRange(await (from e in db.TripEntries.AsNoTracking()
                              where e.Kind == TripEntryKind.Advance && e.OccurredAt >= start && e.OccurredAt < end
                              join t in db.Trips.AsNoTracking() on e.TripId equals t.Id
                              select new CashBookLine(e.OccurredAt, CashBookSource.Technician, t.Technician, PaymentMode.Cash, -e.Amount, null)).ToListAsync(ct));
        lines.AddRange(await db.Trips.AsNoTracking().Where(t => t.SettledAt >= start && t.SettledAt < end && t.Returned != null && t.Returned != 0)
            .Select(t => new CashBookLine(t.SettledAt!.Value, CashBookSource.Technician, t.Technician, PaymentMode.Cash, t.Returned!.Value, null)).ToListAsync(ct));
        // Cash moved by hand, or between the drawer and the bank.
        lines.AddRange((await db.CashMoves.AsNoTracking().Where(m => m.OccurredAt >= start && m.OccurredAt < end && m.StruckAt == null).ToListAsync(ct))
            .Select(m => new CashBookLine(m.OccurredAt, m.Kind switch
            {
                CashMoveKind.Added => CashBookSource.CashAdded, CashMoveKind.Removed => CashBookSource.CashRemoved,
                CashMoveKind.ToBank => CashBookSource.ToBank, _ => CashBookSource.FromBank,
            }, m.Note ?? "", PaymentMode.Cash, m.CashEffect, null, m.Id)));
        // Empty bags: sold to the bardana dealer, or bought.
        lines.AddRange(await (from m in db.BagMoves.AsNoTracking()
                              where m.OccurredAt >= start && m.OccurredAt < end && m.StruckAt == null && m.Amount != null && m.Mode != null
                              join t in db.BagTypes.AsNoTracking() on m.BagTypeId equals t.Id
                              select new CashBookLine(m.OccurredAt, m.Kind == BagMoveKind.Sold ? CashBookSource.BagsSold : CashBookSource.BagsBought,
                                  m.Party ?? t.Name, m.Mode!.Value, m.Kind == BagMoveKind.Sold ? m.Amount!.Value : -m.Amount!.Value, null)).ToListAsync(ct));
        lines.Sort((a, b) => a.OccurredAt.CompareTo(b.OccurredAt));

        // Money out of the drawer that isn't a refund of money in: kharcha, cash taken out, cash put in the bank.
        static bool Out(CashBookSource s) => s is CashBookSource.Expense or CashBookSource.CashRemoved or CashBookSource.ToBank or CashBookSource.BagsBought;
        var money = lines.Where(l => !Out(l.Source)).ToList();
        var cash = money.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount);
        var upi = money.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount);
        var spent = lines.Where(l => Out(l.Source)).ToList();
        var udhaar = entries.Where(e => e.PaymentMode is null && e.Kind != LedgerEntryKind.Opening).Sum(e => e.Amount)
            + entries.Where(e => e.PaymentMode is not null && e.BillId is not null).Sum(e => e.Amount);
        return new CashBookDto(day, cash, upi, cash + upi, bills.Where(b => b.CancelledAt == null).Sum(b => b.Total), udhaar, lines,
            -spent.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount), -spent.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount));
    }
}
