using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>The month's numbers and the Tally file agree with where the money and tax really went.</summary>
[Collection(ApiCollection.Name)]
public sealed class TallyAccuracyTests(ApiFactory api)
{
    private async Task<HttpClient> RegisteredShopAsync(bool composition = false)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Mehta Kirana", fullName = "Raj Mehta", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var settings = await client.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        (await client.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0", GstComposition = composition }, ApiFactory.Json)).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal price, decimal cost, decimal? gst = 5m)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = price, costPrice = cost, gstRate = gst, openingStock = 100m })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<string> VouchersAsync(HttpClient shop)
    {
        var month = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM");
        var zip = await shop.GetAsync($"/api/v1/gst/export.zip?month={month}");
        zip.EnsureSuccessStatusCode();
        using var archive = new ZipArchive(await zip.Content.ReadAsStreamAsync());
        return await new StreamReader(archive.GetEntry("2-vouchers.xml")!.Open()).ReadToEndAsync();
    }

    private static string Voucher(string xml, string guid) =>
        xml.Split("<TALLYMESSAGE>").Single(v => v.Contains($"<GUID>gk-{guid}</GUID>"));

    [Fact]
    public async Task A_walk_in_sale_paid_by_upi_goes_to_the_upi_account_and_a_split_one_to_both()
    {
        var shop = await RegisteredShopAsync();
        var rice = await ItemAsync(shop, "Rice", 105m, 80m);
        var upi = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bills", new { id = upi, lines = new[] { new { productId = rice, name = "Rice", quantity = 2m, unitPrice = 105m } }, paidAmount = 210m, paymentMode = "upi" })).EnsureSuccessStatusCode();
        var split = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = split, lines = new[] { new { productId = rice, name = "Rice", quantity = 1m, unitPrice = 105m } }, paidAmount = 105m, paymentMode = "cash",
            payments = new object[] { new { mode = "cash", amount = 5m }, new { mode = "upi", amount = 100m } },
        })).EnsureSuccessStatusCode();

        var xml = await VouchersAsync(shop);
        var one = Voucher(xml, upi.ToString());
        Assert.Contains("<LEDGERNAME>UPI Collections</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-210.00</AMOUNT>", one);
        Assert.DoesNotContain("<LEDGERNAME>Cash</LEDGERNAME>", one);
        var two = Voucher(xml, split.ToString());
        Assert.Contains("<LEDGERNAME>UPI Collections</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-100.00</AMOUNT>", two);
        Assert.Contains("<LEDGERNAME>Cash</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-5.00</AMOUNT>", two);
    }

    [Fact]
    public async Task Input_tax_is_taken_only_from_a_registered_supplier_and_credit_notes_and_unpaid_freight_reach_tally()
    {
        var shop = await RegisteredShopAsync();
        var pipe = await ItemAsync(shop, "Pipe", 300m, 200m, 18m);
        var registered = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = registered, name = "Jain Traders", gstin = "08AAPFU0939F1ZV" })).EnsureSuccessStatusCode();
        var unregistered = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = unregistered, name = "Local Mistri" })).EnsureSuccessStatusCode();

        var withTax = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = withTax, supplierId = registered, paidAmount = 0m, lines = new[] { new { productId = pipe, quantity = 10m, unitCost = 100m, gstRate = 18m } },
            charges = new[] { new { label = "Lorry freight", amount = 50m } },
        })).EnsureSuccessStatusCode();
        var noTax = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = noTax, supplierId = unregistered, paidAmount = 0m, lines = new[] { new { productId = pipe, quantity = 10m, unitCost = 100m, gstRate = 18m } } }))
            .EnsureSuccessStatusCode();
        var back = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/returns", new { id = back, supplierId = registered, lines = new[] { new { productId = pipe, quantity = 1m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/suppliers/returns/{back}/credit-note", new { creditNoteNo = "CN-7", amount = 118m })).EnsureSuccessStatusCode();

        var gst = await shop.GetJsonAsync<GstMonthDto>("/api/v1/gst/month");
        Assert.Equal(180m, gst.InputGst); // only the registered supplier's

        var xml = await VouchersAsync(shop);
        Assert.Contains("Input CGST", Voucher(xml, withTax.ToString()));
        var plain = Voucher(xml, noTax.ToString());
        Assert.DoesNotContain("Input CGST", plain);
        Assert.Contains("<LEDGERNAME>Purchase</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-1180.00</AMOUNT>", plain);
        Assert.Contains("VCHTYPE=\"Debit Note\"", xml);
        Assert.Contains("<LEDGERNAME>Freight Payable</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>50.00</AMOUNT>", xml);
    }

    [Fact]
    public async Task A_composition_shop_keeps_its_whole_sale_as_its_own_and_bag_sales_count_as_other_income()
    {
        var shop = await RegisteredShopAsync(composition: true);
        var dal = await ItemAsync(shop, "Dal", 120m, 100m);
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = dal, name = "Dal", quantity = 1m, unitPrice = 120m } }, paidAmount = 120m, paymentMode = "cash" }))
            .EnsureSuccessStatusCode();
        var jute = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bardana/types", new { id = jute, name = "Jute 50 kg" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/bardana/moves", new { id = Guid.CreateVersion7(), bagTypeId = jute, kind = "opening", quantity = 10m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/bardana/moves", new { id = Guid.CreateVersion7(), bagTypeId = jute, kind = "sold", quantity = 10m, rate = 12m, mode = "cash" })).EnsureSuccessStatusCode();

        var month = await shop.GetJsonAsync<MonthReport>("/api/v1/reports/month");
        // A bill of supply has no GST in it: the profit is 120 - 100, not 114.29 - 100.
        Assert.Equal((20m, 120m, 120m), (month.EstimatedProfit, month.BagsSold, month.Cash - 120m));
        Assert.Equal((0m, 140m), (month.Statement!.GstInSales, month.Statement.NetProfit));
        Assert.Equal(0m, (await shop.GetJsonAsync<ItemMarginReport>("/api/v1/reports/items")).Items.Single().Sales - 120m);
    }
}
