using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>Custom admin roles: a super admin ticks permissions, gives the role to an admin, and access follows at once.</summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformRolesTests(ApiFactory api)
{
    private async Task<(HttpClient Client, Guid UserId, string Phone)> AdminAsync(string role = "super")
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
        return (client, auth.User.Id, phone);
    }

    [Fact]
    public async Task A_custom_role_gives_exactly_its_ticked_permissions()
    {
        var (super, superId, _) = await AdminAsync();
        var name = "Account desk " + Guid.NewGuid().ToString("N")[..6];

        // Managing admins can't be handed out.
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PostJsonAsync("/api/v1/platform/roles", new { name, permissions = new[] { "support", "admins.manage" } })).StatusCode);
        var roles = await (await super.PostJsonAsync("/api/v1/platform/roles", new { name, permissions = new[] { "accounts.remove", "support" } })).ReadAsync<RolesDto>();
        var role = Assert.Single(roles.Custom, r => r.Name == name);
        Assert.Equal(["support", "accounts.remove"], role.Permissions);

        var (desk, deskId, _) = await AdminAsync("support");
        (await super.PutJsonAsync($"/api/v1/platform/admins/{deskId}", new { role = "custom", customRoleId = role.Id })).EnsureSuccessStatusCode();
        var me = await desk.GetJsonAsync<MyPermissionsDto>("/api/v1/platform/me");
        Assert.Equal((AdminRole.Custom, name), (me.Role, me.CustomRoleName));

        // It may remove accounts (a support admin can't), but not manage roles or approve shops.
        var customer = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/register", new { fullName = "Old Customer", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi" })).ReadAsync<AuthResponse>();
        Assert.Equal(HttpStatusCode.NoContent, (await desk.DeleteAsync($"/api/v1/platform/users/{customer.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.GetAsync("/api/v1/platform/roles")).StatusCode);
        var (_, shop) = await api.SignUpAsync("Waiting Shop");
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.PostJsonAsync($"/api/v1/platform/shops/{shop.ShopId}/approve", new { })).StatusCode);

        // Untick a permission: it stops at once.
        (await super.PutJsonAsync($"/api/v1/platform/roles/{role.Id}", new { name, permissions = new[] { "support" } })).EnsureSuccessStatusCode();
        var another = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/register", new { fullName = "Another Customer", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi" })).ReadAsync<AuthResponse>();
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.DeleteAsync($"/api/v1/platform/users/{another.User.Id}")).StatusCode);

        // A role in use can't be deleted; nobody changes their own role.
        Assert.Equal(HttpStatusCode.Conflict, (await super.DeleteAsync($"/api/v1/platform/roles/{role.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await super.PutJsonAsync($"/api/v1/platform/admins/{superId}", new { role = "support" })).StatusCode);
    }
}
