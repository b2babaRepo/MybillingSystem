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
        if (context.User.Identity?.IsAuthenticated != true || shopClaim is null)
        {
            // Anonymous, or a user token before a shop is picked: no shop, so shop-owned queries return nothing.
            await next(context);
            return;
        }

        if (!Guid.TryParse(shopClaim, out var shopId) || shopId == Guid.Empty
            || !Guid.TryParse(context.User.FindFirst(AppClaims.MembershipId)?.Value, out var membershipId)
            || context.User.UserId() is not { } userId)
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

        if (!await access.IsAllowedAsync(userId, membershipId, shopId, context.RequestAborted))
        {
            await DenyAsync(context, "Your access to this shop has ended.", "Please sign in again.");
            return;
        }

        await next(context);
    }

    private static Task DenyAsync(HttpContext context, string title, string? detail = null) =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: title, detail: detail).ExecuteAsync(context);
}

/// <summary>Checks on every request that a shop token is still good: user active, membership active, shop open.</summary>
public sealed class ShopAccessChecker(AppDbContext db)
{
    public Task<bool> IsAllowedAsync(Guid userId, Guid membershipId, Guid shopId, CancellationToken cancellationToken) =>
        // One query per request: membership, user and shop together.
        (from m in db.Memberships.AsNoTracking()
         join u in db.Users.AsNoTracking() on m.UserId equals u.Id
         join s in db.Shops.AsNoTracking() on m.ShopId equals s.Id
         where m.Id == membershipId && m.UserId == userId && m.ShopId == shopId && m.IsActive && u.IsActive && s.IsActive
         select m.Id).AnyAsync(cancellationToken);
}
