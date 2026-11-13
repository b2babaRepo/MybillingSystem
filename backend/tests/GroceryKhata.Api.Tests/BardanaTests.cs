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

    [Fact]
    public async Task Bags_on_goods_that_come_in_are_counted_on_their_own_and_go_if_the_receipt_is_cancelled()
    {
        var shop = await ShopAsync();
        var jute = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bardana/types", new { id = jute, name = "Jute 50 kg", fromGoods = true })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders" })).EnsureSuccessStatusCode();
        var rice = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = rice, name = "Rice", unit = "kg", sellingPrice = 50m, openingStock = 0m })).EnsureSuccessStatusCode();

        var receipt = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = receipt, supplierId = supplier, lines = new[] { new { productId = rice, quantity = 500m, bags = 10m } } })).EnsureSuccessStatusCode();
        var summary = await shop.GetJsonAsync<BardanaDto>("/api/v1/bardana");
        Assert.Equal((10m, true, 10m), (summary.Types.Single().OnHand, summary.Types.Single().FromGoods, summary.Suppliers.Single().Came));

        (await shop.PostJsonAsync($"/api/v1/goods-receipts/{receipt}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<BardanaDto>("/api/v1/bardana")).Types.Single().OnHand);
    }

    [Fact]
    public async Task A_weighbridge_shortfall_is_claimed_once_off_what_the_shop_owes_and_undone_with_the_receipt()
    {
        var shop = await ShopAsync();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders", gstin = "08ABCDE1234F1Z0" })).EnsureSuccessStatusCode();
        var rice = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = rice, name = "Rice", unit = "kg", sellingPrice = 50m, openingStock = 0m, gstRate = 5m })).EnsureSuccessStatusCode();

        // 500 kg on the receipt, 490 kg on the weighbridge: 10 kg short.
        var receipt = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = receipt, supplierId = supplier, grossWeight = 3490m, tareWeight = 3000m, lines = new[] { new { productId = rice, quantity = 500m } } })).EnsureSuccessStatusCode();
        var claimed = await (await shop.PostJsonAsync($"/api/v1/goods-receipts/{receipt}/short-claim", new { ratePerKg = 40m })).ReadAsync<GoodsReceiptDto>();
        // A debit note: 10 kg at 40 is 400, plus 5% GST from a registered supplier, 420 off what the shop owes.
        Assert.Equal(((decimal?)10m, (decimal?)420m), (claimed.ShortKg, claimed.ShortClaim));
        var detail = await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}");
        Assert.Equal((-420m, SupplierEntryKind.CreditNote), (detail.Supplier.Due, detail.Entries.Single().Kind));

        // The 10 kg that never came come off stock: 490 kg on the shelf, not 500.
        Assert.Equal(490m, (await shop.GetJsonAsync<GroceryKhata.Api.Catalogue.ProductDto>($"/api/v1/products/{rice}")).Stock);

        // Only once.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/goods-receipts/{receipt}/short-claim", new { ratePerKg = 40m })).StatusCode);

        // Goods that never came carry no claim: cancelling the receipt puts the 420 back.
        (await shop.PostJsonAsync($"/api/v1/goods-receipts/{receipt}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Supplier.Due);
        Assert.Equal(0m, (await shop.GetJsonAsync<GroceryKhata.Api.Catalogue.ProductDto>($"/api/v1/products/{rice}")).Stock);
    }
}
