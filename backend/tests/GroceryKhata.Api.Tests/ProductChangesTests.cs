using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>A till keeps the item list and asks only for what changed since its last look.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProductChangesTests(ApiFactory api)
{
    [Fact]
    public async Task Only_changed_and_removed_items_come_after_the_first_full_list_whatever_changed_them()
    {
        var (shop, _) = await api.SignUpAsync("Changes Mart");
        async Task<Guid> ItemAsync(string name)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = 10m, openingStock = 5m })).EnsureSuccessStatusCode();
            return id;
        }

        var (rice, dal, soap, oil) = (await ItemAsync("Rice"), await ItemAsync("Dal"), await ItemAsync("Soap"), await ItemAsync("Oil"));
        var full = await shop.GetJsonAsync<ProductChanges>("/api/v1/products/changes");
        Assert.True(full.Full);
        Assert.Equal(4, full.Changed.Count);
        Assert.Empty(full.Removed);

        // The overlap re-sends what changed just before "at"; wait past it by asking from a later point.
        await Task.Delay(50);
        var since = Uri.EscapeDataString((await shop.GetJsonAsync<ProductChanges>("/api/v1/products/changes")).At.Add(ProductChanges.Overlap).ToString("O"));
        await Task.Delay(50);

        // Changed through the app (a price), by a bulk update that skips the app's own stamp (stock), and removed.
        (await shop.PutAsJsonAsync($"/api/v1/products/{rice}", new { id = rice, name = "Rice", unit = "piece", sellingPrice = 12m }, ApiFactory.Json)).EnsureSuccessStatusCode();
        await using (var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var bulk = new NpgsqlCommand("UPDATE products SET stock = stock - 1 WHERE id = @id", db);
            bulk.Parameters.AddWithValue("id", dal);
            await bulk.ExecuteNonQueryAsync();
        }

        (await shop.PutAsJsonAsync($"/api/v1/products/{soap}", new { id = soap, name = "Soap", unit = "piece", sellingPrice = 10m, isActive = false }, ApiFactory.Json)).EnsureSuccessStatusCode();

        var changes = await shop.GetJsonAsync<ProductChanges>($"/api/v1/products/changes?since={since}");
        Assert.False(changes.Full);
        Assert.Equal(new[] { dal, rice }.Order(), changes.Changed.Select(p => p.Id).Order());
        Assert.Equal(12m, changes.Changed.Single(p => p.Id == rice).SellingPrice);
        Assert.Equal(4m, changes.Changed.Single(p => p.Id == dal).Stock);
        Assert.Equal([soap], changes.Removed);
        Assert.DoesNotContain(changes.Changed, p => p.Id == oil);
    }
}
