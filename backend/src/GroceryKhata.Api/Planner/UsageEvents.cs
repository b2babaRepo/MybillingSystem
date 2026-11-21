using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// One step a customer took in the shopping app, counted so the platform can see where people stop: a cart
/// started, checkout opened, an order placed, a shop's change answered. No items, prices, names or phone numbers.
/// Not a shop's row: only the platform reads these, as totals.
/// </summary>
public sealed class UsageEvent : Entity
{
    public UsageKind Kind { get; set; }

    public Guid? ShopId { get; set; }

    /// <summary>Null for a guest; removed with the account.</summary>
    public Guid? UserId { get; set; }

    /// <summary>For an answer to a shop's change: seconds from packing to the answer.</summary>
    public int? Seconds { get; set; }

    /// <summary>For an order: where it came from ("store" or "list").</summary>
    public string? Detail { get; set; }
}

public enum UsageKind
{
    CartStarted,
    CheckoutOpened,
    OrderPlaced,
    ChangeAnswered,
}

public sealed record UsageSeenRequest(string? Kind);

public sealed record UsageLanguages(int En, int Hi, int Gu, int Hn);

public sealed record CustomerUsageDto(
    int Days, int CartsStarted, int CheckoutsOpened, int StoreOrders, int ListOrders,
    int? CartToOrderPercent, int? CheckoutToOrderPercent, int ChangesAnswered, int? MedianAnswerMinutes, UsageLanguages Languages);

public static class UsageEvents
{
    public static IEndpointRouteBuilder MapUsageEvents(this IEndpointRouteBuilder app)
    {
        // From the store page, guest or signed in: a cart started or checkout opened at this shop.
        app.MapPost("/api/v1/store/{slug}/seen", SeenAsync).WithTags("Online store").AllowAnonymous().RequireRateLimiting(StoreEndpoints.StoreRateLimit);
        app.MapGet("/api/v1/platform/customer-usage", UsageAsync).WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin);
        return app;
    }

    /// <summary>Adds an event to the unit of work; it is saved with the caller's own change.</summary>
    public static void Record(AppDbContext db, UsageKind kind, Guid? shopId, Guid? userId, int? seconds = null, string? detail = null) =>
        db.UsageEvents.Add(new UsageEvent { Kind = kind, ShopId = shopId, UserId = userId, Seconds = seconds, Detail = detail });

    private static async Task<IResult> SeenAsync(string slug, UsageSeenRequest request, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        UsageKind? kind = request.Kind switch { "cart" => UsageKind.CartStarted, "checkout" => UsageKind.CheckoutOpened, _ => null };
        if (kind is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Kind"] = ["Kind must be cart or checkout."] });
        }

        var shopId = await db.Shops.AsNoTracking().Where(s => s.Slug == slug && s.IsActive).Select(s => (Guid?)s.Id).SingleOrDefaultAsync(ct);
        if (shopId is null)
        {
            return TypedResults.NotFound();
        }

        Record(db, kind.Value, shopId, user.UserId());
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<CustomerUsageDto> UsageAsync(AppDbContext db, TimeProvider clock, int? days, CancellationToken ct)
    {
        var span = Math.Clamp(days ?? 30, 1, 365);
        var since = clock.GetUtcNow().AddDays(-span);
        var events = db.UsageEvents.AsNoTracking().Where(e => e.CreatedAt >= since);
        var counts = await events.GroupBy(e => new { e.Kind, e.Detail }).Select(g => new { g.Key.Kind, g.Key.Detail, Count = g.Count() }).ToListAsync(ct);
        int Count(UsageKind kind, string? detail = null) => counts.Where(c => c.Kind == kind && (detail is null || c.Detail == detail)).Sum(c => c.Count);

        var carts = Count(UsageKind.CartStarted);
        var checkouts = Count(UsageKind.CheckoutOpened);
        var storeOrders = Count(UsageKind.OrderPlaced, "store");
        int? Percent(int part, int whole) => whole == 0 ? null : (int)Math.Round(100.0 * Math.Min(part, whole) / whole);

        // The middle answer time; the latest 2,000 are plenty.
        var seconds = await events.Where(e => e.Kind == UsageKind.ChangeAnswered && e.Seconds != null)
            .OrderByDescending(e => e.CreatedAt).Select(e => e.Seconds!.Value).Take(2000).ToListAsync(ct);
        seconds.Sort();
        int? median = seconds.Count == 0 ? null : (int)Math.Round(seconds[seconds.Count / 2] / 60.0);

        // Languages customers (people with no shop) chose.
        var languages = await db.Users.AsNoTracking().Where(u => !db.Memberships.Any(m => m.UserId == u.Id))
            .GroupBy(u => u.PreferredLanguage).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Lang(string code) => languages.Where(l => l.Key == code).Sum(l => l.Count);

        return new CustomerUsageDto(span, carts, checkouts, storeOrders, Count(UsageKind.OrderPlaced, "list"),
            Percent(storeOrders, carts), Percent(storeOrders, checkouts), seconds.Count, median,
            new UsageLanguages(Lang("en"), Lang("hi"), Lang("gu"), Lang("hn")));
    }
}
