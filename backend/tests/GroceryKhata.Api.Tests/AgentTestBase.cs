using System.Net.Http.Headers;
using GroceryKhata.Api.Agents;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>Helpers for agent tests: an approved agent, an admin of a role, a shop sign-up with the owner's code.</summary>
public abstract class AgentTestBase(ApiFactory api)
{
    protected ApiFactory Api => api;

    // A tiny real WebP (1×1), as the phone would send a shrunk photo.
    protected const string Photo = "data:image/webp;base64,UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==";

    protected async Task SqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    protected static Registration Form(string phone, string name, double lat, double lng, string mode, string? reference) => new(
        Guid.CreateVersion7(), name, "kirana", "Main Road, Lalpur", "Lalpur market", lat, lng, 12, "Ramesh Gupta", phone, null, null,
        "silver", "year1", mode, reference, null, [new { kind = "front", dataUrl = Photo }]);

    protected sealed record Registration(
        Guid id, string shopName, string type, string address, string market, double latitude, double longitude, double accuracyMetres,
        string ownerName, string ownerPhone, string? altPhone, string? gstin, string planCode, string term, string paymentMode, string? paymentRef,
        string? consentCode, object[] photos);

    protected async Task<RegistrationDto> SubmitAsync(HttpClient agent, Registration form)
    {
        await agent.PostJsonAsync("/api/v1/agent/consent-code", new { ownerPhone = form.ownerPhone });
        var code = api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(form.ownerPhone)!);
        return await (await agent.PostJsonAsync("/api/v1/agent/shops", form with { consentCode = code })).ReadAsync<RegistrationDto>();
    }

    /// <summary>A person who applied as an agent and was approved by an agent manager.</summary>
    protected async Task<HttpClient> ApprovedAgentAsync()
    {
        var (agent, _) = await api.SignUpAsync("Agent's own shop");
        string aadhaar;
        do
        {
            aadhaar = Random.Shared.Next(2, 10) + Random.Shared.NextInt64(0, 100_000_000_000).ToString("D11");
        }
        while (Aadhaar.Normalize(aadhaar) is null);

        var applied = await (await agent.PostJsonAsync("/api/v1/agents/apply", new { address = "12 Kanke Road, Ranchi", market = "Lalpur", aadhaar, acceptAgreement = true })).ReadAsync<FieldAgentDto>();
        var manager = await AdminAsync("AgentManager");
        await manager.PostJsonAsync($"/api/v1/platform/agents/{applied.Id}/approve", new { kycChecked = true });
        return agent;
    }

    protected async Task<HttpClient> AdminAsync(string role)
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
