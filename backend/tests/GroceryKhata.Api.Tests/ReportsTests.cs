using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>The ageing rule on its own: payments pay off the oldest udhaar first.</summary>
public sealed class AgeingTests
{
    private static readonly DateTimeOffset Today = new(2026, 10, 27, 6, 0, 0, TimeSpan.Zero);

    private static Ageing.Entry At(int daysAgo, decimal amount) => new(Today.AddDays(-daysAgo), amount);

    [Fact]
    public void A_payment_clears_the_oldest_udhaar_first()
    {
        var unpaid = Ageing.Unpaid([At(40, 100m), At(20, 200m), At(2, 50m), At(10, -150m)]);

        Assert.Equal([(Today.AddDays(-20), 150m), (Today.AddDays(-2), 50m)], unpaid.Select(e => (e.At, e.Amount)));
    }

    [Fact]
    public void An_advance_is_used_up_by_later_udhaar()
    {
        var unpaid = Ageing.Unpaid([At(30, -100m), At(20, 60m), At(5, 80m)]);

        Assert.Equal([(Today.AddDays(-5), 40m)], unpaid.Select(e => (e.At, e.Amount)));
    }

    [Fact]
    public void Unpaid_udhaar_always_adds_up_to_the_balance()
    {
        var random = new Random(7);
        for (var run = 0; run < 200; run++)
        {
            var entries = Enumerable.Range(0, 20).Select(_ => At(random.Next(0, 90), random.Next(-500, 800))).Where(e => e.Amount != 0).ToList();
            var balance = entries.Sum(e => e.Amount);
            var unpaid = Ageing.Unpaid(entries).Sum(e => e.Amount);
            Assert.Equal(Math.Max(balance, 0m), balance > 0 ? unpaid : 0m);
        }
    }
}

