using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryKhata.Api.Agents;
using GroceryKhata.Api.Identity;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>Field agents: applying with Aadhaar, approval with an agent code, rejecting, blocking, and who may do it.</summary>
[Collection(ApiCollection.Name)]
public sealed class FieldAgentTests(ApiFactory api)
{
    [Theory]
    [InlineData("2341 2341 2346", true)]
    [InlineData("2341-2341-2346", true)]
    [InlineData("234123412347", false)] // one digit typed wrong
    [InlineData("034123412346", false)] // can't start with 0 or 1
    [InlineData("23412341234", false)]
    public void Aadhaar_numbers_are_checked_with_their_check_digit(string number, bool valid) =>
        Assert.Equal(valid, Aadhaar.Normalize(number) is not null);

    [Fact]
    public async Task A_person_applies_an_agent_manager_approves_and_they_get_an_agent_code()
    {
        var (person, _) = await api.SignUpAsync("Agent's own shop");
        Assert.Equal(HttpStatusCode.NoContent, (await person.GetAsync("/api/v1/agents/me")).StatusCode);

        var aadhaar = NewAadhaar();
        var form = new { address = "12 Kanke Road, Ranchi", market = "Lalpur market", aadhaar, upiId = "ravi@okaxis", acceptAgreement = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await person.PostJsonAsync("/api/v1/agents/apply", form with { acceptAgreement = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await person.PostJsonAsync("/api/v1/agents/apply", form with { aadhaar = "234123412347" })).StatusCode);
        var applied = await (await person.PostJsonAsync("/api/v1/agents/apply", form)).ReadAsync<FieldAgentDto>();
        Assert.Equal((FieldAgentStatus.Applied, (string?)null, aadhaar[^4..]), (applied.Status, applied.Code, applied.AadhaarLast4));

        // Someone else can't use the same card.
        var (other, _) = await api.SignUpAsync("Other shop");
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostJsonAsync("/api/v1/agents/apply", form)).StatusCode);

        // Billing admins don't handle agents; an agent manager does, and must have checked the card.
        var billing = await AdminAsync("Billing");
        Assert.Equal(HttpStatusCode.Forbidden, (await billing.GetAsync("/api/v1/platform/agents")).StatusCode);
        var manager = await AdminAsync("AgentManager");
        Assert.Contains(await manager.GetJsonAsync<List<FieldAgentDto>>("/api/v1/platform/agents?status=applied"), a => a.Id == applied.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostJsonAsync($"/api/v1/platform/agents/{applied.Id}/approve", new { kycChecked = false })).StatusCode);
        var approved = await (await manager.PostJsonAsync($"/api/v1/platform/agents/{applied.Id}/approve", new { kycChecked = true })).ReadAsync<FieldAgentDto>();
        Assert.Matches(@"^GKA-\d{4,}$", approved.Code);
        Assert.True(approved.KycChecked);
        Assert.Equal(approved.Code, (await person.GetJsonAsync<FieldAgentDto>("/api/v1/agents/me")).Code);

        // Blocked with a reason, then unblocked: the code stays theirs.
        Assert.Equal(FieldAgentStatus.Blocked, (await (await manager.PostJsonAsync($"/api/v1/platform/agents/{applied.Id}/block", new { reason = "Fake shop sign-ups" })).ReadAsync<FieldAgentDto>()).Status);
        var back = await (await manager.PostJsonAsync($"/api/v1/platform/agents/{applied.Id}/unblock", new { })).ReadAsync<FieldAgentDto>();
        Assert.Equal((FieldAgentStatus.Approved, approved.Code), (back.Status, back.Code));

        // The admin record shows the approval but never the Aadhaar number.
        var super = await AdminAsync("Super");
        var activity = await super.GetJsonAsync<JsonElement>("/api/v1/platform/activity");
        Assert.DoesNotContain(aadhaar, activity.GetRawText());
    }

    [Fact]
    public async Task An_agent_manager_adds_an_agent_met_in_person_and_can_reject_an_application()
    {
        var manager = await AdminAsync("AgentManager");
        var aadhaar = NewAadhaar();
        var phone = ApiFactory.NewPhone();
        var added = await (await manager.PostJsonAsync("/api/v1/platform/agents",
            new { fullName = "Sunil Kumar", phone, address = "Main Road, Doranda", market = "Doranda", aadhaar })).ReadAsync<AddedAgent>();
        Assert.NotNull(added.TempPassword);
        Assert.Equal(FieldAgentStatus.Applied, added.Agent.Status);

        // The activity record keeps the name, not the card number.
        var super = await AdminAsync("Super");
        Assert.DoesNotContain(aadhaar, (await super.GetJsonAsync<JsonElement>("/api/v1/platform/activity")).GetRawText());

        var rejected = await (await manager.PostJsonAsync($"/api/v1/platform/agents/{added.Agent.Id}/reject", new { reason = "Address could not be checked" })).ReadAsync<FieldAgentDto>();
        Assert.Equal((FieldAgentStatus.Rejected, "Address could not be checked"), (rejected.Status, rejected.StatusReason));

        // A rejected person may apply again, signed in with the one-time password.
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = added.TempPassword })).ReadAsync<AuthResponse>();
        var agent = api.CreateClient();
        agent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var again = await (await agent.PostJsonAsync("/api/v1/agents/apply", new { address = "Flat 4, Main Road, Doranda", market = "Doranda", aadhaar, acceptAgreement = true })).ReadAsync<FieldAgentDto>();
        Assert.Equal((added.Agent.Id, FieldAgentStatus.Applied), (again.Id, again.Status));
    }

    /// <summary>A random 12-digit number with a correct Verhoeff check digit (not a real person's).</summary>
    private static string NewAadhaar()
    {
        while (true)
        {
            var body = Random.Shared.Next(2, 10) + Random.Shared.NextInt64(0, 10_000_000_000).ToString("D10");
            for (var d = 0; d <= 9; d++)
            {
                if (Aadhaar.Normalize(body + d) is { } ok)
                {
                    return ok;
                }
            }
        }
    }

    private async Task<HttpClient> AdminAsync(string role)
    {
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Admin's own shop", phone);
        await using (var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var insert = new NpgsqlCommand("INSERT INTO platform_admins (id, user_id, role, created_at, updated_at) VALUES (gen_random_uuid(), @u, @r, now(), now())", db);
            insert.Parameters.AddWithValue("u", auth.User.Id);
            insert.Parameters.AddWithValue("r", role);
            await insert.ExecuteNonQueryAsync();
        }

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        return client;
    }
}
