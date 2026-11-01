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
