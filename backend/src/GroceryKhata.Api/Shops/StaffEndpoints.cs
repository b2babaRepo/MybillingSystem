using System.Security.Claims;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

public sealed record StaffDto(Guid MembershipId, Guid UserId, string Name, string Phone, ShopRole Role, bool IsActive, bool HasPin);

public sealed record AddStaffRequest(string? Name, string? Phone, string? Pin);

public sealed record PinRequest(string? Pin);

/// <summary>
/// The owner's staff list: add a helper by name and phone with a counter PIN, change the PIN, turn access off or
/// on. Staff sign in with phone + PIN and land straight in this shop; they bill and keep khata but can't see owner
/// screens (reports, settings, export, answering disputes, late removals). Turning access off works at once: the
/// next request from their phone is refused (the shop check runs on every request).
/// </summary>
public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaff(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shop/staff").WithTags("Staff").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync).RequireFeature(Billing.Features.Staff).AddEndpointFilter(StaffLimitAsync);
        group.MapPost("/{membershipId:guid}/pin", SetPinAsync);
        group.MapPost("/{membershipId:guid}/active", SetActiveAsync);
        return app;
    }

    /// <summary>The plan's limit on helpers (Pro: 5). Turned-off helpers don't count.</summary>
    private static async ValueTask<object?> StaffLimitAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var tenant = services.GetRequiredService<ITenantContext>();
        var entitlement = await services.GetRequiredService<Billing.Entitlements>().ForShopAsync(tenant.ShopId, context.HttpContext.RequestAborted);
        var helpers = await services.GetRequiredService<AppDbContext>().Memberships
            .CountAsync(m => m.ShopId == tenant.ShopId && m.Role == ShopRole.Staff && m.IsActive, context.HttpContext.RequestAborted);
        return helpers >= entitlement.MaxStaff
            ? TypedResults.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: $"The {entitlement.PlanName} plan has room for {entitlement.MaxStaff} helpers.")
            : await next(context);
    }

    private static async Task<IReadOnlyList<StaffDto>> ListAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await (from m in db.Memberships.AsNoTracking()
               join u in db.Users.AsNoTracking() on m.UserId equals u.Id
               where m.ShopId == tenant.ShopId
               orderby m.Role, u.FullName
               select new StaffDto(m.Id, u.Id, u.FullName, u.Phone, m.Role, m.IsActive, m.PinHash != null))
            .ToListAsync(ct);

    /// <summary>
    /// Adds a helper. A new phone gets an account with no password (they sign in with the PIN); a phone that already
    /// has an account (a household, or another shop's staff) just gains this shop.
    /// </summary>
    private static async Task<IResult> AddAsync(
        AddStaffRequest request, AppDbContext db, ITenantContext tenant, IPasswordHasher<User> hasher, ILogger<StaffDto> logger, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim();
        var phone = PhoneNumber.Normalize(request.Phone);
        if (name is not { Length: >= 2 and <= 120 })
        {
            errors[nameof(request.Name)] = ["Enter the helper's name."];
        }

        if (phone is null)
        {
            errors[nameof(request.Phone)] = ["Enter a 10-digit mobile number."];
        }

        if (Pins.Problem(request.Pin) is { } pinProblem)
        {
            errors[nameof(request.Pin)] = [pinProblem];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is null)
        {
            user = new User { Phone = phone!, FullName = name! };
            db.Users.Add(user);
        }
        else if (await db.Memberships.AnyAsync(m => m.UserId == user.Id && m.ShopId == tenant.ShopId, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This number is already in your staff list.");
        }

        db.Memberships.Add(new Membership
        {
            UserId = user.Id,
            ShopId = tenant.ShopId,
            Role = ShopRole.Staff,
            PinHash = hasher.HashPassword(user, request.Pin!),
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Shop {ShopId} added staff {UserId}", tenant.ShopId, user.Id);
        return TypedResults.Created("/api/v1/shop/staff", (await ListAsync(db, tenant, ct)).Single(s => s.UserId == user.Id));
    }

    /// <summary>A new PIN for a helper (forgotten, or told to someone else). The old one stops working.</summary>
    private static async Task<IResult> SetPinAsync(
        Guid membershipId, PinRequest request, AppDbContext db, ITenantContext tenant, IPasswordHasher<User> hasher, CancellationToken ct)
    {
        if (Pins.Problem(request.Pin) is { } problem)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Pin)] = [problem] });
        }

        var membership = await db.Memberships.SingleOrDefaultAsync(m => m.Id == membershipId && m.ShopId == tenant.ShopId && m.Role == ShopRole.Staff, ct);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        var user = await db.Users.SingleAsync(u => u.Id == membership.UserId, ct);
        membership.PinHash = hasher.HashPassword(user, request.Pin!);
        user.FailedSignInCount = 0;
        user.LockoutEndsAt = null; // a new PIN from the owner also lifts a lockout
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Turns a helper's access off (they're signed out on their next request) or back on. The owner can't turn themself off.</summary>
    private static async Task<IResult> SetActiveAsync(
        Guid membershipId, bool active, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var membership = await db.Memberships.SingleOrDefaultAsync(m => m.Id == membershipId && m.ShopId == tenant.ShopId, ct);
        if (membership is null || membership.Role == ShopRole.Owner)
        {
            return membership is null ? TypedResults.NotFound() : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "The owner's access can't be turned off.");
        }

        membership.IsActive = active;
        if (!active)
        {
            // Their refresh tokens for this shop go too, so the phone can't quietly get a new access token.
            await db.RefreshTokens.Where(t => t.MembershipId == membershipId).ExecuteDeleteAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
