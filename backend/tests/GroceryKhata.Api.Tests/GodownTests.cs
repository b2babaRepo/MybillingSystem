using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;

namespace GroceryKhata.Api.Tests;

/// <summary>Godowns hold part of an item's stock; transfers move it between the shop shelf and godowns.</summary>
[Collection(ApiCollection.Name)]
public sealed class GodownTests(ApiFactory api)
{
    private async Task<HttpClient> ShopAsync()
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Gupta Kirana", fullName = "Ramesh Gupta", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal stock)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "kg", sellingPrice = 50m, costPrice = 40m, openingStock = stock })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<Guid> GodownAsync(HttpClient shop, string name)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/godowns", new { id, name })).EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient shop, Guid? from, Guid? to, Guid item, decimal quantity, Guid? id = null) =>
        shop.PostJsonAsync("/api/v1/godowns/transfers", new { id = id ?? Guid.CreateVersion7(), fromGodownId = from, toGodownId = to, lines = new[] { new { productId = item, quantity } } });

    private static async Task<PlaceStockDto> WhereAsync(HttpClient shop, Guid item) =>
        (await shop.GetJsonAsync<List<PlaceStockDto>>("/api/v1/godowns/stock")).Single(s => s.ProductId == item);

    [Fact]
    public async Task Goods_move_between_the_shelf_and_godowns_without_changing_the_shops_total()
    {
        var shop = await ShopAsync();
        var rice = await ItemAsync(shop, "Rice", 100m);
        var back = await GodownAsync(shop, "Back room");
        var mandi = await GodownAsync(shop, "Mandi godown");

        (await MoveAsync(shop, null, back, rice, 70m)).EnsureSuccessStatusCode();
        (await MoveAsync(shop, back, mandi, rice, 20m)).EnsureSuccessStatusCode();
        var where = await WhereAsync(shop, rice);
        Assert.Equal((100m, 30m, 50m, 20m), (where.Total, where.Shelf, where.Godowns[back], where.Godowns[mandi]));

        // Can't take more than is there, from a godown or from the shelf.
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(shop, mandi, null, rice, 21m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(shop, null, back, rice, 31m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(shop, back, back, rice, 1m)).StatusCode);

        var godowns = await shop.GetJsonAsync<List<GodownDto>>("/api/v1/godowns");
        Assert.Equal((50m, 2000m), (godowns.Single(g => g.Id == back).Units, godowns.Single(g => g.Id == back).Value));
        Assert.Equal(2, (await shop.GetJsonAsync<List<TransferDto>>($"/api/v1/godowns/transfers?godownId={back}")).Count);
    }

    [Fact]
    public async Task A_move_is_undone_only_while_the_goods_are_still_where_it_put_them()
    {
        var shop = await ShopAsync();
        var dal = await ItemAsync(shop, "Toor dal", 40m);
        var back = await GodownAsync(shop, "Back room");
        var mandi = await GodownAsync(shop, "Mandi godown");
        var first = await (await MoveAsync(shop, null, back, dal, 30m)).ReadAsync<TransferDto>();
        (await MoveAsync(shop, back, mandi, dal, 25m)).EnsureSuccessStatusCode();

        // Only 5 left in the back room, so the first move can't be undone.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/godowns/transfers/{first.Id}/cancel", new { })).StatusCode);
        var second = (await shop.GetJsonAsync<List<TransferDto>>("/api/v1/godowns/transfers")).First();
        (await shop.PostJsonAsync($"/api/v1/godowns/transfers/{second.Id}/cancel", new { })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/godowns/transfers/{first.Id}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(40m, (await WhereAsync(shop, dal)).Shelf);

        // A godown holding goods can't be put away; an empty one can.
        (await MoveAsync(shop, null, back, dal, 1m)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/godowns", new { id = back, name = "Back room", isActive = false })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/godowns", new { id = mandi, name = "Mandi godown", isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(shop, back, mandi, dal, 1m)).StatusCode);
    }

    [Fact]
    public async Task One_shops_godowns_are_not_seen_by_another()
    {
        var shop = await ShopAsync();
        await GodownAsync(shop, "Back room");
        Assert.Empty(await (await ShopAsync()).GetJsonAsync<List<GodownDto>>("/api/v1/godowns"));
    }
}
