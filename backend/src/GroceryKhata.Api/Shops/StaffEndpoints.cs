using System.Security.Claims;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

public sealed record StaffDto(Guid MembershipId, Guid UserId, string Name, string Phone, ShopRole Role, bool IsActive, bool HasPin, decimal? CommissionPercent = null);

public sealed record CommissionRequest(decimal? Percent);

public sealed record SellerDto(Guid UserId, string Name);

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
        group.MapPost("", AddAsync);
        group.MapPost("/{membershipId:guid}/pin", SetPinAsync);
        group.MapPost("/{membershipId:guid}/active", SetActiveAsync);
        group.MapPost("/{membershipId:guid}/commission", SetCommissionAsync);
        // Who can be named as the salesperson on a bill line: everyone with access, for the owner and helpers alike.
        app.MapGet("/api/v1/shop/sellers", async (AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
            await (from m in db.Memberships.AsNoTracking()
                   join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                   where m.ShopId == tenant.ShopId && m.IsActive
                   orderby m.Role, u.FullName
                   select new SellerDto(u.Id, u.FullName)).ToListAsync(ct))
            .WithTags("Staff").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<IReadOnlyList<StaffDto>> ListAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await (from m in db.Memberships.AsNoTracking()
               join u in db.Users.AsNoTracking() on m.UserId equals u.Id
               where m.ShopId == tenant.ShopId
               orderby m.Role, u.FullName
               select new StaffDto(m.Id, u.Id, u.FullName, u.Phone, m.Role, m.IsActive, m.PinHash != null, m.CommissionPercent))
            .ToListAsync(ct);

    /// <summary>
    /// Adds a helper (with a PIN) or, when the plan allows, another owner. A new phone gets an account; a phone that
    /// already has one (a household, or another shop's staff) just gains this shop.
    /// </summary>
    private static async Task<IResult> AddAsync(
        AddPersonRequest request, AppDbContext db, ITenantContext tenant, Billing.Entitlements entitlements, IPasswordHasher<User> hasher,
        ILogger<StaffDto> logger, CancellationToken ct)
    {
        request = request with { Role = request.Role ?? ShopRole.Staff };
        if (await ShopPeople.CheckAsync(request, tenant.ShopId, db, entitlements, ct) is { } problem)
        {
            return problem;
        }

        var result = await ShopPeople.AddAsync(request, tenant.ShopId, db, hasher, ct);
        logger.LogInformation("Shop {ShopId} added a {Role}", tenant.ShopId, request.Role);
        return result;
    }

    /// <summary>A person's commission on their sales, 0–50%; null or 0 clears it.</summary>
    private static async Task<IResult> SetCommissionAsync(Guid membershipId, CommissionRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (request.Percent is < 0 or > 50)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Percent"] = ["Set a commission between 0 and 50%."] });
        }

        var membership = await db.Memberships.SingleOrDefaultAsync(m => m.Id == membershipId && m.ShopId == tenant.ShopId, ct);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        membership.CommissionPercent = request.Percent is > 0 ? request.Percent : null;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
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

    /// <summary>Turns someone's access off (signed out on their next request) or back on. Not your own; never the last owner.</summary>
    private static Task<IResult> SetActiveAsync(
        Guid membershipId, bool active, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        ShopPeople.SetActiveAsync(membershipId, active, tenant.ShopId, user.UserId(), db, ct);
}
