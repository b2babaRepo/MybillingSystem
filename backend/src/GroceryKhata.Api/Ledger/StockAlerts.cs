using System.Net;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace GroceryKhata.Api.Ledger;

/// <summary>A phone (browser) that asked for the morning stock reminder: its Web Push address and keys.</summary>
public sealed class PushDevice : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid UserId { get; set; }

    public required string Endpoint { get; set; }

    public required string P256dh { get; set; }

    public required string Auth { get; set; }

    public DateTimeOffset? LastSentAt { get; set; }
}

/// <summary>The server's Web Push (VAPID) key pair: made once and kept, so phones that subscribed keep receiving.</summary>
public sealed class PushKey
{
    public int Id { get; set; }

    public required string PublicKey { get; set; }

    public required string PrivateKey { get; set; }
}

public sealed record PushSubscribeRequest(string? Endpoint, string? P256dh, string? Auth);

public sealed record OrderTodayItem(Guid ProductId, string Name, decimal Stock, decimal? DaysLeft, decimal Suggested, int LeadDays);

public sealed record OrderTodayDto(int Count, IReadOnlyList<OrderTodayItem> Items);

/// <summary>
/// Reminders before stock runs out: the "order today" list (from sales and each supplier's delivery time, see
/// PharmacyAssist.NeedsAsync), and a phone notification with it every morning at 8 for the devices that asked.
/// </summary>
public static class StockAlerts
{
    public static IServiceCollection AddStockAlerts(this IServiceCollection services)
    {
        services.AddHostedService<MorningStockAlerts>();
        return services;
    }

