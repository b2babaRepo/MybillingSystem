using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// What a salon sells ahead: a package (6 facials for ₹4,000, used within 180 days) or a membership (a year of 10% off
/// every service for ₹999).
/// </summary>
public sealed class PrepaidPlan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public PlanKind Kind { get; set; }

    public decimal Price { get; set; }

    /// <summary>A package's service and how many times it can be used.</summary>
    public Guid? ServiceId { get; set; }

    public int? Sessions { get; set; }

    /// <summary>A membership's discount on every service.</summary>
    public decimal? DiscountPercent { get; set; }

    public int ValidDays { get; set; }

    public bool IsActive { get; set; } = true;
}

public enum PlanKind
{
    Package,
    Membership,
}

/// <summary>
/// A package or membership a customer bought. A package is paid as an advance on the khata and becomes sales one
/// session at a time (each session billed at its share of the price); a membership is billed when sold.
/// </summary>
public sealed class CustomerPlan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid PlanId { get; set; }

    public required string Name { get; set; }

    public PlanKind Kind { get; set; }

    public Guid? ServiceId { get; set; }

    public int SessionsTotal { get; set; }

    public int SessionsLeft { get; set; }

    /// <summary>One session's share of the package price: what each use is billed at.</summary>
    public decimal SessionValue { get; set; }

    public decimal? DiscountPercent { get; set; }

    public decimal Price { get; set; }

    public DateOnly StartsOn { get; set; }

    public DateOnly ExpiresOn { get; set; }

    /// <summary>Set when a package's unused sessions were closed after it ran out.</summary>
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed record PrepaidPlanRequest(Guid Id, string? Name, PlanKind? Kind, decimal Price, Guid? ServiceId, int? Sessions, decimal? DiscountPercent, int ValidDays);

public sealed record PrepaidPlanDto(Guid Id, string Name, PlanKind Kind, decimal Price, Guid? ServiceId, string? ServiceName, int? Sessions, decimal? DiscountPercent, int ValidDays);

public sealed record SellPlanRequest(Guid Id, Guid CustomerId, Guid PlanId, PaymentMode? Mode, Guid? BillId);

public sealed record CustomerPlanDto(
    Guid Id, Guid CustomerId, string CustomerName, string? CustomerPhone, string Name, PlanKind Kind, Guid? ServiceId, int SessionsTotal, int SessionsLeft,
    decimal SessionValue, decimal? DiscountPercent, decimal Price, DateOnly StartsOn, DateOnly ExpiresOn, string State);

/// <summary>An appointment's bill before it is made: each line at the price it will be billed, and why.</summary>
public sealed record QuoteLine(Guid ProductId, string Name, decimal ListPrice, decimal Price, string? Applied);

public sealed record AppointmentQuoteDto(IReadOnlyList<QuoteLine> Lines, decimal Total);

public static class Prepaid
{
    public static IEndpointRouteBuilder MapPrepaid(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Appointments").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/prepaid-plans", PlansAsync);
        group.MapPost("/prepaid-plans", AddPlanAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/customer-plans", CustomerPlansAsync);
        group.MapPost("/customer-plans", SellAsync);
        group.MapPost("/customer-plans/{id:guid}/close", CloseAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);

    private static string State(CustomerPlan p, DateOnly today) =>
        p.ClosedAt is not null ? "closed" : p.ExpiresOn < today ? "expired" : p.Kind == PlanKind.Package && p.SessionsLeft == 0 ? "used" : "active";

    private static async Task<IReadOnlyList<PrepaidPlanDto>> PlansAsync(AppDbContext db, CancellationToken ct)
    {
        var names = await db.Products.AsNoTracking().Where(p => p.IsService).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return (await db.PrepaidPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Kind).ThenBy(p => p.Name).ToListAsync(ct))
            .Select(p => new PrepaidPlanDto(p.Id, p.Name, p.Kind, p.Price, p.ServiceId, p.ServiceId is { } s ? names.GetValueOrDefault(s) : null, p.Sessions, p.DiscountPercent, p.ValidDays))
            .ToList();
    }

