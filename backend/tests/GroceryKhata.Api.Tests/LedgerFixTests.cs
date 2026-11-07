using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;

namespace GroceryKhata.Api.Tests;

/// <summary>The money and stock rules around returns, exchanges, challans, orders, batches and helpers.</summary>
[Collection(ApiCollection.Name)]
public sealed class LedgerFixTests(ApiFactory api)
{
    private async Task<HttpClient> ShopAsync(string type)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Verma Traders", fullName = "Suresh Verma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> HelperOfAsync(HttpClient owner)
    {
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return helper;
    }

    private static async Task<ProductDto> ItemAsync(HttpClient shop, string name, decimal price, decimal stock, object? extra = null)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = price, openingStock = stock })).EnsureSuccessStatusCode();
        return await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{id}");
    }

    private static Task<HttpResponseMessage> Bill(HttpClient shop, Guid? customerId, Guid product, decimal quantity, decimal price, decimal paid, Guid? exchangeNoteId = null) =>
        shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId, lines = new[] { new { productId = product, quantity, unitPrice = price } }, paidAmount = paid, paymentMode = "cash", exchangeNoteId,
        });

    private static async Task<CreditNoteDto> NoteAsync(HttpClient shop, Guid billId, decimal quantity, bool exchange = false, string[]? serials = null) =>
        await (await shop.PostJsonAsync($"/api/v1/bills/{billId}/credit-notes", new
        {
            id = Guid.CreateVersion7(), reason = "Returned", lines = new[] { new { lineNo = 1, quantity } }, exchange, serials,
        })).ReadAsync<CreditNoteDto>();

    private static async Task AssertBalanceIs(HttpClient shop, Guid customer, decimal expected)
    {
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal((expected, expected), (statement.Customer.Balance, statement.ClosingBalance));
        Assert.Empty(await shop.GetJsonAsync<List<BalanceMismatch>>("/api/v1/ledger/reconcile"));
    }

    [Fact]
    public async Task An_exchange_worth_more_than_the_new_goods_pays_the_difference_once()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Mixer", 500m, 5m);
        var small = await ItemAsync(shop, "Kettle", 300m, 5m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        var bill = await (await Bill(shop, customer, item.Id, 1m, 500m, 500m)).ReadAsync<BillDto>();

        var note = await NoteAsync(shop, bill.Id, 1m, exchange: true);
        await AssertBalanceIs(shop, customer, 0m); // nothing moves until the credit is used

        // ₹200 comes back out of the drawer, so the khata owes nobody anything.
        (await Bill(shop, customer, small.Id, 1m, 300m, 0m, note.Id)).EnsureSuccessStatusCode();
        await AssertBalanceIs(shop, customer, 0m);
    }

    [Fact]
    public async Task A_walk_in_exchange_put_on_a_khata_charges_only_what_the_credit_did_not_cover()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Mixer", 500m, 5m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        var bill = await (await Bill(shop, null, item.Id, 1m, 500m, 500m)).ReadAsync<BillDto>();
        var note = await NoteAsync(shop, bill.Id, 1m, exchange: true);

        (await Bill(shop, customer, item.Id, 2m, 500m, 0m, note.Id)).EnsureSuccessStatusCode();
        await AssertBalanceIs(shop, customer, 500m);
    }

    [Fact]
    public async Task An_exchange_credit_is_spent_once_and_only_by_its_customer()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Mixer", 500m, 50m);
        var (asha, other) = (await KhataTests.CreateCustomerAsync(shop, "Asha"), await KhataTests.CreateCustomerAsync(shop, "Binod"));
        var bill = await (await Bill(shop, asha, item.Id, 1m, 500m, 500m)).ReadAsync<BillDto>();
        var note = await NoteAsync(shop, bill.Id, 1m, exchange: true);

        Assert.Equal(HttpStatusCode.BadRequest, (await Bill(shop, other, item.Id, 1m, 500m, 0m, note.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Bill(shop, null, item.Id, 1m, 500m, 0m, note.Id)).StatusCode);

        var both = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Bill(shop, asha, item.Id, 1m, 500m, 0m, note.Id)));
        Assert.Single(both, r => r.IsSuccessStatusCode);
        await AssertBalanceIs(shop, asha, 0m);
    }

    [Fact]
    public async Task A_bill_with_goods_taken_back_cannot_be_cancelled_whole()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Mixer", 500m, 5m);
        var walkIn = await (await Bill(shop, null, item.Id, 2m, 500m, 1000m)).ReadAsync<BillDto>();
        await NoteAsync(shop, walkIn.Id, 1m);
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/bills/{walkIn.Id}/cancel", new { reason = "Made by mistake" })).StatusCode);
        Assert.Equal(4m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{item.Id}")).Stock);

        // The new bill of an exchange too: cancelling it would lose the customer's credit.
        var old = await (await Bill(shop, null, item.Id, 1m, 500m, 500m)).ReadAsync<BillDto>();
        var note = await NoteAsync(shop, old.Id, 1m, exchange: true);
        var swapped = await (await Bill(shop, null, item.Id, 1m, 500m, 0m, note.Id)).ReadAsync<BillDto>();
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/bills/{swapped.Id}/cancel", new { reason = "Made by mistake" })).StatusCode);
    }

    [Fact]
    public async Task A_bills_khata_lines_are_removed_only_with_the_bill()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Mixer", 500m, 5m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        var helper = await HelperOfAsync(shop);
        await (await Bill(shop, customer, item.Id, 1m, 500m, 100m)).ReadAsync<BillDto>();

        var lines = (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).Entries;
        Assert.Equal(2, lines.Count);
        foreach (var line in lines)
        {
            Assert.Equal(HttpStatusCode.Conflict, (await helper.PostJsonAsync($"/api/v1/ledger-entries/{line.Id}/reverse", new { id = Guid.CreateVersion7() })).StatusCode);
        }

        await AssertBalanceIs(shop, customer, 400m);
    }

    [Fact]
    public async Task Two_invoices_at_once_bill_a_challan_once()
    {
        var shop = await ShopAsync("building_materials");
        var cement = await ItemAsync(shop, "Cement", 385m, 100m);
        var contractor = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var challan = await (await shop.PostJsonAsync("/api/v1/challans", new
        {
            id = Guid.CreateVersion7(), customerId = contractor, lines = new[] { new { productId = cement.Id, quantity = 10m, unitPrice = 385m } },
        })).ReadAsync<ChallanDto>();

        var tries = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            shop.PostJsonAsync("/api/v1/challans/invoice", new { id = Guid.CreateVersion7(), challanIds = new[] { challan.Id }, paidAmount = 0m })));

        Assert.Single(tries, r => r.IsSuccessStatusCode);
        await AssertBalanceIs(shop, contractor, 3850m);
    }

    [Fact]
    public async Task Items_a_challan_could_never_bill_are_stopped_before_they_leave()
    {
        var shop = await ShopAsync("building_materials");
        var contractor = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var pump = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = pump, name = "Pump", unit = "piece", sellingPrice = 9000m, tracksSerials = true })).EnsureSuccessStatusCode();
        var paint = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = paint, name = "Paint", unit = "piece", sellingPrice = 400m, mrp = 500m })).EnsureSuccessStatusCode();

        Task<HttpResponseMessage> Send(Guid product, decimal price) => shop.PostJsonAsync("/api/v1/challans", new
        {
            id = Guid.CreateVersion7(), customerId = contractor, lines = new[] { new { productId = product, quantity = 1m, unitPrice = price } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(pump, 9000m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(paint, 600m)).StatusCode);
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pump}")).Stock);
    }

    private static async Task<OrderDto> StoreOrderAsync(HttpClient shop, string slug, HttpClient guest, Guid product, decimal quantity)
    {
        (await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", new
        {
            id = Guid.CreateVersion7(), name = "Neha", phone = ApiFactory.NewPhone(), lines = new[] { new { productId = product, quantity } },
        })).EnsureSuccessStatusCode();
        return (await shop.GetJsonAsync<List<OrderDto>>("/api/v1/orders")).First();
    }

    [Fact]
    public async Task Packing_another_item_moves_the_hold_with_it()
    {
        var (shop, auth) = await api.SignUpAsync("Swap Store");
        var (brandA, brandB) = (await ItemAsync(shop, "Atta A", 44m, 10m), await ItemAsync(shop, "Atta B", 44m, 10m));
        var order = await StoreOrderAsync(shop, auth.Memberships.Single().ShopSlug, api.CreateClient(), brandA.Id, 2m);

        (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/pack", new
        {
            lines = new[] { new { id = order.Lines[0].Id, productId = brandB.Id, packedQuantity = 2m, unitPrice = 44m, removed = false } },
        })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{brandA.Id}")).Reserved);
        Assert.Equal(2m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{brandB.Id}")).Reserved);

        (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{brandB.Id}")).Reserved);
    }

    [Fact]
    public async Task An_order_line_sold_by_serial_is_stopped_at_packing_with_a_reason()
    {
        var (shop, auth) = await api.SignUpAsync("Serial Store");
        var pump = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = pump, name = "Pump", unit = "piece", sellingPrice = 9000m, openingStock = 3m, tracksSerials = true })).EnsureSuccessStatusCode();
        var order = await StoreOrderAsync(shop, auth.Memberships.Single().ShopSlug, api.CreateClient(), pump, 1m);

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/pack", new
        {
            lines = new[] { new { id = order.Lines[0].Id, packedQuantity = 1m, unitPrice = 9000m, removed = false } },
        })).StatusCode);
    }

    [Fact]
    public async Task A_new_batch_keeps_the_shops_discount_and_never_prices_old_packs_above_their_mrp()
    {
        var shop = await ShopAsync("medical");
        var med = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = med, name = "Syrup", unit = "piece", sellingPrice = 100m })).EnsureSuccessStatusCode();
        string Month(int add) => DateTime.Today.AddMonths(add).ToString("yyyy-MM");
        Task<HttpResponseMessage> Receive(string batchNo, int months, decimal mrp) =>
            shop.PostJsonAsync($"/api/v1/products/{med}/batches", new { id = Guid.CreateVersion7(), batchNo, expiryMonth = Month(months), mrp, quantity = 5m });

        (await Receive("OLD", 3, 100m)).EnsureSuccessStatusCode();
        (await Receive("NEW", 12, 110m)).EnsureSuccessStatusCode();
        var item = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{med}");
        Assert.Equal((100m, 110m), (item.SellingPrice, item.Mrp!.Value)); // the old packs print ₹100

        // The regular price bills; above an old pack's MRP doesn't.
        (await Bill(shop, null, med, 1m, 100m, 100m)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Bill(shop, null, med, 1m, 105m, 105m)).StatusCode);

        // A discount the shop chose survives the next delivery.
        (await shop.PutAsJsonAsync($"/api/v1/products/{med}", new { name = "Syrup", unit = "piece", sellingPrice = 90m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await Receive("NEWER", 14, 120m)).EnsureSuccessStatusCode();
        Assert.Equal(90m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{med}")).SellingPrice);
    }

    [Fact]
    public async Task An_mrp_equal_to_the_price_still_stops_a_bill_above_it()
    {
        var shop = await ShopAsync("hardware");
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name = "Biscuits", unit = "piece", sellingPrice = 30m, mrp = 30m, openingStock = 5m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Bill(shop, null, id, 1m, 35m, 35m)).StatusCode);
    }

    [Fact]
    public async Task Returns_and_stock_corrections_keep_batches_in_step()
    {
        var shop = await ShopAsync("medical");
        var med = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = med, name = "Syrup", unit = "piece", sellingPrice = 100m })).EnsureSuccessStatusCode();
        var month = DateTime.Today.AddMonths(6).ToString("yyyy-MM");
        (await shop.PostJsonAsync($"/api/v1/products/{med}/batches", new { id = Guid.CreateVersion7(), batchNo = "B1", expiryMonth = month, mrp = 100m, quantity = 2m })).EnsureSuccessStatusCode();
        async Task<decimal> Batch() => (await shop.GetJsonAsync<List<BatchDto>>($"/api/v1/products/{med}/batches")).Sum(b => b.Quantity);

        var bill = await (await Bill(shop, null, med, 2m, 100m, 200m)).ReadAsync<BillDto>();
        await NoteAsync(shop, bill.Id, 1m);
        Assert.Equal(1m, await Batch());
        (await Bill(shop, null, med, 1m, 100m, 100m)).EnsureSuccessStatusCode(); // the returned pack sells again

        // A sold-out batch item doesn't sell with no batch.
        Assert.Equal(HttpStatusCode.BadRequest, (await Bill(shop, null, med, 1m, 100m, 100m)).StatusCode);

        // A count up, then damage: the batch follows the shelf.
        (await shop.PostJsonAsync("/api/v1/stocktakes", new { id = Guid.CreateVersion7(), counts = new[] { new { productId = med, counted = 3m } } })).EnsureSuccessStatusCode();
        Assert.Equal(3m, await Batch());
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = med, kind = "adjustment", quantity = -2m, reason = "damage" })).EnsureSuccessStatusCode();
        Assert.Equal(1m, await Batch());
    }

    [Fact]
    public async Task A_returned_serial_goes_back_in_stock_and_can_be_sold_again()
    {
        var shop = await ShopAsync("hardware");
        var pump = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = pump, name = "Pump", unit = "piece", sellingPrice = 9000m, openingStock = 2m, tracksSerials = true, warrantyMonths = 12 })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/serials/receive", new { productId = pump, serials = new[] { "P1", "P2" } })).EnsureSuccessStatusCode();
        Task<HttpResponseMessage> Sell(params string[] serials) => shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), lines = new[] { new { productId = pump, quantity = (decimal)serials.Length, unitPrice = 9000m } }, paidAmount = 9000m * serials.Length, paymentMode = "cash",
            serials = serials.Select(s => new { productId = pump, serialNo = s }).ToArray(),
        });
        var bill = await (await Sell("P1", "P2")).ReadAsync<BillDto>();

        // One of two back: the shop must say which.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/credit-notes", new
        {
            id = Guid.CreateVersion7(), reason = "Returned", lines = new[] { new { lineNo = 1, quantity = 1m } },
        })).StatusCode);
        await NoteAsync(shop, bill.Id, 1m, serials: ["p2"]);

        var units = await shop.GetJsonAsync<List<SerialDto>>($"/api/v1/serials?productId={pump}");
        var back = units.Single(u => u.SerialNo == "P2");
        Assert.Equal((SerialStatus.InStock, (Guid?)null, (DateOnly?)null), (back.Status, back.BillId, back.WarrantyUntil));
        Assert.Equal(SerialStatus.Sold, units.Single(u => u.SerialNo == "P1").Status);
        (await Sell("P2")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Helpers_stay_inside_the_discount_limit_on_every_route()
    {
        var (shop, auth) = await api.SignUpAsync("Limit Store");
        var item = await ItemAsync(shop, "Rice", 100m, 50m);
        var helper = await HelperOfAsync(shop);

        // Not by changing the price.
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PutAsJsonAsync($"/api/v1/products/{item.Id}", new { name = "Rice", unit = "piece", sellingPrice = 1m }, ApiFactory.Json)).StatusCode);
        // Other details still save.
        (await helper.PutAsJsonAsync($"/api/v1/products/{item.Id}", new { name = "Basmati rice", unit = "piece", sellingPrice = 100m }, ApiFactory.Json)).EnsureSuccessStatusCode();

        // Not by packing an order cheap, though the order's own price is fine.
        var order = await StoreOrderAsync(shop, auth.Memberships.Single().ShopSlug, api.CreateClient(), item.Id, 1m);
        object Pack(decimal price) => new { lines = new[] { new { id = order.Lines[0].Id, packedQuantity = 1m, unitPrice = price, removed = false } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync($"/api/v1/orders/{order.Id}/pack", Pack(1m))).StatusCode);
        (await helper.PostJsonAsync($"/api/v1/orders/{order.Id}/pack", Pack(100m))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Helpers_stay_inside_the_discount_limit_on_a_challan()
    {
        var shop = await ShopAsync("building_materials");
        var cement = await ItemAsync(shop, "Cement", 400m, 100m);
        var contractor = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var helper = await HelperOfAsync(shop);
        Task<HttpResponseMessage> Send(HttpClient who, decimal price) => who.PostJsonAsync("/api/v1/challans", new
        {
            id = Guid.CreateVersion7(), customerId = contractor, lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = price } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(helper, 10m)).StatusCode);
        (await Send(helper, 390m)).EnsureSuccessStatusCode();
        (await Send(shop, 10m)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_composition_shops_credit_note_carries_no_tax()
    {
        var shop = await ShopAsync("hardware");
        (await shop.PutAsJsonAsync("/api/v1/shop", new { name = "Verma Traders", gstin = "08ABCDE1234F1Z0", gstComposition = true }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name = "Soap", unit = "piece", sellingPrice = 118m, openingStock = 5m, gstRate = 18m, hsnCode = "3401" })).EnsureSuccessStatusCode();
        var bill = await (await Bill(shop, null, id, 1m, 118m, 118m)).ReadAsync<BillDto>();

        var line = (await NoteAsync(shop, bill.Id, 1m)).Lines.Single();
        Assert.Equal((118m, 0m, 0m, 0m), (line.TaxableValue, line.Cgst, line.Sgst, line.Igst));
    }

    [Fact]
    public async Task A_distributors_credit_note_comes_off_once()
    {
        var shop = await ShopAsync("medical");
        var med = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = med, name = "Syrup", unit = "piece", sellingPrice = 100m })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Distributor" })).EnsureSuccessStatusCode();
        var month = DateTime.Today.AddMonths(6).ToString("yyyy-MM");
        var batches = await (await shop.PostJsonAsync($"/api/v1/products/{med}/batches", new { id = Guid.CreateVersion7(), batchNo = "B1", expiryMonth = month, mrp = 100m, costPrice = 80m, quantity = 2m })).ReadAsync<List<BatchDto>>();
        var back = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/returns", new { id = back, supplierId = supplier, lines = new[] { new { batchId = batches[0].Id, quantity = 2m } } })).EnsureSuccessStatusCode();

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => shop.PostJsonAsync($"/api/v1/suppliers/returns/{back}/credit-note", new { creditNoteNo = "CN1", amount = 150m })));

        var detail = await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}");
        Assert.Equal(-150m, detail.Supplier.Due);
        Assert.Contains("· return", detail.Entries.Single().Note);
    }
}
