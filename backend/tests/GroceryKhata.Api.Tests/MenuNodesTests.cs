using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>The platform admin's category menu: names, order, hiding, new categories and pictures, read by every shop.</summary>
[Collection(ApiCollection.Name)]
public sealed class MenuNodesTests(ApiFactory api)
{
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

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
    public async Task The_admin_renames_hides_adds_and_pictures_menu_nodes_and_every_shop_reads_them()
    {
        var admin = await AdminAsync();
        var (shop, _) = await api.SignUpAsync();
        var trade = $"t{Guid.NewGuid():N}"[..12]; // a menu of its own, so other tests don't see it

        // A shop can't edit the platform's menu.
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.PutAsJsonAsync($"/api/v1/platform/menu/{trade}/tea", new { kind = "dept", nameEn = "Chai" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/platform/menu/{trade}/Bad Key", new { kind = "dept" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/platform/menu/{trade}/tea", new { kind = "dept", parentKey = "staples" }, ApiFactory.Json)).StatusCode);

        (await admin.PutAsJsonAsync($"/api/v1/platform/menu/{trade}/tea", new { kind = "dept", nameHi = "चाय", nameEn = "Chai", sortOrder = 1 }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/platform/menu/{trade}/baby", new { kind = "cat", isHidden = true }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/platform/menu/{trade}/rusk", new { kind = "cat", parentKey = "dairy", nameEn = "Rusk & toast", words = "rusk, toast" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/platform/menu/{trade}/tea/picture", new { dataUrl = Png, kind = "dept" }, ApiFactory.Json)).EnsureSuccessStatusCode();

        var menu = await api.CreateClient().GetJsonAsync<List<MenuNodeDto>>($"/api/v1/menu/{trade}");
        var tea = menu.Single(n => n.Key == "tea");
        Assert.Equal(("चाय", "Chai", 1), (tea.NameHi, tea.NameEn, tea.SortOrder));
        Assert.NotNull(tea.PictureAt);
        Assert.True(menu.Single(n => n.Key == "baby").IsHidden);
        Assert.Equal(("dairy", "rusk, toast"), (menu.Single(n => n.Key == "rusk").ParentKey, menu.Single(n => n.Key == "rusk").Words));

        var picture = await api.CreateClient().GetAsync($"/api/v1/menu-pictures/{trade}/tea");
        picture.EnsureSuccessStatusCode();
        Assert.StartsWith("image/", picture.Content.Headers.ContentType?.MediaType);

        // Removing the picture keeps the rename; resetting the node goes back to the built-in tree.
        (await admin.DeleteAsync($"/api/v1/platform/menu/{trade}/tea/picture")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/api/v1/menu-pictures/{trade}/tea")).StatusCode);
        Assert.Equal("Chai", (await api.CreateClient().GetJsonAsync<List<MenuNodeDto>>($"/api/v1/menu/{trade}")).Single(n => n.Key == "tea").NameEn);
        (await admin.DeleteAsync($"/api/v1/platform/menu/{trade}/tea")).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await api.CreateClient().GetJsonAsync<List<MenuNodeDto>>($"/api/v1/menu/{trade}"), n => n.Key == "tea");
    }
}
