using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>
/// A whole shop day for every trade, end to end: buy stock on credit, sell for cash and on khata, take a payment, pay
/// rent, cancel a bill, take goods back, then check every report adds up. Each step is checked as it happens.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ShopDayTests(ApiFactory api)
{
    public static TheoryData<string> Trades => ["water_supply", "beauty", "sweets", "toys", "clothes", "building_materials", "paint", "hardware", "electrical"];

    [Theory]
    [MemberData(nameof(Trades))]
    public async Task A_shop_day_adds_up_in_every_report(string trade)
    {
        var shop = await SignUpAsync(trade);
        var settings = await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        (await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z5" }, ApiFactory.Json)).EnsureSuccessStatusCode();

        // Stock list from the trade's starter items, priced at ₹500 and costed at ₹350.
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var products = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var (a, b) = (products[0], products[1]);
        foreach (var p in new[] { a, b })
        {
            (await shop.PutAsJsonAsync($"/api/v1/products/{p.Id}", new { name = p.Name, unit = p.Unit, sellingPrice = 500m, costPrice = 350m, gstRate = 5m, hsnCode = p.HsnCode, replaceTax = true }, ApiFactory.Json))
                .EnsureSuccessStatusCode();
        }

        // Buying: 20 of each on a supplier bill, ₹100 paid now.
        var supplier = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Wholesaler" });
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 100m, paymentMode = "cash",
            lines = new[] { new { productId = a.Id, quantity = 20m, unitCost = 350m }, new { productId = b.Id, quantity = 20m, unitCost = 350m } },
        })).EnsureSuccessStatusCode();
        Assert.Equal(20m, (await Item(shop, a.Id)).Stock);
        Assert.Equal(13_900m, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Supplier.Due);

        // Selling: 2 for cash at the counter, 3 on the khata.
        var customer = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var cash = await Bill(shop, null, a.Id, 2m, 1000m);
        var khata = await Bill(shop, customer, b.Id, 3m, 0m);
        Assert.All(cash.Lines.Concat(khata.Lines), l => Assert.NotNull(l.TaxableValue));

        // Money in, money out.
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 100m, paymentMode = "upi" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount = 250m, category = "rent", mode = "cash", note = "Rent" })).EnsureSuccessStatusCode();
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal(1400m, statement.ClosingBalance);

        // A bill made by mistake is cancelled: its stock comes back and it stops counting.
        var mistake = await Bill(shop, null, a.Id, 1m, 500m);
        (await shop.PostJsonAsync($"/api/v1/bills/{mistake.Id}/cancel", new { reason = "Made by mistake" })).EnsureSuccessStatusCode();
        Assert.Equal(18m, (await Item(shop, a.Id)).Stock);

        // One item comes back on a credit note and goes on the shelf.
        var note = await (await shop.PostJsonAsync($"/api/v1/bills/{khata.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Damaged", lines = new[] { new { lineNo = 1, quantity = 1m } } }))
            .ReadAsync<CreditNoteDto>();
        Assert.Equal(500m, note.Total);
        Assert.Equal(900m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);
        Assert.Equal(18m, (await Item(shop, b.Id)).Stock);

        // Reports: sales leave out the cancelled bill; profit is the margin without GST, less the return and rent.
        var today = await shop.GetJsonAsync<GroceryKhata.Api.Ledger.TodayDto>("/api/v1/dashboard/today");
        Assert.Equal(2500m, today.Sales);
        var month = await shop.GetJsonAsync<MonthReport>("/api/v1/reports/month");
        var margin = decimal.Round(500m / 1.05m, 2) - 350m;
        Assert.Equal(250m, month.Expenses);
        Assert.InRange(month.EstimatedProfit, (margin * 4) - 1m, (margin * 4) + 1m); // 5 sold, 1 back on the shelf
        var pnl = await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl");
        Assert.Equal((36m * 350m, 13_900m), (pnl.ClosingStock, pnl.SupplierDue));
        var gst = await shop.GetJsonAsync<GstMonthDto>("/api/v1/gst/month");
        Assert.Equal((2, 1, 1), (gst.Bills, gst.Cancelled, gst.CreditNotes));
        var zip = await shop.GetAsync($"/api/v1/gst/export.zip?month={gst.Month}");
        zip.EnsureSuccessStatusCode();
        using var archive = new ZipArchive(await zip.Content.ReadAsStreamAsync());
        using var vouchers = new StreamReader(archive.GetEntry("2-vouchers.xml")!.Open());
        var xml = await vouchers.ReadToEndAsync();
        Assert.Equal(2, xml.Split("VCHTYPE=\"Sales\"").Length - 1);
        Assert.Contains("VCHTYPE=\"Credit Note\"", xml);
    }

    [Fact]
    public async Task A_shop_in_several_trades_is_found_by_each_and_loads_each_list()
    {
        var shop = await SignUpAsync("sweets");
        var settings = await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        var saved = await (await shop.PutAsJsonAsync("/api/v1/shop", settings with { ExtraTypes = [ShopType.Toys] }, ApiFactory.Json)).ReadAsync<ShopSettingsDto>();
        Assert.Equal([ShopType.Toys], saved.ExtraTypes);

        (await shop.PostJsonAsync("/api/v1/products/starter?type=toys", new { })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var names = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Select(p => p.Name).ToList();
        Assert.Contains("Soft toy", names);
        Assert.Contains("Kaju katli", names);

        var found = await shop.GetJsonAsync<List<GroceryKhata.Api.Planner.ShopDirectoryDto>>($"/api/v1/shops?type=toys&search={Uri.EscapeDataString(settings.Name)}");
        Assert.Contains(found, s => s.Name == settings.Name);
    }

    private static async Task<BillDto> Bill(HttpClient shop, Guid? customer, Guid product, decimal quantity, decimal paid) =>
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, paidAmount = paid, paymentMode = "cash",
            lines = new[] { new { productId = product, quantity, unitPrice = 500m } },
        })).ReadAsync<BillDto>();

    private static Task<ProductDto> Item(HttpClient shop, Guid id) => shop.GetJsonAsync<ProductDto>($"/api/v1/products/{id}");

    private async Task<HttpClient> SignUpAsync(string type)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = $"Day {type} {Guid.NewGuid():N}"[..30], fullName = "Suresh Verma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
