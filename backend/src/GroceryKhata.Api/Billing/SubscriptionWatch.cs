using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Billing;

public enum ProposalKind
{
    /// <summary>A person should call or message the shop (nothing changes by itself).</summary>
    FollowUp,
    /// <summary>Give a busy shop whose trial ended more trial days (an approver decides).</summary>
    ExtendTrial,
}

public enum ProposalStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// Something the admin agent suggests. The agent only reads and suggests: it never changes a subscription itself.
/// A person approves (which carries the action out under their own role) or rejects, and both are kept.
/// </summary>
public sealed class AgentProposal : Entity
{
    public Guid ShopId { get; set; }

    public ProposalKind Kind { get; set; }

    /// <summary>Why, in a sentence ("Billing daily, trial ended 3 days ago, not paid").</summary>
    public required string Reason { get; set; }

    /// <summary>For <see cref="ProposalKind.ExtendTrial"/>: how many days.</summary>
    public int? Days { get; set; }

    public ProposalStatus Status { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }
}

public sealed record ProposalDto(Guid Id, Guid ShopId, string ShopName, ProposalKind Kind, string Reason, int? Days, ProposalStatus Status, DateTimeOffset CreatedAt);

/// <summary>The weekly picture for admins, worked out from the data (no AI needed for the numbers).</summary>
public sealed record Briefing(
    int NewShopsThisWeek, int Trials, int TrialsEndingThisWeek, int Paying, int InGrace, int OnHold, int Complimentary, int Free,
    decimal RevenueThisMonth, int PendingClaims, int PendingSuggestions, IReadOnlyList<string> Notes);

