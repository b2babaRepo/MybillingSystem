using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Agents;

/// <summary>Commission paid to an agent (UPI or cash), recorded by a billing admin. Never edited.</summary>
public sealed class AgentPayout : Entity
{
    public Guid AgentId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The UPI reference or receipt.</summary>
    public string? Reference { get; set; }

    public Guid PaidByUserId { get; set; }
}

/// <summary>How agents earn and are ranked. Change in settings ("Agents" section).</summary>
public sealed class CommissionOptions
{
    /// <summary>Share of a shop's first payment, once the shop has stayed paid for <see cref="QualifyDays"/>.</summary>
    public decimal FirstPaymentPercent { get; set; } = 20;

    /// <summary>Share of each renewal by a shop the agent brought.</summary>
    public decimal RenewalPercent { get; set; } = 5;

    /// <summary>Days after the first payment before it counts: stops sign-ups that are refunded or fake.</summary>
    public int QualifyDays { get; set; } = 30;

    /// <summary>Paid shops in a month for the Bronze, Silver and Gold badges.</summary>
    public int[] BadgeThresholds { get; set; } = [5, 15, 30];
}

public enum AgentBadge
{
    None,
    Bronze,
    Silver,
    Gold,
}

public sealed record AgentMonth(int Submitted, int Approved, int Rejected, int Paid, AgentBadge Badge, int? NextBadgeAt);

public sealed record LeaderRow(int Rank, string Code, string Name, string Market, int Paid, AgentBadge Badge);

/// <summary>What the agent sees: this month, money (cash they hold, commission earned, waiting, paid, due), and where they rank.</summary>
public sealed record AgentSummary(
    string Code, string Market, string Month, AgentMonth ThisMonth, int ShopsAllTime, int PaidShopsAllTime,
    decimal CashToDeposit, decimal CommissionEarned, decimal CommissionWaiting, decimal CommissionPaid, decimal CommissionDue,
    int? RankInMarket, int AgentsInMarket, int? RankOverall, IReadOnlyList<LeaderRow> MarketLeaders, IReadOnlyList<PayoutDto> Payouts,
    decimal FirstPaymentPercent, decimal RenewalPercent, int QualifyDays);

public sealed record AgentPerformanceRow(
    Guid AgentId, string Code, string Name, string Phone, string Market, string? UpiId, AgentMonth ThisMonth, int ShopsAllTime, int PaidShopsAllTime,
    decimal CashToDeposit, decimal CommissionEarned, decimal CommissionWaiting, decimal CommissionPaid, decimal CommissionDue, int RankOverall);

public sealed record PayoutDto(Guid Id, decimal Amount, string? Reference, DateTimeOffset PaidAt, string? PaidBy);

public sealed record PayoutRequest(decimal Amount, string? Reference);

/// <summary>
/// Agent numbers worked out from their sign-ups and the shops' payments, never stored: a refund or a late payment
/// changes them at once. A shop "counts" once its first payment is confirmed; its first-payment commission is earned
/// after it has stayed paid for the qualifying days, renewals straight away.
/// </summary>
public static class AgentPerformance
{
    public static IServiceCollection AddAgentPerformance(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CommissionOptions>(configuration.GetSection("Agents"));
        return services;
    }

