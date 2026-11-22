using System.Net;
using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// When a khata's udhaar falls due, from the shop's credit days for that customer: each udhaar is due that many days
/// after it was taken, oldest first. <see cref="DueOn"/>: the next (or a missed) due day; <see cref="Overdue"/>: what is
/// past its day now; <see cref="DueAmount"/>: what falls due on <see cref="DueOn"/>.
/// </summary>
public sealed record KhataDueDto(Guid ShopId, string ShopName, Guid CustomerId, decimal Balance, int CreditDays, DateOnly? DueOn, decimal DueAmount, decimal Overdue);

public static class KhataDues
{
    public static IEndpointRouteBuilder MapKhataDues(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/khata-dues", MineAsync).WithTags("Khata").RequireAuthorization();
        return app;
    }

    public static IServiceCollection AddKhataDueReminders(this IServiceCollection services) => services.AddHostedService<KhataDueReminders>();

    /// <summary>The khata's due day and amounts from its entries (udhaar positive, payments negative), as the shop's dues list works them out.</summary>
    public static (DateOnly? DueOn, decimal DueAmount, decimal Overdue) Due(IEnumerable<Ageing.Entry> entries, int creditDays, DateOnly today)
    {
        var unpaid = Ageing.Unpaid(entries);
        DateOnly Day(Ageing.Entry e) => DateOnly.FromDateTime(e.At.ToOffset(IndiaTime.Offset).DateTime).AddDays(creditDays);
        var overdue = unpaid.Where(e => Day(e) < today).Sum(e => e.Amount);
        if (unpaid.Count == 0)
        {
            return (null, 0m, 0m);
        }

        // A missed day stays the day to show; otherwise the next one coming, with what falls due on it.
        var next = Day(unpaid[0]);
        return (next, unpaid.Where(e => Day(e) == next).Sum(e => e.Amount), overdue);
    }

    /// <summary>Due days at every shop where the person confirmed their khata and the shop gives credit days.</summary>
    private static async Task<IReadOnlyList<KhataDueDto>> MineAsync(ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        var phone = await db.Users.AsNoTracking().Where(u => u.Id == user.UserId() && u.PhoneVerifiedAt != null).Select(u => u.Phone).SingleOrDefaultAsync(ct);
        if (phone is null)
        {
            return [];
        }

        var khatas = await (from c in db.Customers.IgnoreQueryFilters().AsNoTracking()
                            join s in db.Shops.AsNoTracking() on c.ShopId equals s.Id
                            where c.Phone == phone && c.PhoneConfirmedAt != null && c.CreditDays != null && c.Balance > 0 && s.IsActive
                            select new { c.ShopId, s.Name, c.Id, c.Balance, Days = c.CreditDays!.Value }).ToListAsync(ct);
        var today = IndiaTime.Today(clock);
        var result = new List<KhataDueDto>();
        foreach (var k in khatas)
        {
            var entries = await CustomerExtras.ReadAsShopAsync(scopes, k.ShopId, shopDb => shopDb.LedgerEntries.AsNoTracking().Where(e => e.CustomerId == k.Id)
                .Select(e => new Ageing.Entry(e.OccurredAt, e.Amount)).ToListAsync(ct));
            var (dueOn, amount, overdue) = Due(entries, k.Days, today);
            result.Add(new KhataDueDto(k.ShopId, k.Name, k.Id, k.Balance, k.Days, dueOn, amount, overdue));
        }

        return result.OrderBy(r => r.DueOn ?? DateOnly.MaxValue).ToList();
    }

    /// <summary>
    /// Once a day: a notification to each person whose khata falls due tomorrow or today, on the phones they turned
    /// order alerts on for. Only confirmed khatas (their proved number), once per khata per day.
    /// </summary>
    public static async Task<int> RemindAsync(IServiceScopeFactory scopes, TimeProvider clock, ILogger logger, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = IndiaTime.Today(clock);
        var shops = await db.Shops.AsNoTracking().Where(s => s.IsActive).Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var sent = 0;
        foreach (var shop in shops)
        {
            var due = await CustomerExtras.ReadAsShopAsync(scopes, shop.Id, async shopDb =>
            {
                var customers = await shopDb.Customers.Where(c => c.CreditDays != null && c.Balance > 0 && c.Phone != null && c.PhoneConfirmedAt != null
                    && (c.LastDueReminderOn == null || c.LastDueReminderOn < today)).ToListAsync(ct);
                var list = new List<(Guid CustomerId, string Phone, decimal Amount, bool Today)>();
                foreach (var c in customers)
                {
                    var entries = await shopDb.LedgerEntries.AsNoTracking().Where(e => e.CustomerId == c.Id).Select(e => new Ageing.Entry(e.OccurredAt, e.Amount)).ToListAsync(ct);
                    var (dueOn, amount, _) = Due(entries, c.CreditDays!.Value, today);
                    if (dueOn == today || dueOn == today.AddDays(1))
                    {
                        list.Add((c.Id, c.Phone!, amount, dueOn == today));
                        c.LastDueReminderOn = today;
                    }
                }

                await shopDb.SaveChangesAsync(ct);
                return list;
            });

            foreach (var d in due)
            {
                var devices = await (from u in db.Users
                                     join p in db.CustomerPushDevices on u.Id equals p.UserId
                                     where u.Phone == d.Phone && u.PhoneVerifiedAt != null
                                     select p).ToListAsync(ct);
                if (devices.Count == 0)
                {
                    continue;
                }

                var keys = await StockAlerts.KeysAsync(db, ct);
                var vapid = new VapidDetails("mailto:support@groceryKhata.app", keys.PublicKey, keys.PrivateKey);
                using var client = new WebPushClient();
                foreach (var device in devices)
                {
                    var (title, body) = Message(device.Language ?? "en", shop.Name, d.Amount, d.Today);
                    try
                    {
                        await client.SendNotificationAsync(new PushSubscription(device.Endpoint, device.P256dh, device.Auth),
                            JsonSerializer.Serialize(new { title, body, url = $"/#/khata/{shop.Id}/{d.CustomerId}" }), vapid, ct);
                        sent++;
                    }
                    catch (WebPushException e) when (e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                    {
                        db.CustomerPushDevices.Remove(device);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        logger.LogWarning(e, "Khata due reminder not delivered");
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    public static (string Title, string Body) Message(string lang, string shop, decimal amount, bool today)
    {
        var rupees = $"₹{amount:0.##}";
        return lang switch
        {
            "hi" => ($"{shop}: खाता", today ? $"{rupees} आज चुकाने हैं।" : $"{rupees} कल चुकाने हैं।"),
            "gu" => ($"{shop}: ખાતું", today ? $"{rupees} આજે ચૂકવવાના છે." : $"{rupees} કાલે ચૂકવવાના છે."),
            "hn" => ($"{shop}: khata", today ? $"{rupees} aaj chukane hain." : $"{rupees} kal chukane hain."),
            _ => ($"{shop}: khata", today ? $"{rupees} is due today." : $"{rupees} is due tomorrow."),
        };
    }
}

/// <summary>Checks every hour; reminders go once a day after 9 in the morning, India time.</summary>
public sealed class KhataDueReminders(IServiceScopeFactory scopes, TimeProvider clock, ILogger<KhataDueReminders> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                if (clock.GetUtcNow().ToOffset(IndiaTime.Offset).Hour >= 9)
                {
                    await KhataDues.RemindAsync(scopes, clock, logger, stoppingToken);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Khata due reminders failed");
            }
        }
    }
}
