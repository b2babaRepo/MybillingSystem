using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Shop_endpoints_need_a_shop_token()
    {
        var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task A_one_shop_owner_gets_a_shop_token_straight_from_sign_in()
    {
        var phone = ApiFactory.NewPhone();
        await api.SignUpAsync(phone: phone);

        var auth = await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" });
        var signedIn = await auth.ReadAsync<AuthResponse>();

        Assert.NotNull(signedIn.ShopId);
        Assert.Equal(ShopRole.Owner, signedIn.Role);
    }

    [Fact]
    public async Task One_person_can_own_several_shops_and_switch_between_them()
    {
        var phone = ApiFactory.NewPhone();
        var (shopOne, first) = await api.SignUpAsync("First Shop", phone);
        var (_, second) = await api.SignUpAsync("Second Shop", phone);
        await KhataTests.CreateCustomerAsync(shopOne, "Only in the first shop");

        Assert.Equal(first.User.Id, second.User.Id);
        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" }))
            .ReadAsync<AuthResponse>();
        Assert.Null(signedIn.ShopId); // two shops: pick one
        Assert.Equal(2, signedIn.Memberships.Count);

        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/customers")).StatusCode);

        var membership = signedIn.Memberships.Single(m => m.ShopName == "Second Shop");
        var selected = await (await client.PostJsonAsync("/api/v1/auth/select-shop", new { membership.MembershipId })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", selected.AccessToken);

        Assert.Equal(membership.ShopId, selected.ShopId);
        Assert.Empty(await client.GetJsonAsync<List<CustomerDto>>("/api/v1/customers"));
    }

    [Fact]
    public async Task Adding_a_shop_to_an_existing_account_needs_its_password()
    {
        var phone = ApiFactory.NewPhone();
        await api.SignUpAsync(phone: phone);

        var response = await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Takeover", fullName = "Someone", phone, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_a_reused_token_ends_every_session()
    {
        var (_, auth) = await api.SignUpAsync();
        var client = api.CreateClient();

        var rotated = await (await client.PostJsonAsync("/api/v1/auth/refresh", new { auth.RefreshToken })).ReadAsync<AuthResponse>();
        Assert.Equal(auth.ShopId, rotated.ShopId);
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);

        // Well past the retry window, so this can't be two tabs or a lost reply.
        await using (var admin = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await admin.OpenAsync();
            await using var age = new Npgsql.NpgsqlCommand("UPDATE refresh_tokens SET revoked_at = revoked_at - interval '1 hour' WHERE revoked_at IS NOT NULL", admin);
            await age.ExecuteNonQueryAsync();
        }

        var reuse = await client.PostJsonAsync("/api/v1/auth/refresh", new { auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The reuse looked like theft, so the newer token stopped working too.
        var afterTheft = await client.PostJsonAsync("/api/v1/auth/refresh", new { rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterTheft.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        var phone = ApiFactory.NewPhone();
        await api.SignUpAsync(phone: phone);
        var client = api.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "wrong" })).StatusCode);
        }

        var locked = await client.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }
}
