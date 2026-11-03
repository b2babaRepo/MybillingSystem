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
    public static TheoryData<string> Trades => ["water_supply", "beauty", "sweets", "toys", "clothes", "building_materials", "paint", "hardware", "electrical", "woodwork", "banquet", "restaurant", "lodge"];

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

    [Fact]
    public async Task A_sofa_job_goes_from_quote_to_delivery_and_shows_its_profit()
    {
        var shop = await SignUpAsync("woodwork");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var ply = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Plywood 18 mm 8x4");
        (await shop.PutAsJsonAsync($"/api/v1/products/{ply.Id}", new { name = ply.Name, unit = ply.Unit, sellingPrice = 2600m, costPrice = 2000m, gstRate = 18m, hsnCode = ply.HsnCode, replaceTax = true }, ApiFactory.Json))
            .EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Sharma ji");

        var id = Guid.CreateVersion7();
        var job = (await (await shop.PostJsonAsync("/api/v1/jobs", new { id, customerId = customer, title = "Sofa set 3+1+1", price = 45000m, estimatedCost = 30000m, status = "quote" })).ReadAsync<JobDetail>()).Job;
        Assert.Equal((1, JobStatus.Quote), (job.Number, job.Status));

        // The advance goes on the khata and makes it an order.
        job = (await (await shop.PostJsonAsync($"/api/v1/jobs/{id}/advance", new { id = Guid.CreateVersion7(), amount = 20000m, mode = "upi" })).ReadAsync<JobDetail>()).Job;
        Assert.Equal((JobStatus.Order, 20000m), (job.Status, job.Advance));
        Assert.Equal(-20000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);

        // Five sheets off the stock list, karigar wages and transport into the kharcha book.
        (await shop.PostJsonAsync($"/api/v1/jobs/{id}/material", new { id = Guid.CreateVersion7(), productId = ply.Id, quantity = 5m })).EnsureSuccessStatusCode();
        Assert.Equal(-5m, (await Item(shop, ply.Id)).Stock);
        (await shop.PostJsonAsync($"/api/v1/jobs/{id}/labour", new { id = Guid.CreateVersion7(), description = "Ramesh, 8 days", amount = 8000m, mode = "cash" })).EnsureSuccessStatusCode();
        job = (await (await shop.PostJsonAsync($"/api/v1/jobs/{id}/other", new { id = Guid.CreateVersion7(), description = "Tempo to site", amount = 500m })).ReadAsync<JobDetail>()).Job;
        Assert.Equal((JobStatus.Making, 10000m, 8000m, 500m, 18500m, 26500m), (job.Status, job.Material, job.Labour, job.Other, job.Cost, job.Profit));

        // Rent of ₹4,000 against ₹8,000 of wages this month: the job carries half its wages again as overhead.
        (await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount = 4000m, category = "rent", mode = "cash", note = "Workshop rent" })).EnsureSuccessStatusCode();
        Assert.Equal(4000m, (await shop.GetJsonAsync<JobDetail>($"/api/v1/jobs/{id}")).Overhead);
        Assert.Equal(18500m, (await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl")).WorkInProgress);
        (await shop.PutAsJsonAsync($"/api/v1/jobs/{id}/status", new { status = "ready" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var ready = await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl");
        Assert.Equal((0m, 18500m), (ready.WorkInProgress, ready.FinishedGoods)); // made, waiting to go: finished furniture
        Assert.Equal(20000m, ready.DigitalReceipts); // the UPI advance
        Assert.Equal(1200m, ready.PresumedProfit);

        // Delivery makes the bill; the customer owes the price less the advance.
        var detail = await (await shop.PostJsonAsync($"/api/v1/jobs/{id}/deliver", new { billId = Guid.CreateVersion7(), paidAmount = 0m })).ReadAsync<JobDetail>();
        Assert.Equal(JobStatus.Delivered, detail.Job.Status);
        Assert.NotNull(detail.Job.BillId);
        Assert.Equal(25000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);
        Assert.Equal(HttpStatusCode.NotFound, (await shop.PostJsonAsync($"/api/v1/jobs/{id}/labour", new { id = Guid.CreateVersion7(), description = "Late", amount = 10m })).StatusCode);
        Assert.Contains(await shop.GetJsonAsync<List<JobDto>>("/api/v1/jobs?done=true"), j => j.Id == id);
        Assert.Equal(0m, (await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl")).WorkInProgress);
    }

    [Fact]
    public void A_machine_wears_15_percent_a_year_on_what_is_left_and_half_in_a_short_first_year()
    {
        var saw = new Machine { Name = "Panel saw", BoughtOn = new DateOnly(2024, 5, 10), Cost = 200000m };
        Assert.Equal((30000m, 170000m), Workshop.WearIn(saw, 2024));
        Assert.Equal((25500m, 144500m), Workshop.WearIn(saw, 2025));
        var late = new Machine { Name = "Edge bander", BoughtOn = new DateOnly(2025, 12, 1), Cost = 100000m };
        Assert.Equal((7500m, 92500m), Workshop.WearIn(late, 2025)); // under 180 days in its first year
        Assert.Equal((0m, 100000m), Workshop.WearIn(late, 2024));
        var sold = new Machine { Name = "Old planer", BoughtOn = saw.BoughtOn, Cost = saw.Cost, DisposedOn = new DateOnly(2025, 6, 1) };
        Assert.Equal((0m, 170000m), Workshop.WearIn(sold, 2025)); // sold that year: no wear
    }

    [Fact]
    public async Task Goods_sent_to_a_karigar_leave_stock_come_back_and_show_on_the_itc04_sheet()
    {
        var shop = await SignUpAsync("woodwork");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var ply = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Plywood 12 mm 8x4");
        (await shop.PutAsJsonAsync($"/api/v1/products/{ply.Id}", new { name = ply.Name, unit = ply.Unit, sellingPrice = 1800m, costPrice = 1500m, gstRate = 18m, hsnCode = ply.HsnCode, replaceTax = true }, ApiFactory.Json))
            .EnsureSuccessStatusCode();

        var id = Guid.CreateVersion7();
        var sent = await (await shop.PostJsonAsync("/api/v1/workshop/karigar", new { id, karigar = "Anwar polish works", productId = ply.Id, quantity = 4m })).ReadAsync<KarigarChallanDto>();
        Assert.Equal((1, 1500m), (sent.Number, sent.UnitCost));
        Assert.Equal(-4m, (await Item(shop, ply.Id)).Stock);
        var back = await (await shop.PostJsonAsync($"/api/v1/workshop/karigar/{id}/receive", new { quantity = 3m })).ReadAsync<KarigarChallanDto>();
        Assert.Equal(3m, back.ReceivedQuantity);
        Assert.Equal(-1m, (await Item(shop, ply.Id)).Stock);

        var csv = await shop.GetStringAsync("/api/v1/workshop/itc04.csv");
        Assert.Contains("\"Anwar polish works\"", csv);
        Assert.Contains(",4,6000,", csv);

        // A machine's wear is a cost in the year's profit.
        (await shop.PostJsonAsync("/api/v1/workshop/machines", new { id = Guid.CreateVersion7(), name = "Panel saw", boughtOn = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)), cost = 120000m })).EnsureSuccessStatusCode();
        var pnl = await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl");
        Assert.True(pnl.MachineWear > 0);
        Assert.Equal(pnl.GoodsProfit - pnl.Returns - pnl.StockLoss - pnl.Expenses - pnl.MachineWear, pnl.NetProfit);
    }

    [Fact]
    public async Task Wages_to_one_karigar_past_the_tds_limits_are_flagged()
    {
        var shop = await SignUpAsync("woodwork");
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/jobs", new { id, title = "Wardrobe", price = 200000m })).EnsureSuccessStatusCode();
        async Task Pay(string who, decimal amount) =>
            (await shop.PostJsonAsync($"/api/v1/jobs/{id}/labour", new { id = Guid.CreateVersion7(), description = who, amount })).EnsureSuccessStatusCode();
        await Pay("Ramesh, 10 days", 25000m);
        await Pay("ramesh , polish", 25000m);
        await Pay("Ramesh, fitting", 35000m);
        await Pay("Salim, 2 days", 2000m);

        var tds = await shop.GetJsonAsync<TdsDto>("/api/v1/workshop/tds");
        var ramesh = Assert.Single(tds.Karigars, k => k.Karigar == "Ramesh");
        Assert.Equal((85000m, 35000m, 3, "single"), (ramesh.Total, ramesh.Largest, ramesh.Payments, ramesh.Alert));
        Assert.Null(Assert.Single(tds.Karigars, k => k.Karigar == "Salim").Alert);
    }

    [Fact]
    public void Turnover_limits_warn_near_and_past_and_44ad_presumes_8_and_6_percent()
    {
        Assert.Empty(ReportEndpoints.TurnoverAlerts(10_000_000m, 0m));
        var near = Assert.Single(ReportEndpoints.TurnoverAlerts(12_500_000m, 0m));
        Assert.Equal(("composition", false), (near.Key, near.Crossed));
        var big = ReportEndpoints.TurnoverAlerts(17_000_000m, 0m);
        Assert.Equal(["composition", "annual_return"], big.Select(a => a.Key));
        Assert.True(big[0].Crossed);
        Assert.Contains(ReportEndpoints.TurnoverAlerts(3_000_000m, 60_000_000m), a => a.Key == "einvoice" && a.Crossed); // last year counts for e-invoices
        Assert.Equal(14_000m, ReportEndpoints.PresumedProfit(100_000m, 100_000m));
    }

    [Fact]
    public async Task A_wedding_is_held_confirmed_settled_on_guests_and_never_double_booked()
    {
        var shop = await SignUpAsync("banquet");
        var hall = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/venues", new { id = hall, name = "Main hall", capacity = 500 })).EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Gupta family");
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(40));

        // A hold keeps the evening; nobody else gets it, even a full-day booking.
        var hold = await (await shop.PostJsonAsync("/api/v1/bookings", new { id = Guid.CreateVersion7(), venueId = hall, date, slot = "evening", customerId = customer, occasion = "wedding", guests = 300, plateRate = 600m, hallRent = 50000m, deposit = 20000m, hold = true })).ReadAsync<BookingDto>();
        Assert.Equal("hold", hold.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/bookings", new { id = Guid.CreateVersion7(), venueId = hall, date, slot = "full_day", customerId = customer, guests = 100, plateRate = 500m, hallRent = 0m, deposit = 0m })).StatusCode);
        var morning = await shop.PostJsonAsync("/api/v1/bookings", new { id = Guid.CreateVersion7(), venueId = hall, date, slot = "morning", customerId = customer, occasion = "birthday", guests = 50, plateRate = 400m, hallRent = 0m, deposit = 0m });
        Assert.Equal(HttpStatusCode.OK, morning.StatusCode); // the morning is still free

        // Confirmed: a job carries the money. ₹50,000 + 300 × ₹600 = ₹2,30,000 expected.
        var booking = await (await shop.PostJsonAsync($"/api/v1/bookings/{hold.Id}/confirm", new { })).ReadAsync<BookingDto>();
        Assert.Equal(("confirmed", 230000m), (booking.Status, booking.Estimate));
        (await shop.PostJsonAsync($"/api/v1/jobs/{booking.JobId}/advance", new { id = Guid.CreateVersion7(), amount = 100000m, mode = "upi" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/jobs/{booking.JobId}/other", new { id = Guid.CreateVersion7(), description = "Decorator", amount = 30000m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/bookings/{hold.Id}/deposit", new { amount = 20000m })).EnsureSuccessStatusCode();

        // 340 came (more than the 300 guaranteed), DJ extra, ₹5,000 damage taken from the deposit.
        var settled = await (await shop.PostJsonAsync($"/api/v1/bookings/{hold.Id}/settle", new
        {
            billId = Guid.CreateVersion7(), actualGuests = 340, extras = new[] { new { name = "DJ", amount = 15000m } }, damage = 5000m, paidAmount = 0m,
        })).ReadAsync<BookingDto>();
        Assert.Equal(("settled", 15000m), (settled.Status, settled.DepositReturned));
        // Bill: 50,000 + 340 × 600 + 15,000 + 5,000 = 2,74,000; the advance 1,00,000 and 5,000 from the deposit are paid.
        Assert.Equal(169000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);
        var job = (await shop.GetJsonAsync<JobDetail>($"/api/v1/jobs/{booking.JobId}")).Job;
        Assert.Equal((JobStatus.Delivered, 274000m, 30000m), (job.Status, job.Price, job.Other));
    }

    [Fact]
    public async Task Cancelling_keeps_the_charge_on_the_khata_and_frees_the_slot()
    {
        var shop = await SignUpAsync("banquet");
        var hall = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/venues", new { id = hall, name = "Garden" });
        var customer = await KhataTests.CreateCustomerAsync(shop, "Sharma");
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        var b = await (await shop.PostJsonAsync("/api/v1/bookings", new { id = Guid.CreateVersion7(), venueId = hall, date, slot = "full_day", customerId = customer, occasion = "anniversary", guests = 80, plateRate = 500m, hallRent = 10000m, deposit = 0m })).ReadAsync<BookingDto>();
        (await shop.PostJsonAsync($"/api/v1/jobs/{b.JobId}/advance", new { id = Guid.CreateVersion7(), amount = 20000m })).EnsureSuccessStatusCode();

        var cancelled = await (await shop.PostJsonAsync($"/api/v1/bookings/{b.Id}/cancel", new { reason = "Date changed", keep = 5000m })).ReadAsync<BookingDto>();
        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal(-15000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance); // ₹15,000 to give back
        Assert.Equal(HttpStatusCode.OK, (await shop.PostJsonAsync("/api/v1/bookings", new { id = Guid.CreateVersion7(), venueId = hall, date, slot = "evening", customerId = customer, guests = 50, plateRate = 400m, hallRent = 0m, deposit = 0m })).StatusCode);
    }

    [Fact]
    public async Task A_birthday_table_is_booked_with_a_deposit_that_the_bill_takes_off()
    {
        var shop = await SignUpAsync("restaurant");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Riya's family");
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var id = Guid.CreateVersion7();
        var booking = await (await shop.PostJsonAsync("/api/v1/table-bookings", new
        {
            id, date, time = "20:00", people = 12, customerId = customer, occasion = "birthday", cake = "1 kg chocolate, Happy Birthday Riya", decoration = true, table = "Rooftop",
        })).ReadAsync<TableBookingDto>();
        Assert.Equal(("Riya's family", PartyOccasion.Birthday, TableBookingStatus.Booked), (booking.Name, booking.Occasion, booking.Status));
        (await shop.PostJsonAsync("/api/v1/table-bookings", new { id = Guid.CreateVersion7(), date, time = "19:30", people = 4, name = "Walk-in Mehta", phone = "98765 43210" })).EnsureSuccessStatusCode();

        // A ₹2,000 deposit is a khata payment; the ₹6,500 table bill leaves ₹4,500 to pay.
        booking = await (await shop.PostJsonAsync($"/api/v1/table-bookings/{id}/deposit", new { amount = 2000m, mode = "upi" })).ReadAsync<TableBookingDto>();
        Assert.Equal(2000m, booking.Deposit);
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer, paidAmount = 0m, lines = new[] { new { name = "Party dinner", quantity = 1m, unit = "piece", unitPrice = 6500m } } })).EnsureSuccessStatusCode();
        Assert.Equal(4500m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);

        (await shop.PostJsonAsync($"/api/v1/table-bookings/{id}/status", new { status = "seated" })).EnsureSuccessStatusCode();
        var day = await shop.GetJsonAsync<TableDayDto>($"/api/v1/table-bookings?date={date:yyyy-MM-dd}");
        Assert.Equal((2, 16), (day.Bookings.Count, day.People));
        Assert.Equal("Walk-in Mehta", day.Bookings[0].Name); // 7:30 before 8:00
    }

    [Fact]
    public async Task A_room_stay_is_never_double_booked_and_checks_out_on_one_bill()
    {
        var shop = await SignUpAsync("lodge");
        var room = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/rooms", new { id = room, name = "101", kind = "Deluxe", rate = 2000m })).EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Mr Kapoor");
        var today = DateOnly.FromDateTime(DateTime.Today);

        var id = Guid.CreateVersion7();
        var stay = await (await shop.PostJsonAsync("/api/v1/stays", new { id, roomId = room, checkIn = today, checkOut = today.AddDays(3), customerId = customer, guests = 2, commission = 900m, source = "make_my_trip", sourceRef = "MMT123", foreigner = true })).ReadAsync<StayDto>();
        Assert.Equal((3, 6000m), (stay.Nights, stay.RoomTotal));
        // Night 2 is taken; the morning it frees up, the room can be booked again.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/stays", new { id = Guid.CreateVersion7(), roomId = room, checkIn = today.AddDays(2), checkOut = today.AddDays(4), guestName = "Late guest", guests = 1 })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/stays", new { id = Guid.CreateVersion7(), roomId = room, checkIn = today.AddDays(3), checkOut = today.AddDays(4), guestName = "Next guest", guests = 1 })).EnsureSuccessStatusCode();

        // Check-in keeps only the last four of the ID; a foreign guest's Form C is due until marked filed.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/stays/{id}/check-in", new { idType = "Passport" })).StatusCode);
        stay = await (await shop.PostJsonAsync($"/api/v1/stays/{id}/check-in", new { idType = "Passport", idNumber = "Z1234567" })).ReadAsync<StayDto>();
        Assert.Equal((StayStatus.CheckedIn, "4567", true), (stay.Status, stay.IdLast4, stay.FormCDue));
        Assert.False((await (await shop.PostJsonAsync($"/api/v1/stays/{id}/form-c", new { })).ReadAsync<StayDto>()).FormCDue);

        (await shop.PostJsonAsync($"/api/v1/stays/{id}/advance", new { amount = 3000m, mode = "upi" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/stays/{id}/charges", new { id = Guid.CreateVersion7(), name = "Dinner", amount = 450m })).EnsureSuccessStatusCode();
        stay = await (await shop.PostJsonAsync($"/api/v1/stays/{id}/checkout", new { billId = Guid.CreateVersion7(), paidAmount = 0m })).ReadAsync<StayDto>();
        Assert.Equal((StayStatus.CheckedOut, 6450m), (stay.Status, stay.Total));
        Assert.NotNull(stay.BillId);
        Assert.Equal(3450m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance); // 6,450 less the 3,000 advance
        var kharcha = await shop.GetJsonAsync<ExpensePageDto>("/api/v1/expenses");
        Assert.Contains(kharcha.Lines, e => e.Amount == 900m && e.Note!.Contains("MMT123"));
    }

    [Fact]
    public async Task Season_prices_set_each_night_and_a_gst_shop_bills_the_room_at_room_gst()
    {
        var shop = await SignUpAsync("lodge");
        var settings = await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        (await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z5" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var room = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/rooms", new { id = room, name = "301", rate = 2000m })).EnsureSuccessStatusCode();
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Festival: the first two nights at ₹3,500 for every room.
        (await shop.PostJsonAsync("/api/v1/room-rates", new { id = Guid.CreateVersion7(), name = "Diwali", from = today, to = today.AddDays(1), rate = 3500m })).EnsureSuccessStatusCode();
        var quote = await shop.GetJsonAsync<QuoteDto>($"/api/v1/rooms/{room}/quote?checkIn={today:yyyy-MM-dd}&checkOut={today.AddDays(3):yyyy-MM-dd}");
        Assert.Equal((3, 9000m), (quote.Nights, quote.Total));

        var id = Guid.CreateVersion7();
        var stay = await (await shop.PostJsonAsync("/api/v1/stays", new { id, roomId = room, checkIn = today, checkOut = today.AddDays(3), guestName = "Walk-in Bose", guests = 2 })).ReadAsync<StayDto>();
        Assert.Equal(9000m, stay.RoomTotal);
        (await shop.PostJsonAsync($"/api/v1/stays/{id}/check-in", new { idType = "Aadhaar", idNumber = "1234 5678 9012" })).EnsureSuccessStatusCode();
        stay = await (await shop.PostJsonAsync($"/api/v1/stays/{id}/checkout", new { billId = Guid.CreateVersion7(), paidAmount = 9000m, mode = "upi" })).ReadAsync<StayDto>();
        var bill = await shop.GetJsonAsync<BillView>($"/api/v1/bills/{stay.BillId}");
        var line = Assert.Single(bill.Bill.Lines);
        Assert.Equal((5m, "996311", 9000m), (line.GstRate, line.HsnCode, line.Amount)); // ₹3,000 a night on average: 5%
        Assert.NotNull(line.TaxableValue);
        Assert.Equal((0m, 5m, 18m), (Stays.RoomGstRate(900m), Stays.RoomGstRate(7500m), Stays.RoomGstRate(7501m)));
    }

    [Fact]
    public async Task Rooms_taken_for_a_wedding_go_on_the_functions_final_bill()
    {
        var shop = await SignUpAsync("banquet");
        var hall = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/venues", new { id = hall, name = "Main hall" });
        var room = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/rooms", new { id = room, name = "Baraat 1", rate = 1500m })).EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Jain family");
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(20));
        var function = await (await shop.PostJsonAsync("/api/v1/bookings", new { id = Guid.CreateVersion7(), venueId = hall, date, slot = "evening", customerId = customer, guests = 100, plateRate = 500m, hallRent = 20000m, deposit = 0m })).ReadAsync<BookingDto>();
        var stay = await (await shop.PostJsonAsync("/api/v1/stays", new { id = Guid.CreateVersion7(), roomId = room, checkIn = date, checkOut = date.AddDays(1), customerId = customer, guests = 4, bookingId = function.Id })).ReadAsync<StayDto>();
        Assert.Equal(function.Id, stay.BookingId);
        Assert.Single(await shop.GetJsonAsync<List<StayDto>>($"/api/v1/stays?bookingId={function.Id}"));

        await (await shop.PostJsonAsync($"/api/v1/bookings/{function.Id}/settle", new { billId = Guid.CreateVersion7(), actualGuests = 100, damage = 0m, paidAmount = 0m })).ReadAsync<BookingDto>();
        Assert.Equal(71500m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance); // 20,000 + 50,000 + 1,500
        Assert.Equal(StayStatus.CheckedOut, (await shop.GetJsonAsync<StayDto>($"/api/v1/stays/{stay.Id}")).Status);
    }

    [Fact]
    public async Task A_rooms_calendar_link_is_read_by_booking_sites_and_their_calendars_are_understood()
    {
        var shop = await SignUpAsync("lodge");
        var room = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/rooms", new { id = room, name = "5", rate = 1200m });
        var today = DateOnly.FromDateTime(DateTime.Today);
        (await shop.PostJsonAsync("/api/v1/stays", new { id = Guid.CreateVersion7(), roomId = room, checkIn = today.AddDays(1), checkOut = today.AddDays(3), guestName = "Phone guest", guests = 1 })).EnsureSuccessStatusCode();
        var path = (await shop.GetJsonAsync<List<RoomDto>>("/api/v1/rooms/calendars")).Single().IcalExportPath!;
        var ics = await api.CreateClient().GetStringAsync(path); // read with no sign-in, as a booking site does
        Assert.Contains($"DTSTART;VALUE=DATE:{today.AddDays(1):yyyyMMdd}", ics);
        Assert.DoesNotContain("Phone guest", ics); // no guest details leave the app
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/rooms/{room}/ical", new { importUrl = "http://example.com/cal.ics" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PutAsJsonAsync($"/api/v1/rooms/{room}/ical", new { importUrl = "https://www.airbnb.co.in/calendar/ical/123.ics?s=abc" }, ApiFactory.Json)).StatusCode);

        var events = Ical.Events("BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:abc@airbnb\r\nDTSTART;VALUE=DATE:20261210\r\nDTEND;VALUE=DATE:20261213\r\nSUMMARY:Reserved\r\nEND:VEVENT\r\nEND:VCALENDAR").ToList();
        Assert.Equal(("abc@airbnb", new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 13)), (events[0].Uid, events[0].Start, events[0].End));
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
