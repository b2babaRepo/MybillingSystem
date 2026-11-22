using System.Collections.Concurrent;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary><see cref="Current"/>: the passcode in use, needed to change or remove it. <see cref="Passcode"/> null removes it.</summary>
public sealed record BillPasscodeRequest(string? Passcode, string? Current);

/// <summary>
/// A passcode the shop sets for undoing money: cancelling a bill, a credit note (goods taken back), cancelling an exchange
/// slip or a supplier bill. Those are owner actions already; the passcode stops someone else who picks up the owner's
/// phone. Sent in the <see cref="Header"/> header; a missing or wrong one answers 403 with problem type <see cref="ProblemType"/>.
/// Five wrong tries lock it for 10 minutes.
/// </summary>
public static class BillPasscode
{
    public const string Header = "X-Bill-Passcode";
    public const string ProblemType = "bill-passcode";
    private const int MaxTries = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(10);
    private static readonly PasswordHasher<Shop> Hasher = new();
    private static readonly ConcurrentDictionary<Guid, (int Tries, DateTimeOffset Until)> Wrong = new();

    public static IEndpointRouteBuilder MapBillPasscode(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shop/bill-passcode").WithTags("Shop");
        group.MapGet("", async (AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
            TypedResults.Ok(new { set = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.BillPasscodeHash != null).SingleAsync(ct) }))
            .RequireAuthorization(AppPolicies.ShopMember);
        group.MapPut("", SetAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>Adds the passcode check to an endpoint that undoes money.</summary>
    public static RouteHandlerBuilder RequireBillPasscode(this RouteHandlerBuilder builder) => builder.AddEndpointFilter(CheckAsync);

    public static RouteGroupBuilder RequireBillPasscode(this RouteGroupBuilder builder) => builder.AddEndpointFilter(CheckAsync);

    private static async ValueTask<object?> CheckAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        // Only what changes something: reading a bill's credit notes needs no passcode.
        if (HttpMethods.IsGet(http.Request.Method))
        {
            return await next(context);
        }

        var services = http.RequestServices;
        var tenant = services.GetRequiredService<ITenantContext>();
        var db = services.GetRequiredService<AppDbContext>();
        var clock = services.GetRequiredService<TimeProvider>();
        var hash = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.BillPasscodeHash).SingleOrDefaultAsync(http.RequestAborted);
        if (hash is null)
        {
            return await next(context);
        }

        var now = clock.GetUtcNow();
        if (Wrong.TryGetValue(tenant.ShopId, out var wrong) && wrong.Tries >= MaxTries && wrong.Until > now)
        {
            return Refuse("Too many wrong passcodes. Try again in 10 minutes.");
        }

        var given = http.Request.Headers[Header].ToString();
        if (string.IsNullOrEmpty(given))
        {
            return Refuse("Enter the bill passcode.");
        }

        if (Hasher.VerifyHashedPassword(null!, hash, given) == PasswordVerificationResult.Failed)
        {
            Wrong.AddOrUpdate(tenant.ShopId, (1, now + LockFor), (_, w) => (w.Until > now ? w.Tries + 1 : 1, now + LockFor));
            return Refuse("That passcode is wrong.");
        }

        Wrong.TryRemove(tenant.ShopId, out _);
        return await next(context);
    }

    private static IResult Refuse(string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, type: ProblemType, title: "Bill passcode needed", detail: detail);

    private static async Task<IResult> SetAsync(BillPasscodeRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var shop = await db.Shops.SingleAsync(s => s.Id == tenant.ShopId, ct);
        if (shop.BillPasscodeHash is { } current && (string.IsNullOrEmpty(request.Current) || Hasher.VerifyHashedPassword(shop, current, request.Current) == PasswordVerificationResult.Failed))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Current)] = ["Enter the passcode in use now to change it."] });
        }

        if (request.Passcode is not null && Pins.Problem(request.Passcode) is { } problem)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Passcode)] = [problem] });
        }

        shop.BillPasscodeHash = request.Passcode is null ? null : Hasher.HashPassword(shop, request.Passcode);
        await db.SaveChangesAsync(ct);
        Wrong.TryRemove(tenant.ShopId, out _);
        return TypedResults.Ok(new { set = shop.BillPasscodeHash != null });
    }
}
