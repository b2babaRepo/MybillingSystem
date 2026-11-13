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
}
