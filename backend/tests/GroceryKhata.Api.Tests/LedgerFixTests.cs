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

    private static async Task<ProductDto> ItemAsync(HttpClient shop, string name, decimal price, decimal stock, decimal? reorderLevel = null)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = price, openingStock = stock, reorderLevel })).EnsureSuccessStatusCode();
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

    [Fact]
    public async Task A_customers_state_is_saved_and_a_bill_gets_what_it_needs_to_know()
    {
        var shop = await ShopAsync("hardware");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        var helper = await HelperOfAsync(shop);

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/state", new { state = "99" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PutAsJsonAsync($"/api/v1/customers/{customer}/state", new { state = "24" }, ApiFactory.Json)).StatusCode);
        (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/state", new { state = "24" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/address", new { address = "Surat" }, ApiFactory.Json)).EnsureSuccessStatusCode();

        // A helper at the counter reads it: the bill needs it.
        var details = await helper.GetJsonAsync<CustomerBillDetails>($"/api/v1/customers/{customer}/bill-details");
        Assert.Equal(("24", "Surat", (string?)null), (details.State, details.Address, details.Gstin));

        (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/state", new { state = "" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        Assert.Null((await shop.GetJsonAsync<CustomerBillDetails>($"/api/v1/customers/{customer}/bill-details")).State);
    }

    [Fact]
    public async Task A_shop_chooses_whether_it_delivers_and_saving_other_settings_keeps_that()
    {
        var shop = await ShopAsync("hardware");
        Assert.Null((await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop")).DeliversGoods);

        (await shop.PutAsJsonAsync("/api/v1/shop", new { name = "Verma Traders", deliversGoods = false }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await shop.PutAsJsonAsync("/api/v1/shop", new { name = "Verma Traders" }, ApiFactory.Json)).EnsureSuccessStatusCode();

        Assert.False((await shop.GetJsonAsync<GroceryKhata.Api.Shops.ShopSettingsDto>("/api/v1/shop")).DeliversGoods);
    }

    [Fact]
    public async Task A_registered_shop_on_the_free_plan_still_makes_tax_invoices_but_gets_no_tally_files()
    {
        var (shop, auth) = await api.SignUpAsync("Free GST Store");
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var free = new Npgsql.NpgsqlCommand("""
                INSERT INTO subscriptions (id, shop_id, trial_ends_at, referral_rewarded, created_at, updated_at)
                VALUES (gen_random_uuid(), @s, now() - interval '60 days', false, now(), now())
                ON CONFLICT (shop_id) DO UPDATE SET trial_ends_at = now() - interval '60 days'
                """, db);
            free.Parameters.AddWithValue("s", auth.Memberships.Single().ShopId);
            await free.ExecuteNonQueryAsync();
        }

        (await shop.PutAsJsonAsync("/api/v1/shop", new { name = "Free GST Store", gstin = "08ABCDE1234F1Z0" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name = "Soap", unit = "piece", sellingPrice = 118m, openingStock = 5m, gstRate = 18m, hsnCode = "3401" })).EnsureSuccessStatusCode();

        var bill = await (await Bill(shop, null, id, 1m, 118m, 118m)).ReadAsync<BillDto>();
        Assert.Equal("08ABCDE1234F1Z0", bill.Gstin);
        Assert.Equal((100m, 9m, 9m), (bill.Lines[0].TaxableValue!.Value, bill.Lines[0].Cgst!.Value, bill.Lines[0].Sgst!.Value));

        var month = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM");
        Assert.NotEqual(HttpStatusCode.OK, (await shop.GetAsync($"/api/v1/gst/export.zip?month={month}")).StatusCode);
    }

    [Fact]
    public async Task A_suppliers_page_tells_the_whole_story_from_opening_balance_to_a_cheque()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Pipe", 300m, 5m);
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders" })).EnsureSuccessStatusCode();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/suppliers/{supplier}", new { id = supplier, name = "Jain Traders", gstin = "08ABCDE1234F1Z5" }, ApiFactory.Json)).StatusCode);
        (await shop.PutAsJsonAsync($"/api/v1/suppliers/{supplier}", new { id = supplier, name = "Jain Traders", phone = "9811177701", gstin = "08abcde1234f1z0", address = "Jaipur", paymentDays = 15 }, ApiFactory.Json)).EnsureSuccessStatusCode();

        // A bill dated ten days ago, with ₹50 freight: dues equal the printed total.
        var bill = Guid.CreateVersion7();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = item.Id, quantity = 2m, unitCost = 100m } }, paidAmount = 0m, billDate = today.AddDays(1).ToString("yyyy-MM-dd"),
        })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = bill, supplierId = supplier, billNo = "J-77", lines = new[] { new { productId = item.Id, quantity = 2m, unitCost = 100m, gstRate = 18m } }, paidAmount = 0m, billDate = today.AddDays(-10).ToString("yyyy-MM-dd"), extra = 50m,
        })).EnsureSuccessStatusCode();

        // The opening balance goes before it, however late it's entered, and only once.
        var openingBody = new { id = Guid.CreateVersion7(), amount = 1000m };
        (await shop.PostJsonAsync($"/api/v1/suppliers/{supplier}/opening", openingBody)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/suppliers/{supplier}/opening", new { id = Guid.CreateVersion7(), amount = 5m })).StatusCode);

        (await shop.PostJsonAsync($"/api/v1/suppliers/{supplier}/pay", new { id = Guid.CreateVersion7(), amount = 500m, paymentMode = "bank", reference = "CHQ 4471" })).EnsureSuccessStatusCode();

        var detail = await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}");
        Assert.Equal(("08ABCDE1234F1Z0", "Jaipur", 15), (detail.Supplier.Gstin, detail.Supplier.Address, detail.Supplier.PaymentDays!.Value));
        Assert.Equal(786m, detail.Supplier.Due);
        Assert.Equal([SupplierEntryKind.Payment, SupplierEntryKind.Bill, SupplierEntryKind.Opening], detail.Entries.Select(e => e.Kind));
        Assert.Equal([786m, 1286m, 1000m], detail.Entries.Select(e => e.Balance));
        var billLine = detail.Entries[1];
        Assert.Equal(today.AddDays(5), billLine.DueOn);
        Assert.Equal(("CHQ 4471", PaymentMode.Bank), (detail.Entries[0].Reference, detail.Entries[0].Mode!.Value));

        var opened = await shop.GetJsonAsync<PurchaseDetailDto>($"/api/v1/suppliers/purchases/{bill}");
        Assert.Equal(("J-77", 286m, 50m, "Pipe", 200m, 36m), (opened.BillNo, opened.Total, opened.Extra, opened.Lines[0].Name, opened.Lines[0].Taxable, opened.Lines[0].Gst));

        // A cheque isn't cash out of the drawer.
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((0m, 0m), (book.SpentCash, book.SpentUpi));

        // Cancelled, the bill stops counting as an old unpaid one, and put away the supplier keeps its history.
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{bill}/cancel", new { })).EnsureSuccessStatusCode();
        (await shop.PutAsJsonAsync($"/api/v1/suppliers/{supplier}", new { id = supplier, name = "Jain Traders", isActive = false }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var listed = (await shop.GetJsonAsync<List<SupplierDto>>("/api/v1/suppliers")).Single();
        Assert.Equal((false, 500m, "08ABCDE1234F1Z0"), (listed.IsActive, listed.Due, listed.Gstin));
        Assert.Equal(SupplierEntryKind.BillCancelled, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Entries[0].Kind);
    }

    [Fact]
    public async Task An_opening_balance_entered_first_still_starts_the_khata_before_an_older_bill()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Pipe", 300m, 5m);
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/suppliers/{supplier}/opening", new { id = Guid.CreateVersion7(), amount = 1000m })).EnsureSuccessStatusCode();
        var billDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(-5).ToString("yyyy-MM-dd");
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = item.Id, quantity = 1m, unitCost = 200m } }, paidAmount = 0m, billDate })).EnsureSuccessStatusCode();

        var entries = (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Entries;
        Assert.Equal([(SupplierEntryKind.Bill, 1200m), (SupplierEntryKind.Opening, 1000m)], entries.Select(e => (e.Kind, e.Balance)));
    }

    private static async Task<Guid> SupplierAsync(HttpClient shop, string name = "Jain Traders")
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id, name })).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task An_order_counts_down_as_goods_come_and_stops_them_being_ordered_twice()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m, reorderLevel: 2m);
        var tap = await ItemAsync(shop, "Tap", 150m, 0m, reorderLevel: 2m);
        var supplierId = await SupplierAsync(shop);

        // Out of stock and kept in stock (a reorder level), so both are on What to order.
        Assert.Equal(2, (await shop.GetJsonAsync<List<ReorderGroupDto>>("/api/v1/pharmacy/reorder")).SelectMany(g => g.Lines).Count());

        var orderId = Guid.CreateVersion7();
        var order = await (await shop.PostJsonAsync("/api/v1/suppliers/orders", new
        {
            id = orderId, supplierId, lines = new object[] { new { productId = pipe.Id, quantity = 10m, rate = 200m }, new { productId = tap.Id, quantity = 5m } },
        })).ReadAsync<PurchaseOrderDto>();
        Assert.Equal((1, 15m), (order.Number, order.Pending));

        // On order: What to order leaves them alone, and no stock moved yet.
        Assert.Empty((await shop.GetJsonAsync<List<ReorderGroupDto>>("/api/v1/pharmacy/reorder")).SelectMany(g => g.Lines));
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);

        // Part of it comes on a bill matched to the order.
        var bill = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = bill, supplierId, orderId, lines = new[] { new { productId = pipe.Id, quantity = 6m, unitCost = 200m } }, paidAmount = 0m })).EnsureSuccessStatusCode();
        var open = (await shop.GetJsonAsync<List<PurchaseOrderDto>>($"/api/v1/suppliers/orders?supplierId={supplierId}")).Single();
        Assert.Equal((PurchaseOrderStatus.Open, 9m, 6m), (open.Status, open.Pending, open.Lines.Single(l => l.ProductId == pipe.Id).Received));

        // Cancelling that bill puts it back on order; then the rest closes short.
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{bill}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(15m, (await shop.GetJsonAsync<List<PurchaseOrderDto>>($"/api/v1/suppliers/orders?supplierId={supplierId}")).Single().Pending);
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId, orderId, lines = new[] { new { productId = pipe.Id, quantity = 10m, unitCost = 200m } }, paidAmount = 0m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/cancel", new { })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/close", new { })).EnsureSuccessStatusCode();
        Assert.Empty(await shop.GetJsonAsync<List<PurchaseOrderDto>>($"/api/v1/suppliers/orders?supplierId={supplierId}"));

        // A closed order takes no more goods.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId, orderId, lines = new[] { new { productId = tap.Id, quantity = 1m, unitCost = 100m } }, paidAmount = 0m })).StatusCode);
    }

    [Fact]
    public async Task An_order_closes_itself_when_everything_has_come()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var supplierId = await SupplierAsync(shop);
        var orderId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = orderId, supplierId, lines = new[] { new { productId = pipe.Id, quantity = 10m } } })).EnsureSuccessStatusCode();
        // 9 paid and 1 free make the 10.
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId, orderId, lines = new[] { new { productId = pipe.Id, quantity = 9m, unitCost = 200m, freeQuantity = 1m } }, paidAmount = 0m })).EnsureSuccessStatusCode();
        var all = await shop.GetJsonAsync<List<PurchaseOrderDto>>($"/api/v1/suppliers/orders?supplierId={supplierId}&all=true");
        Assert.Equal((PurchaseOrderStatus.Closed, 10m), (all.Single().Status, all.Single().Lines[0].Received));
    }

    [Fact]
    public async Task An_order_line_keeps_its_bags_and_the_order_totals_each_unit_apart()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var tap = await ItemAsync(shop, "Tap", 150m, 0m);
        var supplierId = await SupplierAsync(shop);
        var order = await (await shop.PostJsonAsync("/api/v1/suppliers/orders", new
        {
            id = Guid.CreateVersion7(), supplierId, lines = new object[] { new { productId = pipe.Id, quantity = 24m, bags = 2 }, new { productId = tap.Id, quantity = 6m } },
        })).ReadAsync<PurchaseOrderDto>();
        Assert.Equal((2, (int?)2), (order.Bags, order.Lines.Single(l => l.ProductId == pipe.Id).Bags));
        Assert.Equal(30m, order.Totals!.Single(t => t.Unit == pipe.Unit).Quantity);

        // Bought by the carton of 12: the line says so.
        (await shop.PutJsonAsync($"/api/v1/products/{tap.Id}", new { name = "Tap", unit = "piece", sellingPrice = 150m, bagSize = 12m, purchaseUnit = "carton" })).EnsureSuccessStatusCode();
        var cartons = await (await shop.PostJsonAsync("/api/v1/suppliers/orders", new
        {
            id = Guid.CreateVersion7(), supplierId, lines = new[] { new { productId = tap.Id, quantity = 24m, bags = 2 } },
        })).ReadAsync<PurchaseOrderDto>();
        Assert.Equal(Unit.Carton, cartons.Lines.Single().PackUnit);

        // Pieces come whole.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/orders", new
        {
            id = Guid.CreateVersion7(), supplierId, lines = new[] { new { productId = tap.Id, quantity = 2.5m } },
        })).StatusCode);

        // Zero bags is not a count.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/orders", new
        {
            id = Guid.CreateVersion7(), supplierId, lines = new[] { new { productId = pipe.Id, quantity = 1m, bags = 0 } },
        })).StatusCode);
    }

    [Fact]
    public async Task An_order_shows_its_load_and_freight_is_never_shared_over_kg_and_pieces_added_together()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var cement = await ItemAsync(shop, "Cement", 400m, 0m);
        var supplierId = await SupplierAsync(shop);
        (await shop.PutJsonAsync($"/api/v1/products/{pipe.Id}", new { name = "Pipe", unit = "piece", sellingPrice = 300m, weightKg = 2m })).EnsureSuccessStatusCode();
        (await shop.PutJsonAsync($"/api/v1/products/{cement.Id}", new { name = "Cement", unit = "kg", sellingPrice = 8m })).EnsureSuccessStatusCode();
        var tap = await ItemAsync(shop, "Tap", 150m, 0m);

        // 10 pipes of 2 kg and 500 kg of cement: a 520 kg load; the tap's weight isn't known, and it says so.
        var order = await (await shop.PostJsonAsync("/api/v1/suppliers/orders", new
        {
            id = Guid.CreateVersion7(), supplierId,
            lines = new[] { new { productId = pipe.Id, quantity = 10m }, new { productId = cement.Id, quantity = 500m }, new { productId = tap.Id, quantity = 4m } },
        })).ReadAsync<PurchaseOrderDto>();
        Assert.Equal(((decimal?)520m, 1), (order.WeightKg, order.UnweighedLines));

        object Bill(string split) => new
        {
            id = Guid.CreateVersion7(), supplierId, paidAmount = 0m, split, charges = new[] { new { label = "Lorry", amount = 520m } },
            lines = new[] { new { productId = pipe.Id, quantity = 10m, unitCost = 200m }, new { productId = cement.Id, quantity = 500m, unitCost = 6m } },
        };

        // Units would add 10 pieces to 500 kg: refused. By weight, the pipes carry 20 of the 520 kg.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/purchases", Bill("quantity"))).StatusCode);
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", Bill("weight"))).EnsureSuccessStatusCode();
        Assert.Equal(202m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).CostPrice);
    }

    [Fact]
    public async Task Suppliers_come_in_from_a_file_once_with_their_opening_balance()
    {
        var shop = await ShopAsync("hardware");
        var rows = new object[]
        {
            new { name = "Jain Traders", phone = "9811177701", gstin = "08abcde1234f1z0", balance = 4500m },
            new { name = "Gupta Pipes", balance = -200m },
            new { name = "Bad GSTIN Co", gstin = "08ABCDE1234F1Z5", balance = 0m },
        };
        var first = await (await shop.PostJsonAsync("/api/v1/suppliers/import", new { rows })).ReadAsync<SupplierImportResult>();
        Assert.Equal((2, 0, 1), (first.Created, first.Skipped, first.Problems.Count));
        var again = await (await shop.PostJsonAsync("/api/v1/suppliers/import", new { rows })).ReadAsync<SupplierImportResult>();
        Assert.Equal((0, 2), (again.Created, again.Skipped));

        var list = await shop.GetJsonAsync<List<SupplierDto>>("/api/v1/suppliers");
        var jain = list.Single(s => s.Name == "Jain Traders");
        Assert.Equal((4500m, "08ABCDE1234F1Z0"), (jain.Due, jain.Gstin));
        var gupta = list.Single(s => s.Name == "Gupta Pipes");
        Assert.Equal(-200m, gupta.Due);
        Assert.Equal(SupplierEntryKind.Opening, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{gupta.Id}")).Entries.Single().Kind);
    }

    [Fact]
    public async Task Goods_kept_without_batches_go_back_to_any_supplier_and_the_credit_note_settles_it()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var supplierId = await SupplierAsync(shop);
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId, lines = new[] { new { productId = pipe.Id, quantity = 10m, unitCost = 200m } }, paidAmount = 0m })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/returns", new { id = Guid.CreateVersion7(), supplierId, lines = new[] { new { quantity = 1m } } })).StatusCode);
        var back = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/returns", new { id = back, supplierId, lines = new[] { new { productId = pipe.Id, quantity = 2m } } })).EnsureSuccessStatusCode();
        Assert.Equal(8m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);

        var open = (await shop.GetJsonAsync<List<SupplierReturnDto>>($"/api/v1/suppliers/returns?supplierId={supplierId}")).Single();
        Assert.Equal((400m, "Pipe", (DateOnly?)null), (open.Value, open.Lines.Single().ProductName, open.Lines.Single().Expiry));
        (await shop.PostJsonAsync($"/api/v1/suppliers/returns/{back}/credit-note", new { creditNoteNo = "CN-9", amount = 400m })).EnsureSuccessStatusCode();
        Assert.Equal(1600m, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplierId}")).Supplier.Due);
    }

    [Fact]
    public async Task The_purchase_register_lists_each_bill_by_gst_rate_for_the_ca()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var tap = await ItemAsync(shop, "Tap", 150m, 0m);
        var supplierId = await SupplierAsync(shop, "Jain, Traders");
        (await shop.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}", new { id = supplierId, name = "Jain, Traders", gstin = "08ABCDE1234F1Z0" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await shop.PutJsonAsync($"/api/v1/products/{pipe.Id}", new { name = "Pipe", unit = "piece", sellingPrice = 300m, weightKg = 2m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId, billNo = "J-1", paidAmount = 0m, extra = 10m,
            lines = new[] { new { productId = pipe.Id, quantity = 2m, unitCost = 100m, gstRate = 18m }, new { productId = tap.Id, quantity = 1m, unitCost = 100m, gstRate = 5m } },
        })).EnsureSuccessStatusCode();

        var month = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM");
        var csv = await (await shop.GetAsync($"/api/v1/suppliers/register?month={month}")).Content.ReadAsStringAsync();
        var lines = csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Date,Bill no,Supplier,Supplier GSTIN", lines[0]);
        // Tax split into CGST and SGST (same state), input tax claimable from a supplier with a valid GSTIN.
        Assert.Contains("J-1,\"Jain, Traders\",08ABCDE1234F1Z0,Rajasthan,5.00,100.00,2.50,2.50,0.00,Yes,10.00,351.00,", lines[1]);
        Assert.Contains(",18.00,200.00,18.00,18.00,0.00,Yes,,,", lines[2]);
        Assert.StartsWith("Total (cancelled bills left out),,,,,,300.00,20.50,20.50,0.00,,10.00,351.00", lines.Single(l => l.StartsWith("Total")));

        // Weight: the two 2 kg pipes weigh 4 kg; the tap's weight isn't known, so its row is blank, not counted as kg.
        Assert.EndsWith(",Status,Weight (kg)", lines[0]);
        Assert.EndsWith(",351.00,,", lines[1]);
        Assert.EndsWith(",,,,4", lines[2]);
        Assert.EndsWith(",,4", lines.Single(l => l.StartsWith("Total")));
    }

    private static object OfferBill(Guid product, decimal price, Guid? offerId) => new
    {
        id = Guid.CreateVersion7(), lines = new[] { new { productId = product, quantity = 1m, unitPrice = price, offerId } }, paidAmount = price, paymentMode = "cash",
    };

    [Fact]
    public async Task A_planned_offer_lets_a_helper_sell_at_its_price_and_reports_how_it_did()
    {
        var (shop, _) = await api.SignUpAsync("Offer Store");
        var kurta = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = kurta, name = "Kurta", unit = "piece", sellingPrice = 1000m, openingStock = 20m, category = "Kurtas" })).EnsureSuccessStatusCode();
        var socks = await ItemAsync(shop, "Socks", 100m, 20m);
        var helper = await HelperOfAsync(shop);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

        // Helpers can't plan offers; the owner can, on one category.
        var body = new { id = Guid.CreateVersion7(), name = "Summer sale", kind = "percent", value = 30m, startsOn = today.ToString("yyyy-MM-dd"), endsOn = today.AddDays(7).ToString("yyyy-MM-dd"), scope = "category", category = "Kurtas" };
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PostJsonAsync("/api/v1/offers", body)).StatusCode);
        var offer = await (await shop.PostJsonAsync("/api/v1/offers", body)).ReadAsync<SaleOfferDto>();

        // 30% is past the helper's 10%, but it's the owner's offer: it goes through, and the line carries its name.
        var bill = await (await helper.PostJsonAsync("/api/v1/bills", OfferBill(kurta, 700m, offer.Id))).ReadAsync<BillDto>();
        Assert.Equal((700m, 1000m), (bill.Lines[0].UnitPrice, bill.Lines[0].ListPrice!.Value));

        // Below the offer price, or on an item the offer isn't on, the helper's own limit applies again.
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/bills", OfferBill(kurta, 600m, offer.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/bills", OfferBill(socks.Id, 70m, offer.Id))).StatusCode);

        // The owner sees how it did; a helper sees the offer but not the takings.
        var mine = (await shop.GetJsonAsync<List<SaleOfferDto>>("/api/v1/offers")).Single();
        Assert.Equal((1, 1m, 700m, 300m), (mine.Stats!.Bills, mine.Stats.Quantity, mine.Stats.Sales, mine.Stats.Saved));
        Assert.Null((await helper.GetJsonAsync<List<SaleOfferDto>>("/api/v1/offers")).Single().Stats);

        // Stopped, it no longer lets a helper go past their limit.
        (await shop.PostJsonAsync($"/api/v1/offers/{offer.Id}/stop", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/bills", OfferBill(kurta, 700m, offer.Id))).StatusCode);
    }

    [Fact]
    public async Task An_offer_must_have_a_name_a_sensible_discount_and_real_items()
    {
        var shop = await ShopAsync("hardware");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).ToString("yyyy-MM-dd");
        object Body(string name, string kind, decimal value, string scope, Guid[]? items = null) => new { id = Guid.CreateVersion7(), name, kind, value, startsOn = today, endsOn = today, scope, productIds = items };

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/offers", Body("", "percent", 10m, "all"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/offers", Body("Mega sale", "percent", 95m, "all"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/offers", Body("Mega sale", "amount", 50m, "items", [Guid.CreateVersion7()]))).StatusCode);
        (await shop.PostJsonAsync("/api/v1/offers", Body("Flat 50 off", "amount", 50m, "all"))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Dues_downloads_say_which_way_the_money_is_owed()
    {
        var shop = await ShopAsync("hardware");
        var customers = await (await shop.GetAsync("/api/v1/reports/dues.csv")).Content.ReadAsStringAsync();
        var suppliers = await (await shop.GetAsync("/api/v1/reports/supplier-dues.csv")).Content.ReadAsStringAsync();
        Assert.StartsWith("Name,Phone,Customer owes (minus = advance),", customers.TrimStart('﻿'));
        Assert.StartsWith("Supplier,Phone,We owe (minus = they owe us),", suppliers.TrimStart('﻿'));
    }

    [Fact]
    public async Task Kharcha_means_running_costs_only_and_the_month_is_for_the_owner()
    {
        var shop = await ShopAsync("hardware");
        async Task Spend(decimal amount, string category) =>
            (await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount, category, mode = "cash" })).EnsureSuccessStatusCode();
        await Spend(10000m, "rent");
        await Spend(1500m, "salary");
        await Spend(20000m, "purchase");
        await Spend(5000m, "tax");
        await Spend(8000m, "equipment");
        await Spend(700m, "home");

        // Stock, tax, a fridge and money taken home are not the shop's kharcha: they are shown apart.
        var month = await shop.GetJsonAsync<ExpenseMonthDto>("/api/v1/expenses/month");
        Assert.Equal((11500m, 33000m, 700m, 45200m), (month.ShopKharcha, month.OutsideProfit, month.TakenHome, month.Total));

        // Helpers write kharcha but don't read the month's salary and money taken home.
        var helper = await HelperOfAsync(shop);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.GetAsync("/api/v1/expenses/month")).StatusCode);
        (await helper.GetAsync("/api/v1/expenses")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_notebook_reads_loan_and_bank_lines_under_their_own_headings()
    {
        var shop = await ShopAsync("hardware");
        var read = await (await shop.PostJsonAsync("/api/v1/expenses/read", new { text = "emi 3000, bank jama 5000, chai 40" })).ReadAsync<List<ExpenseGuess>>();
        Assert.Equal(
            [(3000m, ExpenseCategory.Loan, PaymentMode.Cash), (5000m, ExpenseCategory.Deposit, PaymentMode.Cash), (40m, ExpenseCategory.Tea, PaymentMode.Cash)],
            read.Select(g => (g.Amount, g.Category, g.Mode)).ToList());
    }

    [Fact]
    public async Task The_month_statement_adds_up_from_sales_to_net_profit()
    {
        var (shop, _) = await api.SignUpAsync("Statement Store");
        var costed = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = costed, name = "Bucket", unit = "piece", sellingPrice = 200m, costPrice = 150m, openingStock = 10m })).EnsureSuccessStatusCode();
        var noCost = await ItemAsync(shop, "Mug", 50m, 10m);
        (await Bill(shop, null, costed, 2m, 200m, 400m)).EnsureSuccessStatusCode();
        (await Bill(shop, null, noCost.Id, 1m, 50m, 50m)).EnsureSuccessStatusCode();
        async Task Spend(decimal amount, string category) =>
            (await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount, category, mode = "cash" })).EnsureSuccessStatusCode();
        await Spend(30m, "rent");
        await Spend(10m, "tea");
        await Spend(1000m, "loan");

        var st = (await shop.GetJsonAsync<MonthReport>("/api/v1/reports/month")).Statement!;
        // ₹450 sold; ₹50 of it has no cost, so profit is on the ₹400: less ₹300 cost = ₹100 gross, less ₹40 kharcha = ₹60.
        Assert.Equal((450m, 450m, 50m, 300m, 100m), (st.SalesWithGst, st.Sales, st.NoCostSales, st.CostOfGoods, st.GrossProfit));
        Assert.Equal([(ExpenseCategory.Rent, 30m), (ExpenseCategory.Tea, 10m)], st.Running.Select(x => (x.Category, x.Amount)).ToList());
        Assert.Equal((40m, 60m), (st.Expenses, st.NetProfit));
        Assert.Equal((ExpenseCategory.Loan, 1000m), (st.NotInProfit.Single().Category, st.NotInProfit.Single().Amount));

        // Helpers see sales, not the statement.
        var helper = await HelperOfAsync(shop);
        Assert.Null((await helper.GetJsonAsync<MonthReport>("/api/v1/reports/month")).Statement);
    }

    [Fact]
    public async Task The_year_profit_and_loss_lists_kharcha_heading_by_heading()
    {
        var (shop, _) = await api.SignUpAsync("Year Store");
        async Task Spend(decimal amount, string category) =>
            (await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount, category, mode = "cash" })).EnsureSuccessStatusCode();
        await Spend(9000m, "rent");
        await Spend(4000m, "salary");
        await Spend(2500m, "equipment");

        var year = await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl");
        Assert.Equal([(ExpenseCategory.Rent, 9000m), (ExpenseCategory.Salary, 4000m)], year.ExpenseLines!.Select(x => (x.Category, x.Amount)).ToList());
        Assert.Equal(13000m, year.Expenses);
        Assert.Equal((ExpenseCategory.Equipment, 2500m), (year.NotInProfit!.Single().Category, year.NotInProfit!.Single().Amount));

        var csv = await (await shop.GetAsync("/api/v1/reports/pnl.csv")).Content.ReadAsStringAsync();
        Assert.Contains("  of which Rent,-9000", csv);
        Assert.Contains("Not in profit: Equipment bought,2500", csv);
    }

    [Fact]
    public async Task Meter_readings_show_units_per_day_and_ask_why_a_reading_went_down()
    {
        var shop = await ShopAsync("hardware");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        object Reading(DateOnly day, decimal reading, string? reset = null) => new { id = Guid.CreateVersion7(), day = day.ToString("yyyy-MM-dd"), reading, reset };

        (await shop.PostJsonAsync("/api/v1/meter", Reading(today.AddDays(-26), 14520m))).EnsureSuccessStatusCode();
        var history = await (await shop.PostJsonAsync("/api/v1/meter", Reading(today, 14870m))).ReadAsync<List<MeterReadingDto>>();
        Assert.Equal((350m, 26, 13.5m), (history[0].Units!.Value, history[0].Days!.Value, history[0].PerDay!.Value));
        Assert.Null(history[1].Units);

        // Lower than the last: a typing slip unless the shop says it's a new meter.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/meter", Reading(today, 120m))).StatusCode);
        history = await (await shop.PostJsonAsync("/api/v1/meter", Reading(today, 120m, "new_meter"))).ReadAsync<List<MeterReadingDto>>();
        Assert.Equal((120m, (decimal?)null), (history[0].Reading, history[0].Units));

        // A helper can write a reading too.
        var helper = await HelperOfAsync(shop);
        (await helper.PostJsonAsync("/api/v1/meter", Reading(today, 130m))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_meter_number_in_the_notebook_is_not_read_as_rupees()
    {
        var shop = await ShopAsync("hardware");
        var read = await (await shop.PostJsonAsync("/api/v1/expenses/read", new { text = "bijli 3200 reading 14520" })).ReadAsync<List<ExpenseGuess>>();
        var line = Assert.Single(read);
        Assert.Equal((3200m, ExpenseCategory.Electricity), (line.Amount, line.Category));
        Assert.Contains("14520", line.Note);
    }

    [Fact]
    public async Task Goods_counted_in_before_the_bill_are_not_added_again_when_the_bill_comes()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 150m, 0m);
        var supplier = await SupplierAsync(shop, "Jain Traders");
        var helper = await HelperOfAsync(shop);

        // The boxes come with no bill: a helper counts 10 in. They are on the shelf; nothing is owed.
        var receipt = await (await helper.PostJsonAsync("/api/v1/goods-receipts", new { id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = pipe.Id, quantity = 10m } } }))
            .ReadAsync<GoodsReceiptDto>();
        Assert.Equal(10m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);
        Assert.Single(await shop.GetJsonAsync<List<GoodsReceiptDto>>($"/api/v1/goods-receipts?supplierId={supplier}&open=true"));

        // The bill says 12: the 10 already counted aren't added twice, only the 2 more.
        var billId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = billId, supplierId = supplier, billNo = "J-9", paidAmount = 0m, receiptIds = new[] { receipt.Id },
            lines = new[] { new { productId = pipe.Id, quantity = 12m, unitCost = 100m } },
        })).EnsureSuccessStatusCode();
        var item = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}");
        Assert.Equal((12m, 100m), (item.Stock, item.CostPrice!.Value));
        Assert.Empty(await shop.GetJsonAsync<List<GoodsReceiptDto>>($"/api/v1/goods-receipts?supplierId={supplier}&open=true"));

        // A billed receipt can't be cancelled; cancelling the bill takes off only the 2 it added and reopens the receipt.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/goods-receipts/{receipt.Id}/cancel", new { })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{billId}/cancel", new { reason = "Wrong bill" })).EnsureSuccessStatusCode();
        Assert.Equal(10m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);
        (await shop.PostJsonAsync($"/api/v1/goods-receipts/{receipt.Id}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);
    }

    [Fact]
    public async Task An_estimate_moves_no_stock_or_khata_and_is_marked_when_billed()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 150m, 20m);
        var estimate = await (await shop.PostJsonAsync("/api/v1/estimates", new
        {
            id = Guid.CreateVersion7(), partyName = "Sharma ji",
            lines = new object[] { new { productId = pipe.Id, quantity = 4m, unitPrice = 140m }, new { name = "Fitting labour", quantity = 1m, unit = "piece", unitPrice = 200m } },
        })).ReadAsync<EstimateDto>();
        Assert.Equal((760m, 1, "Pipe"), (estimate.Total, estimate.Number, estimate.Lines[0].Name));
        Assert.Equal(20m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);
        Assert.Single(await shop.GetJsonAsync<List<EstimateDto>>("/api/v1/estimates?open=true"));

        var bill = await (await Bill(shop, null, pipe.Id, 4m, 140m, 560m)).ReadAsync<BillDto>();
        (await shop.PostJsonAsync($"/api/v1/estimates/{estimate.Id}/billed", new { billId = bill.Id })).EnsureSuccessStatusCode();
        Assert.Empty(await shop.GetJsonAsync<List<EstimateDto>>("/api/v1/estimates?open=true"));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/estimates/{estimate.Id}/cancel", new { })).StatusCode);
    }

    [Fact]
    public async Task Review_fixes_measures_receipts_meter_and_estimates_hold()
    {
        var shop = await ShopAsync("hardware");

        // "meter" is a measure here: the rupees are the 200.
        var read = await (await shop.PostJsonAsync("/api/v1/expenses/read", new { text = "rassi 10 meter 200" })).ReadAsync<List<ExpenseGuess>>();
        var rope = Assert.Single(read);
        Assert.Equal(200m, rope.Amount);
        Assert.Contains("10 meter", rope.Note);
        var flour = Assert.Single(await (await shop.PostJsonAsync("/api/v1/expenses/read", new { text = "aata 2 kg 80" })).ReadAsync<List<ExpenseGuess>>());
        Assert.Equal(80m, flour.Amount);

        // A receipt can only close on a bill that has all its items.
        var pipe = await ItemAsync(shop, "Pipe", 150m, 0m);
        var tap = await ItemAsync(shop, "Tap", 90m, 0m);
        var supplier = await SupplierAsync(shop, "Jain Traders");
        var receipt = await (await shop.PostJsonAsync("/api/v1/goods-receipts", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = pipe.Id, quantity = 2m }, new { productId = tap.Id, quantity = 3m } },
        })).ReadAsync<GoodsReceiptDto>();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, receiptIds = new[] { receipt.Id }, lines = new[] { new { productId = pipe.Id, quantity = 2m, unitCost = 100m } },
        })).StatusCode);

        // A back-dated meter reading can't be above a later one.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        (await shop.PostJsonAsync("/api/v1/meter", new { id = Guid.CreateVersion7(), day = today.AddDays(-20).ToString("yyyy-MM-dd"), reading = 14500m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/meter", new { id = Guid.CreateVersion7(), day = today.ToString("yyyy-MM-dd"), reading = 14800m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/meter", new { id = Guid.CreateVersion7(), day = today.AddDays(-10).ToString("yyyy-MM-dd"), reading = 14900m })).StatusCode);

        // An estimate closes only on a real bill.
        var estimate = await (await shop.PostJsonAsync("/api/v1/estimates", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = pipe.Id, quantity = 1m, unitPrice = 150m } } })).ReadAsync<EstimateDto>();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/estimates/{estimate.Id}/billed", new { billId = Guid.CreateVersion7() })).StatusCode);
    }

    [Fact]
    public async Task The_week_report_ranks_items_and_lists_what_did_not_sell()
    {
        var (shop, _) = await api.SignUpAsync("Week Store");
        var soap = await ItemAsync(shop, "Soap", 40m, 50m);
        var oil = await ItemAsync(shop, "Oil", 200m, 10m);
        var idle = await ItemAsync(shop, "Umbrella", 300m, 4m);
        (await Bill(shop, null, soap.Id, 5m, 40m, 200m)).EnsureSuccessStatusCode();
        (await Bill(shop, null, oil.Id, 2m, 200m, 400m)).EnsureSuccessStatusCode();
        (await Bill(shop, null, soap.Id, 3m, 40m, 120m)).EnsureSuccessStatusCode();

        var week = await shop.GetJsonAsync<SalesRangeDto>("/api/v1/reports/range");
        Assert.Equal((720m, 3), (week.Sales, week.Bills));
        Assert.Equal(("Oil", 400m), (week.Items[0].Name, week.Items[0].Sales)); // most rupees first
        Assert.Equal((8m, 2), (week.Items.Single(i => i.Name == "Soap").Quantity, week.Items.Single(i => i.Name == "Soap").Bills));
        Assert.Contains(week.NotSold, u => u.ProductId == idle.Id);
        Assert.DoesNotContain(week.NotSold, u => u.ProductId == soap.Id);

        var csv = await (await shop.GetAsync("/api/v1/reports/items.csv")).Content.ReadAsStringAsync();
        Assert.Contains("Soap,piece,8,320,2,0,0", csv);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.GetAsync("/api/v1/reports/range?from=2026-01-01&to=2026-06-30")).StatusCode);
    }

    [Fact]
    public async Task Customer_patterns_show_spend_usual_items_and_who_is_new()
    {
        var (shop, _) = await api.SignUpAsync("Pattern Store");
        var milk = await ItemAsync(shop, "Milk", 30m, 100m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        for (var i = 0; i < 2; i++)
        {
            (await Bill(shop, customer, milk.Id, 2m, 30m, 60m)).EnsureSuccessStatusCode();
        }

        var report = await shop.GetJsonAsync<CustomerPatternsDto>("/api/v1/reports/customers");
        var asha = Assert.Single(report.Customers);
        Assert.Equal(("Asha", 120m, CustomerHabit.New), (asha.Name, asha.Spend, asha.Habit));
        Assert.Equal(["Milk"], asha.UsualItems);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await HelperOfAsync(shop)).GetAsync("/api/v1/reports/customers")).StatusCode);
    }

    [Fact]
    public async Task Barcodes_have_one_spelling_a_right_check_digit_and_one_item_each()
    {
        var shop = await ShopAsync("hardware");
        var a = await ItemAsync(shop, "Glue", 50m, 5m);
        var b = await ItemAsync(shop, "Tape", 30m, 5m);

        // A 12-digit UPC-A is kept as the 13-digit EAN with its leading 0, the way cameras read it.
        var saved = await (await shop.PutAsJsonAsync($"/api/v1/products/{a.Id}/barcode", new { barcode = "012345678905" }, ApiFactory.Json)).ReadAsync<System.Text.Json.JsonElement>();
        Assert.Equal("0012345678905", saved.GetProperty("barcode").GetString());

        // A wrong last digit is a misread; the same code can't go on a second item.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/products/{b.Id}/barcode", new { barcode = "8901234567891" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/products/{b.Id}/barcode", new { barcode = "0012345678905" }, ApiFactory.Json)).StatusCode);

        // A new item can be made with the code a scan didn't know.
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name = "Fevicol 50g", unit = "piece", sellingPrice = 45m, barcode = "8901030865336" })).EnsureSuccessStatusCode();
        Assert.Equal("8901030865336", (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{id}")).Barcode);
    }

    [Fact]
    public async Task A_registered_shop_can_choose_a_bill_without_gst()
    {
        var (shop, _) = await api.SignUpAsync("Choice Store");
        (await shop.PutAsJsonAsync("/api/v1/shop", new { name = "Choice Store", gstin = "08ABCDE1234F1Z0" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var shirt = await ItemAsync(shop, "Shirt", 1199m, 5m);
        (await shop.PutAsJsonAsync($"/api/v1/products/{shirt.Id}", new { id = shirt.Id, name = "Shirt", unit = "piece", sellingPrice = 1199m, gstRate = 5m, replaceTax = true }, ApiFactory.Json)).EnsureSuccessStatusCode();

        object Body(bool withoutGst) => new { id = Guid.CreateVersion7(), lines = new[] { new { productId = shirt.Id, quantity = 1m, unitPrice = 1199m } }, paidAmount = 1199m, paymentMode = "cash", withoutGst };
        var gstBill = await (await shop.PostJsonAsync("/api/v1/bills", Body(false))).ReadAsync<BillDto>();
        var plain = await (await shop.PostJsonAsync("/api/v1/bills", Body(true))).ReadAsync<BillDto>();
        Assert.Equal("08ABCDE1234F1Z0", gstBill.Gstin);
        Assert.Null(plain.Gstin);
        Assert.Equal(1199m, plain.Total);
    }

    [Fact]
    public async Task The_day_report_lists_every_bill_with_its_items_and_keeps_cancelled_ones_apart()
    {
        var (shop, _) = await api.SignUpAsync("Day Store");
        var soap = await ItemAsync(shop, "Soap", 40m, 50m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        (await Bill(shop, null, soap.Id, 2m, 40m, 80m)).EnsureSuccessStatusCode();
        (await Bill(shop, customer, soap.Id, 5m, 40m, 50m)).EnsureSuccessStatusCode();
        var wrong = await (await Bill(shop, null, soap.Id, 1m, 40m, 40m)).ReadAsync<BillDto>();
        (await shop.PostJsonAsync($"/api/v1/bills/{wrong.Id}/cancel", new { reason = "Made by mistake" })).EnsureSuccessStatusCode();

        var day = await shop.GetJsonAsync<DayReportDto>("/api/v1/reports/day");
        Assert.Equal((2, 280m, 130m, 150m), (day.Totals.Bills, day.Totals.Sales, day.Totals.Cash, day.Totals.OnKhata));
        Assert.Contains(day.Bills, b => b.Customer == "Asha" && b.ItemsText == "Soap × 5" && b.OnKhata == 150m);
        Assert.Equal("Made by mistake", Assert.Single(day.Cancelled).CancelReason);
        Assert.Equal(7m, day.Items.Single().Quantity);

        // The cancelled bill stays on its day in the cash book, and its money goes back on the day it was cancelled.
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Contains(book.Lines, l => l.Source == CashBookSource.Bill && l.BillId == wrong.Id && l.Amount == 40m);
        Assert.Contains(book.Lines, l => l.Source == CashBookSource.BillCancelled && l.BillId == wrong.Id && l.Amount == -40m);
        Assert.Equal(130m, book.Cash);
    }

    [Fact]
    public async Task A_customer_payment_cannot_be_booked_as_bank()
    {
        var (shop, _) = await api.SignUpAsync("Bank Store");
        var soap = await ItemAsync(shop, "Soap", 40m, 50m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), lines = new[] { new { productId = soap.Id, quantity = 1m, unitPrice = 40m } }, paidAmount = 40m, paymentMode = "bank",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 100m, paymentMode = "bank" })).StatusCode);
    }

    [Fact]
    public async Task A_bill_paid_part_cash_part_upi_counts_each_part_under_its_own_mode()
    {
        var (shop, _) = await api.SignUpAsync("Split Store");
        var saree = await ItemAsync(shop, "Saree", 1000m, 5m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Meena");
        object Body(decimal paid, object[] payments) => new
        {
            id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { productId = saree.Id, quantity = 1m, unitPrice = 1000m } },
            paidAmount = paid, paymentMode = "cash", payments,
        };

        // Parts that don't add up to what was paid are refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Body(800m, [new { mode = "cash", amount = 500m }, new { mode = "upi", amount = 200m }]))).StatusCode);

        var bill = await (await shop.PostJsonAsync("/api/v1/bills", Body(800m, [new { mode = "cash", amount = 500m }, new { mode = "upi", amount = 300m, reference = "UPI123456" }]))).ReadAsync<BillDto>();
        Assert.Equal((800m, 200m, 2), (bill.PaidAmount, bill.OnKhata, bill.Payments!.Count));

        var today = await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today");
        Assert.Equal((500m, 300m), (today.Cash, today.Upi));
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((500m, 300m), (book.Cash, book.Upi));
        var day = await shop.GetJsonAsync<DayReportDto>("/api/v1/reports/day");
        Assert.Equal((500m, 300m, 200m), (day.Totals.Cash, day.Totals.Upi, day.Totals.OnKhata));

        // The khata shows the bill, then one payment line per way it was paid; Meena owes the ₹200 left.
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal(200m, statement.ClosingBalance);
    }

    [Fact]
    public async Task A_cheque_lowers_the_khata_at_once_but_counts_as_money_only_once_cleared_and_a_bounce_reopens_it()
    {
        var (shop, _) = await api.SignUpAsync("Cheque Store");
        var saree = await ItemAsync(shop, "Saree", 1000m, 5m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Meena");
        // A ₹1,000 bill on khata, ₹200 paid by card at the counter.
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { productId = saree.Id, quantity = 1m, unitPrice = 1000m } },
            paidAmount = 200m, paymentMode = "card",
        })).ReadAsync<BillDto>();
        Assert.Equal((200m, 800m), (bill.PaidAmount, bill.OnKhata));

        // A cheque needs its number; a bill can't be paid by cheque.
        object Cheque(string? no) => new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 500m, paymentMode = "cheque", chequeNo = no, chequeBank = "SBI" };
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/khata-entries", Cheque(null))).StatusCode);
        var first = await (await shop.PostJsonAsync("/api/v1/khata-entries", Cheque("123456"))).ReadAsync<KhataEntryResponse>();
        Assert.Equal(300m, first.CustomerBalance);

        // Received the day it comes in, and shown as pending until the bank clears it.
        var today = await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today");
        Assert.Equal((200m, 500m, 500m, 500m, 1), (today.Card, today.Cheque, today.Collected, today.ChequesPending, today.ChequesPendingCount));

        // Cleared: still received, no longer pending.
        Assert.Equal(HttpStatusCode.Created, (await shop.PostJsonAsync($"/api/v1/cheques/{first.Entry.Id}/clear", new { })).StatusCode);
        today = await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today");
        Assert.Equal((500m, 500m, 0m), (today.Cheque, today.Collected, today.ChequesPending));

        // A second cheque bounces: the ₹500 goes back on Meena's khata, and the bounce takes it back out of received.
        var second = await (await shop.PostJsonAsync("/api/v1/khata-entries", Cheque("654321"))).ReadAsync<KhataEntryResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/cheques/{second.Entry.Id}/bounce", new { reason = "" })).StatusCode);
        var bounced = await (await shop.PostJsonAsync($"/api/v1/cheques/{second.Entry.Id}/bounce", new { reason = "Funds insufficient" })).ReadAsync<KhataEntryResponse>();
        Assert.Equal(300m, bounced.CustomerBalance);
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/cheques/{second.Entry.Id}/clear", new { })).StatusCode);

        today = await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today");
        Assert.Equal((500m, 500m, 0), (today.Cheque, today.Collected, today.ChequesPendingCount));
        var list = await shop.GetJsonAsync<ChequeListDto>("/api/v1/cheques");
        Assert.Equal([ChequeStatus.Cleared, ChequeStatus.Bounced], list.Cheques.Select(c => c.Status).Order());
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal(300m, statement.ClosingBalance);
        Assert.Contains(statement.Entries, e => e.ChequeNo == "654321" && e.Kind == LedgerEntryKind.Reversal);

        // The cash book shows a cheque that hasn't cleared as pending.
        var third = await (await shop.PostJsonAsync("/api/v1/khata-entries", Cheque("777777"))).ReadAsync<KhataEntryResponse>();
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var book = await shop.GetJsonAsync<CashBookDto>($"/api/v1/cash-book?date={day:yyyy-MM-dd}");
        Assert.Single(book.Lines, l => l.Mode == PaymentMode.Cheque && l.ChequePending);
    }

    [Fact]
    public async Task The_bank_book_runs_from_the_passbook_balance_with_upi_card_cleared_cheques_and_cash_deposits()
    {
        var (shop, _) = await api.SignUpAsync("Bank Book Store");
        var saree = await ItemAsync(shop, "Saree", 1000m, 10m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Meena");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PutAsJsonAsync("/api/v1/bank-book/opening", new { amount = 10000m, date = today.AddDays(-1).ToString("yyyy-MM-dd") })).StatusCode);

        object Bill(decimal paid, string mode, object[]? payments = null) => new
        {
            id = Guid.CreateVersion7(), customerId = (Guid?)null, lines = new[] { new { productId = saree.Id, quantity = 1m, unitPrice = 1000m } },
            paidAmount = paid, paymentMode = mode, payments,
        };
        await shop.PostJsonAsync("/api/v1/bills", Bill(1000m, "upi"));
        await shop.PostJsonAsync("/api/v1/bills", Bill(1000m, "card"));
        await shop.PostJsonAsync("/api/v1/bills", Bill(1000m, "cash", [new { mode = "cash", amount = 600m }, new { mode = "upi", amount = 400m }]));
        await shop.PostJsonAsync("/api/v1/bills", Bill(1000m, "cash")); // all cash: not in the bank
        // A cheque counts once cleared; a pending one doesn't.
        var cheque = await (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 700m, paymentMode = "cheque", chequeNo = "111111" })).ReadAsync<KhataEntryResponse>();
        await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 300m, paymentMode = "cheque", chequeNo = "222222" });
        await shop.PostJsonAsync($"/api/v1/cheques/{cheque.Entry.Id}/clear", new { });
        // ₹2,000 of cash put in the bank, and ₹250 paid out by UPI.
        await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount = 2000m, category = "deposit", note = "SBI deposit", mode = "cash" });
        await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount = 250m, note = "electrician", mode = "upi" });

        var book = await shop.GetJsonAsync<BankBookDto>($"/api/v1/bank-book?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Equal(10000m, book.Opening);
        Assert.Equal(new BankBookIn(1400m, 1000m, 700m, 2000m), book.InBy);
        Assert.Equal((5100m, 250m, 14850m), (book.In, book.Out, book.Closing));
        Assert.Equal(book.Closing, book.Lines[^1].Balance);

        // The next day opens where today closed.
        var tomorrow = await shop.GetJsonAsync<BankBookDto>($"/api/v1/bank-book?from={today.AddDays(1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}");
        Assert.Equal(14850m, tomorrow.Opening);
    }

    [Fact]
    public async Task Cash_today_adds_up_one_customers_cash_bills_and_cash_khata_payments_but_not_upi()
    {
        var (shop, _) = await api.SignUpAsync("Cash Limit Store");
        var gold = await ItemAsync(shop, "Gold coin", 100000m, 5m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Meena");
        await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { productId = gold.Id, quantity = 2m, unitPrice = 100000m } },
            paidAmount = 150000m, paymentMode = "cash", payments = new[] { new { mode = "cash", amount = 120000m }, new { mode = "upi", amount = 30000m } },
        });
        await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 40000m, paymentMode = "cash" });
        await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 10000m, paymentMode = "upi" });

        var today = await shop.GetJsonAsync<CashTodayDto>($"/api/v1/customers/{customer}/cash-today");
        Assert.Equal((160000m, 200000m), (today.Cash, today.Limit));
    }

    [Fact]
    public async Task Card_money_can_go_to_a_second_bank_account_and_a_bills_utr_shows_on_the_khata()
    {
        var (shop, _) = await api.SignUpAsync("Two Banks Store");
        var saree = await ItemAsync(shop, "Saree", 1000m, 10m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Meena");
        var setup = await (await shop.PutAsJsonAsync("/api/v1/bank-book/accounts", new
        {
            accounts = new[] { new { id = "main", name = "SBI" }, new { id = "", name = "HDFC" } },
            upi = "main", card = "#1", cheque = "main", @out = "main", deposit = "main",
        })).ReadAsync<BankSetup>();
        var hdfc = setup.Accounts[1].Id;
        Assert.Equal((2, hdfc), (setup.Accounts.Count, setup.Card));

        object Bill(Guid? who, decimal paid, string mode, object[]? payments = null) => new
        {
            id = Guid.CreateVersion7(), customerId = who, lines = new[] { new { productId = saree.Id, quantity = 1m, unitPrice = 1000m } },
            paidAmount = paid, paymentMode = mode, payments,
        };
        // A bad UTR is refused; a good one is kept on the bill and on the khata line.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Bill(customer, 1000m, "upi", [new { mode = "upi", amount = 1000m, reference = "12" }]))).StatusCode);
        var upiBill = await (await shop.PostJsonAsync("/api/v1/bills", Bill(customer, 1000m, "upi", [new { mode = "upi", amount = 1000m, reference = "utr998877" }]))).ReadAsync<BillDto>();
        Assert.Equal("UTR998877", Assert.Single(upiBill.Payments!).Reference);
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Contains(statement.Entries, e => e.UpiRef == "UTR998877");

        // Cash + card on one bill: the card part lands in HDFC, the UPI bill in SBI.
        await shop.PostJsonAsync("/api/v1/bills", Bill(null, 1000m, "cash", [new { mode = "cash", amount = 400m }, new { mode = "card", amount = 600m }]));
        var sbi = await shop.GetJsonAsync<BankBookDto>("/api/v1/bank-book?account=main");
        var second = await shop.GetJsonAsync<BankBookDto>($"/api/v1/bank-book?account={hdfc}");
        Assert.Equal((1000m, 600m), (sbi.In, second.In));
        Assert.Equal(["SBI", "HDFC"], second.Accounts!.Select(a => a.Name));
    }

    [Fact]
    public async Task Cash_moved_by_hand_and_to_the_bank_shows_in_the_cash_ledger_with_a_running_balance()
    {
        var (shop, _) = await api.SignUpAsync("Cash Ledger Store");
        var saree = await ItemAsync(shop, "Saree", 1000m, 10m);
        await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = (Guid?)null, lines = new[] { new { productId = saree.Id, quantity = 3m, unitPrice = 1000m } },
            paidAmount = 3000m, paymentMode = "cash",
        });
        object Move(string kind, decimal amount, string? note) => new { id = Guid.CreateVersion7(), kind, amount, note };
        // Added or removed by hand needs a reason.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/cash-moves", Move("added", 500m, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await shop.PostJsonAsync("/api/v1/cash-moves", Move("added", 500m, "Change brought from home"))).StatusCode);
        var removed = await (await shop.PostJsonAsync("/api/v1/cash-moves", Move("removed", 200m, "Kept aside"))).ReadAsync<CashMoveDto>();
        await shop.PostJsonAsync("/api/v1/cash-moves", Move("to_bank", 2000m, "SBI deposit"));
        await shop.PostJsonAsync("/api/v1/cash-moves", Move("from_bank", 300m, null));

        // 3,000 + 500 − 200 − 2,000 + 300 = 1,600 in hand.
        var ledger = await shop.GetJsonAsync<CashLedgerDto>("/api/v1/cash-ledger");
        Assert.Equal((0m, 1600m), (ledger.Opening, ledger.Closing));
        Assert.Equal(1600m, ledger.Lines[^1].Balance);
        var count = await shop.GetJsonAsync<CashCountDto>("/api/v1/cash-count");
        Assert.Equal(1600m, count.Expected);

        // The bank book sees the deposit in and the withdrawal out.
        var bank = await shop.GetJsonAsync<BankBookDto>("/api/v1/bank-book");
        Assert.Equal((2000m, 300m), (bank.InBy.Deposits, bank.Out));

        // Striking a move takes it out everywhere.
        await shop.PostJsonAsync($"/api/v1/cash-moves/{removed.Id}/strike", new { });
        Assert.Equal(1800m, (await shop.GetJsonAsync<CashLedgerDto>("/api/v1/cash-ledger")).Closing);
    }

    [Fact]
    public async Task A_paid_walk_in_bill_can_go_on_a_khata_later_and_udhaar_can_wait_for_a_name()
    {
        var (shop, _) = await api.SignUpAsync("Walk In Store");
        var saree = await ItemAsync(shop, "Saree", 1000m, 100m);
        var meena = await KhataTests.CreateCustomerAsync(shop, "Meena");
        object Bill(Guid? who, decimal qty, decimal paid) => new
        {
            id = Guid.CreateVersion7(), customerId = who, lines = new[] { new { productId = saree.Id, quantity = qty, unitPrice = 1000m } },
            paidAmount = paid, paymentMode = "cash",
        };

        // A paid walk-in bill, put on Meena's khata later: her statement shows it, her balance doesn't move.
        var walkIn = await (await shop.PostJsonAsync("/api/v1/bills", Bill(null, 2m, 2000m))).ReadAsync<BillDto>();
        var attached = await (await shop.PostJsonAsync($"/api/v1/bills/{walkIn.Id}/attach-customer", new { customerId = meena, reason = (string?)null })).ReadAsync<BillDto>();
        Assert.Equal(meena, attached.CustomerId);
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{meena}/statement");
        Assert.Equal((0m, 2), (statement.ClosingBalance, statement.Entries.Count));
        // Once only; the cash book is unchanged.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/bills/{walkIn.Id}/attach-customer", new { customerId = Guid.CreateVersion7(), reason = (string?)null })).StatusCode);
        Assert.Equal(2000m, (await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book")).Cash);

        // Udhaar to someone not named yet.
        var unnamed = await (await shop.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = (string?)null, phone = (string?)null, nameLater = true })).ReadAsync<CustomerDto>();
        Assert.True(unnamed.NameLater);
        Assert.StartsWith("Name later", unnamed.Name);
        await shop.PostJsonAsync("/api/v1/bills", Bill(unnamed.Id, 1m, 0m));
        // A ₹50,000 bill needs the buyer named.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Bill(unnamed.Id, 50m, 0m))).StatusCode);
        var today = await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today");
        Assert.Equal((1000m, 1), (today.NameLaterDue, today.NameLaterCount));

        // Named at last.
        Assert.Equal(HttpStatusCode.OK, (await shop.PutAsJsonAsync($"/api/v1/customers/{unnamed.Id}", new { name = "Ramesh", phone = (string?)null })).StatusCode);
        Assert.Equal(0, (await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today")).NameLaterCount);
    }

    [Fact]
    public async Task A_name_later_khata_moves_into_the_customer_it_turned_out_to_be()
    {
        var (shop, _) = await api.SignUpAsync("Move Khata Store");
        var rice = await ItemAsync(shop, "Rice", 100m, 100m);
        var meena = await KhataTests.CreateCustomerAsync(shop, "Meena");
        var unnamed = await (await shop.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = (string?)null, phone = (string?)null, nameLater = true })).ReadAsync<CustomerDto>();
        object Bill(Guid who, decimal paid) => new
        {
            id = Guid.CreateVersion7(), customerId = who, lines = new[] { new { productId = rice.Id, quantity = 3m, unitPrice = 100m } },
            paidAmount = paid, paymentMode = "cash",
        };
        await shop.PostJsonAsync("/api/v1/bills", Bill(unnamed.Id, 0m));
        await shop.PostJsonAsync("/api/v1/bills", Bill(meena, 100m));

        // A real customer can't be folded into another.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/customers/{meena}/move-into", new { customerId = unnamed.Id })).StatusCode);

        var moved = await (await shop.PostJsonAsync($"/api/v1/customers/{unnamed.Id}/move-into", new { customerId = meena })).ReadAsync<CustomerDto>();
        Assert.Equal(500m, moved.Balance);
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{meena}/statement");
        Assert.Equal(500m, statement.ClosingBalance);
        Assert.Equal(HttpStatusCode.NotFound, (await shop.PostJsonAsync($"/api/v1/customers/{unnamed.Id}/move-into", new { customerId = meena })).StatusCode);

        // The cash rule counts a given day.
        var cash = await shop.GetJsonAsync<CashTodayDto>($"/api/v1/customers/{meena}/cash-today?day=2020-01-01");
        Assert.Equal(0m, cash.Cash);
    }
    [Fact]
    public async Task The_owner_can_stop_a_helper_giving_udhaar_or_seeing_reports_and_bills_keep_their_counter()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Mixer", 500m, 5m);
        var customer = await KhataTests.CreateCustomerAsync(shop, "Asha");
        var helper = await HelperOfAsync(shop);
        var raju = (await shop.GetJsonAsync<List<GroceryKhata.Api.Shops.StaffDto>>("/api/v1/shop/staff")).Single(s => s.Name == "Raju");

        // Nothing stopped yet: udhaar and reports work.
        Assert.Equal(HttpStatusCode.Created, (await Bill(helper, customer, item.Id, 1m, 500m, 0m)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await helper.GetAsync("/api/v1/reports/month")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync($"/api/v1/shop/staff/{raju.MembershipId}/limits", new { blocked = new[] { "udhaar", "reports" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Bill(helper, customer, item.Id, 1m, 500m, 0m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.GetAsync("/api/v1/reports/month")).StatusCode);
        Assert.Equal(new[] { "udhaar", "reports" }, await helper.GetJsonAsync<string[]>("/api/v1/shop/my-limits"));
        // Paid in full still bills; the owner isn't limited.
        Assert.Equal(HttpStatusCode.Created, (await Bill(helper, customer, item.Id, 1m, 500m, 500m)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await shop.GetAsync("/api/v1/reports/month")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/shop/staff/{raju.MembershipId}/limits", new { blocked = new[] { "everything" } })).StatusCode);

        // A bill remembers the counter it was made on.
        var counted = await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = (Guid?)null, lines = new[] { new { productId = item.Id, quantity = 1m, unitPrice = 500m } }, paidAmount = 500m, paymentMode = "cash", counter = "Counter 2",
        })).ReadAsync<BillDto>();
        Assert.Equal("Counter 2", counted.Counter);
        Assert.Equal("Counter 2", (await shop.GetJsonAsync<BillView>($"/api/v1/bills/{counted.Id}")).Bill.Counter);
    }
    [Fact]
    public async Task The_stock_account_shows_every_movement_values_stock_and_keeps_counts()
    {
        var shop = await ShopAsync("hardware");
        var item = await ItemAsync(shop, "Cement", 400m, 10m);
        // Two purchases at different prices: the cost averages in, it is not overwritten.
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = item.Id, kind = "purchase", quantity = 10m, costPrice = 300m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = item.Id, kind = "purchase", quantity = 10m, costPrice = 360m })).EnsureSuccessStatusCode();
        Assert.Equal(320m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{item.Id}")).CostPrice);
        (await Bill(shop, null, item.Id, 5m, 400m, 2000m)).EnsureSuccessStatusCode();
        // A loss the GST stock account asks for.
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = item.Id, kind = "adjustment", quantity = -1m, reason = "stolen" })).EnsureSuccessStatusCode();

        var ledger = await shop.GetJsonAsync<ItemLedger>($"/api/v1/products/{item.Id}/ledger");
        Assert.Equal((0m, 30m, 6m, 24m), (ledger.Opening, ledger.In, ledger.Out, ledger.Closing));
        Assert.Equal(24m, ledger.Lines[^1].Balance);
        Assert.Contains(ledger.Lines, l => l.DocType == "bill" && l.DocLabel!.StartsWith("Bill"));
        Assert.All(ledger.Lines, l => Assert.NotEqual("—", l.By));

        var statement = await shop.GetJsonAsync<StockStatement>("/api/v1/reports/stock");
        var line = Assert.Single(statement.Items, i => i.ProductId == item.Id);
        Assert.Equal((20m, 5m, 1m, 24m), (line.Purchased - 10m, line.Sold, line.Lost, line.Closing));
        Assert.Equal(24m * 320m, line.Value);

        // A helper can't write off more than the limit at cost; the owner can.
        var helper = await HelperOfAsync(shop);
        var big = new { id = Guid.CreateVersion7(), productId = item.Id, kind = "adjustment", quantity = -10m, reason = "damage" };
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/stock-movements", big)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await shop.PostJsonAsync("/api/v1/stock-movements", big)).StatusCode);

        // A count is kept, with what it found.
        var takeId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/stocktakes", new { id = takeId, counts = new[] { new { productId = item.Id, counted = 12m } } })).EnsureSuccessStatusCode();
        var takes = await shop.GetJsonAsync<List<StocktakeSummary>>("/api/v1/stocktakes");
        Assert.Equal((1, 1, -640m), (takes[0].Number, takes[0].Differences, takes[0].ValueDifference));
        var view = await shop.GetJsonAsync<StocktakeView>($"/api/v1/stocktakes/{takeId}");
        Assert.Equal((14m, 12m), (view.Lines[0].Books, view.Lines[0].Counted));

        // Sales by category; days of stock left come from the average daily sale.
        var by = await shop.GetJsonAsync<SalesBy>("/api/v1/reports/sales-by?by=category");
        Assert.Equal(2000m, by.Total);
        Assert.True((await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Id == item.Id).DailySales > 0);
    }
    [Fact]
    public async Task Bags_and_weight_travel_together_through_receipts_bills_and_sales()
    {
        var shop = await ShopAsync("kirana");
        var rice = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = rice, name = "Sona Masoori rice", unit = "kg", sellingPrice = 60m, bagSize = 50m })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Mandi Traders" })).EnsureSuccessStatusCode();

        // 40 bags came, weighing 1,985 kg (not the 2,000 the bags say), on a truck with a weighbridge slip.
        var receipt = await (await shop.PostJsonAsync("/api/v1/goods-receipts", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = rice, quantity = 1985m, bags = 40m } },
            vehicleNo = "rj14 ga 1234", bilty = "LR-5521", transporter = "Shree Logistics", grossWeight = 9985m, tareWeight = 8000m,
        })).ReadAsync<GoodsReceiptDto>();
        Assert.Equal(("RJ14 GA 1234", 40m), (receipt.VehicleNo, receipt.Lines[0].Bags));
        var item = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{rice}");
        Assert.Equal((1985m, 40m, 50m), (item.Stock, item.Bags, item.BagSize));

        // Two bags sold whole, then one bag opened to sell loose.
        (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = (Guid?)null, lines = new[] { new { productId = rice, quantity = 100m, unitPrice = 60m, bags = 2m } }, paidAmount = 6000m, paymentMode = "cash",
        })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync($"/api/v1/products/{rice}/open-bag", new { id = Guid.CreateVersion7(), bags = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/products/{rice}/open-bag", new { id = Guid.CreateVersion7(), bags = 99m })).StatusCode);

        // A supplier bill straight to stock, with its bags; weight lost in storage.
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, billNo = "M-9", lines = new[] { new { productId = rice, quantity = 500m, unitCost = 40m, bags = 10m } }, paidAmount = 0m,
        })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = rice, kind = "adjustment", quantity = -5m, reason = "weight_loss" })).EnsureSuccessStatusCode();

        item = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{rice}");
        Assert.Equal((2380m, 47m), (item.Stock, item.Bags));
        var ledger = await shop.GetJsonAsync<ItemLedger>($"/api/v1/products/{rice}/ledger");
        Assert.Equal((50m, 47m), (ledger.BagSize, ledger.ClosingBags));
        Assert.Contains(ledger.Lines, l => l.Kind == StockMovementKind.OpenBag && l.Bags == -1m && l.Out == 0);
        var statement = await shop.GetJsonAsync<StockStatement>("/api/v1/reports/stock");
        Assert.Equal(47m, statement.Items.Single(i => i.ProductId == rice).ClosingBags);
    }
    [Fact]
    public async Task Order_today_comes_from_sales_and_the_suppliers_delivery_time_and_phones_can_ask_for_it()
    {
        var shop = await ShopAsync("kirana");
        var atta = await ItemAsync(shop, "Atta 10 kg", 400m, 0m);
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Agarwal Flour" })).EnsureSuccessStatusCode();
        // Ordered and received: the supplier's delivery time is measured from this (at least a day).
        var orderId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = orderId, supplierId = supplier, lines = new[] { new { productId = atta.Id, quantity = 10m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = Guid.CreateVersion7(), supplierId = supplier, orderId, lines = new[] { new { productId = atta.Id, quantity = 10m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, lines = new[] { new { productId = atta.Id, quantity = 0.001m, unitCost = 300m } }, paidAmount = 0m })).EnsureSuccessStatusCode();
        Assert.Equal(0, (await shop.GetJsonAsync<OrderTodayDto>("/api/v1/stock/order-today")).Count);

        // Nine sold this month: 0.3 a day, so one left won't last until a new order comes.
        (await Bill(shop, null, atta.Id, 9.001m, 400m, 3600.40m)).EnsureSuccessStatusCode();
        // On the shelf all month (the rate counts only days with stock): the stock came in 40 days ago.
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var back = new Npgsql.NpgsqlCommand("UPDATE stock_movements SET occurred_at = now() - interval '40 days' WHERE product_id = @p AND kind <> 'Sale'", db);
            back.Parameters.AddWithValue("p", atta.Id);
            await back.ExecuteNonQueryAsync();
        }

        var today = await shop.GetJsonAsync<OrderTodayDto>("/api/v1/stock/order-today");
        var line = Assert.Single(today.Items);
        Assert.Equal((atta.Id, 1, 3.3m), (line.ProductId, line.LeadDays, line.DaysLeft));
        Assert.True(line.Suggested >= 9);

        // A phone asks for the morning reminder: the server's key, then its push address (https only).
        var key = await shop.GetJsonAsync<System.Text.Json.JsonElement>("/api/v1/push/key");
        Assert.True(key.GetProperty("key").GetString()!.Length > 40);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/push/subscribe", new { endpoint = "http://push.example/abc", p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM", auth = "tBHItJI5svbpez7KI4CCXg" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync("/api/v1/push/subscribe", new { endpoint = "https://push.example/abc", p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM", auth = "tBHItJI5svbpez7KI4CCXg" })).StatusCode);
    }
    [Fact]
    public async Task Sale_orders_hold_stock_show_who_is_short_and_count_down_as_bills_deliver()
    {
        var shop = await ShopAsync("building_materials");
        var cement = await ItemAsync(shop, "Cement OPC 53", 400m, 100m);
        var contractor = await KhataTests.CreateCustomerAsync(shop, "Verma Builders");
        object Order(Guid id, decimal qty, string? party = null) => new
        {
            id, customerId = party is null ? contractor : (Guid?)null, partyName = party, lines = new[] { new { productId = cement.Id, quantity = qty, unitPrice = 380m } },
        };

        // 80 bags booked first, then 60 by someone else: 100 on the shelf, so the second order is 40 short.
        var first = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/sale-orders", Order(first, 80m))).EnsureSuccessStatusCode();
        var second = await (await shop.PostJsonAsync("/api/v1/sale-orders", Order(Guid.CreateVersion7(), 60m, "Site 4 Mukesh"))).ReadAsync<SaleOrderDto>();
        Assert.Equal((60m, 40m), (second.Lines[0].Pending, second.Lines[0].ShortBy));
        Assert.Equal(140m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Promised);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/sale-orders", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = cement.Id, quantity = 1m, unitPrice = 1m } } })).StatusCode);

        // 30 bags delivered on a bill: the first order has 50 to go; the same bill twice counts once.
        var bill = await (await Bill(shop, contractor, cement.Id, 30m, 380m, 0m)).ReadAsync<BillDto>();
        var after = await (await shop.PostJsonAsync($"/api/v1/sale-orders/{first}/delivered", new { billId = bill.Id })).ReadAsync<SaleOrderDto>();
        Assert.Equal(("part", 50m, 30m), (after.State, after.Lines[0].Pending, after.Lines[0].Delivered));
        after = await (await shop.PostJsonAsync($"/api/v1/sale-orders/{first}/delivered", new { billId = bill.Id })).ReadAsync<SaleOrderDto>();
        Assert.Equal(50m, after.Lines[0].Pending);
        // Partly delivered can't be cancelled, only closed short; then it holds nothing.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/sale-orders/{first}/cancel", new { })).StatusCode);
        Assert.Equal("closed", (await (await shop.PostJsonAsync($"/api/v1/sale-orders/{first}/close", new { })).ReadAsync<SaleOrderDto>()).State);
        var open = await shop.GetJsonAsync<List<SaleOrderDto>>("/api/v1/sale-orders?open=true");
        var left = Assert.Single(open);
        Assert.Equal((second.Id, 0m), (left.Id, left.Lines[0].ShortBy));
        Assert.Equal(60m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{cement.Id}")).Promised);
    }
    [Fact]
    public async Task Stock_health_ranks_items_and_shows_where_the_money_sits()
    {
        var shop = await ShopAsync("kirana");
        var oil = await ItemAsync(shop, "Mustard oil", 200m, 0m);
        var soap = await ItemAsync(shop, "Soap", 40m, 0m);
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Gupta Agency" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m,
            lines = new object[] { new { productId = oil.Id, quantity = 50m, unitCost = 150m }, new { productId = soap.Id, quantity = 100m, unitCost = 30m } },
        })).EnsureSuccessStatusCode();
        // Oil sells; soap sits.
        (await Bill(shop, null, oil.Id, 40m, 200m, 8000m)).EnsureSuccessStatusCode();

        var health = await shop.GetJsonAsync<StockHealthDto>("/api/v1/reports/stock-health");
        var oilLine = health.Items.Single(i => i.ProductId == oil.Id);
        var soapLine = health.Items.Single(i => i.ProductId == soap.Id);
        Assert.Equal(("A", "fast", 1500m), (oilLine.Rank, oilLine.Movement, oilLine.Value));
        // Just bought and not sold yet: new, not dead, until it has sat a month.
        Assert.Equal(("C", "new", 3000m), (soapLine.Rank, soapLine.Movement, soapLine.Value));
        Assert.Equal(4500m, health.Value);
        Assert.Equal(4500m, health.Ageing.Single(a => a.Label == "0-30").Value);
        Assert.True(health.Turnover > 0);
    }
    [Fact]
    public async Task Goods_receipt_against_an_order_counts_it_down_once_through_the_bill()
    {
        var shop = await ShopAsync("kirana");
        var sugar = await ItemAsync(shop, "Sugar", 45m, 0m);
        var supplierId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplierId, name = "Agarwal Traders" })).EnsureSuccessStatusCode();
        var orderId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = orderId, supplierId, lines = new[] { new { productId = sugar.Id, quantity = 50m } }, expectedOn = "2030-01-05", note = "Tuesday truck" })).EnsureSuccessStatusCode();

        var receiptId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = receiptId, supplierId, orderId, lines = new[] { new { productId = sugar.Id, quantity = 30m } } })).EnsureSuccessStatusCode();
        async Task<PurchaseOrderDto> Order() => (await shop.GetJsonAsync<List<PurchaseOrderDto>>($"/api/v1/suppliers/orders?supplierId={supplierId}&all=true")).Single();
        var afterReceipt = await Order();
        Assert.Equal((30m, 20m, "Tuesday truck"), (afterReceipt.Lines[0].Received, afterReceipt.Pending, afterReceipt.Note));
        Assert.Equal(new DateOnly(2030, 1, 5), afterReceipt.ExpectedOn);

        // The bill for those goods plus 5 more that came with it: only the 5 count again.
        var billId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = billId, supplierId, orderId, receiptIds = new[] { receiptId }, paidAmount = 0m,
            lines = new[] { new { productId = sugar.Id, quantity = 35m, unitCost = 40m } },
        })).EnsureSuccessStatusCode();
        Assert.Equal(35m, (await Order()).Lines[0].Received);

        // Cancelling the bill takes back only its own 5: the receipt's 30 are still on the shelf.
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{billId}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(30m, (await Order()).Lines[0].Received);
    }

    [Fact]
    public async Task Cancelled_supplier_bill_comes_off_purchased_in_the_stock_statement()
    {
        var shop = await ShopAsync("kirana");
        var dal = await ItemAsync(shop, "Toor dal", 140m, 0m);
        var supplierId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplierId, name = "Shree Wholesale" })).EnsureSuccessStatusCode();
        async Task<Guid> Bill(decimal qty)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id, supplierId, paidAmount = 0m, lines = new[] { new { productId = dal.Id, quantity = qty, unitCost = 120m } } })).EnsureSuccessStatusCode();
            return id;
        }

        await Bill(20m);
        var mistake = await Bill(10m);
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{mistake}/cancel", new { })).EnsureSuccessStatusCode();

        var line = (await shop.GetJsonAsync<StockStatement>("/api/v1/reports/stock")).Items.Single(i => i.ProductId == dal.Id);
        Assert.Equal((20m, 0m, 20m), (line.Purchased, line.OtherOut, line.Closing));
    }
    [Fact]
    public async Task Checking_goods_in_records_damaged_extra_and_short_closed_lines()
    {
        var shop = await ShopAsync("kirana");
        var sugar = await ItemAsync(shop, "Sugar 50 kg", 2200m, 0m);
        var dal = await ItemAsync(shop, "Toor dal", 140m, 0m);
        var salt = await ItemAsync(shop, "Salt", 25m, 0m);
        var supplierId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplierId, name = "Agarwal Traders" })).EnsureSuccessStatusCode();
        var orderId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new
        {
            id = orderId, supplierId,
            lines = new[] { new { productId = sugar.Id, quantity = 10m, rate = (decimal?)2000m }, new { productId = dal.Id, quantity = 20m, rate = (decimal?)120m } },
        })).EnsureSuccessStatusCode();

        // The helper unloading the truck sees what to expect, without rates.
        var helper = await HelperOfAsync(shop);
        var check = (await helper.GetJsonAsync<List<CheckOrder>>($"/api/v1/goods-in/orders?supplierId={supplierId}")).Single();
        Assert.Equal(30m, check.Lines.Sum(l => l.Pending));
        Assert.DoesNotContain("rate", await (await helper.GetAsync($"/api/v1/goods-in/orders?supplierId={supplierId}")).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        // 10 sugar (1 burst), 12 dal, and 5 salt nobody ordered.
        (await helper.PostJsonAsync("/api/v1/goods-receipts", new
        {
            id = Guid.CreateVersion7(), supplierId, orderId,
            lines = new object[] { new { productId = sugar.Id, quantity = 10m, damaged = 1m }, new { productId = dal.Id, quantity = 12m }, new { productId = salt.Id, quantity = 5m } },
        })).EnsureSuccessStatusCode();
        Assert.Equal(9m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{sugar.Id}")).Stock);

        var arrivals = await shop.GetJsonAsync<List<ArrivalDto>>($"/api/v1/goods-in/arrivals?orderId={orderId}");
        Assert.Equal(3, arrivals.Count);
        Assert.Equal((1m, false), (arrivals.Single(a => a.ProductId == sugar.Id).Damaged, arrivals.Single(a => a.ProductId == sugar.Id).Extra));
        Assert.True(arrivals.Single(a => a.ProductId == salt.Id).Extra);

        // 8 dal still to come: the supplier says they're out, so the owner closes it short and the order closes.
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/close-short", new { productId = dal.Id, reason = "out_of_stock" })).EnsureSuccessStatusCode();
        var order = (await shop.GetJsonAsync<List<PurchaseOrderDto>>($"/api/v1/suppliers/orders?supplierId={supplierId}&all=true")).Single();
        var dalLine = order.Lines.Single(l => l.ProductId == dal.Id);
        Assert.Equal((PurchaseOrderStatus.Closed, 8m, ShortReason.OutOfStock), (order.Status, dalLine.ShortClosed, dalLine.ShortReason));
        Assert.Empty(await helper.GetJsonAsync<List<CheckOrder>>($"/api/v1/goods-in/orders?supplierId={supplierId}"));
    }

    [Fact]
    public async Task Every_arrival_row_adds_up_to_the_purchases_in_the_stock_ledger()
    {
        var shop = await ShopAsync("kirana");
        var oil = await ItemAsync(shop, "Mustard oil", 180m, 0m);
        var supplierId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplierId, name = "Gupta & Sons" })).EnsureSuccessStatusCode();
        async Task<Guid> Receipt(decimal qty)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id, supplierId, lines = new[] { new { productId = oil.Id, quantity = qty } } })).EnsureSuccessStatusCode();
            return id;
        }

        async Task<Guid> Bill(decimal qty, Guid[]? receiptIds = null)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id, supplierId, receiptIds, paidAmount = 0m, lines = new[] { new { productId = oil.Id, quantity = qty, unitCost = 150m } } })).EnsureSuccessStatusCode();
            return id;
        }

        var first = await Receipt(20m);
        await Bill(25m, [first]);                         // 20 from the receipt, 5 more on the bill
        await Bill(10m);                                  // straight on a bill
        var mistake = await Bill(7m);
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{mistake}/cancel", new { })).EnsureSuccessStatusCode();
        var wrong = await Receipt(3m);
        (await shop.PostJsonAsync($"/api/v1/goods-receipts/{wrong}/cancel", new { })).EnsureSuccessStatusCode();
        await Receipt(4m);                                // still waiting for its bill

        var arrived = (await shop.GetJsonAsync<List<ArrivalDto>>($"/api/v1/goods-in/arrivals?productId={oil.Id}")).Sum(a => a.Received);
        var purchased = (await shop.GetJsonAsync<StockStatement>("/api/v1/reports/stock")).Items.Single(i => i.ProductId == oil.Id).Purchased;
        Assert.Equal((39m, 39m), (arrived, purchased));
        Assert.All(await shop.GetJsonAsync<List<ArrivalDto>>($"/api/v1/goods-in/arrivals?productId={oil.Id}"), a => Assert.Equal(a.ReceiptNo is null || a.PurchaseId is not null ? 150m : null, a.Rate));
    }
    [Fact]
    public async Task One_category_per_name_with_rename_merge_and_defaults()
    {
        var shop = await ShopAsync("kirana");
        async Task<ProductDto> Item(string name, string? category, object? tax = null)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = 50m, category })).EnsureSuccessStatusCode();
            return await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{id}");
        }

        // Typed three ways, one category: the first spelling is kept.
        var dal = await Item("Toor dal", "Pulses");
        var moong = await Item("Moong dal", "  pulses ");
        await Item("Chana dal", "PULSES");
        var oil = await Item("Mustard oil", "Oil");
        var cats = await shop.GetJsonAsync<List<CategoryDto>>("/api/v1/categories");
        Assert.Equal(["Pulses", "Oil"], cats.Select(c => c.Name));
        Assert.Equal(3, cats.Single(c => c.Name == "Pulses").Items);
        Assert.Equal("Pulses", moong.Category);

        // Rename: every item follows.
        var pulses = cats.Single(c => c.Name == "Pulses");
        (await shop.PutAsJsonAsync($"/api/v1/categories/{pulses.Id}", new { name = "Dal & pulses", defaultGstRate = 5m, defaultHsn = "0713" })).EnsureSuccessStatusCode();
        Assert.Equal("Dal & pulses", (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{dal.Id}")).Category);

        // A new item in it takes its GST and HSN.
        var urad = await Item("Urad dal", "dal & PULSES");
        Assert.Equal(("Dal & pulses", 5m, "0713"), (urad.Category, urad.GstRate, urad.HsnCode));

        // Renaming onto another name is refused: merge instead. Merging moves every item and leaves a pointer.
        var oilCat = cats.Single(c => c.Name == "Oil");
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PutAsJsonAsync($"/api/v1/categories/{oilCat.Id}", new { name = "dal & pulses" })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/categories/{oilCat.Id}/merge", new { intoId = pulses.Id })).EnsureSuccessStatusCode();
        Assert.Equal("Dal & pulses", (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{oil.Id}")).Category);
        var after = await shop.GetJsonAsync<List<CategoryDto>>("/api/v1/categories");
        Assert.Equal((1, 5), (after.Count, after.Single().Items));
        // Typing the merged name again lands in the one it went into.
        Assert.Equal("Dal & pulses", (await Item("Groundnut oil", "oil")).Category);
    }
    [Fact]
    public async Task Item_by_supplier_scorecard_and_preferred_supplier_for_reorder()
    {
        var shop = await ShopAsync("kirana");
        var sugar = await ItemAsync(shop, "Sugar", 45m, 0m);
        async Task<Guid> Supplier(string name)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/suppliers", new { id, name })).EnsureSuccessStatusCode();
            return id;
        }

        var agarwal = await Supplier("Agarwal Traders");
        var shree = await Supplier("Shree Wholesale");
        // Agarwal: ordered 40, all came on the bill at 40. Shree: ordered 30, 25 came at 38, 1 of them damaged.
        var o1 = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = o1, supplierId = agarwal, lines = new[] { new { productId = sugar.Id, quantity = 40m, rate = (decimal?)40m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = agarwal, orderId = o1, paidAmount = 0m, lines = new[] { new { productId = sugar.Id, quantity = 40m, unitCost = 40m } } })).EnsureSuccessStatusCode();
        var o2 = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = o2, supplierId = shree, lines = new[] { new { productId = sugar.Id, quantity = 30m, rate = (decimal?)36m } } })).EnsureSuccessStatusCode();
        var receipt = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = receipt, supplierId = shree, orderId = o2, lines = new[] { new { productId = sugar.Id, quantity = 25m, damaged = 1m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = shree, orderId = o2, receiptIds = new[] { receipt }, paidAmount = 0m, lines = new[] { new { productId = sugar.Id, quantity = 25m, unitCost = 38m } } })).EnsureSuccessStatusCode();

        var grid = await shop.GetJsonAsync<ReceivedBySupplier>("/api/v1/reports/received-by-supplier");
        var row = grid.Rows.Single(r => r.ProductId == sugar.Id);
        Assert.Equal((65m, 5m), (row.Received, row.Pending));
        Assert.Equal((30m, 25m), (row.Cells.Single(c => c.SupplierId == shree).Ordered, row.Cells.Single(c => c.SupplierId == shree).Received));

        // Shree has the lower rate, so it is the best rate and the default to order from, until Agarwal is preferred.
        var of = await shop.GetJsonAsync<List<ItemSupplierDto>>($"/api/v1/products/{sugar.Id}/suppliers");
        Assert.True(of.Single(s => s.SupplierId == shree).BestRate);
        Assert.Equal((5m, 38m), (of.Single(s => s.SupplierId == shree).Pending, of.Single(s => s.SupplierId == shree).LastRate!.Value));

        var score = (await shop.GetJsonAsync<List<ScorecardDto>>("/api/v1/reports/supplier-scorecard")).ToDictionary(s => s.SupplierId);
        Assert.Equal((1m, 0.833m), (score[agarwal].FillRate!.Value, score[shree].FillRate!.Value));
        Assert.Equal(0.04m, score[shree].DamageRate);
        Assert.Equal(0.056m, score[shree].RateChange);   // (38 − 36) ÷ 36
        Assert.Equal(0m, score[agarwal].RateChange);

        (await shop.PutAsJsonAsync($"/api/v1/products/{sugar.Id}/suppliers/{agarwal}/preferred", new { preferred = true })).EnsureSuccessStatusCode();
        Assert.True((await shop.GetJsonAsync<List<ItemSupplierDto>>($"/api/v1/products/{sugar.Id}/suppliers")).Single(s => s.SupplierId == agarwal).Preferred);
        Assert.Single(await shop.GetJsonAsync<List<SupplierProductDto>>($"/api/v1/suppliers/{agarwal}/items"));
    }
    [Fact]
    public async Task Holds_and_orders_show_in_one_stock_picture()
    {
        var shop = await ShopAsync("kirana");
        var rice = await ItemAsync(shop, "Basmati rice", 120m, 10m);
        // Kept aside at the counter: 4 for Sharmaji; then a sale order promises 9 more.
        var hold = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/holds", new { id = hold, productId = rice.Id, quantity = 4m, partyName = "Sharmaji", until = "2030-01-01" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/sale-orders", new { id = Guid.CreateVersion7(), partyName = "Hotel Annapurna", lines = new[] { new { productId = rice.Id, name = "Basmati rice", quantity = 9m, unit = "piece", unitPrice = 110m } } })).EnsureSuccessStatusCode();
        // 20 on order from a supplier.
        var supplierId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplierId, name = "Agarwal Traders" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = Guid.CreateVersion7(), supplierId, lines = new[] { new { productId = rice.Id, quantity = 20m } } })).EnsureSuccessStatusCode();

        // Oldest first: Sharmaji is covered, the hotel is 3 short.
        var holds = await shop.GetJsonAsync<List<HoldDto>>($"/api/v1/holds?productId={rice.Id}");
        Assert.Equal([("counter", 0m), ("sale_order", 3m)], holds.Select(h => (h.Source, h.ShortBy)));
        Assert.Equal(13m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{rice.Id}")).Promised);

        var line = (await shop.GetJsonAsync<StockStatement>("/api/v1/reports/stock")).Items.Single(i => i.ProductId == rice.Id);
        Assert.Equal((10m, 13m, 20m), (line.InShop, line.Promised, line.Coming));

        // Sharmaji took it: the hold ends and no longer counts.
        (await shop.PostJsonAsync($"/api/v1/holds/{hold}/done", new { })).EnsureSuccessStatusCode();
        Assert.Equal(9m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{rice.Id}")).Promised);
        Assert.Single(await shop.GetJsonAsync<List<HoldDto>>($"/api/v1/holds?productId={rice.Id}"));
    }
    [Fact]
    public async Task Goods_to_come_tracks_stages_lots_and_moved_promises()
    {
        var shop = await ShopAsync("kirana");
        var sugar = await ItemAsync(shop, "Sugar", 45m, 0m);
        var supplierId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplierId, name = "Agarwal Traders", phone = "9876543210" })).EnsureSuccessStatusCode();
        var orderId = Guid.CreateVersion7();
        var late = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(-3);
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = orderId, supplierId, expectedOn = late.ToString("yyyy-MM-dd"), lines = new[] { new { productId = sugar.Id, quantity = 50m, rate = (decimal?)40m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/stage", new { stage = "sent", via = "whatsapp" })).EnsureSuccessStatusCode();

        var coming = (await shop.GetJsonAsync<List<ComingOrderDto>>("/api/v1/goods-to-come")).Single();
        Assert.Equal(("sent", 3, 0, 2000m), (coming.Stage, coming.DaysLate!.Value, coming.PromiseMoves, coming.Value));

        // The supplier says next week: the date moves and that counts. Then the truck leaves with a bilty and e-way bill.
        var next = late.AddDays(10);
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/follow-ups", new { id = Guid.CreateVersion7(), channel = "call", note = "Truck on Monday", newPromisedDate = next.ToString("yyyy-MM-dd") })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/shipments", new { id = Guid.CreateVersion7(), ewayBillNo = "123" })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/shipments", new
        {
            id = Guid.CreateVersion7(), dispatchedOn = late.AddDays(1).ToString("yyyy-MM-dd"), expectedOn = next.AddDays(1).ToString("yyyy-MM-dd"),
            transporter = "Shree Roadways", lrNo = "LR-4471", vehicleNo = "rj14 ab 1234", ewayBillNo = "331234567890", freight = 600m, freightPaidBy = "shop",
        })).EnsureSuccessStatusCode();
        coming = (await shop.GetJsonAsync<List<ComingOrderDto>>("/api/v1/goods-to-come")).Single();
        Assert.Equal(("dispatched", 2, next.AddDays(1), 0), (coming.Stage, coming.PromiseMoves, coming.ExpectedOn!.Value, coming.DaysLate!.Value));
        Assert.Equal(("RJ14 AB 1234", false), (coming.Shipments.Single().VehicleNo, coming.Shipments.Single().Arrived));
        Assert.Equal("Truck on Monday", coming.FollowUps.Single().Note);

        // The goods come against the order: the lot is marked arrived and the order shows part received.
        (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = Guid.CreateVersion7(), supplierId, orderId, lines = new[] { new { productId = sugar.Id, quantity = 30m } } })).EnsureSuccessStatusCode();
        coming = (await shop.GetJsonAsync<List<ComingOrderDto>>("/api/v1/goods-to-come")).Single();
        Assert.Equal(("part", 20m, true), (coming.Stage, coming.Pending, coming.Shipments.Single().Arrived));
    }

    [Fact]
    public async Task A_draft_order_is_kept_editable_and_counts_as_on_order_only_once_placed()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var tap = await ItemAsync(shop, "Tap", 150m, 0m);
        var supplierId = await SupplierAsync(shop);
        var orderId = Guid.CreateVersion7();
        var draft = await (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = orderId, supplierId, draft = true, lines = new[] { new { productId = pipe.Id, quantity = 10m } } })).ReadAsync<PurchaseOrderDto>();
        Assert.Equal(PurchaseOrderStatus.Draft, draft.Status);

        // Not on order yet: no goods can come against it and it isn't waited for.
        Assert.Empty(await shop.GetJsonAsync<List<ComingOrderDto>>("/api/v1/goods-to-come"));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId, orderId, lines = new[] { new { productId = pipe.Id, quantity = 1m, unitCost = 200m } }, paidAmount = 0m })).StatusCode);

        // Edited, then placed; a placed order can't be edited.
        var edited = await (await shop.PutJsonAsync($"/api/v1/suppliers/orders/{orderId}", new { id = orderId, supplierId, note = "Tuesday truck", lines = new object[] { new { productId = pipe.Id, quantity = 8m }, new { productId = tap.Id, quantity = 4m } } })).ReadAsync<PurchaseOrderDto>();
        Assert.Equal((2, "Tuesday truck", draft.Number), (edited.Lines.Count, edited.Note, edited.Number));
        var placed = await (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/place", new { })).ReadAsync<PurchaseOrderDto>();
        Assert.Equal((PurchaseOrderStatus.Open, 12m), (placed.Status, placed.Pending));
        Assert.Single(await shop.GetJsonAsync<List<ComingOrderDto>>("/api/v1/goods-to-come"));
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PutJsonAsync($"/api/v1/suppliers/orders/{orderId}", new { id = orderId, supplierId, lines = new[] { new { productId = pipe.Id, quantity = 1m } } })).StatusCode);

        // A second draft is simply thrown away.
        var other = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = other, supplierId, draft = true, lines = new[] { new { productId = tap.Id, quantity = 2m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{other}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(PurchaseOrderStatus.Cancelled, (await shop.GetJsonAsync<List<PurchaseOrderDto>>($"/api/v1/suppliers/orders?supplierId={supplierId}&all=true")).Single(o => o.Id == other).Status);
    }

    [Fact]
    public async Task A_lot_keeps_a_photo_of_its_bilty()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var supplierId = await SupplierAsync(shop);
        var orderId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/orders", new { id = orderId, supplierId, lines = new[] { new { productId = pipe.Id, quantity = 10m } } })).EnsureSuccessStatusCode();
        var lot = Guid.CreateVersion7();
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/shipments", new { id = lot, lrNo = "LR-881" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/shipments/{lot}/photo", new { photoDataUrl = "data:text/plain;base64,aGk=" })).StatusCode);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        (await shop.PostJsonAsync($"/api/v1/suppliers/orders/{orderId}/shipments/{lot}/photo", new { photoDataUrl = "data:image/png;base64," + Convert.ToBase64String(png) })).EnsureSuccessStatusCode();
        Assert.True((await shop.GetJsonAsync<List<ComingOrderDto>>("/api/v1/goods-to-come")).Single().Shipments.Single().HasPhoto);
        var photo = await shop.GetAsync($"/api/v1/suppliers/orders/{orderId}/shipments/{lot}/photo");
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await photo.Content.ReadAsByteArrayAsync());

        // Another shop can't see it.
        Assert.Equal(HttpStatusCode.NotFound, (await (await ShopAsync("hardware")).GetAsync($"/api/v1/suppliers/orders/{orderId}/shipments/{lot}/photo")).StatusCode);
    }

    [Fact]
    public async Task A_receipt_shows_its_goods_from_the_delivery_lines_even_once_cancelled()
    {
        var shop = await ShopAsync("hardware");
        var pipe = await ItemAsync(shop, "Pipe", 300m, 0m);
        var supplierId = await SupplierAsync(shop);
        var receipt = await (await shop.PostJsonAsync("/api/v1/goods-receipts", new { id = Guid.CreateVersion7(), supplierId, lines = new[] { new { productId = pipe.Id, quantity = 10m, damaged = 2m } } })).ReadAsync<GoodsReceiptDto>();
        Assert.Equal(("Pipe", 10m, 2m), (receipt.Lines.Single().Name, receipt.Lines.Single().Quantity, receipt.Lines.Single().Damaged));
        Assert.Equal(8m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);

        (await shop.PostJsonAsync($"/api/v1/goods-receipts/{receipt.Id}/cancel", new { })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{pipe.Id}")).Stock);
        var listed = (await shop.GetJsonAsync<List<GoodsReceiptDto>>($"/api/v1/goods-receipts?supplierId={supplierId}")).Single();
        Assert.Equal((10m, true), (listed.Lines.Single().Quantity, listed.CancelledAt != null));
    }
}
