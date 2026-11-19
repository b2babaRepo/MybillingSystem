using GroceryKhata.Api.Ledger;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>"Verify my data": a clean shop checks clean, and a record changed behind the app's back is found.</summary>
[Collection(ApiCollection.Name)]
public sealed class DataCheckTests(ApiFactory api)
{
    [Fact]
    public async Task A_clean_shop_checks_clean_and_stock_changed_by_hand_is_found()
    {
        var (shop, _) = await api.SignUpAsync("Data Check Store");
        var atta = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = atta, name = "Atta 5 kg", unit = "packet", sellingPrice = 250m, costPrice = 200m, openingStock = 10m })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Salt", unit = "packet", sellingPrice = 20m, openingStock = 5m })).EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { productId = atta, name = "Atta 5 kg", quantity = 2m, unitPrice = 250m } }, paidAmount = 100m, paymentMode = "cash" })).EnsureSuccessStatusCode();

        var clean = await shop.GetJsonAsync<DataCheckDto>("/api/v1/data-check");
        Assert.All(clean.Checks.Where(c => !c.Warning), c => Assert.Equal(0, c.Problems));
        var noCost = clean.Checks.Single(c => c.Code == "noCost");
        Assert.Equal("Salt", Assert.Single(noCost.Samples).Name);

        // Someone changes the stock in the database by hand.
        await using (var admin = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await admin.OpenAsync();
            await using var edit = new NpgsqlCommand("UPDATE products SET stock = 99 WHERE id = @id", admin);
            edit.Parameters.AddWithValue("id", atta);
            await edit.ExecuteNonQueryAsync();
        }

        var after = await shop.GetJsonAsync<DataCheckDto>("/api/v1/data-check");
        var stock = after.Checks.Single(c => c.Code == "stock");
        var off = Assert.Single(stock.Samples);
        Assert.Equal(("Atta 5 kg", "99 / 8"), (off.Name, off.Detail));
    }
}
