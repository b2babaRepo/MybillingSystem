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

    [Fact]
    public async Task A_shop_picks_items_from_its_trades_master_list_only()
    {
        var shop = await SignUpAsync("hardware");
        var list = await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items");
        Assert.NotEmpty(list);
        Assert.All(list, i => Assert.Equal("hardware", i.Trade));
        var picked = await (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = list.Take(3).Select(i => i.Id) })).ReadAsync<ImportResult>();
        Assert.Equal(3, picked.Created);
        var again = await (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = list.Take(3).Select(i => i.Id) })).ReadAsync<ImportResult>();
        Assert.Equal((0, 3), (again.Created, again.Skipped));

        var grocery = await (await api.SignUpAsync()).Client.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items");
        Assert.All(grocery, i => Assert.Equal("kirana", i.Trade));
        var foreign = await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = new[] { grocery[0].Id } });
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Fact]
    public async Task Admins_import_items_into_a_trades_master_list()
    {
        var admin = await AdminAsync();
        var result = await (await admin.PostJsonAsync("/api/v1/platform/master-items/import", new
        {
            trade = "beauty",
            text = "name,hindi,category,unit,gst,hsn\nHair spa,हेयर स्पा,Services,piece,5,999721\nBad line,,,,7,\nFacial,फ़ेशियल,Services,piece,5,999722",
        })).ReadAsync<MasterImportResult>();
        Assert.Equal((1, 1, 1), (result.Added, result.Updated, result.Problems.Count));
    }

    [Fact]
    public async Task An_item_with_pack_sizes_becomes_one_shop_item_per_size()
    {
        var admin = await AdminAsync();
        var item = await (await admin.PostJsonAsync("/api/v1/platform/master-items", new
        {
            trade = "sweets", category = "Namkeen", name = $"Bhujia {Guid.NewGuid():N}"[..14], unit = "kg", packSizes = new[] { "200 g", "1 kg", "1 KG" },
        })).ReadAsync<MasterItemDto>();
        Assert.Equal(["200 g", "1 kg"], item.PackSizes);

        var shop = await SignUpAsync("sweets");
        var picked = await (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = new[] { item.Id } })).ReadAsync<ImportResult>();
        Assert.Equal(2, picked.Created);
        var mine = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Where(p => p.Name.StartsWith(item.Name)).ToList();
        Assert.All(mine, p => Assert.Equal(Unit.Packet, p.Unit));
        Assert.Contains(mine, p => p.Name == $"{item.Name} 200 g");
    }

    [Fact]
    public async Task A_shops_suggestion_joins_the_master_list_once_approved()
    {
        var shop = await SignUpAsync("toys");
        var name = $"Kite {Guid.NewGuid():N}"[..12];
        var suggestion = await (await shop.PostJsonAsync("/api/v1/master-items/suggest", new { name, category = "Outdoor", unit = "piece" })).ReadAsync<SuggestionDto>();
        Assert.Equal(("toys", SuggestionStatus.Pending), (suggestion.Trade, suggestion.Status));
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/master-items/suggest", new { name, category = "Outdoor", unit = "piece" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/master-items/suggest", new { trade = "paint", name, category = "Outdoor", unit = "piece" })).StatusCode);

        var admin = await AdminAsync();
        Assert.Contains(await admin.GetJsonAsync<List<SuggestionDto>>("/api/v1/platform/master-items/suggestions"), s => s.Id == suggestion.Id);
        (await admin.PostJsonAsync($"/api/v1/platform/master-items/suggestions/{suggestion.Id}/approve", new { trade = "toys", name, category = "Outdoor", unit = "piece", gstRate = 18m })).EnsureSuccessStatusCode();
        Assert.Contains(await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items"), i => i.Name == name && i.GstRate == 18m);
        Assert.DoesNotContain(await admin.GetJsonAsync<List<SuggestionDto>>("/api/v1/platform/master-items/suggestions"), s => s.Id == suggestion.Id);
    }

    [Fact]
    public async Task A_master_correction_is_offered_to_shops_that_have_the_item()
    {
        var admin = await AdminAsync();
        var name = $"Diya {Guid.NewGuid():N}"[..12];
        var item = await (await admin.PostJsonAsync("/api/v1/platform/master-items", new { trade = "sweets", category = "Puja", name, unit = "piece" })).ReadAsync<MasterItemDto>();

        var shop = await SignUpAsync("sweets");
        var updates = await shop.GetJsonAsync<MasterUpdatesDto>("/api/v1/master-items/updates");
        Assert.Contains(updates.NewItems, i => i.Id == item.Id);
        (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = new[] { item.Id } })).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await shop.GetJsonAsync<MasterUpdatesDto>("/api/v1/master-items/updates")).Changes, c => c.ProductName == name);

        (await admin.PutAsJsonAsync($"/api/v1/platform/master-items/{item.Id}", new { trade = "sweets", category = "Puja", name, unit = "piece", gstRate = 5m, hsnCode = "6913" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        updates = await shop.GetJsonAsync<MasterUpdatesDto>("/api/v1/master-items/updates");
        var change = Assert.Single(updates.Changes, c => c.ProductName == name);
        Assert.Equal((null, 5m, "6913"), (change.GstFrom, change.GstTo, change.HsnTo));
        Assert.DoesNotContain(updates.NewItems, i => i.Id == item.Id);

        (await shop.PostJsonAsync("/api/v1/master-items/updates/apply", new { productIds = new[] { change.ProductId } })).EnsureSuccessStatusCode();
        var product = await Item(shop, change.ProductId);
        Assert.Equal((5m, "6913"), (product.GstRate, product.HsnCode));
        Assert.DoesNotContain((await shop.GetJsonAsync<MasterUpdatesDto>("/api/v1/master-items/updates")).Changes, c => c.ProductName == name);
    }

    [Fact]
    public async Task Csv_import_keeps_quoted_commas_merges_repeats_and_refuses_number_units()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];
        var result = await (await admin.PostJsonAsync("/api/v1/platform/master-items/import", new
        {
            trade = "clothes",
            text = $"\"Kurta, cotton {tag}\",,Kurta,piece,5,\nKurta, cotton {tag},,Kurta,piece,12,\nShawl {tag},,Winter,5,5,",
        })).ReadAsync<MasterImportResult>();
        Assert.Equal((1, 0, 2), (result.Added, result.Updated, result.Problems.Count)); // line 2 splits wrongly unquoted; line 3 has unit "5"

        result = await (await admin.PostJsonAsync("/api/v1/platform/master-items/import", new
        {
            trade = "clothes",
            text = $"Dupatta {tag},,Dupatta,piece,5,\nDupatta {tag},,Dupatta,piece,12,",
        })).ReadAsync<MasterImportResult>();
        Assert.Equal((1, 1), (result.Added, result.Updated));
        var rows = await admin.GetJsonAsync<List<MasterItemDto>>($"/api/v1/platform/master-items?trade=clothes&q={tag}");
        Assert.Equal(12m, Assert.Single(rows, r => r.Name.StartsWith("Dupatta")).GstRate);
        Assert.Contains(rows, r => r.Name == $"Kurta, cotton {tag}");
    }

    [Fact]
    public async Task Only_items_admins_added_lately_show_as_new()
    {
        var shop = await SignUpAsync("paint");
        var updates = await shop.GetJsonAsync<MasterUpdatesDto>("/api/v1/master-items/updates");
        Assert.Empty(updates.NewItems); // the starting list never shows as new
    }

    [Fact]
    public async Task The_pay_page_shows_the_shops_upi_only_once_it_has_one()
    {
        var shop = await SignUpAsync("kirana");
        var settings = await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        var anyone = api.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/store/{settings.Slug}/pay")).StatusCode);

        (await shop.PutAsJsonAsync("/api/v1/shop", settings with { UpiId = "suresh@okaxis" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var pay = await anyone.GetJsonAsync<GroceryKhata.Api.Planner.StorePayDto>($"/api/v1/store/{settings.Slug}/pay");
        Assert.Equal((settings.Name, "suresh@okaxis"), (pay.Name, pay.UpiId));

        // A shopkeeper signed in to their own shop can open another shop's public pages too.
        var other = await SignUpAsync("hardware");
        Assert.Equal("suresh@okaxis", (await other.GetJsonAsync<GroceryKhata.Api.Planner.StorePayDto>($"/api/v1/store/{settings.Slug}/pay")).UpiId);
        (await other.GetAsync($"/api/v1/store/{settings.Slug}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_phone_set_to_last_month_still_dates_entries_today()
    {
        var shop = await SignUpAsync("kirana");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var phoneNow = DateTimeOffset.UtcNow.AddDays(-31);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/khata-entries")
        {
            Content = JsonContent.Create(new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount = 100m, occurredAt = phoneNow.AddMinutes(-10) }, options: ApiFactory.Json),
        };
        message.Headers.Add("X-Client-Time", phoneNow.ToString("O"));
        (await shop.SendAsync(message)).EnsureSuccessStatusCode();

        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        var at = Assert.Single(statement.Entries).OccurredAt;
        Assert.InRange(at, DateTimeOffset.UtcNow.AddMinutes(-12), DateTimeOffset.UtcNow.AddMinutes(-8));
    }

    [Fact]
    public async Task A_khata_link_stops_after_90_days_and_sharing_again_makes_a_new_one()
    {
        var shop = await SignUpAsync("kirana");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Gupta");
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var anyone = api.CreateClient();
        (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}")).EnsureSuccessStatusCode();

        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var age = new Npgsql.NpgsqlCommand("UPDATE customers SET khata_token_at = now() - interval '91 days' WHERE id = @c", db);
            age.Parameters.AddWithValue("c", customer);
            await age.ExecuteNonQueryAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}")).StatusCode);
        var fresh = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        Assert.NotEqual(link.Token, fresh.Token);
        (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={fresh.Token}")).EnsureSuccessStatusCode();
    }

    private static async Task<BillDto> Bill(HttpClient shop, Guid? customer, Guid product, decimal quantity, decimal paid) =>
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, paidAmount = paid, paymentMode = "cash",
            lines = new[] { new { productId = product, quantity, unitPrice = 500m } },
        })).ReadAsync<BillDto>();

    private static Task<ProductDto> Item(HttpClient shop, Guid id) => shop.GetJsonAsync<ProductDto>($"/api/v1/products/{id}");

    /// <summary>A new account given the Approver platform role directly in the database, then signed in.</summary>
    private async Task<HttpClient> AdminAsync()
    {
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Admin's own shop", phone);
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var insert = new Npgsql.NpgsqlCommand("INSERT INTO platform_admins (id, user_id, role, created_at, updated_at) VALUES (gen_random_uuid(), @u, 'Approver', now(), now())", db);
            insert.Parameters.AddWithValue("u", auth.User.Id);
            await insert.ExecuteNonQueryAsync();
        }

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        return client;
    }

    private async Task<HttpClient> SignUpAsync(string type)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = $"Day {type} {Guid.NewGuid():N}"[..30], fullName = "Suresh Verma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
