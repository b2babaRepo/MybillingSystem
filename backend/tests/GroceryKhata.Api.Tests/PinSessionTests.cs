using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;

namespace GroceryKhata.Api.Tests;

/// <summary>A PIN set by a shop opens that shop's counter and nothing else of the person's account.</summary>
[Collection(ApiCollection.Name)]
public sealed class PinSessionTests(ApiFactory api)
{
    private async Task MakePlatformAdmin(string phone)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var add = new Npgsql.NpgsqlCommand(
            "INSERT INTO platform_admins (id, user_id, role, created_at, updated_at) SELECT gen_random_uuid(), id, 'Super', now(), now() FROM users WHERE phone = @p", db);
        add.Parameters.AddWithValue("p", GroceryKhata.Api.Common.PhoneNumber.Normalize(phone)!);
        await add.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task A_shop_owner_cannot_become_someone_else_by_giving_them_a_pin()
    {
        // The victim: owns a shop, and is a platform admin.
        var victimPhone = ApiFactory.NewPhone();
        var (_, victim) = await api.SignUpAsync("Victim Shop", victimPhone);
        await MakePlatformAdmin(victimPhone);

        // The attacker: a free shop, the victim's number added as a helper, a PIN of their choosing.
        var (attacker, _) = await api.SignUpAsync("Attacker Shop");
        (await attacker.PostJsonAsync("/api/v1/shop/staff", new { name = "Helper", phone = victimPhone, pin = "4826" })).EnsureSuccessStatusCode();

        var signIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone = victimPhone, password = "4826" })).ReadAsync<AuthResponse>();
        var session = api.CreateClient();
        session.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signIn.AccessToken);

        // It works as a counter login for the attacker's own shop, and shows nothing of the victim's other shops.
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(["Attacker Shop"], signIn.Memberships.Select(m => m.ShopName));
        Assert.False(signIn.User.IsPlatformAdmin);

        // Nothing else of the account opens.
        Assert.Equal(HttpStatusCode.Forbidden, (await session.GetAsync("/api/v1/platform/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await session.GetAsync("/api/v1/me/khata")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await session.GetAsync("/api/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await session.PostJsonAsync("/api/v1/auth/select-shop", new { membershipId = victim.Memberships.Single().MembershipId })).StatusCode);

        // And a refresh keeps it that narrow.
        var refreshed = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new { signIn.RefreshToken })).ReadAsync<AuthResponse>();
        var later = api.CreateClient();
        later.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await later.GetAsync("/api/v1/platform/overview")).StatusCode);

        // The victim's own password still opens everything.
        var own = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone = victimPhone, password = "secret123" })).ReadAsync<AuthResponse>();
        Assert.Equal(2, own.Memberships.Count);
    }
}
