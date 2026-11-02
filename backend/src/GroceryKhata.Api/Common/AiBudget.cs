using System.Diagnostics.Metrics;

namespace GroceryKhata.Api.Common;

/// <summary>
/// A brake on AI spend: at most <see cref="DailyLimit"/> Claude calls a day across all shops (Ai:DailyCallLimit), and
/// the tokens each call used, as metrics and in the log. The count lives in memory, so a restart starts it again.
/// </summary>
public static class AiBudget
{
    private static readonly Meter Meter = new("GroceryKhata.Ai");
    private static readonly Counter<long> Tokens = Meter.CreateCounter<long>("ai.tokens");
    private static readonly object Gate = new();
    private static DateOnly day;
    private static int calls;

    public static int DailyLimit { get; set; } = 5000;

    /// <summary>Takes one call from today's allowance, or throws when it is used up.</summary>
    public static void Take()
    {
        lock (Gate)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
            (day, calls) = today == day ? (day, calls) : (today, 0);
            if (calls >= DailyLimit)
            {
                throw new InvalidOperationException("Today's AI allowance is used up. Try again tomorrow, or type the details.");
            }

            calls++;
        }
    }

    public static void Record(ILogger logger, string use, string model, long input, long output, long cacheRead)
    {
        Tokens.Add(input, new("use", use), new("model", model), new("kind", "input"));
        Tokens.Add(output, new("use", use), new("model", model), new("kind", "output"));
        logger.LogInformation("AI {Use} on {Model}: {Input} in, {CacheRead} from cache, {Output} out", use, model, input, cacheRead, output);
    }
}
