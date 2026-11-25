using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>Regressions for the defects found by testing a clothes shop and a medical shop end to end.</summary>
[Collection(ApiCollection.Name)]
public sealed class TestingFixesTests(ApiFactory api)
{
    private async Task<(HttpClient Owner, HttpClient Helper)> ShopAsync(string type = "clothes", bool gst = false)
    {
        var owner = api.CreateClient();
        var auth = await (await owner.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Fix Check", fullName = "Asha Rao", phone = ApiFactory.NewPhone(), password = "secret123", language = "en", type })).ReadAsync<AuthResponse>();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        if (gst)
        {
            var settings = await owner.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
            (await owner.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z0" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        }

        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var helperAuth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helperAuth.AccessToken);
        return (owner, helper);
    }

    [Fact]
    public async Task Cancelling_a_purchase_takes_its_units_out_of_the_average_cost_and_write_offs_keep_their_cost()
    {
        var (shop, _) = await ShopAsync("kirana");
        var sugar = await ItemAsync(shop, "Sugar", 50m, 0m);
        var supplier = await SupplierAsync(shop);
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = sugar, quantity = 14m, unitCost = 40m } } })).EnsureSuccessStatusCode();

        // A damaged packet is written off at ₹40, the cost that day.
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = sugar, kind = "adjustment", quantity = -1m, reason = "damage" })).EnsureSuccessStatusCode();

        // 10 more at ₹44 lift the average to (13×40 + 10×44) / 23 = 41.74; cancelling that bill takes it back to 40.
        var second = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = second, supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = sugar, quantity = 10m, unitCost = 44m } } })).EnsureSuccessStatusCode();
        Assert.Equal(41.74m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{sugar}")).CostPrice);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{second}/cancel", new { })).StatusCode);
        var after = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{sugar}");
        Assert.Equal((13m, 40m), (after.Stock, after.CostPrice));

        // The month and the year agree on the loss: ₹40, whatever the cost did afterwards.
        Assert.Equal(40m, (await shop.GetJsonAsync<MonthReport>("/api/v1/reports/month")).StockLoss);
        Assert.Equal(40m, (await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl")).StockLoss);
    }

    [Fact]
    public async Task Credit_notes_come_off_the_years_sales_gst_and_turnover()
    {
        var (shop, _) = await ShopAsync("kirana", gst: true);
        var soap = await ItemAsync(shop, "Soap", 118m, 20m, cost: 70m, gst: 18m);
        var bill = Guid.CreateVersion7();
        (await SellAsync(shop, soap, 10m, 118m, id: bill)).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/bills/{bill}/credit-notes", new { id = Guid.CreateVersion7(), reason = "damaged", lines = new[] { new { lineNo = 1, quantity = 2m } }, restock = true })).EnsureSuccessStatusCode();

        // 8 soap stay sold: ₹944 with GST, ₹144 of it GST, ₹800 turnover (what the audit limits and 44AD read).
        var pnl = await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl");
        Assert.Equal((944m, 144m, 800m), (pnl.Sales, pnl.Gst, pnl.Turnover));
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal price, decimal stock, decimal? cost = null, decimal? gst = null, string? schedule = null)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = price, costPrice = cost, gstRate = gst, openingStock = stock, schedule })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<Guid> SupplierAsync(HttpClient shop, string? gstin = null)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id, name = "Jain Traders", gstin })).EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> SellAsync(HttpClient shop, Guid item, decimal quantity, decimal price, string mode = "cash", Guid? id = null) =>
        shop.PostJsonAsync("/api/v1/bills", new { id = id ?? Guid.CreateVersion7(), lines = new[] { new { productId = item, name = "Item", quantity, unitPrice = price } }, paidAmount = quantity * price, paymentMode = mode });

    [Fact]
    public async Task A_supplier_bill_cant_be_cancelled_once_its_goods_were_sold_and_stock_reconciles()
    {
        var (shop, _) = await ShopAsync();
        var shirt = await ItemAsync(shop, "Shirt", 500m, 0m);
        var supplier = await SupplierAsync(shop);
        var bill = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = bill, supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = shirt, quantity = 5m, unitCost = 300m } } })).EnsureSuccessStatusCode();
        (await SellAsync(shop, shirt, 2m, 500m)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{bill}/cancel", new { })).StatusCode);
        Assert.Equal(3m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{shirt}")).Stock);
        Assert.Empty(await shop.GetJsonAsync<List<StockMismatch>>("/api/v1/ledger/reconcile/stock"));

        // A bill whose goods are all still here cancels as before.
        var other = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = other, supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = shirt, quantity = 2m, unitCost = 300m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/suppliers/purchases/{other}/cancel", new { })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_cash_ledger_survives_two_counts_on_a_day_and_a_cash_move_in_india_time_saves()
    {
        var (shop, _) = await ShopAsync();
        (await shop.PostJsonAsync("/api/v1/cash-count", new { opening = 100m, counted = 90m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/cash-count", new { counted = 100m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await shop.GetAsync("/api/v1/cash-ledger")).StatusCode);

        var at = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).AddHours(-1);
        var move = await shop.PostJsonAsync("/api/v1/cash-moves", new { id = Guid.CreateVersion7(), kind = "added", amount = 200m, note = "From home", occurredAt = at });
        Assert.True(move.IsSuccessStatusCode, await move.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Holds_take_only_free_stock_and_the_shelf_cant_go_below_zero_while_the_godown_holds_it()
    {
        var (shop, _) = await ShopAsync();
        var saree = await ItemAsync(shop, "Saree", 1000m, 4m);
        (await shop.PostJsonAsync("/api/v1/holds", new { id = Guid.CreateVersion7(), productId = saree, quantity = 3m, partyName = "Meena" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/holds", new { id = Guid.CreateVersion7(), productId = saree, quantity = 3m, partyName = "Kavita" })).StatusCode);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5).AddDays(-1));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/holds", new { id = Guid.CreateVersion7(), productId = saree, quantity = 1m, partyName = "Kavita", until = yesterday })).StatusCode);

        var back = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/godowns", new { id = back, name = "Back room" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/godowns/transfers", new { id = Guid.CreateVersion7(), toGodownId = back, lines = new[] { new { productId = saree, quantity = 3m } } })).EnsureSuccessStatusCode();
        var refused = await SellAsync(shop, saree, 2m, 1000m);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("godown", await refused.Content.ReadAsStringAsync());
        (await SellAsync(shop, saree, 1m, 1000m)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Medicines_need_a_batch_older_packs_keep_their_mrp_and_helpers_cant_invent_stock()
    {
        var (shop, helper) = await ShopAsync("medical");
        var para = await ItemAsync(shop, "Paracetamol 500", 30m, 0m, cost: 20m, schedule: "h");
        var supplier = await SupplierAsync(shop);
        var noBatch = await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = para, quantity = 10m, unitCost = 20m } } });
        Assert.Equal(HttpStatusCode.BadRequest, noBatch.StatusCode);

        var month = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM");
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = para, quantity = 10m, unitCost = 20m, batchNo = "A1", expiryMonth = month, mrp = 30m } } }))
            .EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = para, quantity = 10m, unitCost = 20m, batchNo = "B2", expiryMonth = month, mrp = 28m } } }))
            .EnsureSuccessStatusCode();
        // The item's MRP stays at the highest still on the shelf, so the older pack still sells at its printed 30.
        Assert.Equal(30m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{para}")).Mrp);

        // A helper can't count stock up from nothing beyond the owner's limit.
        var gain = await helper.PostJsonAsync("/api/v1/stocktakes", new { id = Guid.CreateVersion7(), counts = new[] { new { productId = para, counted = 500m } } });
        Assert.Equal(HttpStatusCode.BadRequest, gain.StatusCode);
        var statement = await helper.GetJsonAsync<StockStatement>("/api/v1/reports/stock");
        Assert.All(statement.Items, i => Assert.Null(i.Value));
    }

    [Fact]
    public async Task A_refund_goes_back_the_way_it_was_paid_and_a_supplier_credit_note_takes_back_its_input_tax()
    {
        var (shop, _) = await ShopAsync(gst: true);
        var kurta = await ItemAsync(shop, "Kurta", 1050m, 10m, cost: 600m, gst: 5m);
        var bill = await (await SellAsync(shop, kurta, 1m, 1050m, "upi")).ReadAsync<BillDto>();
        (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Size", refundMode = "upi", lines = new[] { new { lineNo = 1, quantity = 1m } } }))
            .EnsureSuccessStatusCode();
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((0m, 1050m), (book.SpentCash, book.SpentUpi));

        var supplier = await SupplierAsync(shop, "08AAPFU0939F1ZV");
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m, lines = new[] { new { productId = kurta, quantity = 10m, unitCost = 600m, gstRate = 5m } } }))
            .EnsureSuccessStatusCode();
        var back = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers/returns", new { id = back, supplierId = supplier, lines = new[] { new { productId = kurta, quantity = 1m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/suppliers/returns/{back}/credit-note", new { creditNoteNo = "CN-1", amount = 630m, gst = 30m })).EnsureSuccessStatusCode();
        // 300 input tax on the bill, less 30 on what went back.
        Assert.Equal(270m, (await shop.GetJsonAsync<GstMonthDto>("/api/v1/gst/month")).InputGst);
    }

    [Fact]
    public async Task Master_items_take_english_names_in_an_english_shop_and_medicines_get_their_schedule()
    {
        var (shop, _) = await ShopAsync("clothes");
        var master = await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items?trade=clothes");
        var withHindi = master.First(m => m.NameHi != null && m.PackSizes.Count == 0);
        (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = new[] { withHindi.Id } })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        Assert.Contains(items, p => p.Name == withHindi.Name);
    }

    [Fact]
    public async Task A_later_cancel_moves_money_on_the_day_it_happens_and_the_first_count_needs_an_opening()
    {
        var (shop, _) = await ShopAsync();
        var shirt = await ItemAsync(shop, "Shirt", 500m, 10m);
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        var old = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), occurredAt = yesterday, lines = new[] { new { productId = shirt, name = "Shirt", quantity = 1m, unitPrice = 500m } }, paidAmount = 500m, paymentMode = "cash" }))
            .ReadAsync<BillDto>();
        (await SellAsync(shop, shirt, 2m, 500m)).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/bills/{old.Id}/cancel", new { reason = "Wrong size" })).EnsureSuccessStatusCode();

        // Today: 1000 taken, 500 handed back for yesterday's bill. Same in the day report and the cash book.
        var day = await shop.GetJsonAsync<DayReportDto>("/api/v1/reports/day");
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((500m, book.Cash), (day.Totals.Cash, day.Totals.Cash));

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/cash-count", new { counted = 500m })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/cash-count", new { opening = 0m, counted = 500m })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Item_margins_take_off_returns_and_the_gst_month_flags_a_bill_entered_late()
    {
        var (shop, _) = await ShopAsync(gst: true);
        var kurta = await ItemAsync(shop, "Kurta", 1050m, 10m, cost: 600m, gst: 5m);
        var bill = await (await SellAsync(shop, kurta, 2m, 1050m)).ReadAsync<BillDto>();
        (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Size", lines = new[] { new { lineNo = 1, quantity = 1m } } })).EnsureSuccessStatusCode();
        var item = (await shop.GetJsonAsync<ItemMarginReport>("/api/v1/reports/items")).Items.Single();
        Assert.Equal((1m, 1000m, 600m, 400m), (item.Quantity, item.Sales, item.Cost, item.Profit));

        // A bill dated earlier in the month, entered after this one, takes the next number.
        var first = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 6, 0, 0, TimeSpan.Zero);
        if (first < DateTimeOffset.UtcNow.AddHours(-2))
        {
            (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), occurredAt = first, lines = new[] { new { productId = kurta, name = "Kurta", quantity = 1m, unitPrice = 1050m } }, paidAmount = 1050m, paymentMode = "cash" }))
                .EnsureSuccessStatusCode();
            Assert.Contains((await shop.GetJsonAsync<GstMonthDto>("/api/v1/gst/month")).Checks, c => c.Code == "outOfOrder" && c.Count == 1);
        }
    }

    [Fact]
    public async Task Cash_moved_by_hand_reaches_the_tally_file()
    {
        var (shop, _) = await ShopAsync(gst: true);
        (await shop.PostJsonAsync("/api/v1/cash-moves", new { id = Guid.CreateVersion7(), kind = "to_bank", amount = 5000m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/cash-moves", new { id = Guid.CreateVersion7(), kind = "added", amount = 1000m, note = "Change from home" })).EnsureSuccessStatusCode();
        var kurta = await ItemAsync(shop, "Kurta", 1050m, 10m, gst: 5m);
        (await SellAsync(shop, kurta, 1m, 1050m)).EnsureSuccessStatusCode();

        var month = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM");
        var zip = await shop.GetAsync($"/api/v1/gst/export.zip?month={month}");
        zip.EnsureSuccessStatusCode();
        using var archive = new System.IO.Compression.ZipArchive(await zip.Content.ReadAsStreamAsync());
        var xml = await new StreamReader(archive.GetEntry("2-vouchers.xml")!.Open()).ReadToEndAsync();
        Assert.Contains("<LEDGERNAME>Bank Account</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-5000.00</AMOUNT>", xml);
        Assert.Contains("<LEDGERNAME>Capital Account</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>1000.00</AMOUNT>", xml);
    }

    [Fact]
    public async Task Reorder_goes_by_free_stock_and_a_bad_request_body_is_a_400()
    {
        var (shop, _) = await ShopAsync();
        var shirt = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = shirt, name = "Shirt", unit = "piece", sellingPrice = 500m, openingStock = 5m, reorderLevel = 3m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/holds", new { id = Guid.CreateVersion7(), productId = shirt, quantity = 4m, partyName = "Meena" })).EnsureSuccessStatusCode();
        // 5 in stock, 4 kept aside: 1 free, below the reorder level of 3.
        Assert.Contains((await shop.GetJsonAsync<OrderTodayDto>("/api/v1/stock/order-today")).Items, i => i.ProductId == shirt);

        var bad = await shop.PostAsync("/api/v1/products", new StringContent("{\"id\":\"\",\"name\":\"X\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task One_price_sets_every_size_and_a_refund_is_its_own_line_not_kharcha()
    {
        var (shop, helper) = await ShopAsync();
        foreach (var size in new[] { "30", "32" })
        {
            (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = $"Men jeans {size}", unit = "piece", sellingPrice = 1m, openingStock = 5m, styleName = "Men jeans", size }))
                .EnsureSuccessStatusCode();
        }

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await helper.PostJsonAsync("/api/v1/products/style-price", new { styleName = "Men jeans", sellingPrice = 999m })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/products/style-price", new { styleName = "Men jeans", sellingPrice = 999m })).EnsureSuccessStatusCode();
        var jeans = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Where(p => p.Name.StartsWith("Men jeans")).ToList();
        Assert.Equal(2, jeans.Count);
        Assert.All(jeans, p => Assert.Equal(999m, p.SellingPrice));

        var bill = await (await SellAsync(shop, jeans[0].Id, 1m, 999m)).ReadAsync<BillDto>();
        (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Size", refundMode = "cash", lines = new[] { new { lineNo = 1, quantity = 1m } } }))
            .EnsureSuccessStatusCode();
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Contains(book.Lines, l => l.Source == CashBookSource.Refund && l.Amount == -999m);
        Assert.Equal(999m, book.SpentCash);
        var page = await shop.GetJsonAsync<ExpensePageDto>("/api/v1/expenses");
        Assert.DoesNotContain(page.Lines, l => l.Note != null && l.Note.StartsWith("Refund CN/"));
    }
}