    private static async Task<IResult> AddPlanAsync(PrepaidPlanRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Name?.Trim() is not { Length: >= 2 and <= 60 }) errors["Name"] = ["Name it (6 facials, Gold membership)."];
        if (request.Price <= 0 || !Money.IsValidAmount(request.Price)) errors["Price"] = ["Enter the price."];
        if (request.ValidDays is < 1 or > 1100) errors["ValidDays"] = ["Valid for 1 day to 3 years."];
        if (request.Kind == PlanKind.Package)
        {
            if (request.Sessions is not (>= 2 and <= 100)) errors["Sessions"] = ["How many sessions (2 to 100)?"];
            if (request.ServiceId is not { } service || !await db.Products.AnyAsync(p => p.Id == service && p.IsService, ct)) errors["ServiceId"] = ["Choose the service."];
        }
        else if (request.Kind == PlanKind.Membership)
        {
            if (request.DiscountPercent is not (> 0 and <= 100)) errors["DiscountPercent"] = ["Enter the members' discount %."];
        }
        else
        {
            errors["Kind"] = ["A package or a membership."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!await db.PrepaidPlans.AnyAsync(p => p.Id == request.Id, ct))
        {
            db.PrepaidPlans.Add(new PrepaidPlan
            {
                Id = request.Id, Name = request.Name!.Trim(), Kind = request.Kind!.Value, Price = request.Price, ServiceId = request.Kind == PlanKind.Package ? request.ServiceId : null,
                Sessions = request.Kind == PlanKind.Package ? request.Sessions : null, DiscountPercent = request.Kind == PlanKind.Membership ? request.DiscountPercent : null, ValidDays = request.ValidDays,
            });
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(await PlansAsync(db, ct));
    }

    /// <summary>A customer's plans, or (no customer) every live plan running out within <paramref name="expiringDays"/> days, for reminders.</summary>
    private static async Task<IReadOnlyList<CustomerPlanDto>> CustomerPlansAsync(Guid? customerId, int? expiringDays, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = Today(clock);
        var query = db.CustomerPlans.AsNoTracking();
        if (customerId is { } c)
        {
            query = query.Where(p => p.CustomerId == c);
        }
        else
        {
            var until = today.AddDays(Math.Clamp(expiringDays ?? 30, 1, 365));
            query = query.Where(p => p.ClosedAt == null && p.ExpiresOn <= until && (p.Kind == PlanKind.Membership || p.SessionsLeft > 0));
        }

        var plans = await query.OrderBy(p => p.ExpiresOn).Take(300).ToListAsync(ct);
        var ids = plans.Select(p => p.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => new { x.Name, x.Phone }, ct);
        return plans.Select(p => new CustomerPlanDto(p.Id, p.CustomerId, customers.GetValueOrDefault(p.CustomerId)?.Name ?? "", customers.GetValueOrDefault(p.CustomerId)?.Phone,
            p.Name, p.Kind, p.ServiceId, p.SessionsTotal, p.SessionsLeft, p.SessionValue, p.DiscountPercent, p.Price, p.StartsOn, p.ExpiresOn, State(p, today))).ToList();
    }

    /// <summary>
    /// Sells a plan to a khata customer. A package is an advance (a khata payment): it becomes sales as its sessions are
    /// used. A membership is billed now. Either way the customer pays now, in cash or UPI.
    /// </summary>
    private static async Task<IResult> SellAsync(SellPlanRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.CustomerPlans.AnyAsync(p => p.Id == request.Id, ct))
        {
            return TypedResults.Ok(await CustomerPlansAsync(request.CustomerId, null, db, clock, ct));
        }

        var plan = await db.PrepaidPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, ct);
        if (plan is null)
        {
            return Invalid("PlanId", "Choose the package or membership.");
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
        {
            return Invalid("CustomerId", "Choose a khata customer: the plan stays on their record.");
        }

        var userId = user.UserId()!.Value;
        if (plan.Kind == PlanKind.Package)
        {
            var paid = await ledger.AddKhataEntryAsync(
                new KhataEntryRequest(Guid.CreateVersion7(), request.CustomerId, KhataEntryType.Payment, plan.Price, request.Mode, $"Package bought: {plan.Name}", null), userId, ct, IsOwner(user));
            if (paid.Value is null)
            {
                return paid.ToHttp("");
            }
        }
        else
        {
            var bill = await ledger.CreateBillAsync(
                new BillRequest(request.BillId ?? Guid.CreateVersion7(), request.CustomerId, [new BillLineRequest(null, $"Membership: {plan.Name} ({plan.ValidDays} days)", 1, Unit.Piece, plan.Price)], plan.Price, request.Mode, null),
                userId, ct, isOwner: IsOwner(user), taxSnapshot: [(5m, "999729")]);
            if (bill.Value is null)
            {
                return bill.ToHttp("");
            }
        }

        var today = Today(clock);
        db.CustomerPlans.Add(new CustomerPlan
        {
            Id = request.Id, CustomerId = request.CustomerId, PlanId = plan.Id, Name = plan.Name, Kind = plan.Kind, ServiceId = plan.ServiceId,
            SessionsTotal = plan.Sessions ?? 0, SessionsLeft = plan.Sessions ?? 0, SessionValue = plan.Sessions is > 0 ? decimal.Round(plan.Price / plan.Sessions.Value, 2) : 0,
            DiscountPercent = plan.DiscountPercent, Price = plan.Price, StartsOn = today, ExpiresOn = today.AddDays(plan.ValidDays),
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await CustomerPlansAsync(request.CustomerId, null, db, clock, ct));
    }

    /// <summary>
    /// The prices an appointment's services are billed at for this customer: a live package covering the service bills
    /// it at the session's share (paid from the advance), else a live membership takes its discount off.
    /// </summary>
    public static async Task<(List<QuoteLine> Lines, List<(CustomerPlan Plan, int Uses)> Used)> PriceAsync(
        AppDbContext db, Guid? customerId, IReadOnlyList<AppointmentService> services, DateOnly today, bool track, CancellationToken ct)
    {
        var plans = customerId is { } c
            ? await (track ? db.CustomerPlans : db.CustomerPlans.AsNoTracking()).Where(p => p.CustomerId == c && p.ClosedAt == null && p.ExpiresOn >= today).OrderBy(p => p.ExpiresOn).ToListAsync(ct)
            : [];
        var left = plans.ToDictionary(p => p.Id, p => p.SessionsLeft);
        var membership = plans.Where(p => p.Kind == PlanKind.Membership).MaxBy(p => p.DiscountPercent);
        var lines = new List<QuoteLine>();
        var used = new Dictionary<Guid, int>();
        foreach (var s in services.OrderBy(x => x.CreatedAt))
        {
            var package = plans.FirstOrDefault(p => p.Kind == PlanKind.Package && p.ServiceId == s.ProductId && left[p.Id] > 0);
            if (package is not null)
            {
                left[package.Id]--;
                used[package.Id] = used.GetValueOrDefault(package.Id) + 1;
                lines.Add(new QuoteLine(s.ProductId, $"{s.Name} (package {package.SessionsTotal - left[package.Id]}/{package.SessionsTotal})", s.Price, package.SessionValue, "package"));
            }
            else if (membership?.DiscountPercent is { } off)
            {
                lines.Add(new QuoteLine(s.ProductId, $"{s.Name} ({off:0.##}% member)", s.Price, decimal.Round(s.Price * (100 - off) / 100, 2, MidpointRounding.AwayFromZero), "membership"));
            }
            else
            {
                lines.Add(new QuoteLine(s.ProductId, s.Name, s.Price, s.Price, null));
            }
        }

        return (lines, used.Select(u => (plans.Single(p => p.Id == u.Key), u.Value)).ToList());
    }

    /// <summary>
    /// A package that ran out with sessions unused: the unused value (already paid) becomes sales on a bill, so the
    /// advance on the khata is used up and the package closes.
    /// </summary>
    private static async Task<IResult> CloseAsync(Guid id, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var plan = await db.CustomerPlans.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        if (plan.ClosedAt is not null)
        {
            return TypedResults.NoContent();
        }

        if (plan.Kind != PlanKind.Package || plan.ExpiresOn >= Today(clock))
        {
            return Invalid("Id", "Only a package that has run out is closed.");
        }

        if (plan.SessionsLeft > 0)
        {
            var bill = await ledger.CreateBillAsync(
                new BillRequest(Guid.CreateVersion7(), plan.CustomerId, [new BillLineRequest(null, $"{plan.Name}: {plan.SessionsLeft} unused session(s), expired", plan.SessionsLeft, Unit.Piece, plan.SessionValue)], 0, null, null),
                user.UserId()!.Value, ct, isOwner: IsOwner(user), taxSnapshot: [(5m, "999729")]);
            if (bill.Value is null)
            {
                return bill.ToHttp("");
            }
        }

        plan.ClosedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
