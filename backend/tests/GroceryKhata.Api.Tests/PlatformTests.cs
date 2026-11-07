using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net.Http.Json;

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
        var shopPhone = ApiFactory.NewPhone();
        var (shop, shopAuth) = await api.SignUpAsync("Gupta Kirana", shopPhone);

        var newOnes = await admin.GetJsonAsync<List<PlatformShopRow>>("/api/v1/platform/shops?status=new&q=gupta");
        var row = Assert.Single(newOnes, r => r.Id == shopAuth.ShopId);
        Assert.Equal("Ramesh Sharma", row.OwnerName);

        var approved = await (await admin.PostJsonAsync($"/api/v1/platform/shops/{row.Id}/approve", new { })).ReadAsync<PlatformShopRow>();
        Assert.NotNull(approved.ApprovedAt);

        var detail = await admin.GetJsonAsync<PlatformShopDetail>($"/api/v1/platform/shops/{row.Id}");
        Assert.Equal(ShopRole.Owner, Assert.Single(detail.People).Role);

        // Every admin look and change is in the shop's own audit log, which its owner can read.
        var log = await shop.GetJsonAsync<List<Persistence.AuditEntryDto>>("/api/v1/audit");
        Assert.Contains(log, e => e.Table == "platform_admin" && e.Action == "view");
        Assert.Contains(log, e => e.Table == "platform_admin" && e.Action == "approve");

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync($"/api/v1/platform/shops/{row.Id}/block", new { reason = "" })).StatusCode);
        var blocked = await (await admin.PostJsonAsync($"/api/v1/platform/shops/{row.Id}/block", new { reason = "Fake shop reported" })).ReadAsync<PlatformShopRow>();
        Assert.False(blocked.IsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.GetAsync("/api/v1/customers")).StatusCode);

        // The owner signs in again: no shop, but told why.
        var again = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone = shopPhone, password = "secret123" })).ReadAsync<AuthResponse>();
        Assert.Null(again.ShopId);
        var why = Assert.Single(again.BlockedShops!);
        Assert.Equal(("Gupta Kirana", "Fake shop reported"), (why.ShopName, why.Reason));

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

    [Fact]
    public async Task With_hold_on_households_only_find_shops_an_admin_approved()
    {
        await using var held = api.WithWebHostBuilder(b => b.UseSetting("Platform:HoldNewShops", "true"));
        var name = "Held " + Guid.NewGuid().ToString("N")[..10];
        var (_, shopAuth) = await api.SignUpAsync(name);
        var household = held.CreateClient();
        var auth = await (await household.PostJsonAsync("/api/v1/auth/register", new { fullName = "Priya", phone = ApiFactory.NewPhone(), password = "secret123" })).ReadAsync<AuthResponse>();
        household.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var url = $"/api/v1/shops?search={Uri.EscapeDataString(name)}";
        Assert.Empty(await household.GetJsonAsync<List<Planner.ShopDirectoryDto>>(url));

        var admin = await AdminAsync();
        await admin.PostJsonAsync($"/api/v1/platform/shops/{shopAuth.ShopId}/approve", new { });
        Assert.Single(await household.GetJsonAsync<List<Planner.ShopDirectoryDto>>(url));
    }

    [Fact]
    public async Task A_super_admin_gives_support_or_approver_access_and_each_role_can_do_only_its_part()
    {
        var super = await AdminAsync();
        var (_, shopAuth) = await api.SignUpAsync("Gupta Kirana");
        var shopId = shopAuth.ShopId;

        // The helper signs up, and proves the number with an SMS code before they can be given access.
        var phone = ApiFactory.NewPhone();
        await api.SignUpAsync("Helper's own shop", phone);
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PostJsonAsync("/api/v1/platform/admins", new { phone, role = "support" })).StatusCode);
        // The SMS ends any password set before the number was proved, so they choose one now.
        var verified = await SignInWithCodeAsync(phone);
        (await verified.PutAsJsonAsync("/api/v1/profile/password", new { newPassword = "secret123" })).EnsureSuccessStatusCode();

        var admins = await (await super.PostJsonAsync("/api/v1/platform/admins", new { phone, role = "support" })).ReadAsync<List<AdminDto>>();
        var helperId = Assert.Single(admins, a => a.Role == AdminRole.Support).UserId;

        // Their next sign-in carries the admin mark; the role itself is checked on every request.
        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        Assert.Equal(AdminRole.Support, signedIn.User.PlatformRole);
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);

        // Support: sees shops, writes notes; can't approve or manage admins.
        Assert.Equal(HttpStatusCode.OK, (await helper.GetAsync("/api/v1/platform/shops")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await helper.PutAsJsonAsync($"/api/v1/platform/shops/{shopId}/note", new { note = "Called, all fine" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PostJsonAsync($"/api/v1/platform/shops/{shopId}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.GetAsync("/api/v1/platform/admins")).StatusCode);

        // Made an approver: now approves, but no longer writes notes. Takes effect on the next request.
        await super.PutAsJsonAsync($"/api/v1/platform/admins/{helperId}", new { role = "approver" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, (await helper.PostJsonAsync($"/api/v1/platform/shops/{shopId}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PutAsJsonAsync($"/api/v1/platform/shops/{shopId}/note", new { note = "x" }, ApiFactory.Json)).StatusCode);

        // Removed: refused at once.
        Assert.Equal(HttpStatusCode.OK, (await super.DeleteAsync($"/api/v1/platform/admins/{helperId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.GetAsync("/api/v1/platform/shops")).StatusCode);
    }

    [Fact]
    public async Task The_last_super_admin_cannot_be_removed_or_demoted()
    {
        var super = await AdminAsync();
        var others = (await super.GetJsonAsync<List<AdminDto>>("/api/v1/platform/admins")).Where(a => a.Role == AdminRole.Super).ToList();
        // Every other super admin left over from other tests goes first, so this one is the last.
        var me = await super.GetJsonAsync<MeResponse>("/api/v1/auth/me");
        foreach (var other in others.Where(a => a.UserId != me.User.Id))
        {
            await super.DeleteAsync($"/api/v1/platform/admins/{other.UserId}");
        }

        Assert.Equal(HttpStatusCode.Conflict, (await super.DeleteAsync($"/api/v1/platform/admins/{me.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await super.PutAsJsonAsync($"/api/v1/platform/admins/{me.User.Id}", new { role = "support" }, ApiFactory.Json)).StatusCode);
    }

    private async Task<HttpClient> SignInWithCodeAsync(string phone)
    {
        var client = api.CreateClient();
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone });
        var code = api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(Common.PhoneNumber.Normalize(phone)!);
        var auth = await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
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
