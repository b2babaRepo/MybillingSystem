using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>Selling a whole sealed bag (बोरी) at its own bag price, beside loose sales at the loose rate.</summary>
[Collection(ApiCollection.Name)]
public sealed class BagSellingTests(ApiFactory api)
{
    [Fact]
    public async Task A_loose_sale_bigger_than_the_loose_stock_opens_a_bag()
    {
        var (shop, _) = await api.SignUpAsync();
        var rice = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = rice, name = "Kolam rice", unit = "kg", sellingPrice = 60m, bagSize = 50m })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Mandi Traders" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = rice, quantity = 150m, bags = 3m } } }))
            .EnsureSuccessStatusCode();
        async Task Sell(decimal kg, decimal? bags = null) => (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), lines = new[] { new { productId = rice, quantity = kg, unitPrice = 60m, bags } }, paidAmount = kg * 60m, paymentMode = "cash",
        })).EnsureSuccessStatusCode();
        async Task<decimal?> Sealed() => (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{rice}")).Bags;

        // 3 sealed bags and no loose rice: 30 kg loose opens one bag (20 kg left loose beside 2 sealed).
        await Sell(30m);
        Assert.Equal(2m, await Sealed());
        // 20 kg more fits in the open bag: no bag opened. A whole bag then leaves 1 sealed.
        await Sell(20m);
        Assert.Equal(2m, await Sealed());
        await Sell(50m, 1m);
        Assert.Equal(1m, await Sealed());

        var ledger = await shop.GetJsonAsync<ItemLedger>($"/api/v1/products/{rice}/ledger");
        Assert.Single(ledger.Lines, l => l.Kind == StockMovementKind.OpenBag && l.Bags == -1m);
    }

    [Fact]
    public async Task A_helper_sells_whole_bags_at_the_bag_price_but_not_loose_at_it()
    {
        var owner = api.CreateClient();
        var auth = await (await owner.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Arora Stores", fullName = "Vikas Arora", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var settings = await owner.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        (await owner.PutAsJsonAsync("/api/v1/shop", settings with { StaffMaxDiscountPercent = 5m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var helperAuth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helperAuth.AccessToken);

        // Atta at ₹50 a kg loose; a sealed 50 kg bori for ₹2,300 (₹46 a kg, 8% below loose). "Bag" stays a plain bag.
        var atta = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/products", new { id = atta, name = "Chakki atta", unit = "kg", sellingPrice = 50m, costPrice = 42m, openingStock = 200m, bagSize = 50m, purchaseUnit = "bag", bagPrice = 2300m }))
            .EnsureSuccessStatusCode();
        var item = (await owner.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Id == atta);
        Assert.Equal((2300m, (Unit?)null), (item.BagPrice, item.PurchaseUnit));

        // Two bags at the bag price: fine for a helper. The same ₹46 on a loose line is past their 5% limit.
        var bags = await helper.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = atta, name = "Chakki atta", quantity = 100m, unitPrice = 46m, bags = 2m } }, paidAmount = 4600m, paymentMode = "cash" });
        bags.EnsureSuccessStatusCode();
        var loose = await helper.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = atta, name = "Chakki atta", quantity = 5m, unitPrice = 46m } }, paidAmount = 230m, paymentMode = "cash" });
        Assert.Equal(HttpStatusCode.BadRequest, loose.StatusCode);

        // A bag line's quantity is its bags times the bag size, give or take 5% for bags weighed one by one.
        var wrong = await helper.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = atta, name = "Chakki atta", quantity = 60m, unitPrice = 46m, bags = 2m } }, paidAmount = 2760m, paymentMode = "cash" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        (await helper.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = atta, name = "Chakki atta", quantity = 49.4m, unitPrice = 46m, bags = 1m } }, paidAmount = 2272.4m, paymentMode = "cash" }))
            .EnsureSuccessStatusCode();

        // An estimate keeps the bags, so the bill made from it sells them as bags.
        var estimate = await (await owner.PostJsonAsync("/api/v1/estimates", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = atta, name = "Chakki atta", quantity = 150m, unitPrice = 46m, bags = 3m } } }))
            .ReadAsync<System.Text.Json.JsonElement>();
        Assert.Equal(3m, estimate.GetProperty("lines")[0].GetProperty("bags").GetDecimal());

        // So does a sale order.
        var order = await (await owner.PostJsonAsync("/api/v1/sale-orders", new { id = Guid.CreateVersion7(), partyName = "Gupta Hotel", lines = new[] { new { productId = atta, name = "Chakki atta", quantity = 100m, unitPrice = 46m, bags = 2m } } }))
            .ReadAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2m, order.GetProperty("lines")[0].GetProperty("bags").GetDecimal());

        // The bag price survives an edit that doesn't send it, and 0 clears it.
        (await owner.PutAsJsonAsync($"/api/v1/products/{atta}", new { name = "Chakki atta", unit = "kg", sellingPrice = 52m, costPrice = 42m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        Assert.Equal(2300m, (await owner.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Id == atta).BagPrice);
        (await owner.PutAsJsonAsync($"/api/v1/products/{atta}", new { name = "Chakki atta", unit = "kg", sellingPrice = 52m, costPrice = 42m, bagPrice = 0m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        Assert.Null((await owner.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Id == atta).BagPrice);
    }
}
