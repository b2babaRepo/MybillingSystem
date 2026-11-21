using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// A customer's standing order at a milk, kirana or water shop: this item, this much, on these weekdays, from a day
/// on, with an optional pause (away for a week). The shop's row: it sees each day's deliveries from it. Billing stays
/// with the shop, as with any delivery.
/// </summary>
public sealed class DeliveryPlan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Copied when made, so the shop sees who and where without reading the users table.</summary>
    public required string Name { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public Guid ProductId { get; set; }

    public required string ProductName { get; set; }

    public Unit Unit { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Weekdays as bits: Monday 1, Tuesday 2 … Sunday 64; 127 is every day.</summary>
    public int Days { get; set; }

    public DateOnly StartOn { get; set; }

    public DateOnly? PauseFrom { get; set; }

    public DateOnly? PauseTo { get; set; }

    public DateTimeOffset? StoppedAt { get; set; }
}

public sealed record DeliveryPlanRequest(Guid Id, Guid ProductId, decimal Quantity, int Days, DateOnly StartOn, string? Address);

public sealed record DeliveryPauseRequest(DateOnly? From, DateOnly? To);

public sealed record DeliveryPlanDto(Guid Id, Guid ShopId, string ShopName, string? ShopSlug, Guid ProductId, string ProductName, Unit Unit, decimal Quantity,
    int Days, DateOnly StartOn, DateOnly? PauseFrom, DateOnly? PauseTo, string? Address, DateOnly? NextOn);

/// <summary>
/// One delivery on a day, for the shop's round. <see cref="OnKhata"/>: the shop has a khata under this phone, so it
/// can be posted there. <see cref="Posted"/>: already on the khata as a bill.
/// </summary>
public sealed record DeliveryDto(Guid PlanId, string Name, string? Phone, string? Address, string ProductName, Unit Unit, decimal Quantity, bool OnKhata = false, bool Posted = false);

/// <summary>What posting a day did: bills made now, ones already there, and who has no khata here (collect from them).</summary>
public sealed record DeliveryPostDto(int Posted, int AlreadyPosted, IReadOnlyList<string> NoKhata, IReadOnlyList<string> Failed);

/// <summary>A day's round: every delivery, and how much of each item to take.</summary>
public sealed record DeliveryDayDto(DateOnly Date, IReadOnlyList<DeliveryDto> Deliveries, IReadOnlyList<DeliveryTotalDto> Totals);

public sealed record DeliveryTotalDto(string ProductName, Unit Unit, decimal Quantity);

public static class DeliveryPlans
{
    /// <summary>The longest a plan can be paused in one go, in days.</summary>
    public const int MaxPauseDays = 60;

    public static IEndpointRouteBuilder MapDeliveryPlans(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/v1/me/deliveries").WithTags("Customer").RequireAuthorization();
        me.MapGet("", MineAsync);
        me.MapPost("/{shopId:guid}", StartAsync).RequireRateLimiting(StoreEndpoints.StoreRateLimit);
        me.MapPost("/{shopId:guid}/{id:guid}/pause", PauseAsync);
        me.MapPost("/{shopId:guid}/{id:guid}/stop", StopAsync);
        app.MapGet("/api/v1/deliveries", DayAsync).WithTags("Orders").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/deliveries/{date}/post", PostAsync).WithTags("Orders").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    /// <summary>Monday 1 … Sunday 64.</summary>
    public static int DayBit(DateOnly date) => 1 << (((int)date.DayOfWeek + 6) % 7);

    /// <summary>Whether this plan delivers on this day.</summary>
    public static bool DeliversOn(DeliveryPlan p, DateOnly date) => p.StoppedAt is null && OnDay(p, date);

    /// <summary>The plan's days and pause, leaving out whether it was stopped since (a past day still counts).</summary>
    private static bool OnDay(DeliveryPlan p, DateOnly date) =>
        date >= p.StartOn && (p.Days & DayBit(date)) != 0 && !(p.PauseFrom is { } from && date >= from && date <= (p.PauseTo ?? from));

    private static DateOnly? NextOn(DeliveryPlan p, DateOnly today)
    {
        for (var d = today; d <= today.AddDays(MaxPauseDays + 7); d = d.AddDays(1))
        {
            if (DeliversOn(p, d))
            {
                return d;
            }
        }

        return null;
    }

    private static IQueryable<Shops.Shop> DeliveryShops(AppDbContext db) =>
        db.Shops.AsNoTracking().Where(s => s.IsActive && (s.Type == Shops.ShopType.Dairy || s.Type == Shops.ShopType.WaterSupply || s.Type == Shops.ShopType.Kirana
            || s.ExtraTypes.Contains("dairy") || s.ExtraTypes.Contains("water_supply") || s.ExtraTypes.Contains("kirana")));

    private static async Task<IResult> StartAsync(Guid shopId, DeliveryPlanRequest request, ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var me = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FullName, u.Phone, Verified = u.PhoneVerifiedAt != null }).SingleAsync(ct);
        if (!me.Verified)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["Confirm your mobile number so the shop can reach you."] });
        }

        if (!await DeliveryShops(db).AnyAsync(s => s.Id == shopId, ct))
        {
            return TypedResults.NotFound();
        }

        var today = IndiaTime.Today(clock);
        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
        if (request.Quantity <= 0 || request.Quantity > 50 || !Money.IsValidQuantity(request.Quantity)) errors["Quantity"] = ["How much each day?"];
        if (request.Days is < 1 or > 127) errors["Days"] = ["Choose the days."];
        if (request.StartOn <= today || request.StartOn > today.AddDays(30)) errors["StartOn"] = ["Start from tomorrow, within a month."];
        var address = request.Address?.Trim();
        if (string.IsNullOrEmpty(address) || address.Length > 300) errors["Address"] = ["Where should it come?"];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await CustomerExtras.ReadAsShopAsync(scopes, shopId, async shopDb =>
        {
            if (await shopDb.DeliveryPlans.AnyAsync(p => p.Id == request.Id, ct))
            {
                return (IResult)TypedResults.NoContent();
            }

            var product = await shopDb.Products.AsNoTracking().Where(p => p.Id == request.ProductId && p.IsActive && p.SellingPrice > 0 && !p.IsService)
                .Select(p => new { p.Name, p.Unit }).SingleOrDefaultAsync(ct);
            if (product is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ProductId"] = ["Choose an item from this shop."] });
            }

            shopDb.DeliveryPlans.Add(new DeliveryPlan
            {
                Id = request.Id, UserId = userId, Name = me.FullName, Phone = me.Phone, Address = address, ProductId = request.ProductId,
                ProductName = product.Name, Unit = product.Unit, Quantity = request.Quantity, Days = request.Days, StartOn = request.StartOn,
            });
            await shopDb.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        });

        // The shop joins the person's shops, so the plan shows on Home.
        if (result is Microsoft.AspNetCore.Http.HttpResults.NoContent && !await db.SavedShops.AnyAsync(x => x.UserId == userId && x.ShopId == shopId, ct))
        {
            db.SavedShops.Add(new SavedShop { UserId = userId, ShopId = shopId });
            await db.SaveChangesAsync(ct);
        }

        return result;
    }

    /// <summary>The person's running plans at their saved milk, kirana and water shops, with the next delivery day.</summary>
    private static async Task<IReadOnlyList<DeliveryPlanDto>> MineAsync(ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var saved = db.SavedShops.Where(x => x.UserId == userId).Select(x => x.ShopId);
        var shops = await DeliveryShops(db).Where(s => saved.Contains(s.Id)).Select(s => new { s.Id, s.Name, s.Slug }).ToListAsync(ct);
        var today = IndiaTime.Today(clock);
        var result = new List<DeliveryPlanDto>();
        foreach (var s in shops)
        {
            var plans = await CustomerExtras.ReadAsShopAsync(scopes, s.Id, shopDb => shopDb.DeliveryPlans.AsNoTracking().Where(p => p.UserId == userId && p.StoppedAt == null).OrderBy(p => p.CreatedAt).ToListAsync(ct));
            result.AddRange(plans.Select(p => new DeliveryPlanDto(p.Id, s.Id, s.Name, s.Slug, p.ProductId, p.ProductName, p.Unit, p.Quantity, p.Days, p.StartOn, p.PauseFrom, p.PauseTo, p.Address, NextOn(p, today))));
        }

        return result;
    }

    /// <summary>Away for a while: no deliveries from <c>From</c> to <c>To</c>. Both null resumes.</summary>
    private static async Task<IResult> PauseAsync(Guid shopId, Guid id, DeliveryPauseRequest request, ClaimsPrincipal user, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        if (request.From is { } from && (from < today || request.To is not { } to || to < from || to.DayNumber - from.DayNumber >= MaxPauseDays))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["To"] = [$"Pause from today on, for up to {MaxPauseDays} days."] });
        }

        var changed = await CustomerExtras.ReadAsShopAsync(scopes, shopId, shopDb => shopDb.DeliveryPlans
            .Where(p => p.Id == id && p.UserId == user.UserId() && p.StoppedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.PauseFrom, request.From).SetProperty(p => p.PauseTo, request.From == null ? null : request.To), ct));
        return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static async Task<IResult> StopAsync(Guid shopId, Guid id, ClaimsPrincipal user, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var changed = await CustomerExtras.ReadAsShopAsync(scopes, shopId, shopDb => shopDb.DeliveryPlans
            .Where(p => p.Id == id && p.UserId == user.UserId() && p.StoppedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.StoppedAt, now), ct));
        return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    /// <summary>
    /// The bill a plan's delivery on a day becomes: its ID is worked out from the two, so posting a day again finds
    /// the same bills instead of making new ones.
    /// </summary>
    public static Guid BillId(Guid planId, DateOnly date)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"delivery:{planId:N}:{date:yyyy-MM-dd}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80); // version 8: an app-made ID
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    private static async Task<List<DeliveryPlan>> DueAsync(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        var dayStart = IndiaTime.Day(day).Start;
        return
        (await db.DeliveryPlans.AsNoTracking().Where(p => p.StartOn <= day && (p.StoppedAt == null || p.StoppedAt > dayStart)).ToListAsync(ct))
            .Where(p => OnDay(p, day)).OrderBy(p => p.Address).ThenBy(p => p.Name).ToList();
    }

    /// <summary>The shop's round for a day (tomorrow by default): who gets what, and the total of each item to take.</summary>
    private static async Task<DeliveryDayDto> DayAsync(DateOnly? date, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var day = date ?? IndiaTime.Today(clock).AddDays(1);
        var due = await DueAsync(db, day, ct);
        var phones = due.Select(p => p.Phone).Where(p => p != null).Distinct().ToList();
        var khata = (await db.Customers.AsNoTracking().Where(c => c.Phone != null && phones.Contains(c.Phone)).Select(c => c.Phone!).ToListAsync(ct)).ToHashSet();
        var billIds = due.Select(p => BillId(p.Id, day)).ToList();
        var posted = (await db.Bills.AsNoTracking().Where(b => billIds.Contains(b.Id)).Select(b => b.Id).ToListAsync(ct)).ToHashSet();
        return new DeliveryDayDto(day,
            due.Select(p => new DeliveryDto(p.Id, p.Name, p.Phone, p.Address, p.ProductName, p.Unit, p.Quantity, p.Phone != null && khata.Contains(p.Phone), posted.Contains(BillId(p.Id, day)))).ToList(),
            due.GroupBy(p => (p.ProductName, p.Unit)).Select(g => new DeliveryTotalDto(g.Key.ProductName, g.Key.Unit, g.Sum(p => p.Quantity))).OrderBy(t => t.ProductName).ToList());
    }

    /// <summary>The oldest day a round can still be posted, in days back from today.</summary>
    public const int PostDaysBack = 7;

    /// <summary>
    /// Puts a day's deliveries on the customers' khatas: one credit bill per delivery, at the item's price now, which
    /// takes the stock out like any sale. Only for customers with a khata here (matched by phone); safe to press twice.
    /// </summary>
    private static async Task<IResult> PostAsync(DateOnly date, ClaimsPrincipal user, AppDbContext db, Ledger.LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        if (date > today || date < today.AddDays(-PostDaysBack))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Date"] = [$"Post today's round, or one from the last {PostDaysBack} days."] });
        }

        var due = await DueAsync(db, date, ct);
        var phones = due.Select(p => p.Phone).Where(p => p != null).Distinct().ToList();
        var customers = (await db.Customers.AsNoTracking().Where(c => c.Phone != null && phones.Contains(c.Phone)).OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, Phone = c.Phone! }).ToListAsync(ct)).GroupBy(c => c.Phone).ToDictionary(g => g.Key, g => g.First().Id);
        var productIds = due.Select(p => p.ProductId).Distinct().ToList();
        var prices = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id) && p.IsActive).ToDictionaryAsync(p => p.Id, p => p.SellingPrice, ct);
        var isOwner = user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));
        // Today: now; a past day: that morning, so the khata shows it on the day it came.
        DateTimeOffset? at = date == today ? null : new DateTimeOffset(date.ToDateTime(new TimeOnly(8, 0)), IndiaTime.Offset);
        var (posted, again, noKhata, failed) = (0, 0, new List<string>(), new List<string>());
        foreach (var plan in due)
        {
            if (plan.Phone is null || !customers.TryGetValue(plan.Phone, out var customerId))
            {
                noKhata.Add(plan.Name);
                continue;
            }

            if (!prices.TryGetValue(plan.ProductId, out var price))
            {
                failed.Add($"{plan.Name}: {plan.ProductName}");
                continue;
            }

            var result = await ledger.CreateBillAsync(new Ledger.BillRequest(BillId(plan.Id, date), customerId,
                [new Ledger.BillLineRequest(plan.ProductId, plan.ProductName, plan.Quantity, plan.Unit, price)], 0m, null, at), user.UserId()!.Value, ct, isOwner: isOwner);
            if (result.Value is null)
            {
                failed.Add($"{plan.Name}: {result.Conflict ?? result.Errors?.Values.FirstOrDefault()?.FirstOrDefault() ?? "not posted"}");
            }
            else if (result.Replayed)
            {
                again++;
            }
            else
            {
                posted++;
            }
        }

        return TypedResults.Ok(new DeliveryPostDto(posted, again, noKhata.Distinct().ToList(), failed));
    }
}
