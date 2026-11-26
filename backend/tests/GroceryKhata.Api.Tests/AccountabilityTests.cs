using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>Bill serial numbers, how far back a helper may date things, and the record of what admins do.</summary>
[Collection(ApiCollection.Name)]
public sealed class AccountabilityTests(ApiFactory api)
{
    [Fact]
    public async Task Bills_saved_at_the_same_moment_by_many_tills_get_one_number_each_with_no_gaps()
    {
        var (shop, _) = await api.SignUpAsync();
        var year = LedgerService.FinancialYear(DateTimeOffset.UtcNow);
        var saves = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => shop.PostJsonAsync("/api/v1/bills",
            new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Atta", quantity = 1m, unitPrice = 50m } }, paidAmount = 50m, paymentMode = "cash" })));
        var numbers = new List<string?>();
        foreach (var save in saves)
        {
            numbers.Add((await save.ReadAsync<BillDto>()).BillNo);
        }

        Assert.Equal(Enumerable.Range(1, 30).Select(n => $"{year}/{n:0000}"), numbers.Order());
    }

    [Fact]
    public async Task A_bill_refused_at_the_last_step_uses_no_number()
    {
        var shop = api.CreateClient();
        var auth = await (await shop.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Gupta Medical", fullName = "Suresh Gupta", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "medical" })).ReadAsync<AuthResponse>();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var year = LedgerService.FinancialYear(DateTimeOffset.UtcNow);
        var med = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = med, name = "Cetirizine 10 mg", unit = "strip", sellingPrice = 30m })).EnsureSuccessStatusCode();
        var expiry = DateTime.Today.AddMonths(8).ToString("yyyy-MM");
        (await shop.PostJsonAsync($"/api/v1/products/{med}/batches", new { id = Guid.CreateVersion7(), batchNo = "B1", expiryMonth = expiry, mrp = 32m, quantity = 2m })).EnsureSuccessStatusCode();
        object Bill(decimal quantity) => new { id = Guid.CreateVersion7(), lines = new[] { new { productId = med, quantity, unitPrice = 30m } }, paidAmount = 30m * quantity, paymentMode = "cash" };