/// <summary>
/// The admin agent: once a day it looks at every open shop's plan and activity and files suggestions for people to
/// decide. Rules, not guesses: each suggestion says why. It reads totals only (bills counted per shop, inside that shop's
/// own scope), never customers or khata lines, and it sends nothing to shops by itself.
/// </summary>
public sealed class SubscriptionWatch(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SubscriptionWatch> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Every morning at 9:30 India time, before the shop day gets busy.
            var india = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5.5));
            var next = new DateTimeOffset(india.Year, india.Month, india.Day, 9, 30, 0, india.Offset);
            if (next <= india)
            {
                next = next.AddDays(1);
            }

            try
            {
                if (!await JobRuns.MissedAsync(scopes, nameof(SubscriptionWatch), clock, stoppingToken))
                {
                    await Task.Delay(next - india, clock, stoppingToken);
                }

                await RunAsync(stoppingToken);
                await JobRuns.RecordAsync(scopes, nameof(SubscriptionWatch), true, clock, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Subscription watch failed");
                await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
            }
        }
    }

    /// <summary>One pass over all open shops. Returns how many new suggestions were filed.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        List<(Guid Id, DateTimeOffset Opened)> shops;
        Dictionary<string, Plan> plans;
        Dictionary<Guid, Subscription> subs;
        HashSet<(Guid, ProposalKind)> recent;
        HashSet<Guid> everPaid;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            shops = (await db.Shops.AsNoTracking().Where(s => s.IsActive).Select(s => new { s.Id, s.CreatedAt }).ToListAsync(ct)).Select(s => (s.Id, s.CreatedAt)).ToList();
            plans = await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Code, ct);
            subs = await db.Subscriptions.AsNoTracking().ToDictionaryAsync(s => s.ShopId, ct);
            recent = (await db.AgentProposals.AsNoTracking().Where(p => p.CreatedAt > now.AddDays(-14)).Select(p => new { p.ShopId, p.Kind }).ToListAsync(ct))
                .Select(p => (p.ShopId, p.Kind)).ToHashSet();
            everPaid = (await db.SubscriptionPayments.AsNoTracking().Where(p => p.RefundedAt == null).Select(p => p.ShopId).Distinct().ToListAsync(ct)).ToHashSet();
        }

        var proposals = new List<AgentProposal>();
        foreach (var (shopId, opened) in shops)
        {
            var sub = subs.GetValueOrDefault(shopId) ?? new Subscription { ShopId = shopId, TrialEndsAt = opened.AddDays(30) };
            var e = Entitlements.Compute(sub, plans, now, graceDays: 7);
            var bills7 = await BillsSinceAsync(shopId, now.AddDays(-7), ct);
            var bills14 = await BillsSinceAsync(shopId, now.AddDays(-14), ct);

            void Suggest(ProposalKind kind, string reason, int? days = null)
            {
                if (recent.Add((shopId, kind)))
                {
                    proposals.Add(new AgentProposal { ShopId = shopId, Kind = kind, Reason = reason, Days = days });
                }
            }

            if (e.Status == SubscriptionStatus.Trial && e.EndsAt < now.AddDays(3) && bills7 >= 5)
            {
                Suggest(ProposalKind.FollowUp, $"Trial ends {e.EndsAt:dd MMM}; {bills7} bills this week. Call to help choose a plan.");
            }
            else if (e.Status == SubscriptionStatus.Free && !everPaid.Contains(shopId) && sub.TrialEndsAt > now.AddDays(-14) && bills14 >= 10)
            {
                Suggest(ProposalKind.ExtendTrial, $"Trial ended {sub.TrialEndsAt:dd MMM} but still billing ({bills14} bills in 14 days). Extend the trial 14 days?", 14);
            }
            else if (e.Status == SubscriptionStatus.Grace)
            {
                Suggest(ProposalKind.FollowUp, $"Payment due: {e.PlanName} ended, grace until {e.EndsAt:dd MMM}. Remind them to renew.");
            }
            else if (e.Status == SubscriptionStatus.Active && bills14 == 0)
            {
                Suggest(ProposalKind.FollowUp, $"Paying for {e.PlanName} but no bills in 14 days. Check if they need help.");
            }
        }

        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AgentProposals.AddRange(proposals);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Subscription watch: {Shops} shops looked at, {Count} suggestions filed", shops.Count, proposals.Count);
        return proposals.Count;
    }

    /// <summary>How many bills a shop made since a time: counted inside that shop's own scope (row-level security applies).</summary>
    private async Task<int> BillsSinceAsync(Guid shopId, DateTimeOffset since, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(shopId);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Bills.CountAsync(b => b.OccurredAt >= since, ct);
    }
}

