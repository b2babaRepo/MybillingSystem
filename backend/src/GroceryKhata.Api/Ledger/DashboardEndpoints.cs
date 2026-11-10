using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Today at a glance for the home screen. <see cref="Cash"/> and <see cref="Upi"/> are money received
/// today (bills paid now plus khata payments); <see cref="Udhaar"/> is new credit given today.
/// </summary>
public sealed record TodayDto(
    DateOnly Date,
    decimal Sales,
    int Bills,
    decimal Cash,
    decimal Upi,
    decimal Udhaar,
    decimal Outstanding,
    int CustomersOwing,
    IReadOnlyList<CustomerDto> TopDues,
    decimal Collected = 0m,
    decimal Card = 0m,
    decimal Cheque = 0m,
    decimal ChequesPending = 0m,
    int ChequesPendingCount = 0,
    decimal NameLaterDue = 0m,
    int NameLaterCount = 0);

public static class DashboardEndpoints
{
    /// <summary>Shops are in India: the day runs midnight to midnight IST.</summary>
    private static readonly TimeSpan India = TimeSpan.FromHours(5.5);

    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/dashboard/today", GetTodayAsync).WithTags("Dashboard").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<TodayDto> GetTodayAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(India).DateTime);
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), India).ToUniversalTime();
        var end = start.AddDays(1);

        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null)
            .Select(b => new { b.Total, b.PaidAmount, b.PaymentMode, b.Payments })
            .ToListAsync(ct);
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => (e.PaymentMode == PaymentMode.Cheque ? e.ChequeStatus == ChequeStatus.Cleared && (e.ReversesEntryId == null ? e.ChequeSettledAt >= start && e.ChequeSettledAt < end : e.OccurredAt >= start && e.OccurredAt < end) : e.OccurredAt >= start && e.OccurredAt < end))
            .Select(e => new { e.Amount, e.PaymentMode, e.BillId, e.Kind })
            .ToListAsync(ct);

        // Money in: what bills took at the counter, plus khata payments (and their reversals) not tied to a bill.
        decimal Received(PaymentMode mode) =>
            bills.Sum(b => BillPayments.PaidBy(b.PaymentMode, b.PaidAmount, b.Payments, mode))
            - entries.Where(e => e.PaymentMode == mode && e.BillId == null).Sum(e => e.Amount);

        // New udhaar: credits (and their reversals) less what was paid at once on the same bills.
        var udhaar = entries.Where(e => e.PaymentMode == null && e.Kind != LedgerEntryKind.Opening).Sum(e => e.Amount)
            + entries.Where(e => e.PaymentMode != null && e.BillId != null).Sum(e => e.Amount);

        var pending = await Cheques.PendingAsync(db, ct);
        // Udhaar given to people not named yet, to follow up.
        var unnamed = await db.Customers.AsNoTracking().Where(c => c.NameLater && c.Balance > 0).Select(c => c.Balance).ToListAsync(ct);
        var owing = db.Customers.AsNoTracking().Where(c => c.Balance > 0);
        var topDues = await owing.OrderByDescending(c => c.Balance).Take(3)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit))
            .ToListAsync(ct);

        return new TodayDto(
            date,
            bills.Sum(b => b.Total),
            bills.Count,
            Received(PaymentMode.Cash),
            Received(PaymentMode.Upi),
            udhaar,
            await owing.SumAsync(c => (decimal?)c.Balance, ct) ?? 0m,
            await owing.CountAsync(ct),
            topDues,
            // Khata money received today (payments not on a bill are negative ledger entries).
            -entries.Where(e => e.PaymentMode != null && e.BillId == null).Sum(e => e.Amount),
            Received(PaymentMode.Card),
            Received(PaymentMode.Cheque),
            pending.Amount,
            pending.Count,
            unnamed.Sum(),
            unnamed.Count);
    }
}
