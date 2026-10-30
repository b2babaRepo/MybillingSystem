using System.Net;
using GroceryKhata.Api.Agents;

namespace GroceryKhata.Api.Tests;

/// <summary>An agent's month, the cash they hold, commission (waiting, earned, paid) and payouts.</summary>
[Collection(ApiCollection.Name)]
public sealed class AgentPerformanceTests(ApiFactory api) : AgentTestBase(api)
{
    [Fact]
    public async Task Cash_is_owed_until_confirmed_and_commission_is_earned_after_the_shop_stays_paid()
    {
        var agent = await ApprovedAgentAsync();
        var (lat, lng) = (8 + Random.Shared.NextDouble() * 2, 76 + Random.Shared.NextDouble());
        var signup = await SubmitAsync(agent, Form(ApiFactory.NewPhone(), "Kumar Kirana", lat, lng, "cash", null));

        var summary = await agent.GetJsonAsync<AgentSummary>("/api/v1/agent/summary");
        Assert.Matches("^GKA-", summary.Code);
        Assert.Equal((1, 0, 999m), (summary.ThisMonth.Submitted, summary.ThisMonth.Paid, summary.CashToDeposit));

        var approver = await AdminAsync("Approver");
        await approver.PostJsonAsync($"/api/v1/platform/registrations/{signup.Id}/approve", new { });
        var billing = await AdminAsync("Billing");
        await billing.PostJsonAsync($"/api/v1/platform/registrations/{signup.Id}/confirm-payment", new { });

        // Cash handed over; the shop counts; 20% waits 30 days.
        summary = await agent.GetJsonAsync<AgentSummary>("/api/v1/agent/summary");
        Assert.Equal((1, 0m, 199.80m, 0m, 0m), (summary.ThisMonth.Paid, summary.CashToDeposit, summary.CommissionWaiting, summary.CommissionEarned, summary.CommissionDue));
        Assert.NotNull(summary.RankInMarket);

        var row = (await billing.GetJsonAsync<List<AgentPerformanceRow>>("/api/v1/platform/agent-performance")).Single(r => r.Code == summary.Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await billing.PostJsonAsync($"/api/v1/platform/agent-performance/{row.AgentId}/payouts", new { amount = 50m })).StatusCode);

        // A month later the shop is still paid: the commission is earned and can be paid out, but no more than is due.
        await SqlAsync("UPDATE subscription_payments SET paid_at = paid_at - interval '31 days' WHERE shop_id = @s", ("s", signup.ShopId ?? (await approver.GetJsonAsync<List<RegistrationDto>>("/api/v1/platform/registrations?status=approved")).Single(r => r.Id == signup.Id).ShopId!.Value));
        summary = await agent.GetJsonAsync<AgentSummary>("/api/v1/agent/summary");
        Assert.Equal((199.80m, 0m, 199.80m), (summary.CommissionEarned, summary.CommissionWaiting, summary.CommissionDue));

        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostJsonAsync($"/api/v1/platform/agent-performance/{row.AgentId}/payouts", new { amount = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await billing.PostJsonAsync($"/api/v1/platform/agent-performance/{row.AgentId}/payouts", new { amount = 500m })).StatusCode);
        await billing.PostJsonAsync($"/api/v1/platform/agent-performance/{row.AgentId}/payouts", new { amount = 150m, reference = "UPI 99887766" });
        summary = await agent.GetJsonAsync<AgentSummary>("/api/v1/agent/summary");
        Assert.Equal((150m, 49.80m), (summary.CommissionPaid, summary.CommissionDue));
        Assert.Equal("UPI 99887766", Assert.Single(summary.Payouts).Reference);
    }

    [Fact]
    public async Task Someone_who_isnt_an_agent_has_no_summary()
    {
        var (person, _) = await Api.SignUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/v1/agent/summary")).StatusCode);
    }
}