/// <summary>The agent's suggestions for admins to approve or reject, and the weekly briefing.</summary>
public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgent(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/platform/agent").WithTags("Platform agent").RequireAuthorization(AppPolicies.PlatformAdmin).RecordsActivity();
        group.MapGet("/suggestions", ListAsync);
        group.MapPost("/suggestions/{id:guid}/approve", ApproveAsync);
        group.MapPost("/suggestions/{id:guid}/reject", RejectAsync);
        group.MapPost("/run", async (SubscriptionWatch watch, CancellationToken ct) => TypedResults.Ok(new { filed = await watch.RunAsync(ct) }))
            .RequireAuthorization(AppPolicies.PlatformSuper);
        group.MapGet("/briefing", BriefingAsync);
        return app;
    }

    private static async Task<IReadOnlyList<ProposalDto>> ListAsync(AppDbContext db, CancellationToken ct) =>
        await (from p in db.AgentProposals.AsNoTracking()
               join s in db.Shops.AsNoTracking() on p.ShopId equals s.Id
               where p.Status == ProposalStatus.Pending
               orderby p.CreatedAt descending
               select new ProposalDto(p.Id, p.ShopId, s.Name, p.Kind, p.Reason, p.Days, p.Status, p.CreatedAt))
            .Take(100).ToListAsync(ct);

    /// <summary>
    /// Approving carries the suggestion out under the approver's own role: extending a trial needs an approver or super
    /// admin; a follow-up is marked done by anyone (they made the call).
    /// </summary>
    private static async Task<IResult> ApproveAsync(Guid id, ClaimsPrincipal admin, AppDbContext db, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        var proposal = await db.AgentProposals.SingleOrDefaultAsync(p => p.Id == id && p.Status == ProposalStatus.Pending, ct);
        if (proposal is null)
        {
            return TypedResults.NotFound();
        }

        if (proposal.Kind == ProposalKind.ExtendTrial)
        {
            var role = await db.PlatformAdmins.Where(a => a.UserId == admin.UserId()).Select(a => a.Role).SingleAsync(ct);
            if (role is not (AdminRole.Super or AdminRole.Approver))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Extending a trial needs an approver or super admin.");
            }

            var sub = await entitlements.FindOrNewAsync(proposal.ShopId, ct, track: true);
            var now = clock.GetUtcNow();
            sub.TrialEndsAt = (sub.TrialEndsAt > now ? sub.TrialEndsAt : now).AddDays(proposal.Days ?? 14);
        }

        proposal.Status = ProposalStatus.Approved;
        proposal.DecidedByUserId = admin.UserId();
        proposal.DecidedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RejectAsync(Guid id, ClaimsPrincipal admin, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var changed = await db.AgentProposals.Where(p => p.Id == id && p.Status == ProposalStatus.Pending).ExecuteUpdateAsync(u => u
            .SetProperty(p => p.Status, ProposalStatus.Rejected).SetProperty(p => p.DecidedByUserId, admin.UserId()).SetProperty(p => p.DecidedAt, clock.GetUtcNow()), ct);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static async Task<Briefing> BriefingAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var plans = await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Code, ct);
        var shops = await db.Shops.AsNoTracking().Where(s => s.IsActive).Select(s => new { s.Id, s.CreatedAt }).ToListAsync(ct);
        var subs = await db.Subscriptions.AsNoTracking().ToDictionaryAsync(s => s.ShopId, ct);
        var states = shops.Select(s => Entitlements.Compute(subs.GetValueOrDefault(s.Id) ?? new Subscription { ShopId = s.Id, TrialEndsAt = s.CreatedAt.AddDays(30) }, plans, now, 7)).ToList();
        int Count(SubscriptionStatus status) => states.Count(x => x.Status == status);
        var india = now.ToOffset(TimeSpan.FromHours(5.5));
        var monthStart = new DateTimeOffset(india.Year, india.Month, 1, 0, 0, 0, TimeSpan.FromHours(5.5)).ToUniversalTime();
        var revenue = await db.SubscriptionPayments.Where(p => p.RefundedAt == null && p.PaidAt >= monthStart).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var claims = await db.PaymentClaims.CountAsync(c => c.Status == ClaimStatus.Pending, ct);
        var suggestions = await db.AgentProposals.CountAsync(p => p.Status == ProposalStatus.Pending, ct);
        var endingSoon = states.Count(x => x.Status == SubscriptionStatus.Trial && x.EndsAt < now.AddDays(7));
        var notes = new List<string>();
        if (claims > 0)
        {
            notes.Add($"{claims} UPI payments are waiting to be checked against the bank statement.");
        }

        if (endingSoon > 0)
        {
            notes.Add($"{endingSoon} trials end within a week: the best time to call.");
        }

        if (Count(SubscriptionStatus.Grace) > 0)
        {
            notes.Add($"{Count(SubscriptionStatus.Grace)} shops are in grace: remind them before they drop to Free.");
        }

        return new Briefing(shops.Count(s => s.CreatedAt > now.AddDays(-7)), Count(SubscriptionStatus.Trial), endingSoon,
            Count(SubscriptionStatus.Active), Count(SubscriptionStatus.Grace), Count(SubscriptionStatus.OnHold), Count(SubscriptionStatus.Complimentary),
            Count(SubscriptionStatus.Free), revenue, claims, suggestions, notes);
    }
}
