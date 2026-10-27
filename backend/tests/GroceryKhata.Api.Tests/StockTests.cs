using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class StockTests(ApiFactory api)
{
    [Fact]
    public async Task Every_stock_change_is_a_movement_and_stock_always_equals_their_sum()
    {
        var (shop, _) = await api.SignUpAsync();
        var atta = await CreateProductAsync(shop, "Atta", "kg", 44m, openingStock: 20m, category: "Atta & rice");
        Assert.Equal(20m, atta.Stock);

        // A bill takes stock off the shelf by itself.
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            lines = new[] { new { productId = atta.Id, quantity = 2.5m, unitPrice = 44m } },
            paidAmount = 110m,
        })).ReadAsync<BillDto>();

        // A purchase adds stock and updates the cost; a damaged bag comes off with a reason.
        await AddMovementAsync(shop, atta.Id, "purchase", 10m, costPrice: 40m);
        await AddMovementAsync(shop, atta.Id, "adjustment", -1.5m, reason: "damage");

        var product = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{atta.Id}");
        var movements = await shop.GetJsonAsync<List<StockMovementDto>>($"/api/v1/products/{atta.Id}/movements");
        Assert.Equal(26m, product.Stock); // 20 − 2.5 + 10 − 1.5
        Assert.Equal(40m, product.CostPrice);
        Assert.Equal(product.Stock, movements.Sum(m => m.Quantity));
        Assert.Equal(
            [StockMovementKind.Opening, StockMovementKind.Sale, StockMovementKind.Purchase, StockMovementKind.Adjustment],
            movements.OrderBy(m => m.OccurredAt).Select(m => m.Kind));
        Assert.NotNull(movements.Single(m => m.Kind == StockMovementKind.Sale).BillId);
    }

    [Fact]
    public async Task Adjustments_need_a_reason_and_purchases_must_add_stock()
    {
        var (shop, _) = await api.SignUpAsync();
        var sugar = await CreateProductAsync(shop, "Sugar", "kg", 48m, openingStock: 5m);

        var noReason = await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = sugar.Id, kind = "adjustment", quantity = -1m });
        var negativePurchase = await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = sugar.Id, kind = "purchase", quantity = -1m });
        var sale = await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = sugar.Id, kind = "sale", quantity = -1m });

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativePurchase.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sale.StatusCode); // sales only come from bills
        Assert.Equal(5m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{sugar.Id}")).Stock);
    }

    [Fact]
    public async Task A_sale_is_never_blocked_and_low_or_negative_stock_shows_on_the_low_list()
    {
        var (shop, _) = await api.SignUpAsync();
        var oil = await CreateProductAsync(shop, "Mustard oil", "litre", 185m, openingStock: 1m);
        var rice = await CreateProductAsync(shop, "Rice", "kg", 60m, openingStock: 4m, reorderLevel: 5m);
        await CreateProductAsync(shop, "Salt", "packet", 20m, openingStock: 30m, reorderLevel: 5m);

        var sold = await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            lines = new[] { new { productId = oil.Id, quantity = 3m, unitPrice = 185m } },
            paidAmount = 555m,
        });

        Assert.Equal(HttpStatusCode.Created, sold.StatusCode);
        var low = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products?low=true");
        Assert.Equal(["Mustard oil", "Rice"], low.Select(p => p.Name).Order());
        Assert.Equal(-2m, low.Single(p => p.Id == oil.Id).Stock);
        Assert.All(low, p => Assert.True(p.IsLow));
        Assert.Contains(rice.Id, low.Select(p => p.Id));
    }

    [Fact]
    public async Task A_stocktake_corrects_the_books_once_even_if_sent_twice()
    {
        var (shop, _) = await api.SignUpAsync();
        var dal = await CreateProductAsync(shop, "Toor dal", "kg", 160m, openingStock: 10m);
        var tea = await CreateProductAsync(shop, "Tea", "packet", 120m, openingStock: 6m);
        var stocktake = new { id = Guid.CreateVersion7(), counts = new[] { new { productId = dal.Id, counted = 8.5m }, new { productId = tea.Id, counted = 6m } } };

        var first = await (await shop.PostJsonAsync("/api/v1/stocktakes", stocktake)).ReadAsync<StocktakeResult>();
        var again = await (await shop.PostJsonAsync("/api/v1/stocktakes", stocktake)).ReadAsync<StocktakeResult>();

        Assert.Equal(new StocktakeResult(1, 1), first);
        Assert.Equal(0, again.Adjusted);
        Assert.Equal(8.5m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{dal.Id}")).Stock);
        var correction = Assert.Single(
            await shop.GetJsonAsync<List<StockMovementDto>>($"/api/v1/products/{dal.Id}/movements"), m => m.Kind == StockMovementKind.Adjustment);
        Assert.Equal((-1.5m, StockReason.CountCorrection), (correction.Quantity, correction.Reason!.Value));
    }

    [Fact]
    public async Task Import_adds_many_items_skips_names_the_shop_has_and_refuses_bad_rows_by_number()
    {
        var (shop, _) = await api.SignUpAsync();
        await CreateProductAsync(shop, "Parle-G", "packet", 10m);
        var rows = new object[]
        {
            new { name = "Aashirvaad Atta 5kg", unit = "packet", sellingPrice = 245m, category = "Atta & rice", openingStock = 12m, reorderLevel = 4m },
            new { name = "parle-g", unit = "packet", sellingPrice = 10m },
            new { name = "Haldi", unit = "packet", category = "Masala" }, // no price yet: starter-catalogue style
        };

        var result = await (await shop.PostJsonAsync("/api/v1/products/import", new { rows })).ReadAsync<ImportResult>();
        var again = await (await shop.PostJsonAsync("/api/v1/products/import", new { rows })).ReadAsync<ImportResult>();
        var bad = await shop.PostJsonAsync("/api/v1/products/import", new { rows = new object[] { new { name = "Ok", unit = "kg", sellingPrice = 1m }, new { name = "", unit = "kg" } } });

        Assert.Equal(new ImportResult(2, 1), result);
        Assert.Equal(new ImportResult(0, 3), again);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("Rows[2]", await bad.Content.ReadAsStringAsync());
        var products = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        Assert.Equal(3, products.Count);
        Assert.Equal(12m, products.Single(p => p.Name == "Aashirvaad Atta 5kg").Stock);
        Assert.Equal(["Atta & rice", "Masala"], await shop.GetJsonAsync<List<string>>("/api/v1/products/categories"));
    }

    [Fact]
    public async Task Another_shop_cannot_see_or_move_this_shops_stock()
    {
        var (shopA, _) = await api.SignUpAsync("Shop A");
        var (shopB, _) = await api.SignUpAsync("Shop B");
        var atta = await CreateProductAsync(shopA, "Atta", "kg", 44m, openingStock: 20m);

        Assert.Empty(await shopB.GetJsonAsync<List<StockMovementDto>>($"/api/v1/products/{atta.Id}/movements"));
        Assert.Equal(HttpStatusCode.NotFound, (await shopB.GetAsync($"/api/v1/products/{atta.Id}")).StatusCode);
        var move = await shopB.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = atta.Id, kind = "purchase", quantity = 5m });
        var edit = await shopB.PutAsJsonAsync($"/api/v1/products/{atta.Id}", new { name = "Mine", unit = "kg", sellingPrice = 1m }, ApiFactory.Json);
        var count = await (await shopB.PostJsonAsync("/api/v1/stocktakes", new { id = Guid.CreateVersion7(), counts = new[] { new { productId = atta.Id, counted = 0m } } }))
            .ReadAsync<StocktakeResult>();

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(new StocktakeResult(0, 0), count);
        var mine = await shopA.GetJsonAsync<ProductDto>($"/api/v1/products/{atta.Id}");
        Assert.Equal(("Atta", 20m), (mine.Name, mine.Stock));
    }

    private static async Task<ProductDto> CreateProductAsync(
        HttpClient client, string name, string unit, decimal price, decimal? openingStock = null, decimal? reorderLevel = null, string? category = null) =>
        await (await client.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name, unit, sellingPrice = price, openingStock, reorderLevel, category }))
            .ReadAsync<ProductDto>();

    private static async Task AddMovementAsync(HttpClient client, Guid productId, string kind, decimal quantity, string? reason = null, decimal? costPrice = null) =>
        await (await client.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId, kind, quantity, reason, costPrice }))
            .ReadAsync<StockMovementDto>();
}