    public static IEndpointRouteBuilder MapAgentPerformance(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/agent/summary", SummaryAsync).RequireAuthorization().WithTags("Agent shop sign-ups");
        var admin = app.MapGroup("/api/v1/platform/agent-performance").WithTags("Field agents").RecordsActivity();
        admin.MapGet("", (string? month, AppDbContext db, TimeProvider clock, IOptions<CommissionOptions> o, CancellationToken ct) => PerformanceAsync(month, db, clock, o.Value, ct))
            .RequireAuthorization(AppPolicies.PlatformAgentMoney);
        admin.MapPost("/{agentId:guid}/payouts", PayAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        return app;
    }

    private sealed record Stats(
        FieldAgent Agent, string Phone, AgentMonth Month, int ShopsAllTime, int PaidAllTime,
        decimal Cash, decimal Earned, decimal Waiting, decimal Paid);

    /// <summary>Every approved (or blocked) agent's numbers for the month.</summary>
    private static async Task<List<Stats>> ComputeAsync(AppDbContext db, DateTimeOffset now, (int Year, int Month) month, CommissionOptions o, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Month(month.Year, month.Month);
        var agents = await (from a in db.FieldAgents.AsNoTracking()
                            where a.Code != null
                            join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                            select new { Agent = a, u.Phone }).ToListAsync(ct);
        var registrations = await db.ShopRegistrations.AsNoTracking()
            .Select(r => new { r.AgentId, r.Status, r.CreatedAt, r.ReviewedAt, r.ShopId, r.PaymentId, r.PaymentMode, r.PaymentStatus, r.Amount })
            .ToListAsync(ct);
        var shopIds = registrations.Where(r => r.ShopId != null).Select(r => r.ShopId!.Value).ToList();
        var payments = await db.SubscriptionPayments.AsNoTracking()
            .Where(p => shopIds.Contains(p.ShopId) && p.RefundedAt == null)
            .Select(p => new { p.Id, p.ShopId, p.Amount, p.PaidAt }).ToListAsync(ct);
        var payouts = await db.AgentPayouts.AsNoTracking().GroupBy(p => p.AgentId).Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        return agents.Select(x =>
        {
            var mine = registrations.Where(r => r.AgentId == x.Agent.Id).ToList();
            decimal earned = 0, waiting = 0;
            var paidAllTime = 0;
            var paidThisMonth = 0;
            foreach (var r in mine.Where(r => r.ShopId != null))
            {
                var shopPayments = payments.Where(p => p.ShopId == r.ShopId).OrderBy(p => p.PaidAt).ToList();
                // The first payment is the one confirmed from the sign-up; a refunded one no longer counts. A pay-later shop has
                // none confirmed, so its first real payment counts: that is the sale the agent made.
                var firstPayment = r.PaymentId is null ? shopPayments.FirstOrDefault() : shopPayments.FirstOrDefault(p => p.Id == r.PaymentId);
                if (firstPayment is not null)
                {
                    paidAllTime++;
                    if (firstPayment.PaidAt >= start && firstPayment.PaidAt < end)
                    {
                        paidThisMonth++;
                    }

                    var share = decimal.Round(firstPayment.Amount * o.FirstPaymentPercent / 100, 2);
                    if (firstPayment.PaidAt.AddDays(o.QualifyDays) <= now)
                    {
                        earned += share;
                    }
                    else
                    {
                        waiting += share;
                    }
                }

                // Renewals: every later payment for the shop.
                foreach (var renewal in shopPayments.Where(p => p.Id != r.PaymentId && p.Id != firstPayment?.Id && (firstPayment is null || p.PaidAt > firstPayment.PaidAt)))
                {
                    earned += decimal.Round(renewal.Amount * o.RenewalPercent / 100, 2);
                }
            }

            var monthRows = mine.Where(r => r.CreatedAt >= start && r.CreatedAt < end).ToList();
            var badge = Badge(paidThisMonth, o.BadgeThresholds);
            int? next = o.BadgeThresholds.Where(t => t > paidThisMonth).Select(t => (int?)t).FirstOrDefault();
            return new Stats(
                x.Agent, x.Phone,
                new AgentMonth(monthRows.Count, mine.Count(r => r.Status == RegistrationStatus.Approved && r.ReviewedAt >= start && r.ReviewedAt < end),
                    mine.Count(r => r.Status == RegistrationStatus.Rejected && r.ReviewedAt >= start && r.ReviewedAt < end), paidThisMonth, badge, next),
                mine.Count, paidAllTime,
                // Cash the agent collected that the company hasn't confirmed receiving yet.
                mine.Where(r => r.PaymentMode == RegistrationPayment.Cash && r.PaymentStatus == RegistrationPaymentStatus.Received && r.Status != RegistrationStatus.Rejected).Sum(r => r.Amount),
                earned, waiting, payouts.GetValueOrDefault(x.Agent.Id));
        }).ToList();
    }

    private static AgentBadge Badge(int paid, int[] thresholds) =>
        thresholds.Length >= 3 && paid >= thresholds[2] ? AgentBadge.Gold
        : thresholds.Length >= 2 && paid >= thresholds[1] ? AgentBadge.Silver
        : thresholds.Length >= 1 && paid >= thresholds[0] ? AgentBadge.Bronze
        : AgentBadge.None;

    /// <summary>Ranked by paid shops this month, then by sign-ups; agents with neither aren't ranked.</summary>
    private static List<Stats> Ranked(IEnumerable<Stats> stats) =>
        stats.Where(s => s.Agent.Status == FieldAgentStatus.Approved)
            .OrderByDescending(s => s.Month.Paid).ThenByDescending(s => s.Month.Submitted).ThenBy(s => s.Agent.Code).ToList();

    private static async Task<IResult> SummaryAsync(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<CommissionOptions> options, string? month, CancellationToken ct)
    {
        var userId = user.UserId();
        var me = await db.FieldAgents.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId && a.Code != null, ct);
        if (me is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only an approved field agent has a summary.");
        }

        var o = options.Value;
        var now = clock.GetUtcNow();
        var chosen = IndiaTime.ParseMonth(month) ?? (now.ToOffset(IndiaTime.Offset).Year, now.ToOffset(IndiaTime.Offset).Month);
        var all = await ComputeAsync(db, now, chosen, o, ct);
        var ranked = Ranked(all);
        var mine = all.Single(s => s.Agent.Id == me.Id);
        var market = Ranked(all.Where(s => string.Equals(s.Agent.Market, me.Market, StringComparison.OrdinalIgnoreCase)));
        int? Rank(List<Stats> list) => list.FindIndex(s => s.Agent.Id == me.Id) is var i and >= 0 && (mine.Month.Paid > 0 || mine.Month.Submitted > 0) ? i + 1 : null;
        var leaders = market.Where(s => s.Month.Paid > 0 || s.Month.Submitted > 0).Take(10)
            .Select((s, i) => new LeaderRow(i + 1, s.Agent.Code!, s.Agent.FullName, s.Agent.Market, s.Month.Paid, s.Month.Badge)).ToList();
        var payouts = await PayoutsAsync(db, me.Id, ct);
        return TypedResults.Ok(new AgentSummary(
            me.Code!, me.Market, $"{chosen.Year:D4}-{chosen.Month:D2}", mine.Month, mine.ShopsAllTime, mine.PaidAllTime,
            mine.Cash, mine.Earned, mine.Waiting, mine.Paid, Math.Max(0, mine.Earned - mine.Paid),
            Rank(market), market.Count, Rank(ranked), leaders, payouts, o.FirstPaymentPercent, o.RenewalPercent, o.QualifyDays));
    }

