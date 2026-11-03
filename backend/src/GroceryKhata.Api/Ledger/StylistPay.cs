using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A stylist's commission: this percent of the price of every service they do.</summary>
public sealed class StylistRate : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The stylist's membership in this shop.</summary>
    public Guid MembershipId { get; set; }

    public decimal Percent { get; set; }
}

/// <summary>
/// What a stylist earned on one appointment: commission on the services, or a tip the customer left through the shop.
/// Owed until paid out; paying out puts the commission in the kharcha book as wages. Tips were never the shop's money,
/// so paying them out is not a cost.
/// </summary>
public sealed class StylistEarning : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid MembershipId { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid? BillId { get; set; }

    public EarningKind Kind { get; set; }

    /// <summary>For commission: the services' price it was worked out on.</summary>
    public decimal Base { get; set; }

    public decimal Percent { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
}

public enum EarningKind
{
    Commission,
    Tip,
}

public sealed record CommissionRequest(decimal Percent);

public sealed record PayoutRequest(PaymentMode? Mode);

/// <summary>One stylist's month: services done, their price, commission and tips earned, and what is still owed.</summary>
public sealed record StylistEarningsDto(Guid MembershipId, string Name, decimal Percent, int Services, decimal Sales, decimal Commission, decimal Tips, decimal Owed);

public sealed record EarningsMonthDto(string Month, IReadOnlyList<StylistEarningsDto> Stylists);

public static class StylistPay
{
    public static IEndpointRouteBuilder MapStylistPay(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/stylists").WithTags("Appointments").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/earnings", EarningsAsync);
        group.MapPut("/{membershipId:guid}/commission", SetCommissionAsync);
        group.MapPost("/{membershipId:guid}/payout", PayoutAsync);
        return app;
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>The stylist's commission percent, 0 when none is set.</summary>
    public static async Task<decimal> PercentAsync(AppDbContext db, Guid membershipId, CancellationToken ct) =>
        await db.StylistRates.AsNoTracking().Where(r => r.MembershipId == membershipId).Select(r => (decimal?)r.Percent).SingleOrDefaultAsync(ct) ?? 0;

    /// <summary>
    /// Records what a billed appointment earned its stylist: commission at their percent of the services' price as charged
    /// (GST included, as salons usually count it), and any tip.
    /// </summary>
    public static async Task RecordAsync(AppDbContext db, Guid membershipId, Guid appointmentId, Guid billId, decimal servicesTotal, decimal tip, DateTimeOffset at, CancellationToken ct)
    {
        var percent = await PercentAsync(db, membershipId, ct);
        if (percent > 0)
        {
            db.StylistEarnings.Add(new StylistEarning
            {
                MembershipId = membershipId, AppointmentId = appointmentId, BillId = billId, Kind = EarningKind.Commission, Base = servicesTotal, Percent = percent,
                Amount = decimal.Round(servicesTotal * percent / 100, 2, MidpointRounding.AwayFromZero), OccurredAt = at,
            });
        }

        if (tip > 0)
        {
            db.StylistEarnings.Add(new StylistEarning { MembershipId = membershipId, AppointmentId = appointmentId, BillId = billId, Kind = EarningKind.Tip, Amount = tip, OccurredAt = at });
        }
    }

    private static async Task<IResult> SetCommissionAsync(Guid membershipId, CommissionRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!await db.Memberships.AnyAsync(m => m.Id == membershipId && m.ShopId == tenant.ShopId, ct))
        {
            return TypedResults.NotFound();
        }

        if (request.Percent is < 0 or > 100 || decimal.Round(request.Percent, 2) != request.Percent)
        {
            return Invalid("Percent", "Commission is 0 to 100%.");
        }

        var rate = await db.StylistRates.SingleOrDefaultAsync(r => r.MembershipId == membershipId, ct);
        if (rate is null)
        {
            db.StylistRates.Add(new StylistRate { MembershipId = membershipId, Percent = request.Percent });
        }
        else
        {
            rate.Percent = request.Percent;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Each stylist's month (yyyy-MM, default this month), and everything still owed to them whenever earned.</summary>
    private static async Task<EarningsMonthDto> EarningsAsync(string? month, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow().ToOffset(IndiaTime.Offset);
        var (year, m) = DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var d) ? (d.Year, d.Month) : (now.Year, now.Month);
        var (from, to) = IndiaTime.Month(year, m);
        var people = await (from mem in db.Memberships.AsNoTracking()
                            join u in db.Users.AsNoTracking() on mem.UserId equals u.Id
                            where mem.ShopId == tenant.ShopId && mem.IsActive
                            orderby u.FullName
                            select new { mem.Id, u.FullName }).ToListAsync(ct);
        var rates = await db.StylistRates.AsNoTracking().ToDictionaryAsync(r => r.MembershipId, r => r.Percent, ct);
        var earnings = await db.StylistEarnings.AsNoTracking().Where(e => (e.OccurredAt >= from && e.OccurredAt < to) || e.PaidAt == null).ToListAsync(ct);
        var done = await db.Appointments.AsNoTracking().Include(a => a.Services)
            .Where(a => a.Status == AppointmentStatus.Done && a.StylistId != null && a.Date >= DateOnly.FromDateTime(from.DateTime) && a.Date < DateOnly.FromDateTime(to.DateTime))
            .ToListAsync(ct);
        return new EarningsMonthDto($"{year:D4}-{m:D2}", people.Select(p =>
        {
            var mine = earnings.Where(e => e.MembershipId == p.Id).ToList();
            var inMonth = mine.Where(e => e.OccurredAt >= from && e.OccurredAt < to).ToList();
            var work = done.Where(a => a.StylistId == p.Id).ToList();
            return new StylistEarningsDto(p.Id, p.FullName, rates.GetValueOrDefault(p.Id), work.Sum(a => a.Services.Count), work.Sum(a => a.Services.Sum(s => s.Price)),
                inMonth.Where(e => e.Kind == EarningKind.Commission).Sum(e => e.Amount), inMonth.Where(e => e.Kind == EarningKind.Tip).Sum(e => e.Amount),
                mine.Where(e => e.PaidAt == null).Sum(e => e.Amount));
        }).ToList());
    }

    /// <summary>Pays a stylist everything owed: commission goes in the kharcha book as wages; tips are handed over as they are.</summary>
    private static async Task<IResult> PayoutAsync(Guid membershipId, PayoutRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var owed = await db.StylistEarnings.Where(e => e.MembershipId == membershipId && e.PaidAt == null).ToListAsync(ct);
        if (owed.Count == 0)
        {
            return Invalid("MembershipId", "Nothing is owed to this stylist.");
        }

        var name = await (from m in db.Memberships join u in db.Users on m.UserId equals u.Id where m.Id == membershipId select u.FullName).SingleAsync(ct);
        var now = clock.GetUtcNow();
        var commission = owed.Where(e => e.Kind == EarningKind.Commission).Sum(e => e.Amount);
        if (commission > 0)
        {
            db.Expenses.Add(new Expense
            {
                OccurredAt = now, Amount = commission, Category = ExpenseCategory.Salary, Mode = request.Mode ?? PaymentMode.Cash,
                Note = $"Commission: {name}"[..Math.Min(120, $"Commission: {name}".Length)], CreatedByUserId = user.UserId()!.Value,
            });
        }

        foreach (var e in owed)
        {
            e.PaidAt = now;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new { commission, tips = owed.Where(e => e.Kind == EarningKind.Tip).Sum(e => e.Amount) });
    }
}
