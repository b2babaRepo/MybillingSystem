using System.Net;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Ledger;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>Shop B can never see or change Shop A's data. Another shop's record is 404 (or "not found"), never 403.</summary>
[Collection(ApiCollection.Name)]
public sealed class ShopIsolationTests(ApiFactory api)
{
    [Fact]
    public async Task Another_shops_customers_products_and_statements_are_invisible()
    {
        var (shopA, _) = await api.SignUpAsync("Shop A");
        var (shopB, _) = await api.SignUpAsync("Shop B");
        var customerA = await KhataTests.CreateCustomerAsync(shopA, "A's customer");
        await KhataTests.AddEntryAsync(shopA, customerA, "credit", 100m);
        await (await shopA.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Rice", unit = "kg", sellingPrice = 60m }))
            .ReadAsync<ProductDto>();

        Assert.Empty(await shopB.GetJsonAsync<List<CustomerDto>>("/api/v1/customers"));
        Assert.Empty(await shopB.GetJsonAsync<List<ProductDto>>("/api/v1/products"));
        Assert.Equal(HttpStatusCode.NotFound, (await shopB.GetAsync($"/api/v1/customers/{customerA}/statement")).StatusCode);
    }

    [Fact]
    public async Task Writing_to_another_shops_customer_or_entry_fails()
    {
        var (shopA, _) = await api.SignUpAsync("Shop A");
        var (shopB, _) = await api.SignUpAsync("Shop B");
        var customerA = await KhataTests.CreateCustomerAsync(shopA, "A's customer");
        var entryA = await KhataTests.AddEntryAsync(shopA, customerA, "credit", 100m);

        var khata = await shopB.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customerA, type = "credit", amount = 5m });
        var bill = await shopB.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            customerId = customerA,
            lines = new[] { new { name = "Salt", quantity = 1m, unit = "packet", unitPrice = 20m } },
            paidAmount = 0m,
        });
        var reverse = await shopB.PostJsonAsync($"/api/v1/ledger-entries/{entryA.Entry.Id}/reverse", new { id = Guid.CreateVersion7() });
        var replayAsB = await shopB.PostJsonAsync("/api/v1/khata-entries",
            new { id = entryA.Entry.Id, customerId = customerA, type = "credit", amount = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, khata.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bill.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reverse.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, replayAsB.StatusCode); // reusing A's entry ID must not replay A's entry to B
        Assert.Equal(100m, (await shopA.GetJsonAsync<StatementDto>($"/api/v1/customers/{customerA}/statement")).Customer.Balance);
    }

    [Fact]
    public async Task Row_level_security_hides_other_shops_rows_even_without_the_app_filter()
    {
        var (shopA, authA) = await api.SignUpAsync("Shop A");
        var (_, authB) = await api.SignUpAsync("Shop B");
        await KhataTests.CreateCustomerAsync(shopA, "A's customer");

        await using var connection = new NpgsqlConnection(api.AppConnectionString);
        await connection.OpenAsync();

        Assert.Equal(1L, await CountCustomersAsync(connection, authA.ShopId!.Value));
        Assert.Equal(0L, await CountCustomersAsync(connection, authB.ShopId!.Value));
        Assert.Equal(0L, await CountCustomersAsync(connection, null)); // no shop selected: nothing

        await using var insert = new NpgsqlCommand(
            "SELECT set_config('app.shop_id', @b, false); INSERT INTO customers (id, shop_id, name, balance, created_at, updated_at) VALUES (gen_random_uuid(), @a, 'x', 0, now(), now())",
            connection);
        insert.Parameters.AddWithValue("a", authA.ShopId!.Value);
        insert.Parameters.AddWithValue("b", authB.ShopId!.Value.ToString());
        var error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState); // "new row violates row-level security policy"
    }

    private static async Task<long> CountCustomersAsync(NpgsqlConnection connection, Guid? shopId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.shop_id', @shop, false); SELECT count(*) FROM customers", connection);
        command.Parameters.AddWithValue("shop", shopId?.ToString() ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.NextResultAsync();
        await reader.ReadAsync();
        return reader.GetInt64(0);
    }
}
