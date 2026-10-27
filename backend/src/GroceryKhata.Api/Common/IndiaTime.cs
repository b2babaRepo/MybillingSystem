using System.Globalization;

namespace GroceryKhata.Api.Common;

/// <summary>Shops are in India: a "day" and a "month" run midnight to midnight IST (UTC+5:30, no daylight saving).</summary>
public static class IndiaTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5.5);

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(Offset).DateTime);

    /// <summary>The UTC instants a shop day starts and ends (end exclusive).</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Day(DateOnly date)
    {
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();
        return (start, start.AddDays(1));
    }

    public static (DateTimeOffset Start, DateTimeOffset End) Month(int year, int month)
    {
        var start = new DateTimeOffset(new DateTime(year, month, 1), Offset).ToUniversalTime();
        return (start, start.AddMonths(1));
    }

    /// <summary>"2026-10" → (2026, 10); null for anything else.</summary>
    public static (int Year, int Month)? ParseMonth(string? text) =>
        DateOnly.TryParseExact($"{text}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? (date.Year, date.Month)
            : null;
}
