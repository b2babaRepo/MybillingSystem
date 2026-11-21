using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Planner;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Customer app phase 3: hearts with back-in-stock, festive look, data download and closing the account.</summary>
[Collection(ApiCollection.Name)]
public sealed class CustomerExtrasTests(ApiFactory api)
{
    [Fact]
    public async Task A_hearted_item_that_runs_out_is_marked_and_cleared_when_it_is_back()
    {
        var (shop, auth) = await api.SignUpAsync("Heart Store");
        var ghee = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Ghee", unit = "piece", sellingPrice = 600m, openingStock = 0m })).ReadAsync<ProductDto>();
        var priya = await RegisterAsync();

        (await priya.PutAsJsonAsync($"/api/v1/me/saved-items/{auth.ShopId}/{ghee.Id}", new { })).EnsureSuccessStatusCode();
        (await priya.PutAsJsonAsync($"/api/v1/me/saved-items/{auth.ShopId}/{ghee.Id}", new { })).EnsureSuccessStatusCode();
        var saved = Assert.Single(await priya.GetJsonAsync<List<SavedItemDto>>("/api/v1/me/saved-items"));
        Assert.Equal(("Ghee", 0m), (saved.Name, saved.Available));
        Assert.Equal(HttpStatusCode.NotFound, (await priya.PutAsJsonAsync($"/api/v1/me/saved-items/{auth.ShopId}/{Guid.CreateVersion7()}", new { })).StatusCode);

        // Stock arrives: the check finds it back (once), and the saved list shows it available.
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = ghee.Id, kind = "purchase", quantity = 5m, costPrice = 500m })).EnsureSuccessStatusCode();
        var scopes = api.Services.GetRequiredService<IServiceScopeFactory>();
        Assert.True(await CustomerExtras.CheckBackInStockAsync(scopes, default) >= 1);
        Assert.Equal(0, await CountBackAsync(scopes, auth.ShopId!.Value));
        Assert.Equal(5m, Assert.Single(await priya.GetJsonAsync<List<SavedItemDto>>("/api/v1/me/saved-items")).Available);

        (await priya.DeleteAsync($"/api/v1/me/saved-items/{auth.ShopId}/{ghee.Id}")).EnsureSuccessStatusCode();
        Assert.Empty(await priya.GetJsonAsync<List<SavedItemDto>>("/api/v1/me/saved-items"));
        Assert.Contains("is back", CustomerExtras.BackMessage("en", "Ghee", "Heart Store").Title);
    }

    [Fact]
    public async Task The_owner_picks_a_festive_look_from_the_list()
    {
        var (shop, auth) = await api.SignUpAsync("Theme Store");
        (await shop.PutAsJsonAsync("/api/v1/store-settings", new { slots = Array.Empty<string>(), theme = "diwali" })).EnsureSuccessStatusCode();
        var store = await api.CreateClient().GetJsonAsync<StoreDto>($"/api/v1/store/{auth.Memberships.Single().ShopSlug}");
        Assert.Equal("diwali", store.Settings!.Theme);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/store-settings", new { slots = Array.Empty<string>(), theme = "neon" })).StatusCode);
    }

    [Fact]
    public async Task A_customer_downloads_their_data_and_closes_the_account()
    {
        var phone = ApiFactory.NewPhone();
        var priya = await RegisterAsync(phone);
        (await priya.PostJsonAsync("/api/v1/addresses", new { id = Guid.CreateVersion7(), label = "Home", text = "12 MI Road, Jaipur" })).EnsureSuccessStatusCode();
        var export = await priya.GetStringAsync("/api/v1/me/export");
        Assert.Contains("12 MI Road", export);
        Assert.Contains("\"orders\"", export);

        (await priya.PostJsonAsync("/api/v1/me/delete-account", new { })).EnsureSuccessStatusCode();
        // The number is free again: a new account can be opened with it.
        var again = await api.CreateClient().PostJsonAsync("/api/v1/auth/register", new { fullName = "Priya again", phone, password = "secret123" });
        again.EnsureSuccessStatusCode();

        // Someone who runs a shop closes the shop first.
        var (owner, _) = await api.SignUpAsync("Owner Store");
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync("/api/v1/me/delete-account", new { })).StatusCode);
    }

    private async Task<int> CountBackAsync(IServiceScopeFactory scopes, Guid shopId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GroceryKhata.Api.Persistence.AppDbContext>();
        return db.SavedItems.Count(x => x.ShopId == shopId && x.WasOut);
    }

    private async Task<HttpClient> RegisterAsync(string? phone = null)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/register", new { fullName = "Priya", phone = phone ?? ApiFactory.NewPhone(), password = "secret123" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
