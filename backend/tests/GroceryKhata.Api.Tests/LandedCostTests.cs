using System.Net.Http.Headers;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Landed cost: freight, hamali and commission spread into a lot's cost, the kharcha, and the lot's margin.</summary>
[Collection(ApiCollection.Name)]
public sealed class LandedCostTests(ApiFactory api)
{
    private async Task<HttpClient> ShopAsync()
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Sethi Traders", fullName = "Kamal Sethi", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal price)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "kg", sellingPrice = price, openingStock = 0m })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<Guid> SupplierAsync(HttpClient shop)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id, name = "Mandi Traders" })).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task Charges_are_spread_by_value_into_cost_and_paid_ones_go_in_the_kharcha_out_of_profit()
    {
        var shop = await ShopAsync();
        var rice = await ItemAsync(shop, "Rice", 50m);
        var dal = await ItemAsync(shop, "Dal", 120m);
        var supplier = await SupplierAsync(shop);
        var bill = Guid.CreateVersion7();
        // ₹3,000 of rice and ₹1,000 of dal; ₹400 freight (cash) and ₹100 hamali (unpaid) → ₹500 spread 3:1.
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = bill, supplierId = supplier, billNo = "MT-9", paidAmount = 0m,
            lines = new object[] { new { productId = rice, quantity = 100m, unitCost = 30m }, new { productId = dal, quantity = 10m, unitCost = 100m } },
            charges = new object[] { new { label = "Lorry freight", amount = 400m, paidBy = "cash" }, new { label = "Hamali", amount = 100m } },
        })).EnsureSuccessStatusCode();

        var lot = await shop.GetJsonAsync<LotDto>($"/api/v1/suppliers/purchases/{bill}/landed");
        var r = lot.Lines.Single(l => l.ProductId == rice);
        var d = lot.Lines.Single(l => l.ProductId == dal);
        Assert.Equal((30m, 3.75m, 33.75m, 16.25m), (r.BillRate, r.ChargePerUnit, r.LandedCost, r.MarginPerUnit));
        Assert.Equal((100m, 12.5m, 112.5m), (d.BillRate, d.ChargePerUnit, d.LandedCost));
        Assert.Equal((500m, 4500m, 1625m + 75m), (lot.Charges, lot.LandedTotal, lot.ExpectedMargin));
        Assert.Equal(33.75m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{rice}")).CostPrice);

        // The freight paid in cash is in today's kharcha as stock bought; the supplier is owed only the bill.
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal(400m, book.SpentCash);
        Assert.Contains(await shop.GetJsonAsync<List<SupplierDto>>("/api/v1/suppliers"), s => s.Id == supplier && s.Due == 4000m);

        // Cancelling the bill takes the freight back off the kharcha.
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{bill}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book")).SpentCash);
    }

    [Fact]
    public async Task Charges_can_be_spread_by_units_and_the_bills_own_freight_can_go_into_cost()
    {
        var shop = await ShopAsync();
        var sugar = await ItemAsync(shop, "Sugar", 45m);
        var salt = await ItemAsync(shop, "Salt", 20m);
        var supplier = await SupplierAsync(shop);
        var bill = Guid.CreateVersion7();
        // ₹200 freight on the supplier's own bill, spread over 50 + 50 units → ₹2 a unit each.
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = bill, supplierId = supplier, paidAmount = 0m, extra = 200m, extraInCost = true, split = "quantity",
            lines = new object[] { new { productId = sugar, quantity = 50m, unitCost = 40m }, new { productId = salt, quantity = 50m, unitCost = 10m } },
        })).EnsureSuccessStatusCode();
        var lots = await shop.GetJsonAsync<List<LotDto>>($"/api/v1/suppliers/lots?productId={salt}");
        Assert.Equal((CostSplit.Quantity, 12m, 2m), (lots.Single().Split, lots.Single().Lines.Single().LandedCost, lots.Single().Lines.Single().ChargePerUnit));
        Assert.Equal(0m, (await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book")).SpentCash);
    }

    [Fact]
    public async Task Each_lot_shows_what_really_sold_from_it_oldest_first_and_freight_can_be_added_later()
    {
        var shop = await ShopAsync();
        var oil = await ItemAsync(shop, "Oil", 50m);
        var supplier = await SupplierAsync(shop);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = first, supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = oil, quantity = 10m, unitCost = 30m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = second, supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = oil, quantity = 10m, unitCost = 40m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = oil, name = "Oil", quantity = 15m, unitPrice = 50m } }, paidAmount = 750m, paymentMode = "cash" })).EnsureSuccessStatusCode();

        // The older lot sold out first: 10 at 50 on a 30 cost; then 5 of the newer one on a 40 cost.
        var lots = await shop.GetJsonAsync<List<LotDto>>($"/api/v1/suppliers/lots?productId={oil}");
        var old = lots.Single(l => l.Id == first).Lines.Single();
        var fresh = lots.Single(l => l.Id == second).Lines.Single();
        Assert.Equal((10m, 500m, 200m, 0m), (old.Sold, old.Revenue, old.Profit, old.Left));
        Assert.Equal((5m, 250m, 50m, 5m), (fresh.Sold, fresh.Revenue, fresh.Profit, fresh.Left));

        // The transporter's bill comes later: ₹20 over the newer lot's 10 units raises its landed cost by 2, and the 5
        // still on hand (all from it) carry it into today's cost.
        var before = (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{oil}")).CostPrice!.Value;
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{second}/charges", new { charges = new[] { new { label = "Lorry freight", amount = 20m, paidBy = "cash" } } })).EnsureSuccessStatusCode();
        var lot = await shop.GetJsonAsync<LotDto>($"/api/v1/suppliers/purchases/{second}/landed");
        Assert.Equal((42m, 20m, 40m), (lot.Lines.Single().LandedCost, lot.Charges, lot.Lines.Single().Profit));
        Assert.Equal(before + 2m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{oil}")).CostPrice);
        Assert.Equal(20m, (await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book")).SpentCash);
    }
}
