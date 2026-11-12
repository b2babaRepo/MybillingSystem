using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>The bardana account: empty bags counted in and out, their money in the cash book, and suppliers' bags held.</summary>
[Collection(ApiCollection.Name)]
public sealed class BardanaTests(ApiFactory api)
{
    private async Task<HttpClient> ShopAsync()
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Bansal Kirana", fullName = "Mohan Bansal", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient shop, Guid type, string kind, decimal quantity, object? extra = null, Guid? id = null)
    {
        var body = new Dictionary<string, object?> { ["id"] = id ?? Guid.CreateVersion7(), ["bagTypeId"] = type, ["kind"] = kind, ["quantity"] = quantity };
        foreach (var p in extra?.GetType().GetProperties() ?? [])
        {
            body[p.Name] = p.GetValue(extra);
        }

        return shop.PostJsonAsync("/api/v1/bardana/moves", body);
    }

    [Fact]
    public async Task Bags_are_counted_in_and_out_and_their_money_reaches_the_cash_book()
    {
        var shop = await ShopAsync();
        var jute = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bardana/types", new { id = jute, name = "Jute 50 kg", saleRate = 12m })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders" })).EnsureSuccessStatusCode();

        (await MoveAsync(shop, jute, "came_with_goods", 40m, new { supplierId = supplier })).EnsureSuccessStatusCode();
        (await MoveAsync(shop, jute, "returned_to_supplier", 10m, new { supplierId = supplier })).EnsureSuccessStatusCode();
        var sale = Guid.CreateVersion7();
        (await MoveAsync(shop, jute, "sold", 20m, new { rate = 11.5m, mode = "cash", party = "Bardana wala" }, sale)).EnsureSuccessStatusCode();
        (await MoveAsync(shop, jute, "sold", 20m, new { rate = 11.5m, mode = "cash", party = "Bardana wala" }, sale)).EnsureSuccessStatusCode(); // same id: once
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(shop, jute, "used", 11m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(shop, jute, "sold", 1m)).StatusCode); // no rate

        var summary = await shop.GetJsonAsync<BardanaDto>("/api/v1/bardana");
        Assert.Equal((10m, 120m, 230m), (summary.Types.Single().OnHand, summary.Types.Single().Value, summary.SoldThisMonth));
        Assert.Equal((40m, 10m, 30m), (summary.Suppliers.Single().Came, summary.Suppliers.Single().Returned, summary.Suppliers.Single().Holding));

        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((230m, CashBookSource.BagsSold), (book.Cash, book.Lines.Single().Source));

        // A count books the difference; a wrong entry is struck and its money leaves the cash book.
        (await MoveAsync(shop, jute, "counted", 7m)).EnsureSuccessStatusCode();
        Assert.Equal(7m, (await shop.GetJsonAsync<BardanaDto>("/api/v1/bardana")).Types.Single().OnHand);
        (await shop.PostJsonAsync($"/api/v1/bardana/moves/{sale}/strike", new { })).EnsureSuccessStatusCode();
        Assert.Equal(27m, (await shop.GetJsonAsync<BardanaDto>("/api/v1/bardana")).Types.Single().OnHand);
        Assert.Equal(0m, (await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book")).Cash);
    }

    [Fact]
    public async Task Bags_bought_are_money_out_and_one_shops_bags_are_its_own()
    {
        var shop = await ShopAsync();
        var plastic = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bardana/types", new { id = plastic, name = "Plastic 25 kg" })).EnsureSuccessStatusCode();
        (await MoveAsync(shop, plastic, "bought", 100m, new { rate = 4m, mode = "upi" })).EnsureSuccessStatusCode();
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((0m, 400m), (book.Upi, book.SpentUpi));

        var other = await ShopAsync();
        Assert.Empty((await other.GetJsonAsync<BardanaDto>("/api/v1/bardana")).Types);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(other, plastic, "used", 1m)).StatusCode);
    }
}
