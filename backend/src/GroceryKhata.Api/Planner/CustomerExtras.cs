using System.Net;
using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace GroceryKhata.Api.Planner;

/// <summary>An item a customer hearted at a shop. <see cref="WasOut"/>: last seen out of stock, so its return is news.</summary>
public sealed class SavedItem : Entity
{
    public Guid UserId { get; set; }

    public Guid ShopId { get; set; }

    public Guid ProductId { get; set; }

    public bool WasOut { get; set; }
}

/// <summary>A hearted item as it is now at its shop. <see cref="Available"/> 0: out of stock (an alert comes when it is back).</summary>
public sealed record SavedItemDto(Guid ShopId, string ShopName, string ShopSlug, Guid ProductId, string Name, decimal Price, decimal Available, DateTimeOffset? ImageAt, DateTimeOffset SavedAt);

/// <summary>
/// Customer app phase 3, server side: hearts (saved items) with a back-in-stock notification, everything the
/// customer has with us as one download, and closing the account.
/// </summary>
public static class CustomerExtras
{
    public const int MaxSaved = 200;

    public static IEndpointRouteBuilder MapCustomerExtras(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/v1/me").WithTags("Customer").RequireAuthorization();
        me.MapGet("/saved-items", SavedAsync);
        me.MapPut("/saved-items/{shopId:guid}/{productId:guid}", SaveAsync);
        me.MapDelete("/saved-items/{shopId:guid}/{productId:guid}", async (Guid shopId, Guid productId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            await db.SavedItems.Where(x => x.UserId == user.UserId() && x.ShopId == shopId && x.ProductId == productId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        me.MapGet("/export", ExportAsync);
        me.MapPost("/delete-account", DeleteAsync);
        return app;
    }

    public static IServiceCollection AddBackInStock(this IServiceCollection services) => services.AddHostedService<BackInStockAlerts>();

    private sealed record Live(Guid Id, string Name, decimal Price, decimal Available, DateTimeOffset? ImageAt);

    /// <summary>Items at one shop as the store sells them now (read as that shop: its items are behind row security).</summary>
    private static async Task<Dictionary<Guid, Live>> LiveAsync(AppDbContext db, TenantContext tenant, Guid shopId, IReadOnlyCollection<Guid> ids, TimeProvider clock, CancellationToken ct)
    {
        tenant.SetShop(shopId);
        var today = IndiaTime.Today(clock);
        return await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive && p.SellingPrice > 0)
            .Select(p => new Live(p.Id, p.Name, p.SellingPrice, p.OutOn == today ? 0 : p.Uncounted ? StoreStock.Uncounted : p.Stock - p.Reserved, p.ImageAt))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    private static async Task<IReadOnlyList<SavedItemDto>> SavedAsync(ClaimsPrincipal user, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var saved = await db.SavedItems.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var shopIds = saved.Select(x => x.ShopId).Distinct().ToList();
        var shops = await db.Shops.AsNoTracking().Where(s => shopIds.Contains(s.Id) && s.IsActive).ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Slug }, ct);
        var result = new List<SavedItemDto>();
        foreach (var group in saved.Where(x => shops.ContainsKey(x.ShopId)).GroupBy(x => x.ShopId))
        {
            var live = await LiveAsync(db, tenant, group.Key, group.Select(x => x.ProductId).ToList(), clock, ct);
            foreach (var x in group.Where(x => live.ContainsKey(x.ProductId)))
            {
                var p = live[x.ProductId];
                // Seen out now: the alert goes when it comes back.
                if (p.Available <= 0)
                {
                    x.WasOut = true;
                }

                result.Add(new SavedItemDto(x.ShopId, shops[x.ShopId].Name, shops[x.ShopId].Slug, p.Id, p.Name, p.Price, Math.Max(0, p.Available), p.ImageAt, x.CreatedAt));
            }
        }

        await db.SaveChangesAsync(ct);
        return result.OrderByDescending(r => r.SavedAt).ToList();
    }

    private static async Task<IResult> SaveAsync(Guid shopId, Guid productId, ClaimsPrincipal user, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        if (!await db.Shops.AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        var live = await LiveAsync(db, tenant, shopId, [productId], clock, ct);
        if (!live.TryGetValue(productId, out var item))
        {
            return TypedResults.NotFound();
        }

        if (await db.SavedItems.AnyAsync(x => x.UserId == userId && x.ShopId == shopId && x.ProductId == productId, ct))
        {
            return TypedResults.NoContent();
        }

        if (await db.SavedItems.CountAsync(x => x.UserId == userId, ct) >= MaxSaved)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ProductId"] = [$"Keep up to {MaxSaved} saved items."] });
        }

        db.SavedItems.Add(new SavedItem { UserId = userId, ShopId = shopId, ProductId = productId, WasOut = item.Available <= 0 });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Saved twice at once: the other one stands.
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Saved items that were out and are back: one notification each on the customer's phones, then not again until
    /// it runs out once more. Returns how many came back.
    /// </summary>
    public static async Task<int> CheckBackInStockAsync(IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var tenant = sp.GetRequiredService<TenantContext>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("BackInStock");
        var waiting = await db.SavedItems.Where(x => x.WasOut).ToListAsync(ct);
        var back = 0;
        foreach (var group in waiting.GroupBy(x => x.ShopId))
        {
            var shop = await db.Shops.AsNoTracking().Where(s => s.Id == group.Key && s.IsActive).Select(s => new { s.Name, s.Slug }).SingleOrDefaultAsync(ct);
            if (shop is null)
            {
                continue;
            }

            var live = await LiveAsync(db, tenant, group.Key, group.Select(x => x.ProductId).Distinct().ToList(), clock, ct);
            foreach (var x in group.Where(x => live.TryGetValue(x.ProductId, out var p) && p.Available > 0))
            {
                x.WasOut = false;
                back++;
                var devices = await db.CustomerPushDevices.Where(d => d.UserId == x.UserId).ToListAsync(ct);
                if (devices.Count == 0)
                {
                    continue;
                }

                var keys = await Ledger.StockAlerts.KeysAsync(db, ct);
                var vapid = new VapidDetails("mailto:support@groceryKhata.app", keys.PublicKey, keys.PrivateKey);
                using var client = new WebPushClient();
                foreach (var device in devices)
                {
                    var (title, body) = BackMessage(device.Language ?? "en", live[x.ProductId].Name, shop.Name);
                    try
                    {
                        await client.SendNotificationAsync(new PushSubscription(device.Endpoint, device.P256dh, device.Auth),
                            JsonSerializer.Serialize(new { title, body, url = $"/#/s/{shop.Slug}" }), vapid, ct);
                    }
                    catch (WebPushException e) when (e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                    {
                        db.CustomerPushDevices.Remove(device);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        logger.LogWarning(e, "Back-in-stock alert not delivered");
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return back;
    }

    public static (string Title, string Body) BackMessage(string lang, string item, string shop) => lang switch
    {
        "hi" => ($"{item} फिर से आ गया", $"{shop} पर अब मिल रहा है।"),
        "gu" => ($"{item} ફરી આવી ગયું", $"{shop} પર હવે મળે છે."),
        _ => ($"{item} is back", $"In stock again at {shop}."),
    };

    /// <summary>Everything the customer has given us, as one file: account, addresses, lists, carts, shops, saved items, orders.</summary>
    private static async Task<IResult> ExportAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var me = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Phone, u.Email, u.PreferredLanguage, u.Address, u.City, u.Pincode, u.CreatedAt, u.OrderWhatsApp }).SingleAsync(ct);
        var addresses = await db.SavedAddresses.AsNoTracking().Where(a => a.UserId == userId).Select(a => new { a.Label, a.Text, a.Landmark, a.Pincode, a.IsDefault }).ToListAsync(ct);
        var lists = await db.ShoppingLists.AsNoTracking().Include(l => l.Items).Where(l => l.UserId == userId)
            .Select(l => new { l.Kind, Items = l.Items.Select(i => new { i.Name, i.Quantity, i.Unit, i.Note }) }).ToListAsync(ct);
        var carts = await db.CustomerCarts.AsNoTracking().Where(c => c.UserId == userId).Select(c => new { c.ShopId, c.Lines, c.UpdatedAt }).ToListAsync(ct);
        var shops = await db.SavedShops.AsNoTracking().Where(s => s.UserId == userId).Select(s => new { s.ShopId, s.CreatedAt }).ToListAsync(ct);
        var items = await db.SavedItems.AsNoTracking().Where(s => s.UserId == userId).Select(s => new { s.ShopId, s.ProductId, s.CreatedAt }).ToListAsync(ct);
        var orders = await db.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.PlacedByUserId == userId).OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        var data = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            account = me,
            addresses,
            lists,
            carts,
            savedShops = shops,
            savedItems = items,
            orders = (await PlannerEndpoints.WithShopNamesAsync(db, orders, ct)).Select(o => o with { ShopPhone = null }),
        };
        return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            "application/json", "grocery-khata-my-data.json");
    }

    /// <summary>
    /// Closes a customer account: personal details, addresses, lists, carts, saved shops and items and phones go, and
    /// every sign-in ends. Orders stay with the shops that filled them (their sales records), under the name on them.
    /// Someone who runs a shop closes the shop first.
    /// </summary>
    private static async Task<IResult> DeleteAsync(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        if (await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == userId && m.IsActive, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "You work at a shop on this account. Leave or close the shop first.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SavedAddresses.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        await db.ShoppingListItems.Where(i => db.ShoppingLists.Any(l => l.Id == i.ListId && l.UserId == userId)).ExecuteDeleteAsync(ct);
        await db.ShoppingLists.Where(l => l.UserId == userId).ExecuteDeleteAsync(ct);
        await db.CustomerCarts.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
        await db.SavedShops.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await db.SavedItems.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await db.UsageEvents.Where(e => e.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, (Guid?)null), ct);
        await db.CustomerPushDevices.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);
        var now = clock.GetUtcNow();
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        // The phone is freed for a new account; what is left of the row says only that an account was here.
        var freed = "x" + userId.ToString("N")[..15];
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.FullName, "Deleted account").SetProperty(u => u.Phone, freed).SetProperty(u => u.Email, (string?)null)
            .SetProperty(u => u.Address, (string?)null).SetProperty(u => u.City, (string?)null).SetProperty(u => u.Pincode, (string?)null)
            .SetProperty(u => u.PasswordHash, (string?)null).SetProperty(u => u.PhoneVerifiedAt, (DateTimeOffset?)null)
            .SetProperty(u => u.OrderWhatsApp, false).SetProperty(u => u.IsActive, false), ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}

/// <summary>Every 15 minutes: saved items that came back in stock get their notification.</summary>
public sealed class BackInStockAlerts(IServiceScopeFactory scopes, ILogger<BackInStockAlerts> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                await CustomerExtras.CheckBackInStockAsync(scopes, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Back-in-stock check failed");
            }
        }
    }
}
