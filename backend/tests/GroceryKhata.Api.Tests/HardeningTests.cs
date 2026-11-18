using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GroceryKhata.Api.Tests;

/// <summary>Phase 7: production safety, security headers, data export and shop deletion.</summary>
[Collection(ApiCollection.Name)]
public sealed class HardeningTests(ApiFactory api)
{
    [Fact]
    public async Task Every_response_carries_security_headers_and_is_never_cached()
    {
        var (shop, _) = await api.SignUpAsync();

        var response = await shop.GetAsync("/api/v1/customers");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Readiness_checks_the_database()
    {
        var ready = await api.CreateClient().GetAsync("/api/health/ready");
        var body = JsonDocument.Parse(await ready.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(("ready", 0), (body.GetProperty("status").GetString(), body.GetProperty("pendingMigrations").GetInt32()));
    }

    [Fact]
    public void Production_refuses_to_start_with_the_development_signing_key()
    {
        using var production = Production(api.AppConnectionString, "DEVELOPMENT-ONLY-signing-key-change-me-0123456789");

        var error = Assert.Throws<InvalidOperationException>(() => production.CreateClient());
        Assert.Contains("development key", error.Message);
    }

    [Fact]
    public void Production_refuses_a_superuser_database_login_that_would_skip_row_level_security()
    {
        using var production = Production(api.AdminConnectionString + ";Database=grocery_khata", "a-real-random-production-signing-key-0123456789");

        var error = Assert.Throws<InvalidOperationException>(() => production.CreateClient());
        Assert.Contains("superuser", error.Message);
    }

    [Fact]
    public async Task The_owner_can_download_all_the_shops_data_and_only_this_shops()
    {
        var (shopA, _) = await api.SignUpAsync("Shop A");
        var (shopB, _) = await api.SignUpAsync("Shop B");
        var customerA = await KhataTests.CreateCustomerAsync(shopA, "Only in A");
        await KhataTests.AddEntryAsync(shopA, customerA, "credit", 250m);
        await KhataTests.CreateCustomerAsync(shopB, "Only in B");

        var response = await shopA.GetAsync("/api/v1/shop/export");
        var export = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Shop A", export.GetProperty("shop").GetProperty("name").GetString());
        Assert.Equal(["Only in A"], export.GetProperty("customers").EnumerateArray().Select(c => c.GetProperty("name").GetString()));
        Assert.Equal(250m, export.GetProperty("ledgerEntries")[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task A_bill_passcode_guards_cancelling_a_bill_and_needs_the_old_one_to_change()
    {
        var (shop, _) = await api.SignUpAsync("Passcode Store");
        async Task<Guid> NewBillAsync()
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/bills", new { id, lines = new[] { new { name = "Atta", quantity = 1m, unitPrice = 50m } }, paidAmount = 50m, paymentMode = "cash" })).EnsureSuccessStatusCode();
            return id;
        }

        // No passcode: cancelling works as before.
        Assert.True((await shop.PostJsonAsync($"/api/v1/bills/{await NewBillAsync()}/cancel", new { reason = "Wrong item" })).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/shop/bill-passcode", new { passcode = "1234" })).StatusCode);
        (await shop.PutAsJsonAsync("/api/v1/shop/bill-passcode", new { passcode = "4826" })).EnsureSuccessStatusCode();
        Assert.True(JsonDocument.Parse(await shop.GetStringAsync("/api/v1/shop/bill-passcode")).RootElement.GetProperty("set").GetBoolean());

        var bill = await NewBillAsync();
        var bare = await shop.PostJsonAsync($"/api/v1/bills/{bill}/cancel", new { reason = "Wrong item" });
        Assert.Equal(HttpStatusCode.Forbidden, bare.StatusCode);
        Assert.Equal("bill-passcode", JsonDocument.Parse(await bare.Content.ReadAsStringAsync()).RootElement.GetProperty("type").GetString());

        async Task<HttpResponseMessage> CancelWithAsync(string code)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/bills/{bill}/cancel") { Content = JsonContent.Create(new { reason = "Wrong item" }) };
            request.Headers.Add("X-Bill-Passcode", code);
            return await shop.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await CancelWithAsync("1111")).StatusCode);
        Assert.True((await CancelWithAsync("4826")).IsSuccessStatusCode);

        // Changing or removing it needs the one in use.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/shop/bill-passcode", new { passcode = (string?)null })).StatusCode);
        (await shop.PutAsJsonAsync("/api/v1/shop/bill-passcode", new { passcode = (string?)null, current = "4826" })).EnsureSuccessStatusCode();
        Assert.False(JsonDocument.Parse(await shop.GetStringAsync("/api/v1/shop/bill-passcode")).RootElement.GetProperty("set").GetBoolean());
    }

    [Fact]
    public async Task Profit_is_reported_by_customer_bill_and_category_from_each_line_cost()
    {
        var (shop, _) = await api.SignUpAsync("Profit Store");
        var soap = Guid.NewGuid();
        (await shop.PostJsonAsync("/api/v1/products", new { id = soap, name = "Soap", unit = "piece", sellingPrice = 40m, costPrice = 30m, category = "Toiletries", openingStock = 50m })).EnsureSuccessStatusCode();
        var ramesh = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        // Ramesh: 5 soaps (₹50 profit); a walk-in: 2 soaps (₹20) and a loose item with no cost.
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = ramesh, lines = new[] { new { productId = soap, name = "Soap", quantity = 5m, unitPrice = 40m } }, paidAmount = 0m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new object[] { new { productId = soap, name = "Soap", quantity = 2m, unitPrice = 40m }, new { name = "Bag", quantity = 1m, unitPrice = 10m } }, paidAmount = 90m, paymentMode = "cash" })).EnsureSuccessStatusCode();

        var party = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/reports/profit-by?by=party")).RootElement;
        Assert.Equal(70m, party.GetProperty("profit").GetDecimal());
        var rows = party.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal("Ramesh", rows[0].GetProperty("name").GetString());
        Assert.Equal(50m, rows[0].GetProperty("profit").GetDecimal());
        Assert.Equal(10m, rows[1].GetProperty("uncosted").GetDecimal());

        var bills = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/reports/profit-by?by=bill")).RootElement.GetProperty("rows");
        Assert.Equal(2, bills.GetArrayLength());
        var category = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/reports/profit-by?by=category")).RootElement.GetProperty("rows")[0];
        Assert.Equal("Toiletries", category.GetProperty("name").GetString());
        Assert.Equal(70m, category.GetProperty("profit").GetDecimal());

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.GetAsync("/api/v1/reports/profit-by?by=colour")).StatusCode);
    }

    [Fact]
    public async Task Credit_days_give_dues_a_due_date_and_an_overdue_amount_and_old_dues_split_by_age()
    {
        var (shop, _) = await api.SignUpAsync("Ageing Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        // Udhaar 100 days ago, 45 days ago and today.
        foreach (var (days, amount) in new[] { (100, 300m), (45, 200m), (0, 50m) })
        {
            var entry = new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount, occurredAt = DateTimeOffset.UtcNow.AddDays(-days) };
            Assert.Equal(HttpStatusCode.Created, (await shop.PostJsonAsync("/api/v1/khata-entries", entry)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/credit-days", new { creditDays = 400 })).StatusCode);
        (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}/credit-days", new { creditDays = 30 })).EnsureSuccessStatusCode();

        var dues = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/reports/dues")).RootElement;
        var ramesh = dues.GetProperty("customers")[0];
        Assert.Equal(200m, ramesh.GetProperty("days31To60").GetDecimal());
        Assert.Equal(300m, ramesh.GetProperty("over90").GetDecimal());
        Assert.Equal(500m, ramesh.GetProperty("overdue").GetDecimal()); // older than 30 days
        Assert.Equal(30, ramesh.GetProperty("creditDays").GetInt32());
        // The oldest udhaar (100 days ago) was due 70 days ago.
        var dueOn = DateOnly.Parse(ramesh.GetProperty("dueOn").GetString()!);
        Assert.InRange(DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - dueOn.DayNumber, 69, 71);
        Assert.Equal(500m, dues.GetProperty("overdue").GetDecimal());
    }

    [Fact]
    public async Task A_wholesale_price_must_be_below_the_regular_one_and_can_be_cleared()
    {
        var (shop, _) = await api.SignUpAsync("Wholesale Store");
        var soap = Guid.NewGuid();
        (await shop.PostJsonAsync("/api/v1/products", new { id = soap, name = "Soap", unit = "piece", sellingPrice = 40m, costPrice = 30m })).EnsureSuccessStatusCode();
        object Update(decimal wholesale, decimal? from) => new { name = "Soap", unit = "piece", sellingPrice = 40m, costPrice = 30m, wholesalePrice = wholesale, wholesaleMinQty = from };

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/products/{soap}", Update(45m, 12m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync($"/api/v1/products/{soap}", Update(36m, 1m))).StatusCode);

        var set = JsonDocument.Parse(await (await shop.PutAsJsonAsync($"/api/v1/products/{soap}", Update(36m, 12m))).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(36m, set.GetProperty("wholesalePrice").GetDecimal());
        Assert.Equal(12m, set.GetProperty("wholesaleMinQty").GetDecimal());

        var cleared = JsonDocument.Parse(await (await shop.PutAsJsonAsync($"/api/v1/products/{soap}", Update(0m, null))).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("wholesalePrice").ValueKind);
    }

    [Fact]
    public async Task The_print_look_is_saved_checked_and_kept_to_its_shop()
    {
        var (shop, _) = await api.SignUpAsync("Print Look Store");
        var (other, _) = await api.SignUpAsync("Other Look Store");
        const string png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        var bad = await shop.PutAsJsonAsync("/api/v1/shop/print", new { logo = "not a picture", theme = "neon" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);

        var saved = await shop.PutAsJsonAsync("/api/v1/shop/print", new { logo = png, signature = (string?)null, theme = "modern", terms = "No returns" });
        Assert.True(saved.IsSuccessStatusCode);
        var mine = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/shop/print")).RootElement;
        Assert.Equal("modern", mine.GetProperty("theme").GetString());
        Assert.Equal(png, mine.GetProperty("logo").GetString());
        Assert.Equal("No returns", mine.GetProperty("terms").GetString());

        var theirs = JsonDocument.Parse(await other.GetStringAsync("/api/v1/shop/print")).RootElement;
        Assert.Equal("classic", theirs.GetProperty("theme").GetString());
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("logo").ValueKind);
    }

    [Fact]
    public async Task A_backup_is_marked_as_one_and_records_when_it_was_taken()
    {
        var (shop, _) = await api.SignUpAsync("Backup Store");
        var before = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/shop/backup-status")).RootElement;
        Assert.Equal(JsonValueKind.Null, before.GetProperty("lastBackupAt").ValueKind);
        Assert.True(before.GetProperty("empty").GetBoolean());

        await KhataTests.CreateCustomerAsync(shop, "Mohan");
        var export = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/shop/export")).RootElement;
        Assert.Equal("grocery-khata-backup", export.GetProperty("format").GetString());
        Assert.Equal(JsonValueKind.Array, export.GetProperty("suppliers").ValueKind);

        var after = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/shop/backup-status")).RootElement;
        Assert.NotEqual(JsonValueKind.Null, after.GetProperty("lastBackupAt").ValueKind);
        Assert.False(after.GetProperty("empty").GetBoolean());
    }

    [Fact]
    public async Task Deleting_a_shop_needs_its_exact_name_removes_everything_and_leaves_others_alone()
    {
        var phone = ApiFactory.NewPhone();
        var (shop, auth) = await api.SignUpAsync("Closing Store", phone);
        var (other, _) = await api.SignUpAsync("Staying Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Suresh");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 100m);
        var wrong = await KhataTests.AddEntryAsync(shop, customer, "credit", 5m);
        await (await shop.PostJsonAsync($"/api/v1/ledger-entries/{wrong.Entry.Id}/reverse", new { id = Guid.CreateVersion7() })).ReadAsync<KhataEntryResponse>();
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { name = "Atta", quantity = 1m, unit = "kg", unitPrice = 44m } }, paidAmount = 0m,
        })).ReadAsync<BillDto>();
        var otherCustomer = await KhataTests.CreateCustomerAsync(other, "Anita");

        var mistyped = await shop.PostJsonAsync("/api/v1/shop/delete", new { confirmName = "closing store" });
        var deleted = await shop.PostJsonAsync("/api/v1/shop/delete", new { confirmName = "Closing Store" });

        Assert.Equal(HttpStatusCode.BadRequest, mistyped.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        // The old shop token stops working, the other shop is untouched, and the person can still sign in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Single(await other.GetJsonAsync<List<CustomerDto>>("/api/v1/customers"), c => c.Id == otherCustomer);
        var again = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        Assert.Equal((auth.User.Id, null, 0), (again.User.Id, again.ShopId, again.Memberships.Count));
    }

    /// <summary>The API as it would start on the server: Production environment, the given login and key.</summary>
    private static WebApplicationFactory<Program> Production(string connectionString, string signingKey) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Jwt:SigningKey"] = signingKey,
                ["Agents:AadhaarHashKey"] = "test-aadhaar-key",
                ["Billing:BillLinkKey"] = "test-bill-link-key",
                ["Database:MigrateOnStartup"] = "false",
            }));
        });
}
