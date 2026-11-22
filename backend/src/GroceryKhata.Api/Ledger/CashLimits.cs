using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>Cash taken from one customer today, against the ₹2 lakh a day the income-tax law allows from one person.</summary>
public sealed record CashTodayDto(Guid CustomerId, decimal Cash, decimal Limit);

/// <summary>
/// The cash rules a shop can trip over without noticing. Taking ₹2 lakh or more in cash from one person in a day (or for
/// one bill) draws a penalty equal to the amount (s.269ST, carried into the Income-tax Act 2025). The app only warns: the
/// owner decides, and the bill screen suggests UPI for the rest.
/// </summary>
public static class CashLimits
{
    public const decimal PerPersonPerDay = 200_000m;

    public static IEndpointRouteBuilder MapCashLimits(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/customers/{id:guid}/cash-today", TodayAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<CashTodayDto> TodayAsync(Guid id, DateOnly? day, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        new(id, await CashTodayAsync(db, id, day ?? IndiaTime.Today(clock), ct), PerPersonPerDay);

    /// <summary>Cash from this customer on the day: the cash part of their bills, and cash khata payments (less any taken back).</summary>
    internal static async Task<decimal> CashTodayAsync(AppDbContext db, Guid customerId, DateOnly day, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Day(day);
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.CustomerId == customerId && b.CancelledAt == null && b.OccurredAt >= start && b.OccurredAt < end)
            .Select(b => new { b.PaymentMode, b.PaidAmount, b.Payments }).ToListAsync(ct);
        var khata = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.CustomerId == customerId && e.BillId == null && e.PaymentMode == PaymentMode.Cash && e.OccurredAt >= start && e.OccurredAt < end)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        return bills.Sum(b => BillPayments.PaidBy(b.PaymentMode, b.PaidAmount, b.Payments, PaymentMode.Cash)) - khata;
    }
}
