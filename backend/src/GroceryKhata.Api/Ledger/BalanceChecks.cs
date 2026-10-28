using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// One night's check of a shop's khata: every customer's stored balance against the sum of their ledger entries.
/// They always agree unless something went badly wrong (a bug, a hand edit in the database), so a mismatch is
/// shown to the owner straight away rather than found months later.
/// </summary>
public sealed class BalanceCheck : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public DateTimeOffset CheckedAt { get; set; }

    public int Customers { get; set; }

    public int Mismatches { get; set; }

    /// <summary>The customers that didn't match (JSON), empty when all is well.</summary>
    public required string Details { get; set; }
}

public sealed record BalanceCheckDto(DateTimeOffset CheckedAt, int Customers, int Mismatches, IReadOnlyList<BalanceMismatch> Details);

public static class BalanceChecks
{
    public static IServiceCollection AddBalanceChecks(this IServiceCollection services)
    {
        services.AddHostedService<NightlyBalanceCheck>();
        return services;
    }

    public static IEndpointRouteBuilder MapBalanceChecks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ledger/checks").WithTags("Khata").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/latest", LatestAsync);
        group.MapPost("", RunNowAsync);
        return app;
    }

    /// <summary>The most recent check for the owner's home screen; 204 before the first night.</summary>
    private static async Task<IResult> LatestAsync(AppDbContext db, CancellationToken ct)
    {
        var latest = await db.BalanceChecks.AsNoTracking().OrderByDescending(c => c.CheckedAt).FirstOrDefaultAsync(ct);
        return latest is null ? TypedResults.NoContent() : TypedResults.Ok(ToDto(latest));
    }

    /// <summary>Checks now (after a fix, or to see for yourself) instead of waiting for the night.</summary>
    private static async Task<BalanceCheckDto> RunNowAsync(AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct) =>
        ToDto(await CheckAsync(db, ledger, clock.GetUtcNow(), ct));

    /// <summary>Checks the current shop (the caller has set the tenant) and records the result.</summary>
    internal static async Task<BalanceCheck> CheckAsync(AppDbContext db, LedgerService ledger, DateTimeOffset now, CancellationToken ct)
    {
        var mismatches = await ledger.ReconcileAsync(ct);
        var check = new BalanceCheck
        {
            CheckedAt = now,
            Customers = await db.Customers.CountAsync(ct),
            Mismatches = mismatches.Count,
            Details = JsonSerializer.Serialize(mismatches),
        };
        db.BalanceChecks.Add(check);
        await db.SaveChangesAsync(ct);
        return check;
    }

    private static BalanceCheckDto ToDto(BalanceCheck c) =>
        new(c.CheckedAt, c.Customers, c.Mismatches, JsonSerializer.Deserialize<List<BalanceMismatch>>(c.Details) ?? []);
}

/// <summary>
/// Every night at about 3 am India time, checks every open shop's khata, one shop at a time (each in its own scope
/// working as that shop, so row-level security applies), and keeps 90 days of results.
/// </summary>
public sealed class NightlyBalanceCheck(IServiceScopeFactory scopes, TimeProvider clock, ILogger<NightlyBalanceCheck> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(UntilNextRun(clock.GetUtcNow()), clock, stoppingToken);
            await RunAsync(stoppingToken);
        }
    }

    /// <summary>Time from <paramref name="now"/> to the next 03:00 India time (21:30 UTC).</summary>
    public static TimeSpan UntilNextRun(DateTimeOffset now)
    {
        var india = now.ToOffset(TimeSpan.FromHours(5.5));
        var next = new DateTimeOffset(india.Year, india.Month, india.Day, 3, 0, 0, india.Offset);
        if (next <= india)
        {
            next = next.AddDays(1);
        }

        return next - india;
    }

    /// <summary>Checks every open shop. Returns how many shops had mismatches.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        List<Guid> shops;
        await using (var scope = scopes.CreateAsyncScope())
        {
            shops = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Shops.AsNoTracking().Where(s => s.IsActive).Select(s => s.Id).ToListAsync(ct);
        }

        var withProblems = 0;
        foreach (var shopId in shops)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(shopId);
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = clock.GetUtcNow();
                var check = await BalanceChecks.CheckAsync(db, scope.ServiceProvider.GetRequiredService<LedgerService>(), now, ct);
                await db.BalanceChecks.Where(c => c.CheckedAt < now.AddDays(-90)).ExecuteDeleteAsync(ct);
                if (check.Mismatches > 0)
                {
                    withProblems++;
                    logger.LogError("Shop {ShopId}: {Count} khata balances don't match their entries", shopId, check.Mismatches);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Balance check failed for shop {ShopId}", shopId);
            }
        }

        logger.LogInformation("Nightly balance check: {Shops} shops, {Problems} with mismatches", shops.Count, withProblems);
        return withProblems;
    }
}
