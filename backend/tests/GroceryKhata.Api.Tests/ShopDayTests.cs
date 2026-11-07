using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

        // Stock list from the trade's starter items, priced at â‚¹500 and costed at â‚¹350.
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var products = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var stocked = products.Where(p => !p.IsService).ToList(); // a salon's services carry no stock
        var (a, b) = (stocked[0], stocked[1]);
        foreach (var p in new[] { a, b })
        {
            (await shop.PutAsJsonAsync($"/api/v1/products/{p.Id}", new { name = p.Name, unit = p.Unit, sellingPrice = 500m, costPrice = 350m, gstRate = 5m, hsnCode = p.HsnCode, replaceTax = true }, ApiFactory.Json))
                .EnsureSuccessStatusCode();
        }

        // Buying: 20 of each on a supplier bill, â‚¹100 paid now.
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
        // The UPI money received on khata comes in as a Receipt into the UPI bank ledger, credited to the customer.
        Assert.True(gst.Receipts >= 1);
        Assert.Contains("VCHTYPE=\"Receipt\"", xml);
        Assert.Contains("<LEDGERNAME>UPI Collections</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-100.00</AMOUNT>", xml);

        // The CA's own account names: saved once, used in the next file; two accounts can't share a name.
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/gst/ledgers", new Dictionary<string, string> { ["sales"] = "Cash" })).StatusCode);
        (await shop.PutAsJsonAsync("/api/v1/gst/ledgers", new Dictionary<string, string> { ["sales"] = "Sales GST & Local", ["upi"] = "HDFC Bank" })).EnsureSuccessStatusCode();
        var named = await shop.GetJsonAsync<List<GroceryKhata.Api.Ledger.TallyLedgerDto>>("/api/v1/gst/ledgers");
        Assert.Equal(("HDFC Bank", "Output CGST"), (named.Single(x => x.Key == "upi").Name, named.Single(x => x.Key == "cgst").Name));
        using var renamed = new ZipArchive(await (await shop.GetAsync($"/api/v1/gst/export.zip?month={gst.Month}")).Content.ReadAsStreamAsync());
        var xml2 = await new StreamReader(renamed.GetEntry("2-vouchers.xml")!.Open()).ReadToEndAsync();
        Assert.Contains("<LEDGERNAME>HDFC Bank</LEDGERNAME>", xml2);
        Assert.Contains("<LEDGERNAME>Sales GST &amp; Local</LEDGERNAME>", xml2);
        Assert.DoesNotContain("UPI Collections", xml2);
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
    public async Task A_grocery_shop_picks_from_a_long_list_of_staples_masala_dry_fruits_and_home_care_by_size()
    {
        var (shop, _) = await api.SignUpAsync();
        var list = await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items");
        Assert.True(list.Count >= 900, $"only {list.Count} grocery master items");
        foreach (var name in new[] { "Kabuli chana", "Nutrela", "Kaju", "Colgate", "Lifebuoy", "MDH", "Surf Excel", "Parle-G" })
        {
            Assert.Contains(list, i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Equal(list.Count, list.Select(i => i.Name.ToLowerInvariant()).Distinct().Count());
        Assert.InRange(list.Select(i => i.Category).Distinct().Count(), 12, 25);

        // A branded atta comes in sizes: picking it gives one shop item per size, sold by the packet.
        var atta = list.Single(i => i.Name == "Aashirvaad Whole Wheat Atta");
        Assert.Equal(["1 kg", "5 kg", "10 kg"], atta.PackSizes);
        var picked = await (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = new[] { atta.Id } })).ReadAsync<ImportResult>();
        Assert.Equal(3, picked.Created);
        var mine = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        // Named in Hindi where the list has a Hindi name, as the rest of the grocery list is.
        Assert.Contains(mine, p => p.Name == "आशीर्वाद आटा 5 kg" && p.Unit == Unit.Packet && p.HsnCode == "1101");
    }

    [Fact]
    public async Task A_sales_and_service_shop_sells_units_by_serial_with_the_warranty_on_the_bill()
    {
        var shop = await SignUpAsync("sales_service");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var battery = items.Single(p => p.Name.StartsWith("Inverter battery"));
        Assert.Equal((true, 36), (battery.TracksSerials, battery.WarrantyMonths!.Value));
        Assert.False(items.Single(p => p.Name == "Capacitor 36 mfd").TracksSerials);
        Assert.True(items.Single(p => p.Name == "Repair labour").IsService);

        // Two batteries come in; their serials are noted (a repeat is skipped).
        (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = battery.Id, kind = "purchase", quantity = 2m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/serials/receive", new { productId = battery.Id, serials = new[] { "lum 001", "LUM002" } })).EnsureSuccessStatusCode();
        Assert.Equal(2, (await shop.GetJsonAsync<List<SerialDto>>($"/api/v1/serials?productId={battery.Id}&status=in_stock")).Count);

        object Bill(params string[] serials) => new
        {
            id = Guid.CreateVersion7(), lines = new[] { new { productId = battery.Id, quantity = 1m, unitPrice = 14500m } }, paidAmount = 14500m, paymentMode = "upi",
            serials = serials.Select(s => new { productId = battery.Id, serialNo = s }).ToArray(),
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Bill())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Bill("NOPE999"))).StatusCode);
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", Bill("LUM001"))).ReadAsync<BillDto>();
        Assert.StartsWith("S/N LUM001 · warranty till", bill.Lines[0].Batch);

        var sold = Assert.Single(await shop.GetJsonAsync<List<SerialDto>>("/api/v1/serials/lookup?serial=lum001"));
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        Assert.Equal((SerialStatus.Sold, today.AddMonths(36).AddDays(-1), true, false), (sold.Status, sold.WarrantyUntil!.Value, sold.InWarranty, sold.WarrantyRegistered));
        Assert.True((await (await shop.PostJsonAsync($"/api/v1/serials/{sold.Id}/registered", new { })).ReadAsync<SerialDto>()).WarrantyRegistered);
        // Already sold: it can't go out again.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Bill("LUM001"))).StatusCode);

        // Cancelling the bill puts the unit back in stock.
        (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/cancel", new { reason = "Wrong model" })).EnsureSuccessStatusCode();
        Assert.Equal(SerialStatus.InStock, Assert.Single(await shop.GetJsonAsync<List<SerialDto>>("/api/v1/serials/lookup?serial=LUM001")).Status);
    }

    [Fact]
    public async Task A_service_job_finds_the_customer_and_warranty_from_the_serial_and_closes_free_under_warranty()
    {
        var shop = await SignUpAsync("sales_service");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var pump = items.First(p => p.TracksSerials && p.Name.Contains("pump", StringComparison.OrdinalIgnoreCase));
        var capacitor = items.Single(p => p.Name == "Capacitor 36 mfd");
        (await shop.PutAsJsonAsync($"/api/v1/products/{capacitor.Id}", new { name = capacitor.Name, unit = "piece", sellingPrice = 180m, costPrice = 120m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var farmer = await (await shop.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = "Ramdeen" })).ReadAsync<CustomerDto>();

        (await shop.PostJsonAsync("/api/v1/serials/receive", new { productId = pump.Id, serials = new[] { "PMP77" } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = farmer.Id, lines = new[] { new { productId = pump.Id, quantity = 1m, unitPrice = 14500m } }, paidAmount = 14500m, paymentMode = "cash",
            serials = new[] { new { productId = pump.Id, serialNo = "PMP77" } },
        })).EnsureSuccessStatusCode();

        // The pump comes back: the serial brings the farmer and the warranty.
        var id = Guid.CreateVersion7();
        var job = (await (await shop.PostJsonAsync("/api/v1/jobs", new { id, title = "Pump repair", price = 0m, serialNo = "pmp 77", complaint = "Hums, no water" })).ReadAsync<JobDetail>()).Job;
        Assert.Equal(("PMP77", farmer.Id, true, "Hums, no water"), (job.SerialNo, job.CustomerId!.Value, job.InWarranty, job.Complaint));
        (await shop.PostJsonAsync($"/api/v1/jobs/{id}/material", new { id = Guid.CreateVersion7(), productId = capacitor.Id, quantity = 1m })).EnsureSuccessStatusCode();

        // Free under warranty: delivered with no bill; the part stays as the job's cost.
        job = (await (await shop.PostJsonAsync($"/api/v1/jobs/{id}/deliver", new { billId = Guid.CreateVersion7(), paidAmount = 0m })).ReadAsync<JobDetail>()).Job;
        Assert.Equal((JobStatus.Delivered, (Guid?)null, 120m), (job.Status, job.BillId, job.Material));

        // A unit sold by someone else is just noted, out of warranty, and must be charged.
        var other = Guid.CreateVersion7();
        var outside = (await (await shop.PostJsonAsync("/api/v1/jobs", new { id = other, title = "Motor rewinding", price = 0m, serialNo = "XYZ1" })).ReadAsync<JobDetail>()).Job;
        Assert.Equal(("XYZ1", false, (Guid?)null), (outside.SerialNo, outside.InWarranty, outside.CustomerId));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/jobs/{other}/deliver", new { billId = Guid.CreateVersion7(), paidAmount = 0m })).StatusCode);
    }

    [Fact]
    public async Task A_warranty_claim_is_raised_from_the_job_owed_until_the_company_credits_or_rejects_it()
    {
        var shop = await SignUpAsync("sales_service");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var capacitor = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Capacitor 36 mfd");
        (await shop.PutAsJsonAsync($"/api/v1/products/{capacitor.Id}", new { name = capacitor.Name, unit = "piece", sellingPrice = 180m, costPrice = 120m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var company = await (await shop.PostJsonAsync("/api/v1/suppliers", new { id = Guid.CreateVersion7(), name = "Crompton (company)" })).ReadAsync<SupplierDto>();

        var jobId = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/jobs", new { id = jobId, title = "Pump repair", price = 0m, serialNo = "CRP9" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/jobs/{jobId}/material", new { id = Guid.CreateVersion7(), productId = capacitor.Id, quantity = 1m })).EnsureSuccessStatusCode();

        // From the job: serial, part and its cost come in; the company also pays a visit charge.
        var claim = await (await shop.PostJsonAsync("/api/v1/claims", new { id = Guid.CreateVersion7(), supplierId = company.Id, jobId, labour = 250m })).ReadAsync<WarrantyClaimDto>();
        Assert.Equal(("CRP9", "Capacitor 36 mfd", 120m, 370m, 1), (claim.SerialNo, claim.Description, claim.Parts, claim.Amount, claim.JobNumber!.Value));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/claims", new { id = Guid.CreateVersion7(), supplierId = company.Id, description = "x", labour = 0m })).StatusCode);

        (await shop.PostJsonAsync($"/api/v1/claims/{claim.Id}/dispatch", new { dispatchNo = "DTDC 4471" })).EnsureSuccessStatusCode();
        Assert.Equal("DTDC 4471", Assert.Single(await shop.GetJsonAsync<List<WarrantyClaimDto>>("/api/v1/claims")).DispatchNo);

        // The company credits part of it: off the shop's account with them; the rest is the loss.
        (await shop.PostJsonAsync($"/api/v1/claims/{claim.Id}/settle", new { amount = 300m, creditNoteNo = "CN-88" })).EnsureSuccessStatusCode();
        Assert.Empty(await shop.GetJsonAsync<List<WarrantyClaimDto>>("/api/v1/claims"));
        var done = Assert.Single(await shop.GetJsonAsync<List<WarrantyClaimDto>>("/api/v1/claims?all=true"));
        Assert.Equal((300m, 70m), (done.CreditAmount!.Value, done.Loss));

        // A rejection needs a reason.
        var other = await (await shop.PostJsonAsync("/api/v1/claims", new { id = Guid.CreateVersion7(), supplierId = company.Id, description = "PCB", parts = 900m, labour = 0m })).ReadAsync<WarrantyClaimDto>();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/claims/{other.Id}/settle", new { amount = 0m })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/claims/{other.Id}/settle", new { amount = 0m, rejectReason = "Serial mismatch" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_technician_trip_settles_into_the_kharcha_book_shares_its_cost_across_jobs_and_carries_the_cash_forward()
    {
        var shop = await SignUpAsync("sales_service");
        var (jobA, jobB) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        (await shop.PostJsonAsync("/api/v1/jobs", new { id = jobA, title = "Solar pump, Rampur", price = 2000m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/jobs", new { id = jobB, title = "Motor repair, Rampur", price = 1500m })).EnsureSuccessStatusCode();

        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/trips", new { id, technician = "Suresh", place = "Rampur" })).EnsureSuccessStatusCode();
        async Task<TripDto> Add(string kind, decimal amount) =>
            await (await shop.PostJsonAsync($"/api/v1/trips/{id}/entries", new { id = Guid.CreateVersion7(), kind, amount })).ReadAsync<TripDto>();
        await Add("advance", 2000m);
        await Add("travel", 600m);
        await Add("food", 250m);
        await Add("stay", 400m);
        var trip = await Add("collected", 1500m);
        Assert.Equal((2000m, 1250m, 1500m, 2250m), (trip.Advance, trip.Spent, trip.Collected, trip.Holding));
        (await shop.PostJsonAsync($"/api/v1/trips/{id}/jobs", new { jobId = jobA })).EnsureSuccessStatusCode();
        Assert.Equal(2, (await (await shop.PostJsonAsync($"/api/v1/trips/{id}/jobs", new { jobId = jobB })).ReadAsync<TripDto>()).Jobs.Count);

        // He hands back 2,000 of the 2,250 he holds; 250 carries to his next trip.
        trip = await (await shop.PostJsonAsync($"/api/v1/trips/{id}/settle", new { returned = 2000m })).ReadAsync<TripDto>();
        Assert.Equal(250m, trip.Holding);
        Assert.Equal((625m, 625m), ((await shop.GetJsonAsync<JobDetail>($"/api/v1/jobs/{jobA}")).Job.Other, (await shop.GetJsonAsync<JobDetail>($"/api/v1/jobs/{jobB}")).Job.Other));
        Assert.Equal(HttpStatusCode.NotFound, (await shop.PostJsonAsync($"/api/v1/trips/{id}/entries", new { id = Guid.CreateVersion7(), kind = "food", amount = 50m })).StatusCode);

        var next = Guid.CreateVersion7();
        var second = await (await shop.PostJsonAsync("/api/v1/trips", new { id = next, technician = "Suresh", place = "Bilaspur" })).ReadAsync<TripDto>();
        Assert.Equal((250m, 250m), (second.BroughtForward, second.Holding));
    }

    [Fact]
    public async Task An_amc_bills_its_fee_schedules_visits_that_close_free_and_renews_from_its_end()
    {
        var shop = await SignUpAsync("sales_service");
        var customer = await (await shop.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = "Gupta ji", phone = ApiFactory.NewPhone() })).ReadAsync<CustomerDto>();
        var start = new DateOnly(2026, 1, 1);

        var amc = await (await shop.PostJsonAsync("/api/v1/amc", new
        {
            id = Guid.CreateVersion7(), customerId = customer.Id, title = "Inverter + battery", serialNo = "exd 9", startOn = start, months = 12, visits = 2, fee = 1200m, paidAmount = 0m,
        })).ReadAsync<AmcDto>();
        Assert.Equal((new DateOnly(2026, 12, 31), "EXD9", 0, new DateOnly(2026, 5, 2)), (amc.EndsOn, amc.SerialNo, amc.VisitsDone, amc.NextDue!.Value));
        Assert.Equal(1200m, (await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers")).Single(c => c.Id == customer.Id).Balance);

        // Visits open jobs that close without a bill; the third is refused.
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var jobId = Guid.CreateVersion7();
            amc = await (await shop.PostJsonAsync($"/api/v1/amc/{amc.Id}/visit", new { jobId })).ReadAsync<AmcDto>();
            var job = (await (await shop.PostJsonAsync($"/api/v1/jobs/{jobId}/deliver", new { billId = Guid.CreateVersion7(), paidAmount = 0m })).ReadAsync<JobDetail>()).Job;
            Assert.Equal((JobStatus.Delivered, (Guid?)null, "EXD9"), (job.Status, job.BillId, job.SerialNo));
        }
        Assert.Equal((2, (DateOnly?)null), (amc.VisitsDone, amc.NextDue));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/amc/{amc.Id}/visit", new { jobId = Guid.CreateVersion7() })).StatusCode);

        // Renewing starts the day after it ends, bills again and takes the old one off the list.
        var renewed = await (await shop.PostJsonAsync($"/api/v1/amc/{amc.Id}/renew", new { id = Guid.CreateVersion7(), fee = 1500m, paidAmount = 1500m, mode = "cash" })).ReadAsync<AmcDto>();
        Assert.Equal((1500m, 0, 2), (renewed.Fee, renewed.VisitsDone, renewed.Visits));
        Assert.Equal(renewed.Id, Assert.Single(await shop.GetJsonAsync<List<AmcDto>>("/api/v1/amc")).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/amc/{amc.Id}/renew", new { id = Guid.CreateVersion7(), paidAmount = 0m })).StatusCode);
    }

    [Fact]
    public async Task Trip_cash_moves_the_drawer_only_when_given_and_handed_back_and_collections_go_on_the_khata()
    {
        var shop = await SignUpAsync("sales_service");
        var farmer = await (await shop.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = "Ramdeen" })).ReadAsync<CustomerDto>();
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = farmer.Id, type = "credit", amount = 3000m })).EnsureSuccessStatusCode();
        var (big, small) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        (await shop.PostJsonAsync("/api/v1/jobs", new { id = big, title = "Solar pump install", price = 3000m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/jobs", new { id = small, title = "Fan repair", price = 1000m })).EnsureSuccessStatusCode();

        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/trips", new { id, technician = "Suresh", place = "Rampur" })).EnsureSuccessStatusCode();
        async Task<TripDto> Add(object body) => await (await shop.PostJsonAsync($"/api/v1/trips/{id}/entries", body)).ReadAsync<TripDto>();
        await Add(new { id = Guid.CreateVersion7(), kind = "advance", amount = 2000m });
        var photo = "data:image/png;base64," + Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 });
        var trip = await Add(new { id = Guid.CreateVersion7(), kind = "travel", amount = 800m, photoDataUrl = photo });
        Assert.True(trip.Entries.Single(e => e.Kind == TripEntryKind.Travel).HasPhoto);
        var bill = await shop.GetAsync($"/api/v1/trips/photos/{trip.Entries.Single(e => e.Kind == TripEntryKind.Travel).Id}");
        Assert.Equal("image/png", bill.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/trips/{id}/entries", new { id = Guid.CreateVersion7(), kind = "food", amount = 50m, photoDataUrl = "data:text/plain;base64,AAAA" })).StatusCode);

        // Ramdeen pays 1,500 to the technician: off his khata now, but not in the drawer.
        trip = await Add(new { id = Guid.CreateVersion7(), kind = "collected", amount = 1500m, customerId = farmer.Id });
        Assert.Equal(("Ramdeen", 2700m), (trip.Entries.Last().CustomerName, trip.Holding));
        Assert.Equal(1500m, (await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers")).Single(c => c.Id == farmer.Id).Balance);
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal(-2000m, book.NetCash);

        // Settled by job value: the 3,000 job carries 600 of the 800, the 1,000 job 200. The bills don't touch the drawer; the hand-back does.
        (await shop.PostJsonAsync($"/api/v1/trips/{id}/jobs", new { jobId = big })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/trips/{id}/jobs", new { jobId = small })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/trips/{id}/settle", new { returned = 2700m, split = "value" })).EnsureSuccessStatusCode();
        Assert.Equal((600m, 200m), ((await shop.GetJsonAsync<JobDetail>($"/api/v1/jobs/{big}")).Job.Other, (await shop.GetJsonAsync<JobDetail>($"/api/v1/jobs/{small}")).Job.Other));
        book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((700m, 0m), (book.NetCash, book.SpentCash));
        Assert.Equal(new[] { 6m, 4m }, Trips.Shares(10m, [30m, 20m]));
        Assert.Equal(new[] { 3.34m, 3.33m, 3.33m }, Trips.Shares(10m, [0m, 0m, 0m]));
    }

    [Fact]
    public async Task Paint_and_hardware_shops_pick_from_long_brand_lists_with_pack_sizes()
    {
        var paint = await SignUpAsync("paint");
        var paints = await paint.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items?trade=paint");
        Assert.True(paints.Count >= 300, $"only {paints.Count} paint master items");
        foreach (var name in new[] { "Royale", "Apcolite", "Tractor", "Birla Opus", "Birla White", "Berger", "Nerolac", "Dulux", "Fevicol", "Dr. Fixit" })
        {
            Assert.Contains(paints, i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        var royale = paints.First(i => i.Name.StartsWith("Asian Paints Royale Luxury"));
        Assert.Equal(["1 L", "4 L", "10 L", "20 L"], royale.PackSizes);
        Assert.Equal((18m, "3209"), (royale.GstRate!.Value, royale.HsnCode));

        var hardware = await SignUpAsync("hardware");
        var items = await hardware.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items?trade=hardware");
        Assert.True(items.Count >= 300, $"only {items.Count} hardware master items");
        foreach (var name in new[] { "Tiscon", "Wiron", "Agrico", "Pravesh", "Shaktee", "screw", "hinge", "lock", "PVC" })
        {
            Assert.Contains(items, i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Equal(items.Count, items.Select(i => i.Name.ToLowerInvariant()).Distinct().Count());
        Assert.InRange(items.Select(i => i.Category).Distinct().Count(), 10, 20);
    }

    [Fact]
    public async Task A_beauty_shop_picks_from_a_long_list_of_products_with_gst_set_only_where_confirmed()
    {
        var shop = await SignUpAsync("beauty");
        var list = await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items?trade=beauty");
        Assert.True(list.Count >= 800, $"only {list.Count} beauty master items");
        foreach (var name in new[] { "Lakme", "Maybelline", "Himalaya", "Parachute", "L'Oréal Professionnel", "Fogg", "Gillette", "VLCC", "Rica", "Veet" })
        {
            Assert.Contains(list, i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Equal(list.Count, list.Select(i => i.Name.ToLowerInvariant()).Distinct().Count());
        // Hair oil moved to 5% in Sept 2025; a lipstick stays at 18%; the salon's own services are still on the list.
        Assert.Contains(list, i => i.Name.StartsWith("Parachute") && i.GstRate == 5m);
        Assert.Contains(list, i => i.Category == "Makeup" && i.Name.Contains("Lipstick") && i.GstRate == 18m);
        Assert.Contains(list, i => i.Name == "Haircut (men)");
    }

    [Fact]
    public async Task A_master_photo_shows_on_every_shop_item_picked_from_it_until_the_shop_adds_its_own()
    {
        var admin = await AdminAsync();
        var name = $"Aggregate 20 mm {Guid.NewGuid():N}"[..24];
        var item = await (await admin.PostJsonAsync("/api/v1/platform/master-items", new { trade = "hardware", category = "Sand & aggregate", name, unit = "brass" })).ReadAsync<MasterItemDto>();
        byte[] Png(byte red)
        {
            using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(900, 700, new SixLabors.ImageSharp.PixelFormats.Rgba32(red, 120, 120));
            using var stream = new MemoryStream();
            SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, stream);
            return stream.ToArray();
        }

        (await admin.PutAsJsonAsync($"/api/v1/platform/master-items/{item.Id}/image", new { dataUrl = "data:image/png;base64," + Convert.ToBase64String(Png(150)) }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var shop = await SignUpAsync("hardware");
        Assert.NotNull((await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items?trade=hardware")).Single(i => i.Id == item.Id).ImageAt);
        (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = new[] { item.Id } })).EnsureSuccessStatusCode();

        // Picked: the master photo shows, captioned as representative, and the card grid gets the small copy.
        var mine = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == name);
        Assert.Equal((true, MasterPhotos.Representative), (mine.ImageAt != null, mine.ImageCredit));
        var slug = (await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop")).Slug;
        var shopId = (await api.CreateClient().GetJsonAsync<GroceryKhata.Api.Planner.StoreDto>($"/api/v1/store/{slug}")).ShopId;
        var guest = api.CreateClient();
        using (var thumb = SixLabors.ImageSharp.Image.Load(await guest.GetByteArrayAsync($"/api/v1/images/{shopId}/{mine.Id}?v=1&size=thumb")))
        {
            Assert.Equal(320, thumb.Width);
        }

        // The shop's own photo wins, without a caption.
        (await shop.PutAsJsonAsync($"/api/v1/products/{mine.Id}/image", new { dataUrl = "data:image/png;base64," + Convert.ToBase64String(Png(10)) }, ApiFactory.Json)).EnsureSuccessStatusCode();
        mine = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{mine.Id}");
        Assert.Null(mine.ImageCredit);
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.PutAsJsonAsync($"/api/v1/platform/master-items/{item.Id}/image", new { dataUrl = "data:image/png;base64,AAAA" }, ApiFactory.Json)).StatusCode);
    }

    [Fact]
    public async Task An_older_item_with_a_master_items_name_gets_linked_and_shows_its_photo()
    {
        var admin = await AdminAsync();
        var name = $"Binding wire {Guid.NewGuid():N}"[..22];
        var item = await (await admin.PostJsonAsync("/api/v1/platform/master-items", new { trade = "hardware", category = "Wire and mesh", name, unit = "kg" })).ReadAsync<MasterItemDto>();
        using var png = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(600, 600);
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(png, stream);
        (await admin.PutAsJsonAsync($"/api/v1/platform/master-items/{item.Id}/image", new { dataUrl = "data:image/png;base64," + Convert.ToBase64String(stream.ToArray()) }, ApiFactory.Json)).EnsureSuccessStatusCode();

        // Typed in by hand with the same name, before any link existed.
        var shop = await SignUpAsync("hardware");
        var typed = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name, unit = "kg", sellingPrice = 90m })).ReadAsync<ProductDto>();
        Assert.Null(typed.ImageAt);

        // The owner's update check links it; from then on it shows the master photo.
        (await shop.GetAsync("/api/v1/master-items/updates")).EnsureSuccessStatusCode();
        var linked = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{typed.Id}");
        Assert.Equal((true, MasterPhotos.Representative), (linked.ImageAt != null, linked.ImageCredit));
    }

    [Fact]
    public async Task Picking_adds_only_the_chosen_pack_sizes_and_the_list_marks_popular_items()
    {
        var (shop, _) = await api.SignUpAsync();
        var list = await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items");
        Assert.InRange(list.Count(i => i.Popular), 10, 400);

        var atta = list.Single(i => i.Name == "Aashirvaad Whole Wheat Atta");
        var picked = await (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = new[] { atta.Id }, sizes = new Dictionary<string, string[]> { [atta.Id.ToString()] = ["5 kg", "2 kg"] } })).ReadAsync<ImportResult>();
        // Only 5 kg: the item has no 2 kg pack, so that one is ignored rather than invented.
        Assert.Equal(1, picked.Created);
        Assert.Contains(await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products"), p => p.Name == "आशीर्वाद आटा 5 kg");
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
            text = "name,hindi,category,unit,gst,hsn\nHair spa gold,à¤¹à¥‡à¤¯à¤° à¤¸à¥à¤ªà¤¾ à¤—à¥‹à¤²à¥à¤¡,Services,piece,5,999721\nBad line,,,,7,\nFacial,à¤«à¤¼à¥‡à¤¶à¤¿à¤¯à¤²,Services,piece,5,999722",
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

        // Rent of â‚¹4,000 against â‚¹8,000 of wages this month: the job carries half its wages again as overhead.
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

        // Confirmed: a job carries the money. â‚¹50,000 + 300 Ã— â‚¹600 = â‚¹2,30,000 expected.
        var booking = await (await shop.PostJsonAsync($"/api/v1/bookings/{hold.Id}/confirm", new { })).ReadAsync<BookingDto>();
        Assert.Equal(("confirmed", 230000m), (booking.Status, booking.Estimate));
        (await shop.PostJsonAsync($"/api/v1/jobs/{booking.JobId}/advance", new { id = Guid.CreateVersion7(), amount = 100000m, mode = "upi" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/jobs/{booking.JobId}/other", new { id = Guid.CreateVersion7(), description = "Decorator", amount = 30000m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/bookings/{hold.Id}/deposit", new { amount = 20000m })).EnsureSuccessStatusCode();

        // 340 came (more than the 300 guaranteed), DJ extra, â‚¹5,000 damage taken from the deposit.
        var settled = await (await shop.PostJsonAsync($"/api/v1/bookings/{hold.Id}/settle", new
        {
            billId = Guid.CreateVersion7(), actualGuests = 340, extras = new[] { new { name = "DJ", amount = 15000m } }, damage = 5000m, paidAmount = 0m,
        })).ReadAsync<BookingDto>();
        Assert.Equal(("settled", 15000m), (settled.Status, settled.DepositReturned));
        // Bill: 50,000 + 340 Ã— 600 + 15,000 + 5,000 = 2,74,000; the advance 1,00,000 and 5,000 from the deposit are paid.
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
        Assert.Equal(-15000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance); // â‚¹15,000 to give back
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

        // A â‚¹2,000 deposit is a khata payment; the â‚¹6,500 table bill leaves â‚¹4,500 to pay.
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
        // Festival: the first two nights at â‚¹3,500 for every room.
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
        Assert.Equal((5m, "996311", 9000m), (line.GstRate, line.HsnCode, line.Amount)); // â‚¹3,000 a night on average: 5%
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

    [Fact]
    public async Task A_salon_sells_services_without_stock_and_products_with_it()
    {
        var shop = await SignUpAsync("beauty");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var haircut = items.Single(p => p.Name == "Haircut (women)");
        var shampoo = items.Single(p => p.Name == "Shampoo");
        Assert.Equal((true, 45, 5m, "999721"), (haircut.IsService, haircut.DurationMinutes, haircut.GstRate, haircut.HsnCode));
        Assert.Equal((false, 5m), (shampoo.IsService, shampoo.GstRate));
        Assert.False(haircut.IsLow); // a service never shows as low stock

        foreach (var p in new[] { haircut, shampoo })
        {
            (await shop.PutAsJsonAsync($"/api/v1/products/{p.Id}", new { name = p.Name, unit = p.Unit, sellingPrice = 300m, costPrice = (decimal?)null, gstRate = p.GstRate, hsnCode = p.HsnCode }, ApiFactory.Json)).EnsureSuccessStatusCode();
        }

        (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), paidAmount = 600m, paymentMode = "upi",
            lines = new[] { new { productId = haircut.Id, quantity = 1m, unitPrice = 300m }, new { productId = shampoo.Id, quantity = 1m, unitPrice = 300m } },
        })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await Item(shop, haircut.Id)).Stock);
        Assert.Equal(-1m, (await Item(shop, shampoo.Id)).Stock);
        Assert.DoesNotContain(await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products?low=true"), p => p.Id == haircut.Id);

        // A service added by hand keeps its flag, and editing it keeps it too.
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name = "Keratin", unit = "piece", sellingPrice = 2500m, isService = true, durationMinutes = 120 })).EnsureSuccessStatusCode();
        (await shop.PutAsJsonAsync($"/api/v1/products/{id}", new { name = "Keratin", unit = "piece", sellingPrice = 2800m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        Assert.Equal((true, 120), ((await Item(shop, id)).IsService, (await Item(shop, id)).DurationMinutes));
    }

    [Fact]
    public async Task Appointments_keep_a_stylist_in_one_chair_and_bill_the_services_done()
    {
        var shop = await SignUpAsync("beauty");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var (cut, colour) = (items.Single(p => p.Name == "Haircut (women)"), items.Single(p => p.Name == "Hair colour"));
        foreach (var (p, price) in new[] { (cut, 400m), (colour, 1200m) })
        {
            (await shop.PutAsJsonAsync($"/api/v1/products/{p.Id}", new { name = p.Name, unit = p.Unit, sellingPrice = price, gstRate = p.GstRate, hsnCode = p.HsnCode }, ApiFactory.Json)).EnsureSuccessStatusCode();
        }

        var day = await shop.GetJsonAsync<SalonDayDto>("/api/v1/appointments");
        var stylist = day.Stylists[0].Id; // the owner also takes clients
        var customer = await KhataTests.CreateCustomerAsync(shop, "Pooja");
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Haircut (45 min) and colour (90 min) at 11:00 run to 13:15.
        var id = Guid.CreateVersion7();
        var booked = await (await shop.PostJsonAsync("/api/v1/appointments", new { id, date, start = "11:00", stylistId = stylist, customerId = customer, serviceIds = new[] { cut.Id, colour.Id } })).ReadAsync<AppointmentDto>();
        Assert.Equal((new TimeOnly(13, 15), 1600m, AppointmentStatus.Booked), (booked.End, booked.Total, booked.Status));
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/appointments", new { id = Guid.CreateVersion7(), date, start = "13:00", stylistId = stylist, name = "Ritu", serviceIds = new[] { cut.Id } })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/appointments", new { id = Guid.CreateVersion7(), date, start = "13:15", stylistId = stylist, name = "Ritu", serviceIds = new[] { cut.Id } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/appointments", new { id = Guid.CreateVersion7(), date, start = "12:00", name = "Any stylist", serviceIds = new[] { cut.Id } })).EnsureSuccessStatusCode();

        // A walk-in starts now and is already in the chair. "Now" is fixed at 11:00 India time, so the test passes at any hour
        // (a real walk-in at 23:30 can't finish the same day, and is rightly refused).
        var at11 = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(5.5).Date.AddHours(11), DateTimeKind.Unspecified), TimeSpan.FromHours(5.5));
        using var morning = api.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(new FixedClock(at11))));
        var counter = morning.CreateClient();
        counter.DefaultRequestHeaders.Authorization = shop.DefaultRequestHeaders.Authorization;
        var walkIn = await (await counter.PostJsonAsync("/api/v1/appointments", new { id = Guid.CreateVersion7(), date, walkIn = true, serviceIds = new[] { cut.Id } })).ReadAsync<AppointmentDto>();
        Assert.Equal(("Walk-in", AppointmentStatus.Arrived), (walkIn.Name, walkIn.Status));

        (await shop.PostJsonAsync($"/api/v1/appointments/{id}/status", new { status = "arrived" })).EnsureSuccessStatusCode();
        var done = await (await shop.PostJsonAsync($"/api/v1/appointments/{id}/bill", new { billId = Guid.CreateVersion7(), paidAmount = 1000m, mode = "upi" })).ReadAsync<AppointmentDto>();
        Assert.Equal(AppointmentStatus.Done, done.Status);
        Assert.Equal(600m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);
        var bill = await shop.GetJsonAsync<BillView>($"/api/v1/bills/{done.BillId}");
        Assert.All(bill.Bill.Lines, l => Assert.Equal(5m, l.GstRate));
        Assert.Equal(0m, (await Item(shop, cut.Id)).Stock); // services move no stock
        Assert.Equal(3, (await shop.GetJsonAsync<SalonDayDto>($"/api/v1/appointments?date={date:yyyy-MM-dd}")).Appointments.Count(a => a.Status != AppointmentStatus.Arrived));
    }

    [Fact]
    public async Task A_medical_shop_picks_from_a_long_master_list_of_medicines_wellness_and_devices()
    {
        var shop = await SignUpAsync("medical");
        var list = await shop.GetJsonAsync<List<MasterItemDto>>("/api/v1/master-items");
        Assert.True(list.Count >= 800, $"only {list.Count} medical master items");
        Assert.Contains(list, i => i.Name == "Dolo 650 Tablet" && i.GstRate == 5m && i.HsnCode == "3004");
        Assert.Contains(list, i => i.Name.Contains("Chyawanprash", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(list, i => i.Name.Contains("Durex", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(list, i => i.Name.Contains("Hot water", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(list.Count, list.Select(i => i.Name.ToLowerInvariant()).Distinct().Count());
        // About two dozen groups to filter by, not a hundred-odd.
        var groups = list.Select(i => i.Category).Distinct().ToList();
        Assert.InRange(groups.Count, 15, 30);
        Assert.Contains("Sexual wellness & condoms", groups);
        Assert.Contains("Hot, cold & water therapy", groups);
        Assert.Equal("Stomach & digestion", list.Single(i => i.Name.StartsWith("Pantoprazole 40 mg")).Category);

        // Picking adds them to the shop, without a price yet.
        var picked = list.Where(i => i.Name is "Dolo 650 Tablet").Select(i => i.Id).ToArray();
        (await shop.PostJsonAsync("/api/v1/products/from-master", new { ids = picked })).EnsureSuccessStatusCode();
        Assert.Contains(await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products"), p => p.Name == "Dolo 650 Tablet");
    }

    [Fact]
    public async Task A_medical_shop_signs_up_with_its_licence_pharmacist_and_common_medicines()
    {
        var shop = await SignUpAsync("medical");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        Assert.True(items.Count >= 20);
        Assert.All(items.Where(p => p.HsnCode == "3004"), p => Assert.Equal(5m, p.GstRate));

        Assert.False((await shop.GetJsonAsync<PharmacyProfileDto>("/api/v1/pharmacy/profile")).Complete);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/pharmacy/profile", new { gstin = "12345" }, ApiFactory.Json)).StatusCode);

        // Issued almost five years ago: the retention fee is due within 60 days.
        var issued = DateOnly.FromDateTime(DateTime.Today).AddYears(-5).AddDays(30);
        var profile = await (await shop.PutAsJsonAsync("/api/v1/pharmacy/profile",
            new { licenceForm20 = "raj/20/123", licenceForm21 = "RAJ/21/123", licenceIssuedOn = issued, pharmacistName = "Anil Gupta", pharmacistRegNo = "RPC-4567" }, ApiFactory.Json)).ReadAsync<PharmacyProfileDto>();
        Assert.Equal(("RAJ/20/123", true, true, issued.AddYears(5)), (profile.LicenceForm20, profile.Complete, profile.RetentionSoon, profile.RetentionDueOn));
    }

    [Fact]
    public async Task Medicines_sell_from_the_batch_expiring_first_and_expired_stock_is_never_sold()
    {
        var shop = await SignUpAsync("medical");
        var med = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = med, name = "Paracetamol 500 (strip)", unit = "piece", sellingPrice = 30m, gstRate = 5m, hsnCode = "3004" })).EnsureSuccessStatusCode();
        string Month(int add) => DateTime.Today.AddMonths(add).ToString("yyyy-MM");

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/products/{med}/batches", new { id = Guid.CreateVersion7(), batchNo = "OLD", expiryMonth = Month(-2), mrp = 30m, quantity = 5m })).StatusCode);
        // 10 paid + 1 free at â‚¹22 each: â‚¹20 a strip really.
        (await shop.PostJsonAsync($"/api/v1/products/{med}/batches", new { id = Guid.CreateVersion7(), batchNo = "late1", expiryMonth = Month(8), mrp = 32m, costPrice = 22m, quantity = 10m, freeQuantity = 1m })).EnsureSuccessStatusCode();
        var batches = await (await shop.PostJsonAsync($"/api/v1/products/{med}/batches", new { id = Guid.CreateVersion7(), batchNo = "SOON1", expiryMonth = Month(1), mrp = 30m, costPrice = 20m, quantity = 3m })).ReadAsync<List<BatchDto>>();
        Assert.Equal(("SOON1", "LATE1", 20m), (batches[0].BatchNo, batches[1].BatchNo, batches[1].CostPrice));
        Assert.Equal(14m, (await Item(shop, med)).Stock);

        // 5 strips: 3 from the batch expiring next month, 2 from the later one.
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = med, quantity = 5m, unitPrice = 30m } }, paidAmount = 150m, paymentMode = "cash" })).ReadAsync<BillDto>();
        Assert.Contains("SOON1", bill.Lines[0].Batch);
        Assert.Contains("LATE1", bill.Lines[0].Batch);
        var left = await shop.GetJsonAsync<List<BatchDto>>($"/api/v1/products/{med}/batches");
        Assert.Equal(("LATE1", 9m), (Assert.Single(left).BatchNo, left[0].Quantity));
        Assert.Contains((await shop.GetJsonAsync<List<ExpiringDto>>("/api/v1/batches/expiring?days=300")), e => e.BatchNo == "LATE1");

        // More than the batches hold: refused, nothing changes.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = med, quantity = 20m, unitPrice = 30m } }, paidAmount = 600m, paymentMode = "cash" })).StatusCode);
        Assert.Equal(9m, (await Item(shop, med)).Stock);

        // Cancelling the bill puts the strips back on their batches.
        (await shop.PostJsonAsync($"/api/v1/bills/{bill.Id}/cancel", new { reason = "Wrong item" })).EnsureSuccessStatusCode();
        Assert.Equal(2, (await shop.GetJsonAsync<List<BatchDto>>($"/api/v1/products/{med}/batches")).Count);
        Assert.Equal(14m, (await Item(shop, med)).Stock);
    }

    [Fact]
    public async Task Prescription_medicines_need_the_doctor_and_patient_and_H1_sales_fill_the_register()
    {
        var shop = await SignUpAsync("medical");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var (cefixime, ors) = (items.Single(p => p.Name.StartsWith("Cefixime")), items.Single(p => p.Name == "ORS sachet"));
        Assert.Equal((DrugSchedule.H1, (DrugSchedule?)null), (cefixime.Schedule!.Value, ors.Schedule));
        foreach (var p in new[] { cefixime, ors })
        {
            (await shop.PostJsonAsync("/api/v1/stock-movements", new { id = Guid.CreateVersion7(), productId = p.Id, kind = "purchase", quantity = 10m })).EnsureSuccessStatusCode();
        }

        object Bill(object? prescription, params ProductDto[] what) => new
        {
            id = Guid.CreateVersion7(), lines = what.Select(p => new { productId = p.Id, quantity = 1m, unitPrice = 100m }).ToArray(), paidAmount = 100m * what.Length, paymentMode = "cash", prescription,
        };

        // Over the counter: no prescription needed.
        (await shop.PostJsonAsync("/api/v1/bills", Bill(null, ors))).EnsureSuccessStatusCode();
        var refused = await shop.PostJsonAsync("/api/v1/bills", Bill(null, cefixime, ors));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Cefixime", await refused.Content.ReadAsStringAsync());
        // H1 also needs the doctor's address.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Bill(new { doctorName = "Dr. Mehta", patientName = "Sunita" }, cefixime))).StatusCode);

        var bill = await (await shop.PostJsonAsync("/api/v1/bills",
            Bill(new { doctorName = "Dr. Mehta", doctorAddress = "City Clinic, MI Road", doctorRegNo = "RMC 1234", patientName = "Sunita", patientAddress = "Raja Park" }, cefixime, ors))).ReadAsync<BillDto>();
        Assert.Equal("Dr. Mehta", bill.Prescription!.DoctorName);
        Assert.Equal("Sunita", (await shop.GetJsonAsync<BillView>($"/api/v1/bills/{bill.Id}")).Bill.Prescription!.PatientName);

        var register = await shop.GetJsonAsync<List<H1RegisterRow>>("/api/v1/pharmacy/h1-register");
        var row = Assert.Single(register); // ORS isn't H1
        Assert.Equal(("Sunita", "Dr. Mehta", "City Clinic, MI Road", 1m, false), (row.PatientName, row.DoctorName, row.DoctorAddress, row.Quantity, row.Cancelled));
    }

    [Fact]
    public async Task Distributor_bills_bring_batches_with_schemes_and_expiry_returns_wait_for_the_credit_note()
    {
        var shop = await SignUpAsync("medical");
        var med = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = med, name = "Cetirizine (strip)", unit = "piece", sellingPrice = 30m, gstRate = 5m, hsnCode = "3004" })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Shree Pharma Distributors" })).EnsureSuccessStatusCode();
        string Month(int add) => DateTime.Today.AddMonths(add).ToString("yyyy-MM");

        // Batch A: 10 + 1 free at â‚¹22, 10% trade discount = â‚¹198 â†’ â‚¹18 a strip. Batch B: 5 at â‚¹20 = â‚¹100.
        var bill = new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, billNo = "SPD/881", paidAmount = 0m, paymentMode = "cash",
            lines = new object[]
            {
                new { productId = med, quantity = 10m, unitCost = 22m, freeQuantity = 1m, discountPercent = 10m, batchNo = "a1", expiryMonth = Month(2), mrp = 35m },
                new { productId = med, quantity = 5m, unitCost = 20m, batchNo = "B2", expiryMonth = Month(9), mrp = 36m },
            },
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { bill.id, bill.supplierId, bill.paidAmount, lines = new[] { new { productId = med, quantity = 5m, unitCost = 20m, batchNo = "C3", expiryMonth = Month(3) } } })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", bill)).EnsureSuccessStatusCode();
        Assert.Equal(298m, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Supplier.Due);
        var item = await Item(shop, med);
        Assert.Equal((16m, 36m), (item.Stock, item.SellingPrice));
        var batches = await shop.GetJsonAsync<List<BatchDto>>($"/api/v1/products/{med}/batches");
        Assert.Equal(("A1", 11m, 18m), (batches[0].BatchNo, batches[0].Quantity, batches[0].CostPrice));

        // Batch A is near expiry: 6 strips go back (worth â‚¹108). The due stays until the credit note comes.
        var returnId = Guid.CreateVersion7();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/returns", new { id = returnId, supplierId = supplier, lines = new[] { new { batchId = batches[0].Id, quantity = 20m } } })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/suppliers/returns", new { id = returnId, supplierId = supplier, lines = new[] { new { batchId = batches[0].Id, quantity = 6m } } })).EnsureSuccessStatusCode();
        Assert.Equal(10m, (await Item(shop, med)).Stock);
        var open = Assert.Single(await shop.GetJsonAsync<List<SupplierReturnDto>>("/api/v1/suppliers/returns"));
        Assert.Equal((108m, (decimal?)null), (open.Value, open.CreditAmount));
        Assert.Equal(298m, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Supplier.Due);

        // The credit note gives â‚¹100: the due comes down, â‚¹8 is the expiry loss.
        (await shop.PostJsonAsync($"/api/v1/suppliers/returns/{returnId}/credit-note", new { creditNoteNo = "CN-42", amount = 100m })).EnsureSuccessStatusCode();
        Assert.Empty(await shop.GetJsonAsync<List<SupplierReturnDto>>("/api/v1/suppliers/returns"));
        Assert.Equal(8m, Assert.Single(await shop.GetJsonAsync<List<SupplierReturnDto>>("/api/v1/suppliers/returns?all=true")).Loss);
        Assert.Equal(198m, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Supplier.Due);
    }

    [Fact]
    public async Task A_medical_month_counts_input_gst_cost_of_sales_expiry_loss_and_cash_vs_digital()
    {
        var shop = await SignUpAsync("medical");
        var settings = await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop");
        (await shop.PutAsJsonAsync("/api/v1/shop", settings with { Gstin = "08ABCDE1234F1Z5" }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var med = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = med, name = "Montelukast (strip)", unit = "piece", sellingPrice = 42m, gstRate = 5m, hsnCode = "3004" })).EnsureSuccessStatusCode();
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jaipur Medical Agency" })).EnsureSuccessStatusCode();

        // 10 strips at â‚¹20 + 5% GST: â‚¹210 owed, â‚¹10 of it input credit; each strip costs â‚¹20.
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m,
            lines = new[] { new { productId = med, quantity = 10m, unitCost = 20m, gstRate = 5m, batchNo = "M1", expiryMonth = DateTime.Today.AddMonths(6).ToString("yyyy-MM"), mrp = 42m } },
        })).EnsureSuccessStatusCode();
        Assert.Equal(210m, (await shop.GetJsonAsync<SupplierDetail>($"/api/v1/suppliers/{supplier}")).Supplier.Due);
        Assert.Equal(20m, (await Item(shop, med)).CostPrice);

        // 3 strips for cash, 1 by UPI.
        foreach (var (qty, mode) in new[] { (3m, "cash"), (1m, "upi") })
        {
            (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = med, quantity = qty, unitPrice = 42m } }, paidAmount = qty * 42m, paymentMode = mode })).EnsureSuccessStatusCode();
        }

        var month = await shop.GetJsonAsync<PharmacyMonthDto>("/api/v1/pharmacy/month");
        Assert.Equal((168m, 210m, 10m, 80m), (month.Sales, month.Purchases, month.InputGst, month.CostOfSales));
        Assert.Equal((126m, 42m, 0), (month.CashReceipts, month.DigitalReceipts, month.LinesWithoutCost));
        Assert.Equal(month.SalesTaxable - 80m, month.GrossProfit);
        Assert.Equal(Math.Max(0, month.OutputGst - 10m), month.GstPayable);
    }

    [Fact]
    public async Task Loose_tablets_sell_as_part_of_a_strip_and_the_reorder_draft_covers_a_month()
    {
        var shop = await SignUpAsync("medical");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var para = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Paracetamol 500 mg (strip of 10)");
        Assert.Equal(10, para.UnitsPerPack);
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Shree Pharma", phone = "9829935486" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new
        {
            id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m,
            lines = new[] { new { productId = para.Id, quantity = 5m, unitCost = 14m, batchNo = "P1", expiryMonth = DateTime.Today.AddMonths(8).ToString("yyyy-MM"), mrp = 20m } },
        })).EnsureSuccessStatusCode();

        // 3 loose tablets: 0.3 of a strip at the strip's MRP, ₹6.
        var bill = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = para.Id, name = "Paracetamol 500 mg (3 of 10)", quantity = 0.3m, unitPrice = 20m } }, paidAmount = 6m, paymentMode = "cash" })).ReadAsync<BillDto>();
        Assert.Equal(6m, bill.Total);
        Assert.Equal(4.7m, (await Item(shop, para.Id)).Stock);

        // 4.4 more strips sold this month: with 0.3 left, a month's worth is due from Shree Pharma at the last rate.
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = para.Id, quantity = 4.4m, unitPrice = 20m } }, paidAmount = 88m, paymentMode = "cash" })).EnsureSuccessStatusCode();
        var group = Assert.Single(await shop.GetJsonAsync<List<ReorderGroupDto>>("/api/v1/pharmacy/reorder"), g => g.SupplierId == supplier);
        var line = Assert.Single(group.Lines);
        Assert.Equal((4.7m, 5m, 14m), (line.SoldLast30Days, line.Suggested, line.LastRate!.Value));
        Assert.Equal("9829935486", group.Phone);

        // A prescription photo stays with the bill, for the shop only.
        var png = "data:image/png;base64," + Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 });
        Assert.Equal(HttpStatusCode.NotFound, (await shop.GetAsync($"/api/v1/bills/{bill.Id}/prescription-photo")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PutAsJsonAsync($"/api/v1/bills/{bill.Id}/prescription-photo", new { dataUrl = png }, ApiFactory.Json)).StatusCode);
        Assert.Equal("image/png", (await shop.GetAsync($"/api/v1/bills/{bill.Id}/prescription-photo")).Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync($"/api/v1/bills/{bill.Id}/prescription-photo")).StatusCode);
    }

    [Fact]
    public void Refills_are_due_when_a_regular_patients_usual_gap_runs_out()
    {
        var (riya, sunil, bp, cough) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var today = new DateOnly(2026, 11, 4);
        var sales = new List<(Guid, Guid, DateOnly, decimal)>
        {
            // Riya buys her BP medicine every 30 days; last on 6 Oct, so it's due 5 Nov: shown from 3 Nov.
            (riya, bp, new(2026, 8, 7), 2), (riya, bp, new(2026, 9, 6), 2), (riya, bp, new(2026, 10, 6), 2),
            // Only twice: not regular yet.
            (sunil, cough, new(2026, 10, 1), 1), (sunil, cough, new(2026, 10, 20), 1),
            // Monthly too, but 40 days late: moved on.
            (sunil, bp, new(2026, 6, 25), 1), (sunil, bp, new(2026, 7, 25), 1), (sunil, bp, new(2026, 8, 24), 1),
        };
        var due = Assert.Single(PharmacyAssist.Refills(sales, today));
        Assert.Equal((riya, bp, 30, new DateOnly(2026, 11, 5), -1), (due.CustomerId, due.ProductId, due.EveryDays, due.DueOn, due.DaysLate));
    }

    [Fact]
    public async Task Services_use_their_products_from_stock_and_client_cards_keep_the_formula()
    {
        var shop = await SignUpAsync("beauty");
        var (service, tube) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        (await shop.PostJsonAsync("/api/v1/products", new { id = service, name = "Root touch-up", unit = "piece", sellingPrice = 1000m, isService = true, durationMinutes = 60 })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/products", new { id = tube, name = "Colour tube", unit = "piece", sellingPrice = 450m, costPrice = 300m, openingStock = 10m })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/services/{service}/products", new { products = new[] { new { productId = service, quantity = 1m } } }, ApiFactory.Json)).StatusCode);
        var recipe = await (await shop.PutAsJsonAsync($"/api/v1/services/{service}/products", new { products = new[] { new { productId = tube, quantity = 0.5m } } }, ApiFactory.Json)).ReadAsync<ServiceProductsDto>();
        Assert.Equal(150m, recipe.Cost);

        var customer = await KhataTests.CreateCustomerAsync(shop, "Meena");
        var card = await shop.GetJsonAsync<ClientCardDto>($"/api/v1/customers/{customer}/card");
        Assert.True(card.PatchTestDue);
        card = await (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/card",
            new { colourFormula = "5.0 + 20 vol, 1:1.5", allergies = "PPD", patchTestOn = DateOnly.FromDateTime(DateTime.Today), patchTestReaction = false }, ApiFactory.Json)).ReadAsync<ClientCardDto>();
        Assert.Equal(("5.0 + 20 vol, 1:1.5", false), (card.ColourFormula, card.PatchTestDue));

        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/appointments", new { id, date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), start = "10:00", customerId = customer, serviceIds = new[] { service } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync($"/api/v1/appointments/{id}/bill", new { billId = Guid.CreateVersion7(), paidAmount = 1000m, mode = "upi" })).EnsureSuccessStatusCode();
        Assert.Equal(9.5m, (await Item(shop, tube)).Stock);
        Assert.Equal(0m, (await Item(shop, service)).Stock);
        var visit = Assert.Single((await shop.GetJsonAsync<ClientCardDto>($"/api/v1/customers/{customer}/card")).Visits);
        Assert.Equal(("Root touch-up", 1000m), (visit.Services, visit.Total));
    }

    [Fact]
    public async Task Bridal_bookings_keep_the_stylist_free_take_advances_and_bill_the_rest()
    {
        var shop = await SignUpAsync("beauty");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var cut = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Haircut (women)");
        var stylist = (await shop.GetJsonAsync<SalonDayDto>("/api/v1/appointments")).Stylists[0].Id;
        (await shop.PutAsJsonAsync($"/api/v1/stylists/{stylist}/commission", new { percent = 30m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Riya");
        var (mehndi, wedding) = (DateOnly.FromDateTime(DateTime.Today.AddDays(10)), DateOnly.FromDateTime(DateTime.Today.AddDays(11)));

        // The stylist already has a client at 6 am on the wedding day.
        (await shop.PostJsonAsync("/api/v1/appointments", new { id = Guid.CreateVersion7(), date = wedding, start = "06:00", stylistId = stylist, name = "Early", serviceIds = new[] { cut.Id } })).EnsureSuccessStatusCode();
        object Request(Guid id, string start) => new
        {
            id, customerId = customer, title = "Riya's wedding", atHome = true, address = "12 Civil Lines", travelCharge = 2000m,
            events = new object[] { new { name = "Mehndi", date = mehndi, start = "16:00", minutes = 120, price = 5000m }, new { name = "Bridal makeup", date = wedding, start, minutes = 240, price = 20000m, stylistId = stylist } },
        };
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/bridal", Request(Guid.CreateVersion7(), "06:30"))).StatusCode);
        var id = Guid.CreateVersion7();
        var booking = await (await shop.PostJsonAsync("/api/v1/bridal", Request(id, "08:00"))).ReadAsync<BridalDto>();
        Assert.Equal((27000m, 2), (booking.Total, booking.Events.Count));

        // The stylist is now away 08:00â€“12:00 that day, so the chair can't be booked.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/appointments", new { id = Guid.CreateVersion7(), date = wedding, start = "10:00", stylistId = stylist, name = "Late", serviceIds = new[] { cut.Id } })).StatusCode);

        (await shop.PostJsonAsync($"/api/v1/bridal/{id}/payment", new { amount = 2000m, mode = "upi", trial = true })).EnsureSuccessStatusCode();
        var paid = await (await shop.PostJsonAsync($"/api/v1/bridal/{id}/payment", new { amount = 10000m, mode = "cash" })).ReadAsync<BridalDto>();
        Assert.Equal(12000m, paid.Advance);
        Assert.Equal(-12000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);

        var done = await (await shop.PostJsonAsync($"/api/v1/bridal/{id}/complete", new { billId = Guid.CreateVersion7(), paidAmount = 0m })).ReadAsync<BridalDto>();
        Assert.Equal(BridalStatus.Done, done.Status);
        Assert.Equal(15000m, (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance);
        var bill = await shop.GetJsonAsync<BillView>($"/api/v1/bills/{done.BillId}");
        Assert.Equal(3, bill.Bill.Lines.Count);
        Assert.Equal(6000m, (await shop.GetJsonAsync<EarningsMonthDto>("/api/v1/stylists/earnings")).Stylists.Single(s => s.MembershipId == stylist).Commission);
    }

    [Fact]
    public async Task Stylists_earn_commission_and_tips_and_are_paid_out_into_kharcha()
    {
        var shop = await SignUpAsync("beauty");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var facial = (await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products")).Single(p => p.Name == "Facial");
        (await shop.PutAsJsonAsync($"/api/v1/products/{facial.Id}", new { name = facial.Name, unit = facial.Unit, sellingPrice = 1000m, gstRate = facial.GstRate, hsnCode = facial.HsnCode }, ApiFactory.Json)).EnsureSuccessStatusCode();
        var stylist = (await shop.GetJsonAsync<SalonDayDto>("/api/v1/appointments")).Stylists[0].Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/stylists/{stylist}/commission", new { percent = 120m }, ApiFactory.Json)).StatusCode);
        (await shop.PutAsJsonAsync($"/api/v1/stylists/{stylist}/commission", new { percent = 30m }, ApiFactory.Json)).EnsureSuccessStatusCode();

        // Booked for any stylist; the bill says who did it, with a â‚¹100 tip.
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/appointments", new { id, date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), start = "10:00", name = "Asha", serviceIds = new[] { facial.Id } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/appointments/{id}/bill", new { billId = Guid.CreateVersion7(), paidAmount = 1000m, tip = 100m })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/appointments/{id}/bill", new { billId = Guid.CreateVersion7(), paidAmount = 1000m, mode = "upi", stylistId = stylist, tip = 100m })).EnsureSuccessStatusCode();

        var month = await shop.GetJsonAsync<EarningsMonthDto>("/api/v1/stylists/earnings");
        var me = Assert.Single(month.Stylists, s => s.MembershipId == stylist);
        Assert.Equal((30m, 300m, 100m, 400m), (me.Percent, me.Commission, me.Tips, me.Owed));

        (await shop.PostJsonAsync($"/api/v1/stylists/{stylist}/payout", new { mode = "cash" })).EnsureSuccessStatusCode();
        Assert.Equal(0m, Assert.Single((await shop.GetJsonAsync<EarningsMonthDto>("/api/v1/stylists/earnings")).Stylists, s => s.MembershipId == stylist).Owed);
        var kharcha = await shop.GetJsonAsync<ExpensePageDto>("/api/v1/expenses");
        Assert.Contains(kharcha.Lines, e => e.Amount == 300m && e.Category == ExpenseCategory.Salary); // the tip is not a cost
        Assert.DoesNotContain(kharcha.Lines, e => e.Amount == 100m);
    }

    [Fact]
    public async Task Packages_are_used_session_by_session_and_members_get_their_discount()
    {
        var shop = await SignUpAsync("beauty");
        (await shop.PostJsonAsync("/api/v1/products/starter", new { })).EnsureSuccessStatusCode();
        var items = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        var (facial, cut) = (items.Single(p => p.Name == "Facial"), items.Single(p => p.Name == "Haircut (women)"));
        foreach (var (p, price) in new[] { (facial, 1000m), (cut, 400m) })
        {
            (await shop.PutAsJsonAsync($"/api/v1/products/{p.Id}", new { name = p.Name, unit = p.Unit, sellingPrice = price, gstRate = p.GstRate, hsnCode = p.HsnCode }, ApiFactory.Json)).EnsureSuccessStatusCode();
        }

        var (package, gold) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        (await shop.PostJsonAsync("/api/v1/prepaid-plans", new { id = package, name = "3 facials", kind = "package", price = 2400m, serviceId = facial.Id, sessions = 3, validDays = 90 })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/prepaid-plans", new { id = gold, name = "Gold", kind = "membership", price = 999m, discountPercent = 10m, validDays = 365 })).EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Rekha");
        async Task<decimal> Balance() => (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement")).ClosingBalance;

        // The package is an advance; the membership is sold (billed) and paid now.
        var bought = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/customer-plans", new { id = bought, customerId = customer, planId = package, mode = "upi" })).EnsureSuccessStatusCode();
        Assert.Equal(-2400m, await Balance());
        (await shop.PostJsonAsync("/api/v1/customer-plans", new { id = Guid.CreateVersion7(), customerId = customer, planId = gold, mode = "cash" })).EnsureSuccessStatusCode();
        Assert.Equal(-2400m, await Balance());

        // A facial and a haircut: the facial comes off the package (â‚¹800, its share), the haircut gets 10% off.
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/appointments", new { id, date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), start = "15:00", customerId = customer, serviceIds = new[] { facial.Id, cut.Id } })).EnsureSuccessStatusCode();
        var quote = await shop.GetJsonAsync<AppointmentQuoteDto>($"/api/v1/appointments/{id}/quote");
        Assert.Equal((800m, "package", 360m, "membership", 1160m), (quote.Lines[0].Price, quote.Lines[0].Applied, quote.Lines[1].Price, quote.Lines[1].Applied, quote.Total));
        (await shop.PostJsonAsync($"/api/v1/appointments/{id}/bill", new { billId = Guid.CreateVersion7(), paidAmount = 360m, mode = "upi" })).EnsureSuccessStatusCode();
        Assert.Equal(-1600m, await Balance()); // two sessions still paid for
        var mine = await shop.GetJsonAsync<List<CustomerPlanDto>>($"/api/v1/customer-plans?customerId={customer}");
        Assert.Equal(2, mine.Single(p => p.Kind == PlanKind.Package).SessionsLeft);

        // It runs out with two sessions unused: closing it turns the rest of the advance into sales.
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var expire = new Npgsql.NpgsqlCommand("UPDATE customer_plans SET expires_on = current_date - 1 WHERE id = @id", db);
            expire.Parameters.AddWithValue("id", bought);
            await expire.ExecuteNonQueryAsync();
        }

        Assert.Contains(await shop.GetJsonAsync<List<CustomerPlanDto>>("/api/v1/customer-plans?expiringDays=30"), p => p.Id == bought && p.State == "expired");
        (await shop.PostJsonAsync($"/api/v1/customer-plans/{bought}/close", new { })).EnsureSuccessStatusCode();
        Assert.Equal(0m, await Balance());
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
