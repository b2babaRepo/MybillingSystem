using System.Net.Http.Json;

namespace GroceryKhata.Api.Tests;

/// <summary>Weekly price changes: the items whose price moved since their label was printed wait to be printed again.</summary>
[Collection(ApiCollection.Name)]
public sealed class LabelQueueTests(ApiFactory api)
{
    [Fact]
    public async Task A_new_price_puts_the_item_in_the_label_queue_until_its_label_is_printed()
    {
        var (shop, _) = await api.SignUpAsync("Label Mart");
        async Task<Guid> ItemAsync(string name, decimal price)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = price })).EnsureSuccessStatusCode();
            return id;
        }

        var (tea, sugar) = (await ItemAsync("Tea", 120m), await ItemAsync("Sugar", 45m));
        Task<List<Guid>> QueueAsync() => shop.GetJsonAsync<List<Guid>>("/api/v1/products/label-queue");
        Assert.Empty(await QueueAsync());

        (await shop.PutAsJsonAsync("/api/v1/products/rates", new { rates = new[] { new { productId = tea, sellingPrice = 126m }, new { productId = sugar, sellingPrice = 45m } } })).EnsureSuccessStatusCode();
        Assert.Equal([tea], await QueueAsync()); // sugar's price didn't change

        (await shop.PostJsonAsync("/api/v1/products/labels-printed", new { productIds = new[] { tea } })).EnsureSuccessStatusCode();
        Assert.Empty(await QueueAsync());

        await Task.Delay(20);
        (await shop.PutAsJsonAsync("/api/v1/products/rates", new { rates = new[] { new { productId = tea, sellingPrice = 130m } } })).EnsureSuccessStatusCode();
        Assert.Equal([tea], await QueueAsync());
    }
}