        Assert.Equal($"{year}/0001", (await (await shop.PostJsonAsync("/api/v1/bills", Bill(1m))).ReadAsync<BillDto>()).BillNo);
        // Only 1 strip left in batches: refused when its batches are taken, after the bill row was already written.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/bills", Bill(5m))).StatusCode);
        Assert.Equal($"{year}/0002", (await (await shop.PostJsonAsync("/api/v1/bills", Bill(1m))).ReadAsync<BillDto>()).BillNo);
    }

    [Fact]
    public async Task Bills_are_numbered_one_two_three_in_the_financial_year_and_a_retry_keeps_its_number()
    {
        var (shop, _) = await api.SignUpAsync();
        var year = LedgerService.FinancialYear(DateTimeOffset.UtcNow);
        var numbers = new List<string?>();
        object? first = null;
        for (var i = 0; i < 3; i++)
        {
            var bill = new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Atta", quantity = 1m, unitPrice = 50m } }, paidAmount = 50m, paymentMode = "cash" };
            first ??= bill;
            numbers.Add((await (await shop.PostJsonAsync("/api/v1/bills", bill)).ReadAsync<BillDto>()).BillNo);
        }

        Assert.Equal([$"{year}/0001", $"{year}/0002", $"{year}/0003"], numbers);
        Assert.Equal($"{year}/0001", (await (await shop.PostJsonAsync("/api/v1/bills", first!)).ReadAsync<BillDto>()).BillNo);

        // Another shop starts its own series.
        var (other, _) = await api.SignUpAsync();
        var theirs = new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Dal", quantity = 1m, unitPrice = 90m } }, paidAmount = 90m, paymentMode = "cash" };
        Assert.Equal($"{year}/0001", (await (await other.PostJsonAsync("/api/v1/bills", theirs)).ReadAsync<BillDto>()).BillNo);
    }

    [Theory]
    [InlineData("2026-10-30T10:00:00Z", "2026-27")]
    [InlineData("2027-03-31T18:00:00Z", "2026-27")] // 31 March, 11:30 pm in India
    [InlineData("2027-03-31T18:31:00Z", "2027-28")] // already 1 April in India
    public void The_financial_year_runs_april_to_march_india_time(string at, string expected) =>
        Assert.Equal(expected, LedgerService.FinancialYear(DateTimeOffset.Parse(at)));

    [Fact]
    public async Task A_helper_cant_date_an_entry_more_than_two_days_back_but_the_owner_can_and_it_shows_as_entered_later()
    {
        var (owner, _) = await api.SignUpAsync();
        var customer = await (await owner.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = "Ramesh" })).ReadAsync<CustomerDto>();
        var helperPhone = ApiFactory.NewPhone();
        await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone = helperPhone, pin = "4826" });
        var helperAuth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone = helperPhone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helperAuth.AccessToken);

        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var old = new { id = Guid.CreateVersion7(), customerId = customer.Id, type = "payment", amount = 100m, paymentMode = "cash", occurredAt = weekAgo };
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/khata-entries", old)).StatusCode);
        var oldBill = new { id = Guid.CreateVersion7(), customerId = customer.Id, lines = new[] { new { name = "Atta", quantity = 1m, unitPrice = 50m } }, paidAmount = 0m, occurredAt = weekAgo };
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/bills", oldBill)).StatusCode);

        // Yesterday is fine for a helper; a week back is fine for the owner, and is marked as entered later.
        var yesterday = new { id = Guid.CreateVersion7(), customerId = customer.Id, type = "credit", amount = 40m, occurredAt = DateTimeOffset.UtcNow.AddHours(-20) };
        Assert.Equal(HttpStatusCode.Created, (await helper.PostJsonAsync("/api/v1/khata-entries", yesterday)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.PostJsonAsync("/api/v1/khata-entries", old)).StatusCode);

        var statement = await owner.GetJsonAsync<JsonElement>($"/api/v1/customers/{customer.Id}/statement");
        var lines = statement.GetProperty("entries").EnumerateArray().ToList();
        Assert.True(lines.Single(l => l.GetProperty("id").GetGuid() == old.id).GetProperty("enteredLate").GetBoolean());
        Assert.False(lines.Single(l => l.GetProperty("id").GetGuid() == yesterday.id).GetProperty("enteredLate").GetBoolean());
    }

    [Fact]
    public async Task What_admins_do_is_recorded_and_super_admins_can_read_it()
    {
        var (_, shopAuth) = await api.SignUpAsync("Bansal Kirana");
        var approver = await AdminAsync("Approver");
        Assert.Equal(HttpStatusCode.OK, (await approver.PostJsonAsync($"/api/v1/platform/shops/{shopAuth.ShopId}/block", new { reason = "Fake documents" })).StatusCode);
        // A refused action isn't recorded as done.
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostJsonAsync("/api/v1/platform/admins", new { phone = ApiFactory.NewPhone(), role = "super" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await approver.GetAsync("/api/v1/platform/activity")).StatusCode);
        var super = await AdminAsync("Super");
        var activity = await super.GetJsonAsync<JsonElement>($"/api/v1/platform/activity?shopId={shopAuth.ShopId}");
        var row = Assert.Single(activity.EnumerateArray());
        Assert.Equal("POST /shops/{id:guid}/block", row.GetProperty("action").GetString());
        Assert.Equal("Bansal Kirana", row.GetProperty("shopName").GetString());
        Assert.Equal("Fake documents", row.GetProperty("detail").GetProperty("body").GetProperty("reason").GetString());
    }

    private async Task<HttpClient> AdminAsync(string role)
    {
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Admin's own shop", phone);
        await using (var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var insert = new NpgsqlCommand("INSERT INTO platform_admins (id, user_id, role, created_at, updated_at) VALUES (gen_random_uuid(), @u, @r, now(), now())", db);
            insert.Parameters.AddWithValue("u", auth.User.Id);
            insert.Parameters.AddWithValue("r", role);
            await insert.ExecuteNonQueryAsync();
        }

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        return client;
    }
}
