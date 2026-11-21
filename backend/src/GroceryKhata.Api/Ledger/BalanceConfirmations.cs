using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// The customer, signed in on their own proved number, confirmed that their khata balance at this shop was this much.
/// Rows are only added. A written acknowledgement of a debt restarts the three years the shop has to recover it.
/// </summary>
public sealed class BalanceConfirmation : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid UserId { get; set; }

    public decimal Balance { get; set; }

    /// <summary>The number they were signed in with when they confirmed.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(16)]
    public string? Phone { get; set; }
}

public sealed record ConfirmBalanceRequest(decimal Balance);

/// <summary>The latest confirmation: shown to both sides on the statement.</summary>
public sealed record BalanceConfirmedDto(decimal Balance, DateTimeOffset At);

public static class BalanceConfirmations
{
    public static IEndpointRouteBuilder MapBalanceConfirmations(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/me/khata/{shopId:guid}/{customerId:guid}/confirm-balance", ConfirmAsync).WithTags("Khata").RequireAuthorization()
            .RequireRateLimiting(Planner.StoreEndpoints.StoreRateLimit);
        return app;
    }

    public static Task<BalanceConfirmedDto?> LatestAsync(AppDbContext db, Guid customerId, CancellationToken ct) =>
        db.BalanceConfirmations.AsNoTracking().Where(c => c.CustomerId == customerId).OrderByDescending(c => c.CreatedAt)
            .Select(c => new BalanceConfirmedDto(c.Balance, c.CreatedAt)).FirstOrDefaultAsync(ct);

    /// <summary>
    /// The person confirms the balance they see. It must still be the balance (the shop may have added something
    /// meanwhile), on a khata confirmed as theirs with the shop's code.
    /// </summary>
    private static async Task<IResult> ConfirmAsync(Guid shopId, Guid customerId, ConfirmBalanceRequest request, ClaimsPrincipal user, AppDbContext db,
        IServiceScopeFactory scopes, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var phone = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.PhoneVerifiedAt != null).Select(u => u.Phone).SingleOrDefaultAsync(ct);
        if (phone is null)
        {
            return TypedResults.NotFound();
        }

        return await Planner.CustomerExtras.ReadAsShopAsync(scopes, shopId, async shopDb =>
        {
            var balance = await shopDb.Customers.Where(c => c.Id == customerId && c.Phone == phone && c.PhoneConfirmedAt != null).Select(c => (decimal?)c.Balance).SingleOrDefaultAsync(ct);
            if (balance is null)
            {
                return (IResult)TypedResults.NotFound();
            }

            if (balance != request.Balance)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "The balance has changed. Look at the khata again before confirming.");
            }

            shopDb.BalanceConfirmations.Add(new BalanceConfirmation { CustomerId = customerId, UserId = userId, Balance = balance.Value, Phone = phone });
            await shopDb.SaveChangesAsync(ct);
            return TypedResults.Ok(new BalanceConfirmedDto(balance.Value, DateTimeOffset.UtcNow));
        });
    }
}
