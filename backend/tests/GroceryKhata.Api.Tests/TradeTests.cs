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
        Assert.Equal("RJ14GB1234", first.VehicleNumber);

        // The bags left the shelf, but nothing is owed yet and nothing is a sale yet.
        Assert.Equal(-80m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Stock);
        Assert.Equal(0m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{contractor}/statement")).ClosingBalance);
        Assert.Equal(2, (await shop.GetJsonAsync<List<ChallanDto>>($"/api/v1/challans?customerId={contractor}")).Count);

        var invoiceId = Guid.CreateVersion7();
        var invoice = await (await shop.PostJsonAsync("/api/v1/challans/invoice",
            new { id = invoiceId, challanIds = new[] { first.Id, second.Id }, paidAmount = 0m })).ReadAsync<BillDto>();
        Assert.Equal((80m * 385m, "Mansarovar plot 12"), (invoice.Total, invoice.Site));
        Assert.Equal(18m, invoice.Lines[0].GstRate); // the item's GST kept on the line
        Assert.Equal(80m * 385m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{contractor}/statement")).ClosingBalance);
        Assert.Equal(-80m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Stock); // not taken twice
        Assert.Empty(await shop.GetJsonAsync<List<ChallanDto>>($"/api/v1/challans?customerId={contractor}"));

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
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z5" });
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
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z5" });
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
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z5" });
        var buyer = await KhataTests.CreateCustomerAsync(shop, "Delhi Builders");
        async Task<BillDto> Bill(Guid? customer) => await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, paidAmount = 1180m,
            lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 1180m } },
        })).ReadAsync<BillDto>();

        var local = await Bill(null);
        Assert.Equal((1000m, 90m, 90m, 0m), (local.Lines[0].TaxableValue, local.Lines[0].Cgst, local.Lines[0].Sgst, local.Lines[0].Igst));
        (await shop.PutAsJsonAsync($"/api/v1/customers/{buyer}/gstin", new { gstin = "07AAACD1234E1Z2" })).EnsureSuccessStatusCode();
        var other = await Bill(buyer);
        Assert.Equal(("07AAACD1234E1Z2", 180m, 0m), (other.BuyerGstin, other.Lines[0].Igst, other.Lines[0].Cgst));
        await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z5", GstComposition = true });
        var supply = await Bill(null);
        Assert.Equal((true, (decimal?)null), (supply.BillOfSupply, supply.Lines[0].TaxableValue));
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
