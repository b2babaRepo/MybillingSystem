using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>A shop joining with another shop's referral code, and the platform's record of it.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReferralsTests(ApiFactory api)
{
    private async Task<HttpClient> AdminAsync()
    {
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Admin's own shop", phone);
        await using (var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var insert = new NpgsqlCommand("INSERT INTO platform_admins (id, user_id, role, created_at, updated_at) VALUES (gen_random_uuid(), @u, 'super', now(), now())", db);
            insert.Parameters.AddWithValue("u", auth.User.Id);
            await insert.ExecuteNonQueryAsync();
        }

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        return client;
    }

    [Fact]
    public async Task A_shop_that_joins_with_a_referral_code_shows_in_the_admins_referrals()
    {
        var (referrer, _) = await api.SignUpAsync("Sharma Kirana");
        var code = (await referrer.GetJsonAsync<ShopSettingsDto>("/api/v1/shop")).Slug;

        var joined = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-up", new
        {
            shopName = "Gupta General Store", fullName = "Ramesh Gupta", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana", referralCode = code.ToUpperInvariant(),
        })).ReadAsync<AuthResponse>();

        // A shop can't see the platform's records.
        Assert.Equal(HttpStatusCode.Forbidden, (await referrer.GetAsync("/api/v1/platform/referrals")).StatusCode);

        var admin = await AdminAsync();
        var list = await admin.GetJsonAsync<ReferralsDto>("/api/v1/platform/referrals");
        var row = Assert.Single(list.Referrals, r => r.ShopId == joined.ShopId);
        Assert.Equal(("Sharma Kirana", code, false, false), (row.ReferrerName, row.ReferrerCode, row.Paid, row.Rewarded));
        Assert.Contains(list.Referrers, t => t.Code == code && t.Joined >= 1);
    }
}
