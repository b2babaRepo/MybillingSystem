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

    /// <summary>The app's language on that phone ("en", "hi", "gu"), so the morning note comes in it.</summary>
    public string? Language { get; set; }
}

/// <summary>The server's Web Push (VAPID) key pair: made once and kept, so phones that subscribed keep receiving.</summary>
public sealed class PushKey
{
    public int Id { get; set; }

    public required string PublicKey { get; set; }

    public required string PrivateKey { get; set; }
}

public sealed record PushSubscribeRequest(string? Endpoint, string? P256dh, string? Auth, string? Language = null);

/// <summary><see cref="NameEn"/>, <see cref="NameHi"/>: the item's list names, so the app shows the one for its language.</summary>
public sealed record OrderTodayItem(Guid ProductId, string Name, decimal Stock, decimal? DaysLeft, decimal Suggested, int LeadDays, string? NameEn = null, string? NameHi = null);

/// <summary>
/// <see cref="Total"/>: what the whole list costs at each item's last purchase rate (all of it, not only the items shown);
/// <see cref="Unpriced"/>: items with no rate yet, left out of the total. Both for the owner only (what goods cost is theirs).
/// </summary>
public sealed record OrderTodayDto(int Count, IReadOnlyList<OrderTodayItem> Items, decimal? Total = null, int? Unpriced = null);

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
        app.MapGet("/api/v1/stock/order-today", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
                await OrderTodayAsync(db, clock, ct) is var today && LedgerEndpoints.IsOwner(user) ? today : today with { Total = null, Unpriced = null })
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
        var shown = items.Take(20).ToList();
        var ids = shown.Select(i => i.ProductId).ToList();
        var names = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                En = p.MasterItem != null ? p.MasterItem.Name : p.CatalogueItem != null ? p.CatalogueItem.NameEn : null,
                Hi = p.MasterItem != null ? p.MasterItem.NameHi : p.CatalogueItem != null ? p.CatalogueItem.Name : null,
            }).ToDictionaryAsync(p => p.Id, ct);
        return new OrderTodayDto(items.Count, shown.Select(i => names.TryGetValue(i.ProductId, out var n) ? i with { NameEn = n.En, NameHi = n.Hi } : i).ToList(),
            decimal.Round(lines.Sum(l => l.Suggested * (l.LastRate ?? 0)), 2), lines.Count(l => l.LastRate is null));
    }

    private static async Task<IResult> SubscribeAsync(PushSubscribeRequest request, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (request.Endpoint is not { Length: > 10 and <= 1000 } endpoint || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || request.P256dh is not { Length: > 10 and <= 200 } || request.Auth is not { Length: > 5 and <= 100 })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Endpoint"] = ["This phone's notification details are not valid."] });
        }

        var language = request.Language is "hi" or "gu" ? request.Language : "en";
        if (await db.PushDevices.FirstOrDefaultAsync(d => d.Endpoint == endpoint, ct) is { } known)
        {
            // Already signed up: the app sends this again when its language changes.
            known.Language = language;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            db.PushDevices.Add(new PushDevice { UserId = user.UserId()!.Value, Endpoint = endpoint, P256dh = request.P256dh, Auth = request.Auth, Language = language });
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
        // A festival within three weeks with items to stock up on: worth a morning note of its own.
        var festival = await Catalogue.Festivals.PromptAsync(db, clock, 21, ct);
        var jewellery = await JewelleryBookEndpoints.MorningAsync(db, clock, ct);
        var jewelleryAny = jewellery.GirviDue + jewellery.GirviRisk + jewellery.SchemeMissed + jewellery.OrdersReady > 0;
        if (today.Count == 0 && overdue == 0 && late + dueToday + dueTomorrow == 0 && festival is null && !jewelleryAny && !always)
        {
            return 0;
        }

        // Only the festival to tell about: open its list instead of What to order.
        var url = today.Count == 0 && overdue == 0 && late + dueToday + dueTomorrow == 0
            ? jewelleryAny ? "/#/jewellery" : festival is not null ? "/#/festivals" : "/#/reorder"
            : "/#/reorder";
        var payloads = new Dictionary<string, string>();
        string PayloadFor(string? language)
        {
            var lang = language is "hi" or "gu" ? language : "en";
            if (!payloads.TryGetValue(lang, out var p))
            {
                var (title, body) = Message(lang, today, overdue, (late, dueToday, dueTomorrow), festival is { } f ? (f.Name, f.DaysLeft, f.Items) : null, jewellery);
                payloads[lang] = p = JsonSerializer.Serialize(new { title, body, url });
            }

            return p;
        }

        var keys = await KeysAsync(db, ct);
        var vapid = new VapidDetails("mailto:support@groceryKhata.app", keys.PublicKey, keys.PrivateKey);
        using var client = new WebPushClient();
        var sent = 0;
        foreach (var device in devices)
        {
            try
            {
                await client.SendNotificationAsync(new PushSubscription(device.Endpoint, device.P256dh, device.Auth), PayloadFor(device.Language), vapid, ct);
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

    /// <summary>
    /// The morning note in the phone's language ("en", "hi" or "gu"). Item names: the Hindi list name on Hindi phones,
    /// otherwise the English one (the lists have no Gujarati names), falling back to the shop's own name for the item.
    /// </summary>
    public static (string Title, string Body) Message(string lang, OrderTodayDto today, int overdue, (int Late, int Today, int Tomorrow) goods, (string Name, int DaysLeft, int Items)? festival,
        (int GirviDue, int GirviRisk, int SchemeMissed, int OrdersReady) jewellery = default)
    {
        var hi = lang == "hi";
        var gu = lang == "gu";
        string Name(OrderTodayItem i) => (hi ? i.NameHi : i.NameEn) ?? i.Name;
        string Left(OrderTodayItem i) => i.DaysLeft is { } d && i.Stock > 0
            ? $"{Name(i)} ({Math.Floor(d)} {(hi ? "दिन" : gu ? "દિવસ" : "days")})"
            : $"{Name(i)} ({(hi ? "खत्म" : gu ? "ખતમ" : "out")})";
        string S(int n) => n == 1 ? "" : "s";

        var n = today.Count;
        var title = n == 0
            ? (hi ? "आज स्टॉक ठीक है" : gu ? "આજે સ્ટોક બરાબર છે" : "Stock is fine today")
            : (hi ? $"आज ऑर्डर करें: {n} सामान" : gu ? $"આજે ઓર્ડર કરો: {n} વસ્તુ" : $"Order today: {n} item{S(n)}");
        var body = n == 0
            ? (hi ? "कुछ खत्म नहीं हो रहा।" : gu ? "કંઈ ખૂટતું નથી." : "Nothing is running out.")
            : string.Join(", ", today.Items.Take(4).Select(Left))
              + (n > 4 ? (hi ? $" और {n - 4} और" : gu ? $" અને બીજી {n - 4}" : $" and {n - 4} more") : "");
        if (overdue > 0)
        {
            body += hi ? $" · {overdue} रोका सामान तारीख पार" : gu ? $" · {overdue} રોકેલી વસ્તુ તારીખ વીતી" : $" · {overdue} held item{S(overdue)} past the date";
        }

        var g = string.Join(", ", new[]
        {
            goods.Late > 0 ? (hi ? $"{goods.Late} ऑर्डर लेट" : gu ? $"{goods.Late} ઓર્ડર મોડા" : $"{goods.Late} order{S(goods.Late)} late") : null,
            goods.Today > 0 ? (hi ? $"{goods.Today} आज आने हैं" : gu ? $"{goods.Today} આજે આવશે" : $"{goods.Today} due today") : null,
            goods.Tomorrow > 0 ? (hi ? $"{goods.Tomorrow} कल आने हैं" : gu ? $"{goods.Tomorrow} કાલે આવશે" : $"{goods.Tomorrow} due tomorrow") : null,
        }.Where(x => x is not null));
        if (g.Length > 0)
        {
            body += (hi ? " · माल: " : gu ? " · માલ: " : " · Goods: ") + g;
        }

        if (festival is { } f)
        {
            var when = f.DaysLeft == 0 ? (hi ? "आज" : gu ? "આજે" : "today")
                : f.DaysLeft == 1 ? (hi ? "कल" : gu ? "કાલે" : "tomorrow")
                : hi ? $"{f.DaysLeft} दिन में" : gu ? $"{f.DaysLeft} દિવસમાં" : $"in {f.DaysLeft} days";
            body += hi ? $" · {f.Name} {when}: {f.Items} सामान का स्टॉक भरें"
                : gu ? $" · {f.Name} {when}: {f.Items} વસ્તુનો સ્ટોક ભરો"
                : $" · {f.Name} {when}: stock up {f.Items} item{S(f.Items)}";
        }

        // Jewellery: what to chase today (each has a WhatsApp button on the counter's Due today list).
        var j = string.Join(", ", new[]
        {
            jewellery.GirviDue > 0 ? (hi ? $"{jewellery.GirviDue} गिरवी तारीख पार" : gu ? $"{jewellery.GirviDue} ગીરવે તારીખ વીતી" : $"{jewellery.GirviDue} girvi past due") : null,
            jewellery.GirviRisk > 0 ? (hi ? $"{jewellery.GirviRisk} गिरवी सोने की कीमत के पास" : gu ? $"{jewellery.GirviRisk} ગીરવે સોનાની કિંમતની નજીક" : $"{jewellery.GirviRisk} girvi near the gold's value") : null,
            jewellery.SchemeMissed > 0 ? (hi ? $"{jewellery.SchemeMissed} स्कीम किस्त छूटी" : gu ? $"{jewellery.SchemeMissed} સ્કીમ હપ્તા ચૂક્યા" : $"{jewellery.SchemeMissed} scheme instalment{S(jewellery.SchemeMissed)} missed") : null,
            jewellery.OrdersReady > 0 ? (hi ? $"{jewellery.OrdersReady} ऑर्डर तैयार" : gu ? $"{jewellery.OrdersReady} ઓર્ડર તૈયાર" : $"{jewellery.OrdersReady} order{S(jewellery.OrdersReady)} ready") : null,
        }.Where(x => x is not null));
        if (j.Length > 0)
        {
            body += (hi ? " · गहने: " : gu ? " · ઘરેણાં: " : " · Jewellery: ") + j;
        }

        return (title, body);
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
