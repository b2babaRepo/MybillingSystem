using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Agents;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>An agent signs a shop up on a visit; an admin approves it and the owner signs in; billing confirms the money.</summary>
[Collection(ApiCollection.Name)]
public sealed class ShopRegistrationTests(ApiFactory api) : AgentTestBase(api)
{
    [Fact]
    public async Task An_agent_signs_a_shop_up_with_the_owners_code_and_an_admin_approves_it()
    {
        var agent = await ApprovedAgentAsync();
        var ownerPhone = ApiFactory.NewPhone();
        var form = Form(ownerPhone, "Gupta Kirana", 23.3441, 85.3096, "cash", null);

        // No code from the owner, no sign-up.
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostJsonAsync("/api/v1/agent/shops", form with { consentCode = "000000" })).StatusCode);
        var registration = await SubmitAsync(agent, form);
        Assert.Equal((RegistrationStatus.Submitted, RegistrationPaymentStatus.Received, 999m), (registration.Status, registration.PaymentStatus, registration.Amount));
        Assert.Matches("^GKA-", registration.AgentCode);
        Assert.Single((await agent.GetJsonAsync<List<RegistrationDto>>("/api/v1/agent/shops")), r => r.Id == registration.Id);

        // Sent again (offline retry): still one.
        Assert.Equal(registration.Id, (await (await agent.PostJsonAsync("/api/v1/agent/shops", form)).ReadAsync<RegistrationDto>()).Id);

        // A billing admin can't approve shops; an approver can, and sees the photo.
        var billing = await AdminAsync("Billing");
        Assert.Equal(HttpStatusCode.Forbidden, (await billing.PostJsonAsync($"/api/v1/platform/registrations/{registration.Id}/approve", new { })).StatusCode);
        var approver = await AdminAsync("Approver");
        var photo = await approver.GetAsync($"/api/v1/platform/registrations/{registration.Id}/photos/{registration.Photos.Single().Id}");
        Assert.Equal("image/webp", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Conflict, (await billing.PostJsonAsync($"/api/v1/platform/registrations/{registration.Id}/confirm-payment", new { })).StatusCode);
        var approved = await (await approver.PostJsonAsync($"/api/v1/platform/registrations/{registration.Id}/approve", new { })).ReadAsync<RegistrationDto>();
        Assert.NotNull(approved.ShopId);

        // The owner signs in with an SMS code and finds the shop.
        var owner = Api.CreateClient();
        await owner.PostJsonAsync("/api/v1/auth/otp/send", new { phone = ownerPhone });
        var code = Api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(ownerPhone)!);
        var auth = await (await owner.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = ownerPhone, code })).ReadAsync<AuthResponse>();
        Assert.Equal(approved.ShopId, auth.ShopId);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        Assert.Equal(SubscriptionStatus.Trial, (await owner.GetJsonAsync<SubscriptionView>("/api/v1/subscription")).Status);

        // Billing confirms the cash: Silver for a year.
        var confirmed = await (await billing.PostJsonAsync($"/api/v1/platform/registrations/{registration.Id}/confirm-payment", new { })).ReadAsync<RegistrationDto>();
        Assert.Equal(RegistrationPaymentStatus.Confirmed, confirmed.PaymentStatus);
        var plan = await owner.GetJsonAsync<SubscriptionView>("/api/v1/subscription");
        Assert.Equal((SubscriptionStatus.Active, "silver"), (plan.Status, plan.PlanCode));
    }

    [Fact]
    public async Task Duplicates_are_flagged_for_the_reviewer_and_a_rejection_reaches_the_agent()
    {
        var agent = await ApprovedAgentAsync();
        var phone = ApiFactory.NewPhone();
        // Fresh spot each run so other tests' sign-ups aren't nearby.
        var (lat, lng) = (20 + Random.Shared.NextDouble() * 5, 75 + Random.Shared.NextDouble() * 10);
        var first = await SubmitAsync(agent, Form(phone, "Sharma Stores", lat, lng, "upi", "412345678901"));
        // As if the second visit were later: the first code no longer holds back a new one.
        await using (var db = new NpgsqlConnection(Api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var age = new NpgsqlCommand("UPDATE consent_codes SET created_at = now() - interval '1 hour' WHERE phone = @p", db);
            age.Parameters.AddWithValue("p", PhoneNumber.Normalize(phone)!);
            await age.ExecuteNonQueryAsync();
        }

        var second = await SubmitAsync(agent, Form(phone, "Sharma Stores 2", lat + 0.0001, lng, "upi", "412345678901"));

        var reviewer = await AdminAsync("AgentManager");
        var list = await reviewer.GetJsonAsync<List<RegistrationDto>>("/api/v1/platform/registrations?status=submitted");
        var flagged = list.Single(r => r.Id == second.Id);
        Assert.Contains(flagged.Warnings, w => w.Contains("owner's number"));
        Assert.Contains(flagged.Warnings, w => w.Contains("30 m"));
        Assert.Contains(flagged.Warnings, w => w.Contains("payment reference"));

        await reviewer.PostJsonAsync($"/api/v1/platform/registrations/{second.Id}/reject", new { reason = "Same shop entered twice" });
        var mine = await agent.GetJsonAsync<List<RegistrationDto>>("/api/v1/agent/shops");
        Assert.Equal((RegistrationStatus.Rejected, "Same shop entered twice"), (mine.Single(r => r.Id == second.Id).Status, mine.Single(r => r.Id == second.Id).RejectReason));
        Assert.Equal(RegistrationStatus.Submitted, mine.Single(r => r.Id == first.Id).Status);
    }

    [Fact]
    public async Task Only_approved_agents_can_sign_shops_up()
    {
        var (person, _) = await Api.SignUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await person.PostJsonAsync("/api/v1/agent/consent-code", new { ownerPhone = ApiFactory.NewPhone() })).StatusCode);
    }

}
