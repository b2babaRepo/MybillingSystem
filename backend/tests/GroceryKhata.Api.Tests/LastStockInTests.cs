using GroceryKhata.Api.Catalogue;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class LastStockInTests(ApiFactory api)
{
    [Fact]
    public async Task Each_item_shows_when_stock_last_came_in_and_from_which_supplier()
    {
        var (shop, _) = await api.SignUpAsync("Last In Store");
        var kurti = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = kurti, name = "Kurti", unit = "piece", sellingPrice = 699m, openingStock = 5m })).EnsureSuccessStatusCode();
        var saree = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = saree, name = "Saree", unit = "piece", sellingPrice = 1499m })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Surat Saree Centre" })).EnsureSuccessStatusCode();

        // Sarees came in from the supplier as goods received; the kurtis only ever had opening stock entered by hand.
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = saree, quantity = 10m } } })).EnsureSuccessStatusCode();

        var last = await shop.GetJsonAsync<List<LastStockIn>>("/api/v1/stock/last-in");
        var s = last.Single(l => l.ProductId == saree);
        Assert.Equal((10m, "Surat Saree Centre", false), (s.Quantity, s.Supplier, s.ByHand));
        var k = last.Single(l => l.ProductId == kurti);
        Assert.Equal((5m, (string?)null, true), (k.Quantity, k.Supplier, k.ByHand));
    }
}
