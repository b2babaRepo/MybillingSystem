using System.Net.Http.Headers;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>
/// The sales rate behind "days left" and "order today": per day the item was on the shelf, and no order for an item
/// that never sells just because it is at zero.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SalesRateTests(ApiFactory api)
{
    [Fact]
    public async Task Days_with_an_empty_shelf_do_not_count_against_the_sales_rate()
    {
        var (shop, shopId) = await ShopAsync();
        var dal = await ItemAsync(shop, "Toor dal", 40m);
        (await Bill(shop, dal, 30m)).EnsureSuccessStatusCode();
        // Out of stock for the first half of the month: the 40 came in 14 days ago, and 30 sold yesterday.
        await AdminAsync(
            """
            UPDATE stock_movements SET occurred_at = now() - interval '14 days' WHERE product_id = @p AND kind = 'Opening';
            UPDATE stock_movements SET occurred_at = now() - interval '1 day' WHERE product_id = @p AND kind = 'Sale';
            UPDATE bills SET occurred_at = now() - interval '1 day' WHERE shop_id = @s;
            """, ("p", dal), ("s", shopId));

        // 30 sold over the 15 days it was there: 2 a day (not 1 a day over all 30), so the 10 left last 5 days.
        Assert.Equal(2m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{dal}")).DailySales);
        var health = await shop.GetJsonAsync<StockHealthDto>("/api/v1/reports/stock-health");
        Assert.Equal(5m, health.Items.Single(i => i.ProductId == dal).DaysOfCover);
    }

    [Fact]
    public async Task A_rate_is_spread_over_at_least_a_week_and_an_item_always_on_the_shelf_keeps_the_month()
    {
        var (shop, shopId) = await ShopAsync();
        var fresh = await ItemAsync(shop, "New biscuit", 50m);
        var steady = await ItemAsync(shop, "Salt", 60m);
        (await Bill(shop, fresh, 14m)).EnsureSuccessStatusCode();
        (await Bill(shop, steady, 30m)).EnsureSuccessStatusCode();
        // Salt has been on the shelf all month; the biscuit came in today.
        await AdminAsync("UPDATE stock_movements SET occurred_at = now() - interval '40 days' WHERE product_id = @p AND kind = 'Opening'", ("p", steady));

        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        Assert.Equal(2m, items.Single(p => p.Id == fresh).DailySales); // 14 over a week, not over its one day
        Assert.Equal(1m, items.Single(p => p.Id == steady).DailySales); // 30 over 30 days, as before
        _ = shopId;
    }

    [Fact]
    public async Task An_item_at_zero_is_ordered_only_if_it_has_sold_lately_or_has_a_reorder_level()
    {
        var (shop, shopId) = await ShopAsync();
        var never = await ItemAsync(shop, "Old brand soap", 0m);
        var levelled = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = levelled, name = "Matches", unit = "piece", sellingPrice = 2m, reorderLevel = 0m })).EnsureSuccessStatusCode();
        // Sold out two months ago and empty since: no 30-day rate, but it does sell.
        var soldOut = await ItemAsync(shop, "Hing", 5m);
        (await Bill(shop, soldOut, 5m)).EnsureSuccessStatusCode();
        await AdminAsync(
            """
            UPDATE stock_movements SET occurred_at = now() - interval '61 days' WHERE product_id = @p AND kind = 'Opening';
            UPDATE stock_movements SET occurred_at = now() - interval '60 days' WHERE product_id = @p AND kind = 'Sale';
            UPDATE bills SET occurred_at = now() - interval '60 days' WHERE shop_id = @s;
            """, ("p", soldOut), ("s", shopId));

        var today = await shop.GetJsonAsync<OrderTodayDto>("/api/v1/stock/order-today");
        Assert.DoesNotContain(today.Items, i => i.ProductId == never);
        Assert.Equal(1m, today.Items.Single(i => i.ProductId == levelled).Suggested);
        Assert.Equal(1m, today.Items.Single(i => i.ProductId == soldOut).Suggested);
    }

    private async Task<(HttpClient Shop, Guid ShopId)> ShopAsync()
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Verma Traders", fullName = "Suresh Verma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.ShopId!.Value);
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal stock)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = 10m, openingStock = stock })).EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> Bill(HttpClient shop, Guid product, decimal quantity) =>
        shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), lines = new[] { new { productId = product, quantity, unitPrice = 10m } }, paidAmount = quantity * 10m, paymentMode = "cash",
        });

    private async Task AdminAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
