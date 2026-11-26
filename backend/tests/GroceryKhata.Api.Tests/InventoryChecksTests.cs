using System.Net.Http.Headers;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>What the order costs, stock left out of the value for want of a cost, and items sold below zero.</summary>
[Collection(ApiCollection.Name)]
public sealed class InventoryChecksTests(ApiFactory api)
{
    [Fact]
    public async Task Order_today_says_what_the_order_costs_to_the_owner_only()
    {
        var (owner, _) = await api.SignUpAsync();
        var supplier = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Agarwal Agencies" })).EnsureSuccessStatusCode();
        // Bought at ₹25, kept at 4 or more: 6 to order, ₹150. Another with no purchase rate yet.
        var soap = await ItemAsync(owner, "Soap", reorderLevel: 6m);
        (await owner.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = soap, quantity = 0.001m, unitCost = 25m } }, paidAmount = 0m })).EnsureSuccessStatusCode();
        var matches = await ItemAsync(owner, "Matches", reorderLevel: 2m);

        var today = await owner.GetJsonAsync<OrderTodayDto>("/api/v1/stock/order-today");
        var soapLine = today.Items.Single(i => i.ProductId == soap);
        Assert.Contains(today.Items, i => i.ProductId == matches);
        Assert.Equal((soapLine.Suggested * 25m, 1), (today.Total!.Value, today.Unpriced!.Value));

        // A helper sees what to order, not what it costs.
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var helper = api.CreateClient();
        var auth = await (await helper.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var seen = await helper.GetJsonAsync<OrderTodayDto>("/api/v1/stock/order-today");
        Assert.Equal((today.Count, (decimal?)null, (int?)null), (seen.Count, seen.Total, seen.Unpriced));
    }

    [Fact]
    public async Task Stock_health_counts_items_left_out_of_the_value_and_the_data_check_lists_stock_below_zero()
    {
        var (owner, _) = await api.SignUpAsync();
        var costed = await ItemAsync(owner, "Rice", stock: 10m, cost: 40m);
        await ItemAsync(owner, "Loose chana", stock: 5m);
        var milk = await ItemAsync(owner, "Milk", stock: 2m, cost: 25m);
        (await owner.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = milk, quantity = 7m, unitPrice = 30m } }, paidAmount = 210m, paymentMode = "cash" })).EnsureSuccessStatusCode();

        var health = await owner.GetJsonAsync<StockHealthDto>("/api/v1/reports/stock-health");
        Assert.Equal((400m, 1), (health.Value, health.Unvalued)); // rice only; the chana has no cost, milk is below zero
        _ = costed;

        var check = (await owner.GetJsonAsync<DataCheckDto>("/api/v1/data-check")).Checks.Single(c => c.Code == "negativeStock");
        var row = Assert.Single(check.Samples);
        Assert.Equal((true, 1, "Milk", "-5", milk), (check.Warning, check.Problems, row.Name, row.Detail, row.Id!.Value));
    }

    [Fact]
    public async Task A_restaurants_dishes_are_not_counted_so_selling_them_never_puts_them_on_the_order_list()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Sharma Dhaba", fullName = "Suresh Sharma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "restaurant" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var menu = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var naan = menu.Single(p => p.Name == "Butter naan");
        Assert.True(naan.Uncounted);
        Assert.False(menu.Single(p => p.Name == "Cold drink").Uncounted); // bought in and resold: counted

        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = naan.Id, quantity = 4m, unitPrice = 60m } }, paidAmount = 240m, paymentMode = "cash" })).EnsureSuccessStatusCode();

        Assert.DoesNotContain((await shop.GetJsonAsync<OrderTodayDto>("/api/v1/stock/order-today")).Items, i => i.ProductId == naan.Id);
        Assert.Empty((await shop.GetJsonAsync<DataCheckDto>("/api/v1/data-check")).Checks.Single(c => c.Code == "negativeStock").Samples);
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal? stock = null, decimal? cost = null, decimal? reorderLevel = null)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = 30m, costPrice = cost, openingStock = stock, reorderLevel })).EnsureSuccessStatusCode();
        return id;
    }
}
