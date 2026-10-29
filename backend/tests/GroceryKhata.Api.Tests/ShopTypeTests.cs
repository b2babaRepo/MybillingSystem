using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>One app for many trades: a cement shop starts with its own items, units and GST, and sets today's rates.</summary>
[Collection(ApiCollection.Name)]
public sealed class ShopTypeTests(ApiFactory api)
{
    [Fact]
    public async Task A_building_material_shop_gets_its_starter_items_sets_todays_rates_and_bills_by_the_bag()
    {
        var shop = await SignUpAsync("Verma Building Material", "building_materials");
        Assert.Equal(ShopType.BuildingMaterials, (await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop")).Type);

        var added = await (await shop.PostJsonAsync("/api/v1/products/starter", new { })).ReadAsync<ImportResult>();
        Assert.True(added.Created >= 10);
        // Again: nothing doubled.
        Assert.Equal(0, (await (await shop.PostJsonAsync("/api/v1/products/starter", new { })).ReadAsync<ImportResult>()).Created);

        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var cement = Assert.Single(items, p => p.Name == "Cement OPC 53 (50 kg)");
        var sand = Assert.Single(items, p => p.Name == "River sand");
        Assert.Equal((Unit.Bag, 18m, "2523"), (cement.Unit, cement.GstRate!.Value, cement.HsnCode));
        Assert.Equal((Unit.Brass, 5m), (sand.Unit, sand.GstRate!.Value));

        var rates = await shop.PutAsJsonAsync("/api/v1/products/rates", new
        {
            rates = new[] { new { productId = cement.Id, sellingPrice = 385m }, new { productId = sand.Id, sellingPrice = 6500m } },
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, rates.StatusCode);

        // 20 bags of cement and half a brass of sand on one bill.
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            lines = new object[]
            {
                new { productId = cement.Id, quantity = 20m, unitPrice = 385m },
                new { productId = sand.Id, quantity = 0.5m, unitPrice = 6500m },
                new { name = "Transport", quantity = 1m, unit = "piece", unitPrice = 800m },
            },
            paidAmount = 11750m,
        })).ReadAsync<Ledger.BillDto>();
        Assert.Equal(7700m + 3250m + 800m, bill.Total);
        Assert.Equal(Unit.Bag, bill.Lines[0].Unit);
    }

    [Fact]
    public async Task Rates_and_tax_codes_are_checked_and_a_kirana_gets_no_starter_items()
    {
        var shop = await SignUpAsync("Verma Building Material", "building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).First(p => p.Name.StartsWith("Cement OPC"));

        var badGst = await shop.PutAsJsonAsync($"/api/v1/products/{cement.Id}", new
        {
            name = cement.Name, unit = "bag", sellingPrice = 380m, gstRate = 7m,
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, badGst.StatusCode);

        var negative = await shop.PutAsJsonAsync("/api/v1/products/rates", new { rates = new[] { new { productId = cement.Id, sellingPrice = -1m } } }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        // A kirana shop has no starter list: nothing is added.
        var (kirana, _) = await api.SignUpAsync();
        Assert.Equal(0, (await (await kirana.PostJsonAsync("/api/v1/products/starter", new { })).ReadAsync<ImportResult>()).Created);
    }

    private async Task<HttpClient> SignUpAsync(string shopName, string type)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName, fullName = "Suresh Verma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
