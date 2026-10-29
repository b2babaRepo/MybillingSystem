using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>The platform admin (super admin): every shop, approve, block, support.</summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformTests(ApiFactory api)
{
    [Fact]
    public async Task Shopkeepers_and_households_cannot_open_the_platform_screens()
    {
        var (shop, auth) = await api.SignUpAsync("Sharma General Store");
        Assert.False(auth.User.IsPlatformAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.GetAsync("/api/v1/platform/shops")).StatusCode);
    }

    [Fact]
    public async Task An_admin_finds_a_new_shop_approves_it_blocks_it_and_its_people_are_signed_out_at_once()
    {
        var admin = await AdminAsync();
        var (shop, shopAuth) = await api.SignUpAsync("Gupta Kirana");

        var newOnes = await admin.GetJsonAsync<List<PlatformShopRow>>("/api/v1/platform/shops?status=new&q=gupta");
        var row = Assert.Single(newOnes, r => r.Id == shopAuth.ShopId);
        Assert.Equal("Ramesh Sharma", row.OwnerName);

        var approved = await (await admin.PostJsonAsync($"/api/v1/platform/shops/{row.Id}/approve", new { })).ReadAsync<PlatformShopRow>();
        Assert.NotNull(approved.ApprovedAt);

        var detail = await admin.GetJsonAsync<PlatformShopDetail>($"/api/v1/platform/shops/{row.Id}");
        Assert.Equal(ShopRole.Owner, Assert.Single(detail.People).Role);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync($"/api/v1/platform/shops/{row.Id}/block", new { reason = "" })).StatusCode);
        var blocked = await (await admin.PostJsonAsync($"/api/v1/platform/shops/{row.Id}/block", new { reason = "Fake shop reported" })).ReadAsync<PlatformShopRow>();
        Assert.False(blocked.IsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.GetAsync("/api/v1/customers")).StatusCode);

        await admin.PostJsonAsync($"/api/v1/platform/shops/{row.Id}/unblock", new { });
        Assert.Equal(HttpStatusCode.OK, (await shop.GetAsync("/api/v1/customers")).StatusCode);
    }

    [Fact]
    public async Task An_admin_unlocks_a_person_locked_out_by_wrong_passwords()
    {
        var admin = await AdminAsync();
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Verma Store", phone);
        var anonymous = api.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            await anonymous.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "wrong-password" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await anonymous.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostJsonAsync($"/api/v1/platform/users/{auth.User.Id}/unlock", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).StatusCode);
    }

    /// <summary>A new account made a platform admin directly in the database, then signed in again.</summary>
    private async Task<HttpClient> AdminAsync()
    {
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Admin's own shop", phone);
        await using (var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var insert = new NpgsqlCommand("INSERT INTO platform_admins (id, user_id, created_at, updated_at) VALUES (gen_random_uuid(), @u, now(), now())", db);
            insert.Parameters.AddWithValue("u", auth.User.Id);
            await insert.ExecuteNonQueryAsync();
        }

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        Assert.True(signedIn.User.IsPlatformAdmin);
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        return client;
    }
}
