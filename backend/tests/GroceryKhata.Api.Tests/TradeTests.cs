using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Contractors (challans, sites, credit limits), GST kept on bill lines, and painters' commission.</summary>
[Collection(ApiCollection.Name)]
public sealed class TradeTests(ApiFactory api)
{
    [Fact]
    public async Task Challans_take_stock_now_and_go_on_the_khata_only_when_invoiced_once()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var contractor = await KhataTests.CreateCustomerAsync(shop, "Gupta Contractor");

        var first = await Challan(shop, contractor, cement.Id, 50m, "Mansarovar plot 12");
        var second = await Challan(shop, contractor, cement.Id, 30m, "Mansarovar plot 12");

        // Goods sent to another state: the challan keeps the state for the invoice.
        var away = await (await shop.PostJsonAsync("/api/v1/challans", new { id = Guid.CreateVersion7(), customerId = contractor, lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 385m } }, placeOfSupply = "24" })).ReadAsync<ChallanDto>();
        Assert.Equal("24", away.PlaceOfSupply);
        Assert.Equal("RJ14GB1234", first.VehicleNumber);

        // The bags left the shelf, but nothing is owed yet and nothing is a sale yet.
        Assert.Equal(-81m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Stock);
        Assert.Equal(0m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{contractor}/statement")).ClosingBalance);
        Assert.Equal(3, (await shop.GetJsonAsync<List<ChallanDto>>($"/api/v1/challans?customerId={contractor}")).Count);

        var invoiceId = Guid.CreateVersion7();
        var invoice = await (await shop.PostJsonAsync("/api/v1/challans/invoice",
            new { id = invoiceId, challanIds = new[] { first.Id, second.Id }, paidAmount = 0m })).ReadAsync<BillDto>();
        Assert.Equal((80m * 385m, "Mansarovar plot 12", "RJ14GB1234"), (invoice.Total, invoice.Site, invoice.VehicleNumber));
        Assert.Equal(18m, invoice.Lines[0].GstRate); // the item's GST kept on the line
        Assert.Equal(80m * 385m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{contractor}/statement")).ClosingBalance);
        Assert.Equal(-81m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Stock); // not taken twice
        Assert.Single(await shop.GetJsonAsync<List<ChallanDto>>($"/api/v1/challans?customerId={contractor}"));

        // A challan can't be billed again on another invoice.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/challans/invoice",
            new { id = Guid.CreateVersion7(), challanIds = new[] { first.Id }, paidAmount = 0m })).StatusCode);

        var sites = await shop.GetJsonAsync<List<SiteTotal>>($"/api/v1/customers/{contractor}/sites");
        Assert.Equal(("Mansarovar plot 12", 1, 80m * 385m), (sites[0].Site, sites[0].Bills, sites[0].Billed));
    }

    [Fact]
    public async Task The_year_report_takes_out_gst_only_on_gst_bills_and_skips_cancelled_ones()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        async Task<BillDto> Bill() => await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), paidAmount = 1180m, paymentMode = "upi",
            lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 1180m } },
        })).ReadAsync<BillDto>();

        await Bill(); // no GSTIN yet: no GST in it
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0" });
        await Bill();
        var cancelled = await Bill();
        await shop.PostJsonAsync($"/api/v1/bills/{cancelled.Id}/cancel", new { reason = "Wrong bill" });

        var year = await shop.GetJsonAsync<GroceryKhata.Api.Ledger.YearReport>("/api/v1/reports/year");
        Assert.Equal((2, 2360m, 180m, 2180m, 2360m), (year.Total.Bills, year.Total.Sales, year.Total.Gst, year.Total.Turnover, year.Total.Digital));
    }

    [Fact]
    public async Task Past_profit_keeps_the_cost_at_sale_and_takes_out_gst()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0" });
        async Task SetCost(decimal cost) => await shop.PutAsJsonAsync($"/api/v1/products/{cement.Id}", new { name = cement.Name, unit = "bag", sellingPrice = 1180m, costPrice = cost });
        await SetCost(800m);
        await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), paidAmount = 1180m, lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 1180m } } });
        await SetCost(5000m);

        var month = await shop.GetJsonAsync<GroceryKhata.Api.Ledger.MonthReport>("/api/v1/reports/month");
        Assert.Equal(200m, month.EstimatedProfit); // 1000 without GST, less 800
    }

    [Fact]
    public async Task A_supplier_bill_adds_stock_averages_cost_and_tracks_what_is_owed()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var supplier = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jaipur Cement" });
        async Task Buy(decimal qty, decimal cost, decimal paid) => (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = paid, paymentMode = "cash",
            lines = new[] { new { productId = cement.Id, quantity = qty, unitCost = cost } },
        })).EnsureSuccessStatusCode();

        await Buy(10m, 300m, 1000m);
        await Buy(10m, 400m, 0m);
        await shop.PostJsonAsync($"/api/v1/suppliers/{supplier}/pay", new { id = Guid.CreateVersion7(), amount = 500m, paymentMode = "upi" });

        var item = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}");
        var detail = await shop.GetJsonAsync<GroceryKhata.Api.Ledger.SupplierDetail>($"/api/v1/suppliers/{supplier}");
        Assert.Equal((20m, 350m, 5500m), (item.Stock, item.CostPrice, detail.Supplier.Due));

        // The second bill was a mistake: its 10 bags go out again and its ₹4,000 comes off.
        var second = detail.Entries.Last(e => e.Amount == 4000m).PurchaseId!.Value;
        await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{second}/cancel", new { });
        await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{second}/cancel", new { });
        Assert.Equal(10m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Stock);
        Assert.Equal(1500m, (await shop.GetJsonAsync<GroceryKhata.Api.Ledger.SupplierDetail>($"/api/v1/suppliers/{supplier}")).Supplier.Due);
    }

    [Fact]
    public async Task Profit_and_loss_rebuilds_closing_stock_at_purchase_cost()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var supplier = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jaipur Cement" });
        await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = cement.Id, quantity = 10m, unitCost = 300m } } });
        await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), paidAmount = 400m, lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 400m } } });

        var pnl = await shop.GetJsonAsync<GroceryKhata.Api.Ledger.ProfitLossReport>("/api/v1/reports/pnl");
        Assert.Equal((2700m, 3000m, 100m, 3000m), (pnl.ClosingStock, pnl.SupplierDue, pnl.NetProfit, pnl.Purchases));
    }

    [Fact]
    public async Task Households_find_shops_nearby_and_by_kind()
    {
        var shop = await SignUpAsync("building_materials");
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        var near = await shop.GetJsonAsync<List<GroceryKhata.Api.Planner.ShopDirectoryDto>>("/api/v1/shops?type=building_materials&lat=26.9&lng=75.8");
        Assert.DoesNotContain(near, s => s.Name == settings.Name); // no location saved: not "near" anything
        var byKind = await shop.GetJsonAsync<List<GroceryKhata.Api.Planner.ShopDirectoryDto>>($"/api/v1/shops?type=building_materials&search={Uri.EscapeDataString(settings.Name)}");
        Assert.Contains(byKind, s => s.Name == settings.Name);
        var otherKind = await shop.GetJsonAsync<List<GroceryKhata.Api.Planner.ShopDirectoryDto>>($"/api/v1/shops?type=paint&search={Uri.EscapeDataString(settings.Name)}");
        Assert.All(otherKind, s => Assert.Equal(GroceryKhata.Api.Shops.ShopType.Paint, s.Type)); // other tests' shops share names
    }

    [Fact]
    public async Task Bills_keep_their_tax_igst_for_another_state_and_none_on_composition()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0" });
        var buyer = await KhataTests.CreateCustomerAsync(shop, "Delhi Builders");
        async Task<BillDto> Bill(Guid? customer, string? deliveredTo = null) => await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, paidAmount = 1180m, placeOfSupply = deliveredTo,
            lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 1180m } },
        })).ReadAsync<BillDto>();

        var local = await Bill(null);
        Assert.Equal((1000m, 90m, 90m, 0m), (local.Lines[0].TaxableValue, local.Lines[0].Cgst, local.Lines[0].Sgst, local.Lines[0].Igst));
        Assert.Equal("08", local.PlaceOfSupply);

        // Delivered to another state (no GSTIN): the place of supply is where it went, so IGST.
        var delivered = await Bill(null, "27");
        Assert.Equal(("27", 180m, 0m), (delivered.PlaceOfSupply, delivered.Lines[0].Igst, delivered.Lines[0].Cgst));
        Assert.Equal("08", (await Bill(null, "99")).PlaceOfSupply); // not a state code: the shop's own state
        (await shop.PutAsJsonAsync($"/api/v1/customers/{buyer}/gstin", new { gstin = "07AAACD1234E1ZB" })).EnsureSuccessStatusCode();
        var other = await Bill(buyer);
        Assert.Equal(("07AAACD1234E1ZB", 180m, 0m), (other.BuyerGstin, other.Lines[0].Igst, other.Lines[0].Cgst));
        Assert.Equal("07", other.PlaceOfSupply);
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0", GstComposition = true });
        var supply = await Bill(null);
        Assert.Equal((true, (decimal?)null), (supply.BillOfSupply, supply.Lines[0].TaxableValue));
    }

    [Fact]
    public async Task A_bill_carries_the_shop_and_buyer_addresses_for_printing()
    {
        var shop = await SignUpAsync("building_materials");
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        (await shop.PutAsJsonAsync("/api/v1/shop", settings with { Address = "12 Station Road" })).EnsureSuccessStatusCode();
        var buyer = await KhataTests.CreateCustomerAsync(shop, "Mill Lane Traders");
        (await shop.PutAsJsonAsync($"/api/v1/customers/{buyer}/address", new { address = " 4 Mill Lane, Jaipur " })).EnsureSuccessStatusCode();
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bills", new { id, customerId = buyer, paidAmount = 100m, lines = new[] { new { name = "Paint", quantity = 1m, unitPrice = 100m } } })).EnsureSuccessStatusCode();

        var view = await shop.GetJsonAsync<BillView>($"/api/v1/bills/{id}");

        Assert.Contains("12 Station Road", view.Bill.ShopAddress);
        Assert.Equal("4 Mill Lane, Jaipur", view.Bill.BuyerAddress);
    }

    [Fact]
    public void Gstin_check_digits_are_checked()
    {
        Assert.True(GroceryKhata.Api.Ledger.GstExport.IsValidGstin("27AAPFU0939F1ZV"));
        Assert.False(GroceryKhata.Api.Ledger.GstExport.IsValidGstin("27AAPFU0939F1ZX"));
        Assert.False(GroceryKhata.Api.Ledger.GstExport.IsValidGstin("99AAPFU0939F1ZV"));
    }

    [Fact]
    public async Task The_gst_month_balances_and_its_tally_files_download()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0" });
        await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), paidAmount = 1180m, lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 1180m } } });

        var month = await shop.GetJsonAsync<GroceryKhata.Api.Ledger.GstMonthDto>("/api/v1/gst/month");
        Assert.Equal((1180m, 1000m, 90m, 90m, true), (month.Sales, month.Taxable, month.Cgst, month.Sgst, month.CanDownload));
        var zip = await shop.GetAsync($"/api/v1/gst/export.zip?month={month.Month}");
        zip.EnsureSuccessStatusCode();
        using var archive = new System.IO.Compression.ZipArchive(await zip.Content.ReadAsStreamAsync());
        Assert.Equal(["1-masters.xml", "2-vouchers.xml", "READ-ME.txt", $"summary-{month.Month}.csv"], archive.Entries.Select(e => e.Name).Order());
    }

    [Fact]
    public async Task A_credit_note_takes_back_part_of_a_bill_with_its_tax()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0" });
        var customer = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer, paidAmount = 0m, lines = new[] { new { productId = cement.Id, quantity = 4m, unitPrice = 1180m } } })).ReadAsync<BillDto>();

        var note = await (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Two bags torn", lines = new[] { new { lineNo = 1, quantity = 2m } } }))
            .ReadAsync<GroceryKhata.Api.Ledger.CreditNoteDto>();
        Assert.Equal((2360m, 2000m, 180m, 180m), (note.Total, note.Lines[0].TaxableValue, note.Lines[0].Cgst, note.Lines[0].Sgst));
        Assert.StartsWith("CN/", note.NoteNo);
        Assert.Equal(2360m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);
        Assert.Equal(-2m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Stock); // 4 sold, 2 back

        var tooMany = await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "More", lines = new[] { new { lineNo = 1, quantity = 3m } } });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        var month = await shop.GetJsonAsync<GroceryKhata.Api.Ledger.GstMonthDto>("/api/v1/gst/month");
        Assert.Equal((1, 2360m), (month.CreditNotes, month.CreditNoteValue));
    }

    [Fact]
    public void Razorpay_signatures_are_checked()
    {
        var good = Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData("secret"u8, "order|pay"u8));
        Assert.True(GroceryKhata.Api.Billing.RazorpayCheckout.SignatureMatches("order|pay", good, "secret"));
        Assert.False(GroceryKhata.Api.Billing.RazorpayCheckout.SignatureMatches("order|other", good, "secret"));
        Assert.False(GroceryKhata.Api.Billing.RazorpayCheckout.SignatureMatches("order|pay", null, "secret"));
    }

    [Fact]
    public async Task Cancelling_a_bill_undoes_it_but_keeps_its_number()
    {
        var shop = await SignUpAsync("building_materials");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var cement = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Cement OPC 53 (50 kg)");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, paidAmount = 100m,
            lines = new[] { new { productId = cement.Id, quantity = 2m, unitPrice = 400m } },
        })).ReadAsync<BillDto>();

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/cancel", new { reason = "" })).StatusCode);
        var cancelled = await (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/cancel", new { reason = "Wrong customer" })).ReadAsync<BillDto>();
        var again = await (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/cancel", new { reason = "Twice" })).ReadAsync<BillDto>();

        Assert.Equal(("Wrong customer", bill.BillNo), (cancelled.CancelReason, cancelled.BillNo));
        Assert.Equal("Wrong customer", again.CancelReason);
        Assert.Equal(0m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Stock);
    }

    [Fact]
    public async Task A_credit_limit_is_kept_on_the_customer()
    {
        var shop = await SignUpAsync("building_materials");
        var contractor = await KhataTests.CreateCustomerAsync(shop, "Gupta Contractor");
        var set = await shop.PutAsJsonAsync($"/api/v1/customers/{contractor}/credit-limit", new { creditLimit = 50000m }, ApiFactory.Json);
        Assert.Equal(50000m, (await set.ReadAsync<CustomerDto>()).CreditLimit);
        Assert.Equal(50000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{contractor}/statement")).Customer.CreditLimit);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await shop.PutAsJsonAsync($"/api/v1/customers/{contractor}/credit-limit", new { creditLimit = -5m }, ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task Bills_tagged_to_a_painter_add_up_to_their_commission()
    {
        var shop = await SignUpAsync("paint");
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var tin = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Interior emulsion 4 L");
        var painter = Guid.CreateVersion7();
        Assert.Equal(HttpStatusCode.Created, (await shop.PostJsonAsync("/api/v1/painters", new { id = painter, name = "Raju Painter", commissionPercent = 5m })).StatusCode);

        // A tinted tin: the base comes off stock, the line carries the shade and the tinting charge.
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            lines = new[] { new { productId = tin.Id, name = "Interior emulsion 4 L · shade 7B23", quantity = 2m, unitPrice = 1250m } },
            paidAmount = 2500m,
            painterId = painter,
        })).ReadAsync<BillDto>();
        Assert.Equal(painter, bill.PainterId);
        Assert.Equal(-2m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{tin.Id}")).Stock);

        var commission = Assert.Single(await shop.GetJsonAsync<List<PainterCommission>>("/api/v1/painters/commission"));
        Assert.Equal((1, 2500m, 125m), (commission.Bills, commission.Sales, commission.Commission));
    }

    [Fact]
    public async Task Commission_paid_is_taken_off_what_is_owed_and_cannot_go_over()
    {
        var shop = await SignUpAsync("paint");
        var painter = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/painters", new { id = painter, name = "Raju Painter", commissionPercent = 10m });
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Primer", quantity = 1m, unitPrice = 3000m } }, paidAmount = 3000m, painterId = painter })).EnsureSuccessStatusCode();

        var payout = Guid.CreateVersion7();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/painters/{painter}/payouts", new { id = Guid.CreateVersion7(), amount = 301m })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/painters/{painter}/payouts", new { id = payout, amount = 200m, mode = "upi" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/painters/{painter}/payouts", new { id = payout, amount = 200m, mode = "upi" })).EnsureSuccessStatusCode(); // sent twice: once
        var row = Assert.Single(await shop.GetJsonAsync<List<PainterCommission>>("/api/v1/painters/commission"));
        Assert.Equal((300m, 200m, 100m), (row.EarnedAllTime, row.PaidAllTime, row.Due));

        // A cash payout leaves the drawer: ₹3,000 in, ₹100 commission paid in cash out (the ₹200 went by UPI).
        (await shop.PostJsonAsync($"/api/v1/painters/{painter}/payouts", new { id = Guid.CreateVersion7(), amount = 100m, mode = "cash" })).EnsureSuccessStatusCode();
        var drawer = await shop.GetJsonAsync<CashCountDto>("/api/v1/cash-count");
        Assert.Equal(2_900m, drawer.CashIn - drawer.CashOut);
    }

    [Fact]
    public async Task A_customers_earlier_shades_come_back_newest_first_one_each()
    {
        var shop = await SignUpAsync("paint");
        var customer = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Meena" });
        await shop.PostJsonAsync("/api/v1/products/starter", new { });
        var tin = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Interior emulsion 4 L");
        foreach (var shade in new[] { "7B23", "9A01", "7B23" })
        {
            (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { productId = tin.Id, name = $"Interior emulsion 4 L · shade {shade}", quantity = 1m, unitPrice = 1250m } }, paidAmount = 1250m })).EnsureSuccessStatusCode();
        }

        var shades = await shop.GetJsonAsync<List<ShadeHistoryDto>>($"/api/v1/customers/{customer}/shades");
        Assert.Equal(["7B23", "9A01"], shades.Select(x => x.Shade));
        Assert.Equal("Interior emulsion 4 L", shades[0].Name);
    }

    private async Task<HttpClient> HelperOfAsync(HttpClient owner)
    {
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return helper;
    }

    [Fact]
    public async Task A_contractor_pays_the_group_price_and_a_helper_may_bill_it()
    {
        var shop = await SignUpAsync("hardware");
        var settings = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        (await shop.PutAsJsonAsync("/api/v1/shop", settings with { ContractorDiscountPercent = 8m, DealerDiscountPercent = 15m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var pipe = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = pipe, name = "PVC pipe 1 inch", unit = "piece", sellingPrice = 250m, openingStock = 100m })).EnsureSuccessStatusCode();
        var contractor = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/customers", new { id = contractor, name = "Sharma Contractor" });
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/customers/{contractor}/price-group", new { priceGroup = "vip" }, ApiFactory.Json)).StatusCode);
        (await shop.PutAsJsonAsync($"/api/v1/customers/{contractor}/price-group", new { priceGroup = "contractor" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        Assert.Equal("contractor", (await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers")).Single(c => c.Id == contractor).PriceGroup);

        // A helper's own limit is 10% off, but here 8% is the contractor price (₹230): allowed. Below it is the helper's discount: 15% is refused.
        var helper = await HelperOfAsync(shop);
        object Bill(decimal price) => new { id = Guid.CreateVersion7(), customerId = contractor, lines = new[] { new { productId = pipe, quantity = 4m, unitPrice = price } }, paidAmount = 0m };
        (await helper.PostJsonAsync("/api/v1/bills", Bill(230m))).EnsureSuccessStatusCode();
        var settingsAfter = await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop");
        (await shop.PutAsJsonAsync("/api/v1/shop", settingsAfter with { StaffMaxDiscountPercent = 2m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await helper.PostJsonAsync("/api/v1/bills", Bill(230m))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/bills", Bill(212.5m))).StatusCode);
    }

    [Fact]
    public async Task An_estimate_goes_out_on_challans_in_parts_and_never_more_than_was_quoted()
    {
        var shop = await SignUpAsync("hardware");
        var cement = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = cement, name = "Cement bag", unit = "bag", sellingPrice = 400m, openingStock = 200m })).EnsureSuccessStatusCode();
        var builder = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/customers", new { id = builder, name = "Verma Builders" });
        var estimate = await (await shop.PostJsonAsync("/api/v1/estimates", new { id = Guid.CreateVersion7(), customerId = builder, lines = new[] { new { productId = cement, quantity = 100m, unitPrice = 390m } } })).ReadAsync<EstimateDto>();
        object Challan(decimal bags) => new { id = Guid.CreateVersion7(), customerId = builder, estimateId = estimate.Id, site = "Plot 12", lines = new[] { new { productId = cement, quantity = bags, unitPrice = 390m } } };

        (await shop.PostJsonAsync("/api/v1/challans", Challan(60m))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/challans", Challan(41m))).StatusCode);
        (await shop.PostJsonAsync("/api/v1/challans", Challan(40m))).EnsureSuccessStatusCode();
        var after = await shop.GetJsonAsync<EstimateDto>($"/api/v1/estimates/{estimate.Id}");
        Assert.Equal([100m], after.Delivered!);
        Assert.Equal(100m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement}")).Stock);

        // Another customer's challan can't use this estimate.
        var other = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/customers", new { id = other, name = "Someone Else" });
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/challans",
            new { id = Guid.CreateVersion7(), customerId = other, estimateId = estimate.Id, lines = new[] { new { productId = cement, quantity = 1m, unitPrice = 390m } } })).StatusCode);
    }

    [Fact]
    public async Task A_brand_scheme_counts_the_brands_purchases_and_flags_a_short_rebate()
    {
        var shop = await SignUpAsync("paint");
        var royale = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = royale, name = "Asian Paints Royale 20 L", unit = "piece", sellingPrice = 9000m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/products", new { id = other, name = "Berger Silk 20 L", unit = "piece", sellingPrice = 8000m })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Paint Depot" });
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var scheme = await (await shop.PostJsonAsync("/api/v1/brand-schemes", new { id = Guid.CreateVersion7(), name = "Festive target", brand = "asian paints", from = today.AddDays(-1), to = today.AddDays(30), basis = "value", target = 100_000m, rebatePercent = 2m }))
            .ReadAsync<BrandSchemeDto>();
        Assert.False(scheme.Met);

        // ₹1,20,000 of Royale (before GST) counts; the Berger doesn't.
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] {
            new { productId = royale, quantity = 16m, unitCost = 7_500m }, new { productId = other, quantity = 5m, unitCost = 6_000m } } })).EnsureSuccessStatusCode();
        var now = (await shop.GetJsonAsync<List<BrandSchemeDto>>("/api/v1/brand-schemes")).Single();
        Assert.Equal((120_000m, true, 2_400m), (now.BoughtValue, now.Met, now.RebateDue));

        var paid = await (await shop.PostJsonAsync($"/api/v1/brand-schemes/{scheme.Id}/received", new { amount = 2_000m })).ReadAsync<BrandSchemeDto>();
        Assert.Equal(2_000m, paid.Received);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/brand-schemes", new { id = Guid.CreateVersion7(), name = "Bad", brand = "x", from = today, to = today, basis = "value", target = 1m, rebatePercent = 2m })).StatusCode);
    }

    [Fact]
    public async Task A_helper_cannot_bill_past_the_credit_limit_but_the_owner_can()
    {
        var shop = await SignUpAsync("building_materials");
        var contractor = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/customers", new { id = contractor, name = "Sharma Contractor" });
        await shop.PutAsJsonAsync($"/api/v1/customers/{contractor}/credit-limit", new { creditLimit = 10000m }, ApiFactory.Json);
        var helperPhone = ApiFactory.NewPhone();
        await shop.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone = helperPhone, pin = "4826" });
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone = helperPhone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        object Bill(decimal amount, decimal paid) => new { id = Guid.CreateVersion7(), customerId = contractor, lines = new[] { new { name = "Cement", quantity = 1m, unitPrice = amount } }, paidAmount = paid };

        (await helper.PostJsonAsync("/api/v1/bills", Bill(8000m, 0m))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/bills", Bill(3000m, 0m))).StatusCode);
        (await helper.PostJsonAsync("/api/v1/bills", Bill(3000m, 1000m))).EnsureSuccessStatusCode(); // paid enough now: exactly at the limit
        (await shop.PostJsonAsync("/api/v1/bills", Bill(5000m, 0m))).EnsureSuccessStatusCode();
        Assert.Equal(15000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{contractor}/statement")).Customer.Balance);
    }

    private static async Task<ChallanDto> Challan(HttpClient shop, Guid customer, Guid product, decimal bags, string site) =>
        await (await shop.PostJsonAsync("/api/v1/challans", new
        {
            id = Guid.CreateVersion7(),
            customerId = customer,
            lines = new[] { new { productId = product, quantity = bags, unitPrice = 385m } },
            site,
            vehicleNumber = "rj14 gb 1234",
        })).ReadAsync<ChallanDto>();

    private async Task<HttpClient> SignUpAsync(string type)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Verma Traders", fullName = "Suresh Verma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
