using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Identity;

/// <summary>
/// Runs after authentication. Takes the shop only from the token's shop_id claim (never from the body,
/// query or route), checks that the user, membership and shop are still usable, and sets the tenant context.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenant, ShopAccessChecker access, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.UserId() is { } signedIn)
        {
            tenant.SetUser(signedIn);
        }

        var shopClaim = context.User.FindFirst(AppClaims.ShopId)?.Value;
        // Public pages (a shop's store, pay page, khata link) pick their own shop from the address: a signed-in
        // visitor's shop must not be set first, or opening another shop's page fails.
        var isPublic = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null;
        if (context.User.Identity?.IsAuthenticated != true || shopClaim is null || isPublic)
        {
            // Anonymous, a user token before a shop is picked, or a public page: no shop, so shop-owned queries return nothing.
            await next(context);
            return;
        }

        if (!Guid.TryParse(shopClaim, out var shopId) || shopId == Guid.Empty
            || !Guid.TryParse(context.User.FindFirst(AppClaims.MembershipId)?.Value, out var membershipId)
            || context.User.UserId() is not { } userId
            || !RoleNames.TryParse(context.User.FindFirst(AppClaims.Role)?.Value, out var role))
        {
            await DenyAsync(context, "Invalid token.");
            return;
        }

        // One connection for the whole request: the shop session (app.shop_id, for row-level security) is set once
        // when it opens, instead of again for every query EF runs; the context closes it at the end. The access
        // check below runs on it too, and if it fails nothing else is read. (Found by the evening-rush load test,
        // where the extra round trips doubled response times.)
        tenant.SetShop(shopId);
        await db.Database.OpenConnectionAsync(context.RequestAborted);

        var blocked = await access.BlockedAsync(userId, membershipId, shopId, role, context.RequestAborted);
        if (blocked is null)
        {
            await DenyAsync(context, "Your access to this shop has ended.", "Please sign in again.");
            return;
        }

        // A helper the owner has limited: stop what they may not do before it runs.
        var limits = role == ShopRole.Owner ? [] : blocked;
        tenant.SetBlocked(limits);
        if (StaffLimits.Blocks(limits, context.Request.Method, context.Request.Path.Value ?? "") is { } limit)
        {
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Not allowed", detail: StaffLimits.Message(limit)).ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static Task DenyAsync(HttpContext context, string title, string? detail = null) =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: title, detail: detail).ExecuteAsync(context);
}

/// <summary>
/// Checks on every request that a shop token is still good: user active, membership active, shop open, and the role the
/// same as when it was issued. A changed role refreshes the token, so a demoted owner loses owner rights at once.
/// </summary>
public sealed class ShopAccessChecker(AppDbContext db)
{
    public async Task<bool> IsAllowedAsync(Guid userId, Guid membershipId, Guid shopId, ShopRole role, CancellationToken cancellationToken) =>
        await BlockedAsync(userId, membershipId, shopId, role, cancellationToken) is not null;

    /// <summary>Null: no access. Otherwise what the owner has stopped this membership doing (often nothing).</summary>
    public async Task<string[]?> BlockedAsync(Guid userId, Guid membershipId, Guid shopId, ShopRole role, CancellationToken cancellationToken) =>
        // One query per request: membership, user and shop together.
        (await (from m in db.Memberships.AsNoTracking()
                join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                join s in db.Shops.AsNoTracking() on m.ShopId equals s.Id
                where m.Id == membershipId && m.UserId == userId && m.ShopId == shopId && m.Role == role && m.IsActive && u.IsActive && s.IsActive
                select new { m.Blocked }).ToListAsync(cancellationToken)).Select(m => m.Blocked).FirstOrDefault();
}
