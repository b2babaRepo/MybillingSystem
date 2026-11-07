using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

public sealed record AddPersonRequest(string? Name, string? Phone, ShopRole? Role, string? Pin);

/// <summary>
/// Adding owners and helpers to a shop, within its plan's limits (Silver: 2 owners, 3 helper logins). The same rules
/// for the owner in Settings and an admin in the shop's detail. Owners sign in with their number and an SMS code (or a
/// password they set); helpers with the 4-digit PIN they're given. Turned-off people don't count.
/// </summary>
public static class ShopPeople
{
    public static async Task<IResult?> CheckAsync(AddPersonRequest request, Guid shopId, AppDbContext db, Billing.Entitlements entitlements, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Name?.Trim() is not { Length: >= 2 and <= 120 }) errors["Name"] = ["Enter the person's name."];
        if (PhoneNumber.Normalize(request.Phone) is null) errors["Phone"] = ["Enter a 10-digit mobile number."];
        if (request.Role is null) errors["Role"] = ["Choose owner or helper."];
        if (request.Role == ShopRole.Staff && Pins.Problem(request.Pin) is { } pin) errors["Pin"] = [pin];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var plan = await entitlements.ForShopAsync(shopId, ct);
        var count = await db.Memberships.IgnoreQueryFilters().CountAsync(m => m.ShopId == shopId && m.Role == request.Role && m.IsActive, ct);
        var (limit, what) = request.Role == ShopRole.Owner ? (plan.MaxOwners, "owners") : (plan.MaxStaff, "helper logins");
        return count >= limit
            ? TypedResults.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: $"The {plan.PlanName} plan has room for {limit} {what}. Move to a bigger plan to add more.")
            : null;
    }

    /// <summary>Adds the person (a new number gets an account). Call <see cref="CheckAsync"/> first.</summary>
    public static async Task<IResult> AddAsync(AddPersonRequest request, Guid shopId, AppDbContext db, IPasswordHasher<User> hasher, CancellationToken ct)
    {
        var phone = PhoneNumber.Normalize(request.Phone)!;
        var user = await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is null)
        {
            user = new User { Phone = phone, FullName = request.Name!.Trim() };
            db.Users.Add(user);
        }
        else if (await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == user.Id && m.ShopId == shopId, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This number is already in this shop.");
        }

        var membership = new Membership { UserId = user.Id, ShopId = shopId, Role = request.Role!.Value };
        if (request.Role == ShopRole.Staff)
        {
            membership.PinHash = hasher.HashPassword(user, request.Pin!);
        }

        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/shop/staff/{membership.Id}",
            new StaffDto(membership.Id, user.Id, user.FullName, user.Phone, membership.Role, true, membership.PinHash != null));
    }

    /// <summary>Turns someone's access off or on. The last active owner can't be turned off; signing out follows at once.</summary>
    public static async Task<IResult> SetActiveAsync(Guid membershipId, bool active, Guid shopId, Guid? actingUserId, AppDbContext db, Billing.Entitlements entitlements, CancellationToken ct)
    {
        var membership = await db.Memberships.IgnoreQueryFilters().SingleOrDefaultAsync(m => m.Id == membershipId && m.ShopId == shopId, ct);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        if (!active && membership.Role == ShopRole.Owner)
        {
            if (membership.UserId == actingUserId)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "You can't turn off your own access.");
            }

            if (!await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.ShopId == shopId && m.Role == ShopRole.Owner && m.IsActive && m.Id != membershipId, ct))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A shop needs at least one owner.");
            }
        }

        // Coming back counts against the plan like joining does, or off-and-on would beat the limit.
        if (active && !membership.IsActive)
        {
            var plan = await entitlements.ForShopAsync(shopId, ct);
            var count = await db.Memberships.IgnoreQueryFilters().CountAsync(m => m.ShopId == shopId && m.Role == membership.Role && m.IsActive, ct);
            var limit = membership.Role == ShopRole.Owner ? plan.MaxOwners : plan.MaxStaff;
            if (count >= limit)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: $"The {plan.PlanName} plan has room for {limit}. Turn someone off or move to a bigger plan.");
            }
        }

        membership.IsActive = active;
        if (!active)
        {
            await db.RefreshTokens.Where(t => t.MembershipId == membershipId).ExecuteDeleteAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