    private static async Task<IReadOnlyList<AgentPerformanceRow>> PerformanceAsync(string? month, AppDbContext db, TimeProvider clock, CommissionOptions o, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var chosen = IndiaTime.ParseMonth(month) ?? (now.ToOffset(IndiaTime.Offset).Year, now.ToOffset(IndiaTime.Offset).Month);
        var all = await ComputeAsync(db, now, chosen, o, ct);
        var ranked = Ranked(all);
        return all.OrderBy(s => ranked.IndexOf(s) is var i and >= 0 ? i : int.MaxValue).ThenBy(s => s.Agent.Code)
            .Select(s => new AgentPerformanceRow(s.Agent.Id, s.Agent.Code!, s.Agent.FullName, s.Phone, s.Agent.Market, s.Agent.UpiId, s.Month,
                s.ShopsAllTime, s.PaidAllTime, s.Cash, s.Earned, s.Waiting, s.Paid, Math.Max(0, s.Earned - s.Paid), ranked.IndexOf(s) + 1))
            .ToList();
    }

    /// <summary>Records commission paid to an agent; no more than they are due.</summary>
    private static async Task<IResult> PayAsync(
        Guid agentId, PayoutRequest request, ClaimsPrincipal admin, AppDbContext db, TimeProvider clock, IOptions<CommissionOptions> options, CancellationToken ct)
    {
        // One payout at a time per agent, so two can't both pass the "no more than is due" check.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM field_agents WHERE id = {agentId} FOR UPDATE", ct);
        var now = clock.GetUtcNow();
        var all = await ComputeAsync(db, now, (now.Year, now.Month), options.Value, ct);
        if (all.SingleOrDefault(s => s.Agent.Id == agentId) is not { } stats)
        {
            return TypedResults.NotFound();
        }

        var due = stats.Earned - stats.Paid;
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount || request.Amount > due)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = [$"Pay between ₹1 and what is due (₹{due:0.##})."] });
        }

        db.AgentPayouts.Add(new AgentPayout { AgentId = agentId, Amount = request.Amount, Reference = request.Reference?.Trim(), PaidByUserId = admin.UserId()!.Value });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(await PayoutsAsync(db, agentId, ct));
    }

    private static async Task<IReadOnlyList<PayoutDto>> PayoutsAsync(AppDbContext db, Guid agentId, CancellationToken ct) =>
        await (from p in db.AgentPayouts.AsNoTracking()
               where p.AgentId == agentId
               join u in db.Users.AsNoTracking() on p.PaidByUserId equals u.Id into payers
               from u in payers.DefaultIfEmpty()
               orderby p.CreatedAt descending
               select new PayoutDto(p.Id, p.Amount, p.Reference, p.CreatedAt, u == null ? null : u.FullName)).ToListAsync(ct);
}
