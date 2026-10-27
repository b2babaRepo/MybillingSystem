using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;

namespace GroceryKhata.Api.Tests;

/// <summary>Billing and khata, Phase 3: cash book, month-end statements, bill view, audit, fast billing.</summary>
[Collection(ApiCollection.Name)]
public sealed class Phase3Tests(ApiFactory api)
{
    /// <summary>The plan's exit test: one shop day, replayed, matches the shopkeeper's own tally.</summary>
    [Fact]
    public async Task A_shop_day_replayed_matches_the_tally_in_the_cash_book_and_on_the_home_screen()
    {
        var (shop, _) = await api.SignUpAsync();
        var atta = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Atta", unit = "kg", sellingPrice = 44m, openingStock = 50m }))
            .ReadAsync<ProductDto>();
        var suresh = await KhataTests.CreateCustomerAsync(shop, "Suresh");
        var ramesh = await KhataTests.CreateCustomerAsync(shop, "Ramesh");

        // Morning: a walk-in cash sale (5 kg atta + a loose item) = ₹300.
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            lines = new object[] { new { productId = atta.Id, quantity = 5m, unitPrice = 44m }, new { name = "Loose chana", quantity = 1m, unit = "kg", unitPrice = 80m } },
            paidAmount = 300m,
            paymentMode = "cash",
        })).ReadAsync<BillDto>();
        // Suresh: a ₹500 bill, ₹200 by UPI now, ₹300 on khata.
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            customerId = suresh,
            lines = new[] { new { name = "Ration", quantity = 1m, unit = "piece", unitPrice = 500m } },
            paidAmount = 200m,
            paymentMode = "upi",
        })).ReadAsync<BillDto>();
        // Ramesh: ₹340 udhaar; pays ₹100 cash, which turns out to be a mistake and is reversed.
        await KhataTests.AddEntryAsync(shop, ramesh, "credit", 340m);
        var wrongPayment = await KhataTests.AddEntryAsync(shop, ramesh, "payment", 100m);
        await (await shop.PostJsonAsync($"/api/v1/ledger-entries/{wrongPayment.Entry.Id}/reverse", new { id = Guid.CreateVersion7() })).ReadAsync<KhataEntryResponse>();
        // Evening: Suresh pays ₹150 of his khata by UPI.
        await (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = suresh, type = "payment", amount = 150m, paymentMode = "upi" }))
            .ReadAsync<KhataEntryResponse>();

        // The shopkeeper's tally: cash 300 (+100 −100), UPI 200 + 150, sales 800, new udhaar 300 + 340.
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((300m, 350m, 650m, 800m, 640m), (book.Cash, book.Upi, book.Total, book.SalesTotal, book.UdhaarGiven));
        Assert.Equal(5, book.Lines.Count);
        Assert.Contains(book.Lines, l => l.Source == CashBookSource.Reversal && l.Amount == -100m && l.Who == "Ramesh");

        // The home screen agrees with the cash book, the khata balances add up, and stock moved.
        var today = await shop.GetJsonAsync<TodayDto>("/api/v1/dashboard/today");
        Assert.Equal((book.Cash, book.Upi, book.UdhaarGiven, book.SalesTotal), (today.Cash, today.Upi, today.Udhaar, today.Sales));
        Assert.Equal(490m, today.Outstanding); // Suresh 300 − 150, Ramesh 340
        Assert.Empty(await shop.GetJsonAsync<List<BalanceMismatch>>("/api/v1/ledger/reconcile"));
        Assert.Equal(45m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{atta.Id}")).Stock);
    }

    [Fact]
    public async Task A_month_end_statement_carries_previous_dues_forward()
    {
        var (shop, _) = await api.SignUpAsync();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        var now = DateTimeOffset.UtcNow;
        var lastMonthDay = new DateTimeOffset(now.AddMonths(-1).Year, now.AddMonths(-1).Month, 10, 6, 0, 0, TimeSpan.Zero);
        await AddEntryAtAsync(shop, customer, "credit", 1000m, lastMonthDay);
        await AddEntryAtAsync(shop, customer, "payment", 400m, lastMonthDay.AddDays(5));
        await KhataTests.AddEntryAsync(shop, customer, "credit", 250m);

        var thisMonth = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement?month={IndiaMonth(now)}");
        var lastMonth = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement?month={IndiaMonth(lastMonthDay)}");
        var bad = await shop.GetAsync($"/api/v1/customers/{customer}/statement?month=October");

        Assert.Equal((600m, 850m, 1), (thisMonth.OpeningBalance, thisMonth.ClosingBalance, thisMonth.Entries.Count));
        Assert.Equal(850m, thisMonth.Entries[0].RunningBalance);
        Assert.Equal((0m, 600m, 2), (lastMonth.OpeningBalance, lastMonth.ClosingBalance, lastMonth.Entries.Count));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task A_bill_can_be_opened_to_print_or_share_but_only_by_its_shop()
    {
        var (shopA, _) = await api.SignUpAsync("Shop A");
        var (shopB, _) = await api.SignUpAsync("Shop B");
        var customer = await KhataTests.CreateCustomerAsync(shopA, "Suresh");
        var bill = await (await shopA.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            customerId = customer,
            lines = new[] { new { name = "Sugar", quantity = 2m, unit = "kg", unitPrice = 48m } },
            paidAmount = 0m,
        })).ReadAsync<BillDto>();

        var view = await shopA.GetJsonAsync<BillView>($"/api/v1/bills/{bill.Id}");

        Assert.Equal((96m, "Suresh"), (view.Bill.Total, view.Customer!.Name));
        Assert.Equal(HttpStatusCode.NotFound, (await shopB.GetAsync($"/api/v1/bills/{bill.Id}")).StatusCode);
    }

    [Fact]
    public async Task Price_and_customer_edits_are_audited_with_who_and_what_changed()
    {
        var (shop, owner) = await api.SignUpAsync();
        var (other, _) = await api.SignUpAsync("Other shop");
        var atta = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Atta", unit = "kg", sellingPrice = 44m }))
            .ReadAsync<ProductDto>();

        (await shop.PutAsJsonAsync($"/api/v1/products/{atta.Id}", new { name = "Atta", unit = "kg", sellingPrice = 46m, category = "Atta & rice" }, ApiFactory.Json))
            .EnsureSuccessStatusCode();

        var entry = Assert.Single(await shop.GetJsonAsync<List<AuditEntryDto>>($"/api/v1/audit?recordId={atta.Id}"));
        Assert.Equal(("products", "update", owner.User.Id, "Ramesh Sharma"), (entry.Table, entry.Action, entry.UserId, entry.UserName));
        Assert.Equal([44m, 46m], entry.Changes.GetProperty("selling_price").EnumerateArray().Select(v => v.GetDecimal()));
        Assert.Equal(JsonValueKind.Null, entry.Changes.GetProperty("category")[0].ValueKind);
        Assert.False(entry.Changes.TryGetProperty("updated_at", out _));
        Assert.Empty(await other.GetJsonAsync<List<AuditEntryDto>>($"/api/v1/audit?recordId={atta.Id}"));
    }

    [Fact]
    public async Task The_billing_screen_lists_the_most_sold_items_first()
    {
        var (shop, _) = await api.SignUpAsync();
        var products = new List<ProductDto>();
        foreach (var name in new[] { "Atta", "Biscuits", "Candles" })
        {
            products.Add(await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name, unit = "piece", sellingPrice = 10m, openingStock = 100m }))
                .ReadAsync<ProductDto>());
        }

        foreach (var (product, times) in new[] { (products[2], 3), (products[1], 1) })
        {
            for (var i = 0; i < times; i++)
            {
                await (await shop.PostJsonAsync("/api/v1/bills", new
                {
                    id = Guid.CreateVersion7(),
                    lines = new[] { new { productId = product.Id, quantity = 1m, unitPrice = 10m } },
                    paidAmount = 10m,
                })).ReadAsync<BillDto>();
            }
        }

        var popular = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products?sort=popular");
        Assert.Equal(["Candles", "Biscuits", "Atta"], popular.Select(p => p.Name));
    }

    private static async Task AddEntryAtAsync(HttpClient client, Guid customerId, string type, decimal amount, DateTimeOffset occurredAt) =>
        await (await client.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId, type, amount, occurredAt }))
            .ReadAsync<KhataEntryResponse>();

    private static string IndiaMonth(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(5.5)).ToString("yyyy-MM");
}
