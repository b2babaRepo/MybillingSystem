using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Common;

/// <summary>When a daily job last ran, so a restart across its hour runs it late instead of skipping the day.</summary>
public sealed class JobRun
{
    public required string Job { get; set; }

    public DateTimeOffset LastRanAt { get; set; }

    public bool Succeeded { get; set; }
}

/// <summary>A WhatsApp message already taken: Meta sends a message again when a reply is slow.</summary>
public sealed class SeenMessage
{
    public required string MessageId { get; set; }

    public DateTimeOffset SeenAt { get; set; }
}

public static class JobRuns
{
    /// <summary>True when the job has run before but not in the last 25 hours: it missed its slot.</summary>
    public static async Task<bool> MissedAsync(IServiceScopeFactory scopes, string job, TimeProvider clock, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var last = await db.JobRuns.AsNoTracking().Where(r => r.Job == job).Select(r => (DateTimeOffset?)r.LastRanAt).SingleOrDefaultAsync(ct);
        return last is { } at && clock.GetUtcNow() - at > TimeSpan.FromHours(25);
    }

    public static async Task RecordAsync(IServiceScopeFactory scopes, string job, bool succeeded, TimeProvider clock, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO job_runs (job, last_ran_at, succeeded) VALUES ({job}, {now}, {succeeded}) ON CONFLICT (job) DO UPDATE SET last_ran_at = {now}, succeeded = {succeeded}", ct);
    }
}
