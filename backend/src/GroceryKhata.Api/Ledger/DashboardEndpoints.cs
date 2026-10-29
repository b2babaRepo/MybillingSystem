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
    IReadOnlyList<CustomerDto> TopDues);

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
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end)
            .Select(b => new { b.Total, b.PaidAmount, b.PaymentMode })
            .ToListAsync(ct);
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.OccurredAt >= start && e.OccurredAt < end)
            .Select(e => new { e.Amount, e.PaymentMode, e.BillId })
            .ToListAsync(ct);

        // Money in: what bills took at the counter, plus khata payments (and their reversals) not tied to a bill.
        decimal Received(PaymentMode mode) =>
            bills.Where(b => b.PaymentMode == mode).Sum(b => b.PaidAmount)
            - entries.Where(e => e.PaymentMode == mode && e.BillId == null).Sum(e => e.Amount);

        // New udhaar: credits (and their reversals) less what was paid at once on the same bills.
        var udhaar = entries.Where(e => e.PaymentMode == null).Sum(e => e.Amount)
            + entries.Where(e => e.PaymentMode != null && e.BillId != null).Sum(e => e.Amount);

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
            topDues);
    }
}
