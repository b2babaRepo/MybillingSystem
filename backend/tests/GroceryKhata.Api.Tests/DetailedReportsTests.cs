using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>The detailed reports a CA and an owner check: HSN, tax to pay, the sales register, discounts and tills.</summary>
[Collection(ApiCollection.Name)]
public sealed class DetailedReportsTests(ApiFactory api)
{
    private async Task<(HttpClient Owner, HttpClient Helper)> ShopAsync(bool composition = false)
    {
        var owner = api.CreateClient();
        var auth = await (await owner.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Arora Stores", fullName = "Vikas Arora", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var settings = await owner.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        (await owner.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0", GstComposition = composition, StaffMaxDiscountPercent = 50m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var helperAuth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helperAuth.AccessToken);
        return (owner, helper);
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, string unit, decimal price, decimal gst, string hsn)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit, sellingPrice = price, costPrice = price * 0.8m, gstRate = gst, hsnCode = hsn, openingStock = 100m })).EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> SellAsync(HttpClient shop, Guid item, string name, decimal quantity, decimal price, string mode = "cash", string? counter = null) =>
        shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), counter, lines = new[] { new { productId = item, name, quantity, unitPrice = price } }, paidAmount = quantity * price, paymentMode = mode });

    [Fact]
    public async Task The_hsn_table_goes_by_unit_code_nets_credit_notes_and_the_tax_to_pay_uses_input_credit()
    {
        var (shop, _) = await ShopAsync();
        var sugar = await ItemAsync(shop, "Sugar", "kg", 105m, 5m, "1701");
        var soap = await ItemAsync(shop, "Soap", "piece", 118m, 18m, "3401");
        var bill = await (await SellAsync(shop, sugar, "Sugar", 2m, 105m)).ReadAsync<BillDto>();
        (await SellAsync(shop, soap, "Soap", 1m, 118m)).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Wet", lines = new[] { new { lineNo = 1, quantity = 1m } } })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders", gstin = "08AAPFU0939F1ZV" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = soap, quantity = 1m, unitCost = 50m, gstRate = 18m } } }))
            .EnsureSuccessStatusCode();

        var gst = await shop.GetJsonAsync<GstMonthDto>("/api/v1/gst/month");
        var kgs = gst.Hsn!.Single(h => h.Hsn == "1701");
        Assert.Equal(("B2C", "KGS", 1m, 100m, 2.5m), (kgs.Section, kgs.Uqc, kgs.Quantity, kgs.Taxable, kgs.Cgst)); // 2 sold, 1 back
        Assert.Equal("PCS", gst.Hsn!.Single(h => h.Hsn == "3401").Uqc);

        // Output: sugar 2.50 + 2.50 after the note, soap 9 + 9; input 4.50 + 4.50 from the registered supplier.
        var p = gst.Payable!;
        Assert.Equal((11.5m, 11.5m, 4.5m, 7m, 7m), (p.OutputCgst, p.OutputSgst, p.InputCgst, p.PayCgst, p.PaySgst));
    }

    [Fact]
    public async Task Gta_freight_under_reverse_charge_is_paid_in_cash_and_claimed_back()
    {
        var (shop, _) = await ShopAsync();
        var soap = await ItemAsync(shop, "Soap", "piece", 118m, 18m, "3401");
        (await SellAsync(shop, soap, "Soap", 1m, 118m)).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders", gstin = "08AAPFU0939F1ZV" })).EnsureSuccessStatusCode();
        var bad = await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m,
            lines = new[] { new { productId = soap, quantity = 1m, unitCost = 50m, gstRate = 18m } }, charges = new[] { new { label = "Lorry freight", amount = 1_000m, reverseChargeRate = 7m } } });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m,
            lines = new[] { new { productId = soap, quantity = 1m, unitCost = 50m, gstRate = 18m } }, charges = new[] { new { label = "Lorry freight (GTA)", amount = 1_000m, reverseChargeRate = 5m } } }))
            .EnsureSuccessStatusCode();

        // ₹1,000 of GTA freight at 5%: ₹25 CGST + ₹25 SGST paid in cash on top of the ₹9 + ₹9 on the sale less the
        // ₹4.50 + ₹4.50 supplier credit, and the ₹25 + ₹25 also claimed back as input credit (carried forward).
        var p = (await shop.GetJsonAsync<GstMonthDto>("/api/v1/gst/month")).Payable!;
        Assert.Equal((25m, 25m), (p.RcmCgst, p.RcmSgst));
        Assert.Equal((29.5m, 29.5m), (p.InputCgst, p.InputSgst));
        Assert.Equal((25m, 25m), (p.PayCgst, p.PaySgst));
        Assert.Equal((20.5m, 20.5m), (p.CarryCgst, p.CarrySgst));
    }

    [Fact]
    public async Task A_composition_shop_sees_one_percent_of_its_turnover_to_pay()
    {
        var (shop, _) = await ShopAsync(composition: true);
        var dal = await ItemAsync(shop, "Dal", "kg", 100m, 5m, "0713");
        (await SellAsync(shop, dal, "Dal", 10m, 100m)).EnsureSuccessStatusCode();
        var p = (await shop.GetJsonAsync<GstMonthDto>("/api/v1/gst/month")).Payable!;
        Assert.Equal((1000m, 10m, 5m, 5m), (p.CompositionTurnover, p.CompositionTax, p.PayCgst, p.PaySgst));
    }

    [Fact]
    public async Task The_sales_register_lists_every_bill_with_its_money_and_a_total_of_what_stands()
    {
        var (shop, _) = await ShopAsync();
        var sugar = await ItemAsync(shop, "Sugar", "kg", 105m, 5m, "1701");
        (await SellAsync(shop, sugar, "Sugar", 1m, 105m, "upi", "Counter 1")).EnsureSuccessStatusCode();
        var gone = await (await SellAsync(shop, sugar, "Sugar", 2m, 105m)).ReadAsync<BillDto>();
        (await shop.PostJsonAsync($"/api/v1/bills/{gone.Id}/cancel", new { reason = "Typed twice" })).EnsureSuccessStatusCode();

        var month = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM");
        var csv = (await (await shop.GetAsync($"/api/v1/gst/sales-register.csv?month={month}")).Content.ReadAsStringAsync()).TrimStart('﻿');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Date,Time,Bill no,Party,Buyer GSTIN,Type,Status,Taxable,CGST,SGST,IGST,Bill total,Cash,UPI", lines[0]);
        Assert.Contains(lines, l => l.Contains(",B2C,Active,100.00,2.50,2.50,0.00,105.00,0.00,105.00,0.00,0.00,Counter 1,"));
        Assert.Contains(lines, l => l.Contains(",Cancelled,") && l.Contains("Typed twice"));
        Assert.StartsWith("Total (bills that stand),,,,,,,100.00,2.50,2.50,0.00,105.00,0.00,105.00", lines.Single(l => l.StartsWith("Total")));
    }

    [Fact]
    public async Task Discounts_show_each_cashiers_price_cuts_and_the_day_splits_by_counter()
    {
        var (owner, helper) = await ShopAsync();
        var oil = await ItemAsync(owner, "Oil", "litre", 200m, 5m, "1508");
        (await SellAsync(helper, oil, "Oil", 2m, 180m, counter: "Phone 2")).EnsureSuccessStatusCode(); // 40 off by hand
        (await SellAsync(owner, oil, "Oil", 1m, 200m, counter: "Counter 1")).EnsureSuccessStatusCode();

        var report = await owner.GetJsonAsync<DiscountReport>("/api/v1/reports/discounts");
        var raju = report.Cashiers.Single(c => c.Name == "Raju");
        Assert.Equal((40m, 1, 360m), (raju.ByHand, raju.ByHandLines, raju.Sales));
        Assert.Equal((200m, 180m, 40m), (report.Lines.Single().ListPrice, report.Lines.Single().SoldAt, report.Lines.Single().Discount));

        var day = await owner.GetJsonAsync<DayReportDto>("/api/v1/reports/day");
        Assert.Equal((360m, "Raju"), (day.Tills!.Single(t => t.Counter == "Phone 2").Sales, day.Tills!.Single(t => t.Counter == "Phone 2").By));
        Assert.Equal(200m, day.Tills!.Single(t => t.Counter == "Counter 1").Cash);
    }
}