/// <summary>Phase 6 exit test: report totals match the ledger exactly.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReportsTests(ApiFactory api)
{
    [Fact]
    public async Task The_month_report_matches_the_ledger_and_the_cash_book()
    {
        var (shop, _) = await api.SignUpAsync();
        var atta = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Atta", unit = "kg", sellingPrice = 44m, costPrice = 40m, openingStock = 50m }))
            .ReadAsync<ProductDto>();
        var ramesh = await KhataTests.CreateCustomerAsync(shop, "Ramesh");

        // ₹220 of atta for cash (cost ₹200), a ₹100 loose item by UPI (no cost price), ₹300 udhaar, ₹120 paid back.
        await PostBillAsync(shop, new { productId = (Guid?)atta.Id, name = (string?)null, quantity = 5m, unit = "kg", unitPrice = 44m }, 220m, "cash");
        await PostBillAsync(shop, new { productId = (Guid?)null, name = (string?)"Loose dry fruit", quantity = 1m, unit = "piece", unitPrice = 100m }, 100m, "upi");
        await KhataTests.AddEntryAsync(shop, ramesh, "credit", 300m);
        await KhataTests.AddEntryAsync(shop, ramesh, "payment", 120m);

        var report = await shop.GetJsonAsync<MonthReport>("/api/v1/reports/month");
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");

        Assert.Equal((320m, 2, 340m, 100m), (report.Sales, report.Bills, report.Cash, report.Upi));
        Assert.Equal((300m, 120m, 180m), (report.UdhaarGiven, report.KhataCollected, report.Outstanding));
        Assert.Equal((20m, 0.69m), (report.EstimatedProfit, report.ProfitCoverage)); // only the costed atta counts: 220 of 320
        Assert.Equal((book.Cash, book.Upi), (report.Cash, report.Upi));
        Assert.Equal(report.Sales, report.Days.Sum(d => d.Sales));
        Assert.Equal(report.Cash + report.Upi, report.Days.Sum(d => d.Received));
        Assert.Equal(("Atta", 220m), (report.TopItems[0].Name, report.TopItems[0].Amount));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.GetAsync("/api/v1/reports/month?month=Oct")).StatusCode);
    }

    [Fact]
    public async Task Dues_are_aged_by_the_oldest_unpaid_udhaar_and_add_up_to_what_is_owed()
    {
        var (shop, _) = await api.SignUpAsync();
        var (other, _) = await api.SignUpAsync("Other shop");
        var suresh = await KhataTests.CreateCustomerAsync(shop, "Suresh");
        var anita = await KhataTests.CreateCustomerAsync(shop, "Anita");
        var now = DateTimeOffset.UtcNow;
        await EntryAtAsync(shop, suresh, "credit", 500m, now.AddDays(-45));
        await EntryAtAsync(shop, suresh, "credit", 300m, now.AddDays(-12));
        await EntryAtAsync(shop, suresh, "payment", 200m, now.AddDays(-10));
        await EntryAtAsync(shop, suresh, "credit", 100m, now.AddDays(-1));
        await KhataTests.AddEntryAsync(shop, anita, "credit", 80m);

        var dues = await shop.GetJsonAsync<DuesReport>("/api/v1/reports/dues");

        var s = dues.Customers.Single(c => c.Name == "Suresh");
        Assert.Equal((700m, 100m, 300m, 300m, 45), (s.Balance, s.Days0To7, s.Days8To30, s.Over30, s.OldestDays));
        Assert.Equal("Suresh", dues.Customers[0].Name); // oldest dues first
        Assert.Equal((780m, 180m, 300m, 300m), (dues.Total, dues.Days0To7, dues.Days8To30, dues.Over30));
        Assert.Equal(dues.Total, (await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today")).Outstanding);

        // Marking a reminder shows on the list, and only for this shop's customers.
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync($"/api/v1/customers/{suresh}/reminded", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostJsonAsync($"/api/v1/customers/{suresh}/reminded", new { })).StatusCode);
        Assert.NotNull((await shop.GetJsonAsync<DuesReport>("/api/v1/reports/dues")).Customers.Single(c => c.Name == "Suresh").LastRemindedAt);
        Assert.Empty((await other.GetJsonAsync<DuesReport>("/api/v1/reports/dues")).Customers);
    }

    [Fact]
    public async Task The_dues_download_opens_in_excel_with_hindi_and_defuses_formulas()
    {
        var (shop, _) = await api.SignUpAsync();
        var hindi = await KhataTests.CreateCustomerAsync(shop, "रमेश");
        var trick = await KhataTests.CreateCustomerAsync(shop, "=HYPERLINK(\"x\")");
        await KhataTests.AddEntryAsync(shop, hindi, "credit", 50m);
        await KhataTests.AddEntryAsync(shop, trick, "credit", 10m);

        var response = await shop.GetAsync("/api/v1/reports/dues.csv");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("\"रमेश\"", text);
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", text);
    }

    [Fact]
    public async Task The_owner_sets_the_upi_id_and_reminder_wording_with_checks()
    {
        var (shop, _) = await api.SignUpAsync("Sharma Store");

        var badUpi = await shop.PutAsJsonAsync("/api/v1/shop", new { name = "Sharma Store", upiId = "not a upi" }, ApiFactory.Json);
        var noAmount = await shop.PutAsJsonAsync("/api/v1/shop", new { name = "Sharma Store", reminderTemplate = "Please pay soon" }, ApiFactory.Json);
        var saved = await (await shop.PutAsJsonAsync("/api/v1/shop", new
        {
            name = "Sharma Store",
            area = "Raja Park",
            upiId = "SharmaStore@okaxis",
            reminderTemplate = "Namaste {name} ji, ₹{amount} baaki hai. UPI: {upi}",
        }, ApiFactory.Json)).ReadAsync<ShopSettingsDto>();

        Assert.Equal(HttpStatusCode.BadRequest, badUpi.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noAmount.StatusCode);
        Assert.Equal(("sharmastore@okaxis", "Raja Park"), (saved.UpiId, saved.Area));
        // Lists never compare equal in a record: compare the rest.
        Assert.Equal(saved with { ExtraTypes = null }, (await shop.GetJsonAsync<ShopSettingsDto>("/api/v1/shop")) with { ExtraTypes = null });
    }

    private static async Task PostBillAsync(HttpClient client, object line, decimal paid, string mode) =>
        await (await client.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { line }, paidAmount = paid, paymentMode = mode }))
            .ReadAsync<BillDto>();

    private static async Task EntryAtAsync(HttpClient client, Guid customerId, string type, decimal amount, DateTimeOffset occurredAt) =>
        await (await client.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId, type, amount, occurredAt }))
            .ReadAsync<KhataEntryResponse>();
}
