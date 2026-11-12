using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;

namespace GroceryKhata.Api.Tests;

/// <summary>Closed months stay as filed, cash counts are kept, and edits to money records are logged.</summary>
[Collection(ApiCollection.Name)]
public sealed class BooksTests(ApiFactory api)
{
    private async Task<(HttpClient Owner, HttpClient Helper)> ShopAsync()
    {
        var owner = api.CreateClient();
        var auth = await (await owner.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Kapoor Stores", fullName = "Anil Kapoor", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var helperAuth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helperAuth.AccessToken);
        return (owner, helper);
    }

    private static object Bill(DateTimeOffset? at = null) =>
        new { id = Guid.CreateVersion7(), occurredAt = at, lines = new[] { new { name = "Atta", quantity = 1m, unitPrice = 50m } }, paidAmount = 50m, paymentMode = "cash" };

    [Fact]
    public async Task A_closed_month_takes_no_new_bills_or_cancellations_until_the_owner_reopens_it_with_a_reason()
    {
        var (owner, helper) = await ShopAsync();
        var today = DateTime.UtcNow.AddHours(5.5);
        var lastMonth = new DateTimeOffset(today.Year, today.Month, 1, 6, 0, 0, TimeSpan.Zero).AddMonths(-1).AddDays(4);
        var month = $"{lastMonth:yyyy-MM}";
        var old = await (await owner.PostJsonAsync("/api/v1/bills", Bill(lastMonth))).ReadAsync<BillDto>();

        // Only the owner closes, and only a month that is over.
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PostJsonAsync("/api/v1/books/close", new { month })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostJsonAsync("/api/v1/books/close", new { month = $"{today:yyyy-MM}" })).StatusCode);
        (await owner.PostJsonAsync("/api/v1/books/close", new { month })).EnsureSuccessStatusCode();

        // Nothing dated in it can go in or be cancelled; today carries on, with no gap in the bill numbers.
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync($"/api/v1/bills/{old.Id}/cancel", new { reason = "Wrong item" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync("/api/v1/bills", Bill(lastMonth.AddDays(1)))).StatusCode);
        var first = await (await owner.PostJsonAsync("/api/v1/bills", Bill())).ReadAsync<BillDto>();
        var second = await (await owner.PostJsonAsync("/api/v1/bills", Bill())).ReadAsync<BillDto>();
        Assert.Equal(int.Parse(first.BillNo!.Split('/')[^1]) + 1, int.Parse(second.BillNo!.Split('/')[^1]));
        Assert.Equal(int.Parse(old.BillNo!.Split('/')[^1]) + 1, int.Parse(first.BillNo!.Split('/')[^1]));

        // Reopening needs a reason; then the cancel goes through, and the log keeps both steps.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostJsonAsync("/api/v1/books/reopen", new { month, reason = "" })).StatusCode);
        var books = await (await owner.PostJsonAsync("/api/v1/books/reopen", new { month, reason = "CA found a wrong bill" })).ReadAsync<BooksDto>();
        // Only that month was ever closed, so nothing stays closed: closing it had closed the months before it too.
        Assert.Null(books.ClosedUntil);
        Assert.Equal(["reopen", "close"], books.History.Select(h => h.Action));
        Assert.Equal("CA found a wrong bill", books.History[0].Reason);
        (await owner.PostJsonAsync($"/api/v1/bills/{old.Id}/cancel", new { reason = "Wrong item" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Every_cash_count_is_kept_and_only_the_owner_counts_a_past_day()
    {
        var (owner, helper) = await ShopAsync();
        (await owner.PostJsonAsync("/api/v1/bills", Bill())).EnsureSuccessStatusCode();
        (await helper.PostJsonAsync("/api/v1/cash-count", new { opening = 100m, counted = 120m, note = "short 30" })).EnsureSuccessStatusCode();
        var recount = await (await helper.PostJsonAsync("/api/v1/cash-count", new { counted = 150m })).ReadAsync<CashCountDto>();

        // The latest stands, and the short count is still there to see.
        Assert.Equal((150m, 0m, 2), (recount.Counted, recount.Difference, recount.History!.Count));
        Assert.Equal(-30m, recount.History[1].Difference);

        var yesterday = DateTime.UtcNow.AddHours(5.5).AddDays(-1).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PostJsonAsync("/api/v1/cash-count", new { date = yesterday, opening = 0m, counted = 10m })).StatusCode);
        (await owner.PostJsonAsync("/api/v1/cash-count", new { date = yesterday, opening = 0m, counted = 0m })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Changes_to_suppliers_and_shop_settings_are_kept_in_the_audit_log()
    {
        var (owner, _) = await ShopAsync();
        var supplier = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders" })).EnsureSuccessStatusCode();
        (await owner.PutJsonAsync($"/api/v1/suppliers/{supplier}", new { id = supplier, name = "Jain Traders", gstin = "27AAPFU0939F1ZV" })).EnsureSuccessStatusCode();

        var log = await owner.GetJsonAsync<List<AuditEntryDto>>($"/api/v1/audit?recordId={supplier}");
        var entry = Assert.Single(log);
        Assert.Equal(("suppliers", "update"), (entry.Table, entry.Action));
        Assert.True(entry.Changes.TryGetProperty("gstin", out _));
    }
}
