using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>The platform admin's list of every login account: roles, edit, suspend, remove.</summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformUsersTests(ApiFactory api)
{
    private async Task<(HttpClient Client, Guid UserId)> AdminAsync(string role = "super")
    {
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Admin's own shop", phone);
        await using (var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var insert = new NpgsqlCommand($"INSERT INTO platform_admins (id, user_id, role, created_at, updated_at) VALUES (gen_random_uuid(), @u, '{role}', now(), now())", db);
            insert.Parameters.AddWithValue("u", auth.User.Id);
            await insert.ExecuteNonQueryAsync();
        }

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        return (client, auth.User.Id);
    }

    [Fact]
    public async Task Admin_finds_a_shopkeeper_with_roles_edits_suspends_and_reactivates()
    {
        var phone = ApiFactory.NewPhone();
        var (shop, owner) = await api.SignUpAsync("Kapoor Kirana " + Guid.NewGuid().ToString("N")[..6], phone);
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.GetAsync("/api/v1/platform/users")).StatusCode);

        var (admin, _) = await AdminAsync();
        var page = await admin.GetJsonAsync<AccountPage>($"/api/v1/platform/users?q={phone[^6..]}&role=owner");
        var row = Assert.Single(page.Items, r => r.Id == owner.User.Id);
        Assert.Contains(row.Roles, r => r.Kind == "owner" && r.Detail!.StartsWith("Kapoor Kirana"));

        (await admin.PutJsonAsync($"/api/v1/platform/users/{owner.User.Id}", new { fullName = "Vikas Kapoor", email = "Vikas@Example.com", preferredLanguage = "en" })).EnsureSuccessStatusCode();
        var detail = await admin.GetJsonAsync<AccountDetail>($"/api/v1/platform/users/{owner.User.Id}");
        Assert.Equal(("Vikas Kapoor", "vikas@example.com", false), (detail.Account.FullName, detail.Account.Email, detail.Account.EmailVerified));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostJsonAsync($"/api/v1/platform/users/{owner.User.Id}/suspend", new { reason = "test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostJsonAsync($"/api/v1/platform/users/{owner.User.Id}/activate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).StatusCode);

        // No verified email: the admin is told to point them to the SMS code.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostJsonAsync($"/api/v1/platform/users/{owner.User.Id}/send-reset", new { })).StatusCode);
        // An owner of a working shop can't be removed.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/platform/users/{owner.User.Id}")).StatusCode);
    }

    [Fact]
    public async Task Removing_a_customer_wipes_their_details_and_only_super_admins_may()
    {
        var phone = ApiFactory.NewPhone();
        var registered = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/register", new { fullName = "Meena Customer", phone, password = "secret123", language = "hi" })).ReadAsync<AuthResponse>();

        var (support, _) = await AdminAsync("support");
        Assert.Equal(HttpStatusCode.Forbidden, (await support.DeleteAsync($"/api/v1/platform/users/{registered.User.Id}")).StatusCode);
        var (super, me) = await AdminAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await super.DeleteAsync($"/api/v1/platform/users/{me}")).StatusCode);

        var row = Assert.Single((await super.GetJsonAsync<AccountPage>($"/api/v1/platform/users?q={phone[^7..]}&role=customer")).Items);
        Assert.Equal("customer", Assert.Single(row.Roles).Kind);

        Assert.Equal(HttpStatusCode.NoContent, (await super.DeleteAsync($"/api/v1/platform/users/{registered.User.Id}")).StatusCode);
        var gone = await super.GetJsonAsync<AccountDetail>($"/api/v1/platform/users/{registered.User.Id}");
        Assert.Equal(("Removed user", false), (gone.Account.FullName, gone.Account.IsActive));
        Assert.NotEqual(GroceryKhata.Api.Common.PhoneNumber.Normalize(phone), gone.Account.Phone);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).StatusCode);
    }
}
