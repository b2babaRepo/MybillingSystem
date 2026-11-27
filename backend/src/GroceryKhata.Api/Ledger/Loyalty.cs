using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Ledger;

public enum LoyaltyKind
{
    /// <summary>Points for a bill (1 for each <see cref="Shops.Shop.LoyaltyEarnPer"/> rupees).</summary>
    Earn,

    /// <summary>Points spent as money off a bill.</summary>
    Redeem,

    /// <summary>A cancelled bill's points put back as they were.</summary>
    Reverse,
}

/// <summary>
/// One move of a customer's loyalty points: earned on a bill, spent on a bill, or put back when a bill is cancelled.
/// The balance is the sum, so it can always be traced bill by bill.
/// </summary>
public sealed class LoyaltyEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? BillId { get; set; }

    public LoyaltyKind Kind { get; set; }

    public int Points { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

public sealed record LoyaltyDto(int Points, decimal Value, decimal? EarnPer, decimal PointValue);

public static class Loyalty
{
    public static IEndpointRouteBuilder MapLoyalty(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers/{id:guid}/loyalty").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", GetAsync);
        group.MapPost("/code", SendCodeAsync).RequireRateLimiting(AuthSetup.OtpSendRateLimit);
        return app;
    }

    public static Task<int> BalanceAsync(AppDbContext db, Guid customerId, CancellationToken ct) =>
        db.LoyaltyEntries.Where(e => e.CustomerId == customerId).SumAsync(e => e.Points, ct);

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.LoyaltyEarnPer, s.LoyaltyPointValue }).SingleAsync(ct);
        var points = await BalanceAsync(db, id, ct);
        return TypedResults.Ok(new LoyaltyDto(points, points * shop.LoyaltyPointValue, shop.LoyaltyEarnPer, shop.LoyaltyPointValue));
    }

    /// <summary>Sends the customer a code by SMS: they read it out at the counter to spend their points.</summary>
    private static async Task<IResult> SendCodeAsync(Guid id, AppDbContext db, IServiceProvider services, IOptions<OtpOptions> options, TimeProvider clock, ILogger<OtpCode> logger, CancellationToken ct)
    {
        var phone = await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Phone).SingleOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(phone))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["This customer has no mobile number for the code."] });
        }

        return await OtpSignIn.SendAsync(new OtpSendRequest(phone), db, services, options, clock, logger, ct);
    }

    /// <summary>
    /// Points the bill asks to spend: the shop runs points, the customer has them, and the code sent to the customer's
    /// mobile is right (it is used up). Returns the money they take off, or the problem.
    /// </summary>
    public static async Task<(decimal Value, string? Problem)> CheckRedeemAsync(
        AppDbContext db, Guid shopId, Guid? customerId, int points, string? code, OtpOptions options, DateTimeOffset now, CancellationToken ct)
    {
        if (customerId is not { } id)
        {
            return (0, "Choose the customer whose points these are.");
        }

        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => new { s.LoyaltyEarnPer, s.LoyaltyPointValue }).SingleAsync(ct);
        if (shop.LoyaltyEarnPer is null)
        {
            return (0, "This shop doesn't run loyalty points.");
        }

        if (points > await BalanceAsync(db, id, ct))
        {
            return (0, "The customer doesn't have that many points.");
        }

        var phone = PhoneNumber.Normalize(await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Phone).SingleOrDefaultAsync(ct));
        var otp = await OtpSignIn.MatchAsync(db, phone, code, options, now, ct);
        if (otp is null)
        {
            return (0, "The customer's code is wrong or has expired. Send a new one.");
        }

        otp.UsedAt = now;
        return (points * shop.LoyaltyPointValue, null);
    }

    /// <summary>Points a bill of this amount earns: 1 for each full earnPer rupees; none when the shop doesn't run points.</summary>
    public static int Earned(decimal amount, decimal? earnPer) => earnPer is > 0 && amount > 0 ? (int)Math.Floor(amount / earnPer.Value) : 0;
}