    public static IEndpointRouteBuilder MapStockAlerts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/stock/order-today", async (AppDbContext db, TimeProvider clock, CancellationToken ct) => await OrderTodayAsync(db, clock, ct))
            .WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);
        var push = app.MapGroup("/api/v1/push").WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);
        push.MapGet("/key", async (AppDbContext db, CancellationToken ct) => Results.Ok(new { key = (await KeysAsync(db, ct)).PublicKey }));
        push.MapPost("/subscribe", SubscribeAsync);
        push.MapPost("/unsubscribe", async (PushSubscribeRequest request, AppDbContext db, CancellationToken ct) =>
        {
            await db.PushDevices.Where(d => d.Endpoint == request.Endpoint).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        // Sends today's reminder to the caller's own phones now, so the owner sees what it looks like.
        push.MapPost("/test", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, TimeProvider clock, ILoggerFactory logs, CancellationToken ct) =>
        {
            var userId = user.UserId()!.Value;
            var sent = await SendAsync(db, clock, await db.PushDevices.Where(d => d.UserId == userId).ToListAsync(ct), logs.CreateLogger("StockAlerts"), ct, always: true);
            return Results.Ok(new { sent });
        });
        return app;
    }

    internal static async Task<OrderTodayDto> OrderTodayAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var (lines, _) = await PharmacyAssist.NeedsAsync(db, clock, ct);
        // Most urgent first: out of stock, then fewest days left.
        var items = lines.OrderBy(l => l.Stock > 0).ThenBy(l => l.DaysLeft ?? 9999).ThenBy(l => l.Medicine)
            .Select(l => new OrderTodayItem(l.ProductId, l.Medicine, l.Stock, l.DaysLeft, l.Suggested, l.LeadDays)).ToList();
        return new OrderTodayDto(items.Count, items.Take(20).ToList());
    }

    private static async Task<IResult> SubscribeAsync(PushSubscribeRequest request, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (request.Endpoint is not { Length: > 10 and <= 1000 } endpoint || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || request.P256dh is not { Length: > 10 and <= 200 } || request.Auth is not { Length: > 5 and <= 100 })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Endpoint"] = ["This phone's notification details are not valid."] });
        }

        if (!await db.PushDevices.AnyAsync(d => d.Endpoint == endpoint, ct))
        {
            db.PushDevices.Add(new PushDevice { UserId = user.UserId()!.Value, Endpoint = endpoint, P256dh = request.P256dh, Auth = request.Auth });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // The same phone is already signed up under another shop: it keeps that one.
                return Results.Conflict(new { detail = "This phone already gets reminders for another shop." });
            }
        }

        return Results.NoContent();
    }

    /// <summary>The key pair, made on first use.</summary>
    internal static async Task<PushKey> KeysAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.PushKeys.AsNoTracking().FirstOrDefaultAsync(ct) is { } keys)
        {
            return keys;
        }

        var made = VapidHelper.GenerateVapidKeys();
        var key = new PushKey { Id = 1, PublicKey = made.PublicKey, PrivateKey = made.PrivateKey };
        db.PushKeys.Add(key);
        try
        {
            await db.SaveChangesAsync(ct);
            return key;
        }
        catch (DbUpdateException)
        {
            // Another request made it first: use that one.
            db.ChangeTracker.Clear();
            return await db.PushKeys.AsNoTracking().FirstAsync(ct);
        }
    }

    /// <summary>
    /// Sends the current shop's order-today reminder to these devices. Nothing to order, nothing sent (unless
    /// <paramref name="always"/>). Phones that unsubscribed (the push service says gone) are removed.
    /// </summary>
    internal static async Task<int> SendAsync(AppDbContext db, TimeProvider clock, List<PushDevice> devices, ILogger logger, CancellationToken ct, bool always = false)
    {
        if (devices.Count == 0)
        {
            return 0;
        }

        var today = await OrderTodayAsync(db, clock, ct);
        // Holds past their date stay held (the owner decides), so the morning note says how many are waiting.
        var overdue = (await StockHolds.AllAsync(db, clock, ct)).Count(h => h.Overdue);
        var (dueTomorrow, dueToday, late) = await GoodsTracking.RemindersAsync(db, clock, ct);
        var goods = string.Join(", ", new[]
        {
            late > 0 ? $"{late} order{(late == 1 ? "" : "s")} late" : null,
            dueToday > 0 ? $"{dueToday} due today" : null,
            dueTomorrow > 0 ? $"{dueTomorrow} due tomorrow" : null,
        }.Where(x => x is not null));
        if (today.Count == 0 && overdue == 0 && goods.Length == 0 && !always)
        {
            return 0;
        }

        var names = string.Join(", ", today.Items.Take(4).Select(i => i.DaysLeft is { } d && i.Stock > 0 ? $"{i.Name} ({Math.Floor(d)} days)" : $"{i.Name} (out)"));
        var payload = JsonSerializer.Serialize(new
        {
            title = today.Count == 0 ? "Stock is fine today" : $"Order today: {today.Count} item{(today.Count == 1 ? "" : "s")}",
            body = (today.Count == 0 ? "Nothing is running out." : names + (today.Count > 4 ? $" and {today.Count - 4} more" : ""))
                + (overdue > 0 ? $" · {overdue} held item{(overdue == 1 ? "" : "s")} past the date" : "")
                + (goods.Length > 0 ? $" · Goods: {goods}" : ""),
            url = "/#/reorder",
        });
        var keys = await KeysAsync(db, ct);
        var vapid = new VapidDetails("mailto:support@groceryKhata.app", keys.PublicKey, keys.PrivateKey);
        using var client = new WebPushClient();
        var sent = 0;
        foreach (var device in devices)
        {
            try
            {
                await client.SendNotificationAsync(new PushSubscription(device.Endpoint, device.P256dh, device.Auth), payload, vapid, ct);
                device.LastSentAt = clock.GetUtcNow();
                sent++;
            }
            catch (WebPushException e) when (e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                db.PushDevices.Remove(device);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Stock reminder not delivered to a device");
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }
}

/// <summary>Every morning at 8 India time, each open shop's order-today reminder to the phones that asked for it.</summary>
public sealed class MorningStockAlerts(IServiceScopeFactory scopes, TimeProvider clock, ILogger<MorningStockAlerts> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(UntilEight(clock.GetUtcNow()), clock, stoppingToken);
                await RunAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Morning stock reminders failed");
                await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
            }
        }
    }

    public static TimeSpan UntilEight(DateTimeOffset now)
    {
        var india = now.ToOffset(IndiaTime.Offset);
        var next = new DateTimeOffset(india.Year, india.Month, india.Day, 8, 0, 0, india.Offset);
        return (next <= india ? next.AddDays(1) : next) - india;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        List<Guid> shops;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            shops = await db.Shops.AsNoTracking().Where(s => s.IsActive).Select(s => s.Id).ToListAsync(ct);
        }

        foreach (var shopId in shops)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(shopId);
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var devices = await db.PushDevices.ToListAsync(ct);
                await StockAlerts.SendAsync(db, clock, devices, logger, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Stock reminder failed for shop {ShopId}", shopId);
            }
        }
    }
}
