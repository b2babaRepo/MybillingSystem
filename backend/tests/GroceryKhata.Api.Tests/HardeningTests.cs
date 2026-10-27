using System.Net;
using System.Net.Http.Headers;
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
                ["Database:MigrateOnStartup"] = "false",
            }));
        });
}
