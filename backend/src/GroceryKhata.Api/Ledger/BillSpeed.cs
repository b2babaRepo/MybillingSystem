using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary><see cref="Group"/>: small, medium, large, credit or paid. <see cref="MedianSeconds"/>: half of these bills were faster.</summary>
public sealed record BillSpeedRow(string Group, int Bills, int MedianSeconds);

public sealed record BillSpeedDto(int Days, int Bills, int? MedianSeconds, IReadOnlyList<BillSpeedRow> Rows);

/// <summary>
/// How long bills take at the counter, from the first item to saving, as the app timed them. The median, because one
/// bill left open during a chat would drag an average far from what the counter is really like.
/// </summary>
public static class BillSpeed
{
    public const int MaxSeconds = 3600;

    /// <summary>What a bill keeps of the time the app sent: nothing when it can't be a real timing.</summary>
    public static int? Clean(int? seconds) => seconds is >= 0 and <= MaxSeconds ? seconds : null;

    public static int Median(IReadOnlyCollection<int> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (int)Math.Round((sorted[middle - 1] + sorted[middle]) / 2m, MidpointRounding.AwayFromZero);
    }

    public static async Task<BillSpeedDto> ReportAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        const int days = 30;
        var since = clock.GetUtcNow().AddDays(-days);
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.EntrySeconds != null && b.CancelledAt == null && b.OccurredAt >= since)
            .Select(b => new { Seconds = b.EntrySeconds!.Value, Lines = b.Lines.Count, Credit = b.Total - b.PaidAmount - b.ExchangeCredit > 0 })
            .ToListAsync(ct);

        var groups = new (string Group, Func<int, int, bool, bool> In)[]
        {
            ("small", (_, lines, _) => lines <= 5),
            ("medium", (_, lines, _) => lines is > 5 and <= 15),
            ("large", (_, lines, _) => lines > 15),
            ("credit", (_, _, credit) => credit),
            ("paid", (_, _, credit) => !credit),
        };
        var rows = groups
            .Select(g => (g.Group, Seconds: bills.Where(b => g.In(b.Seconds, b.Lines, b.Credit)).Select(b => b.Seconds).ToList()))
            .Where(g => g.Seconds.Count > 0)
            .Select(g => new BillSpeedRow(g.Group, g.Seconds.Count, Median(g.Seconds)))
            .ToList();
        return new BillSpeedDto(days, bills.Count, bills.Count > 0 ? Median(bills.Select(b => b.Seconds).ToList()) : null, rows);
    }
}
