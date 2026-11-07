using System.Diagnostics.Metrics;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Common;

/// <summary>One day's Claude calls across all shops. Not a shop's data: it has no tenant.</summary>
public sealed class AiUsageDay
{
    public DateOnly Day { get; set; }

    public int Calls { get; set; }
}

/// <summary>
/// A brake on AI spend: at most <see cref="DailyLimit"/> Claude calls a day across all shops (Ai:DailyCallLimit), and
/// the tokens each call used, as metrics and in the log. The count is in the database, so a restart or a second server
/// can't reset it.
/// </summary>
public static class AiBudget
{
    private static readonly Meter Meter = new("GroceryKhata.Ai");
    private static readonly Counter<long> Tokens = Meter.CreateCounter<long>("ai.tokens");

    public static int DailyLimit { get; set; } = 5000;

    /// <summary>Set at startup: the AI readers are singletons, so each count opens its own short database scope.</summary>
    public static IServiceScopeFactory? Scopes { get; set; }

    /// <summary>Takes one call from today's allowance, or throws when it is used up. With no way to count, nothing is spent.</summary>
    public static async Task TakeAsync(CancellationToken ct = default)
    {
        var scopes = Scopes ?? throw new InvalidOperationException("The AI allowance can't be checked just now.");
        await using var scope = scopes.CreateAsyncScope();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        if (!await TryTakeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), today, DailyLimit, ct))
        {
            throw new InvalidOperationException("Today's AI allowance is used up. Try again tomorrow, or type the details.");
        }
    }

    /// <summary>One statement, so two calls at once can't both take the last of the allowance.</summary>
    public static async Task<bool> TryTakeAsync(AppDbContext db, DateOnly day, int limit, CancellationToken ct) =>
        limit > 0 && (await db.Database.SqlQuery<int>($"""
            INSERT INTO ai_usage_days (day, calls) VALUES ({day}, 1)
            ON CONFLICT (day) DO UPDATE SET calls = ai_usage_days.calls + 1 WHERE ai_usage_days.calls < {limit}
            RETURNING calls AS "Value"
            """).ToListAsync(ct)).Count > 0;

    public static void Record(ILogger logger, string use, string model, long input, long output, long cacheRead)
    {
        Tokens.Add(input, new("use", use), new("model", model), new("kind", "input"));
        Tokens.Add(output, new("use", use), new("model", model), new("kind", "output"));
        logger.LogInformation("AI {Use} on {Model}: {Input} in, {CacheRead} from cache, {Output} out", use, model, input, cacheRead, output);
    }
}
