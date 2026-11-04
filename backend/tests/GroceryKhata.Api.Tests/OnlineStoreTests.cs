using System.Net.Http.Json;
using System.Net;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;

namespace GroceryKhata.Api.Tests;

/// <summary>Phase 5 exit criteria: both delivery paths work end to end, and cancelling releases reserved stock.</summary>
[Collection(ApiCollection.Name)]
public sealed class OnlineStoreTests(ApiFactory api)
{
    [Fact]
    public async Task A_guest_orders_online_the_shop_packs_and_delivers_paid_in_full_in_cash()
    {
        var (shop, auth) = await api.SignUpAsync("Sharma Store");
        var slug = auth.Memberships.Single().ShopSlug;
        var atta = await CreateProductAsync(shop, "Atta", "kg", 44m, 10m);
        var oil = await CreateProductAsync(shop, "Mustard oil", "litre", 185m, 5m);
        await CreateProductAsync(shop, "Haldi (no price yet)", "packet", 0m, 5m, import: true);
        var guest = api.CreateClient();

        // The store shows priced items only, with what's available.
        var store = await guest.GetJsonAsync<StoreDto>($"/api/v1/store/{slug}");
        Assert.Equal(("Sharma Store", 2), (store.Name, store.Items.Count));
        Assert.Equal(10m, store.Items.Single(i => i.Id == atta.Id).Available);

        var body = new
        {
            id = Guid.CreateVersion7(),
            name = "Neha Verma",
            phone = ApiFactory.NewPhone(),
            fulfilment = "delivery",
            address = "B-12, Raja Park",
            lines = new object[] { new { productId = atta.Id, quantity = 3m }, new { productId = oil.Id, quantity = 1m } },
        };
        var placed = await (await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", body)).ReadAsync<StoreOrderPlaced>();
        Assert.Equal(HttpStatusCode.Conflict, (await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", body)).StatusCode);

        // Stock is held for the order: the shelf still has 10, the store shows 7.
        var reserved = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{atta.Id}");
        Assert.Equal((10m, 3m), (reserved.Stock, reserved.Reserved));
        Assert.Equal(7m, (await guest.GetJsonAsync<StoreDto>($"/api/v1/store/{slug}")).Items.Single(i => i.Id == atta.Id).Available);

        // The shop sees it with the address and catalogue prices; atta weighs in at 2.9 kg.
        var inbox = Assert.Single(await shop.GetJsonAsync<List<OrderDto>>("/api/v1/orders?status=placed"));
        Assert.Equal((OrderSource.Store, "B-12, Raja Park"), (inbox.Source, inbox.Address));
        Assert.Equal(44m, inbox.Lines[0].UnitPrice);
        var packed = await (await shop.PostJsonAsync($"/api/v1/orders/{inbox.Id}/pack", new
        {
            lines = new object[]
            {
                new { id = inbox.Lines[0].Id, packedQuantity = 2.9m, unitPrice = 44m, removed = false },
                new { id = inbox.Lines[1].Id, packedQuantity = 1m, unitPrice = 185m, removed = false },
            },
        })).ReadAsync<OrderDto>();
        Assert.Equal(312.6m, packed.FinalAmount);

        // Delivered, paid in full in cash: real stock comes off, the hold goes, the cash book has it.
        await (await shop.PostJsonAsync($"/api/v1/orders/{inbox.Id}/deliver", new { })).ReadAsync<OrderDto>();
        var after = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{atta.Id}");
        Assert.Equal((7.1m, 0m), (after.Stock, after.Reserved));
        Assert.Equal(0m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{oil.Id}")).Reserved);
        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal(312.6m, book.Cash);

        // The guest follows it with the link's token.
        var status = await guest.GetJsonAsync<OrderDto>($"/api/v1/store/{slug}/orders/{placed.Order.Id}?token={placed.StatusToken}");
        Assert.Equal((OrderStatus.Delivered, 312.6m), (status.Status, status.FinalAmount!.Value));
    }

    [Fact]
    public async Task Delivered_with_part_paid_puts_the_rest_on_the_customers_khata()
    {
        var (shop, auth) = await api.SignUpAsync();
        var slug = auth.Memberships.Single().ShopSlug;
        var oil = await CreateProductAsync(shop, "Mustard oil", "litre", 185m, 5m);
        var phone = ApiFactory.NewPhone();
        await (await api.CreateClient().PostJsonAsync($"/api/v1/store/{slug}/orders", new
        {
            id = Guid.CreateVersion7(), name = "Sunita", phone, lines = new[] { new { productId = oil.Id, quantity = 2m } },
        })).ReadAsync<StoreOrderPlaced>();
        var order = Assert.Single(await shop.GetJsonAsync<List<OrderDto>>("/api/v1/orders"));
        await (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/pack", new { lines = new[] { new { id = order.Lines[0].Id, packedQuantity = 2m, unitPrice = 185m, removed = false } } }))
            .ReadAsync<OrderDto>();

        await (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/deliver", new { paidAmount = 100m })).ReadAsync<OrderDto>();

        var customer = Assert.Single(await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers"));
        Assert.Equal(("Sunita", 270m), (customer.Name, customer.Balance)); // 370 − 100
        Assert.Equal(100m, (await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book")).Cash);
        var product = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{oil.Id}");
        Assert.Equal((3m, 0m), (product.Stock, product.Reserved));
    }

    [Fact]
    public async Task Cancelling_an_order_releases_the_stock_it_held_once()
    {
        var (shop, auth) = await api.SignUpAsync();
        var slug = auth.Memberships.Single().ShopSlug;
        var atta = await CreateProductAsync(shop, "Atta", "kg", 44m, 10m);
        await (await api.CreateClient().PostJsonAsync($"/api/v1/store/{slug}/orders", new
        {
            id = Guid.CreateVersion7(), name = "Ravi", phone = ApiFactory.NewPhone(), lines = new[] { new { productId = atta.Id, quantity = 4m } },
        })).ReadAsync<StoreOrderPlaced>();
        var order = Assert.Single(await shop.GetJsonAsync<List<OrderDto>>("/api/v1/orders"));
        Assert.Equal(4m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{atta.Id}")).Reserved);

        await (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/cancel", new { })).ReadAsync<OrderDto>();
        await (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/cancel", new { })).ReadAsync<OrderDto>();

        var product = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{atta.Id}");
        Assert.Equal((10m, 0m), (product.Stock, product.Reserved));
    }

    [Fact]
    public async Task The_public_store_reveals_nothing_without_the_link_and_takes_only_its_own_items()
    {
        var (shopA, authA) = await api.SignUpAsync("Shop A");
        var (shopB, authB) = await api.SignUpAsync("Shop B");
        var slugA = authA.Memberships.Single().ShopSlug;
        var attaA = await CreateProductAsync(shopA, "Atta", "kg", 44m, 10m);
        var riceB = await CreateProductAsync(shopB, "Rice", "kg", 60m, 10m);
        var guest = api.CreateClient();
        var placed = await (await guest.PostJsonAsync($"/api/v1/store/{slugA}/orders", new
        {
            id = Guid.CreateVersion7(), name = "Guest", phone = ApiFactory.NewPhone(), lines = new[] { new { productId = attaA.Id, quantity = 1m } },
        })).ReadAsync<StoreOrderPlaced>();

        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/store/{slugA}/orders/{placed.Order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/store/{slugA}/orders/{placed.Order.Id}?token=wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/store/{authB.Memberships.Single().ShopSlug}/orders/{placed.Order.Id}?token={placed.StatusToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync("/api/v1/store/no-such-shop")).StatusCode);

        // Shop B's item can't be bought through Shop A's store, and Shop B never sees Shop A's order.
        var crossShop = await guest.PostJsonAsync($"/api/v1/store/{slugA}/orders", new
        {
            id = Guid.CreateVersion7(), name = "Guest", phone = ApiFactory.NewPhone(), lines = new[] { new { productId = riceB.Id, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, crossShop.StatusCode);
        Assert.Equal(0m, (await shopB.GetJsonAsync<ProductDto>($"/api/v1/products/{riceB.Id}")).Reserved);
        Assert.Empty(await shopB.GetJsonAsync<List<OrderDto>>("/api/v1/orders"));
    }

    [Fact]
    public async Task Store_cards_get_the_mrp_and_a_small_photo_for_the_grid()
    {
        var (shop, auth) = await api.SignUpAsync("Card Store");
        var slug = auth.Memberships.Single().ShopSlug;
        var anjeer = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Afghani anjeer 500 g", unit = "packet", sellingPrice = 227m, openingStock = 5m, mrp = 850m }))
            .ReadAsync<ProductDto>();
        var dates = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Dates", unit = "packet", sellingPrice = 205m, openingStock = 5m, mrp = 150m }))
            .ReadAsync<ProductDto>();
        // An MRP at or below the price has nothing "off", so it isn't kept.
        Assert.Equal((850m, (decimal?)null), (anjeer.Mrp!.Value, dates.Mrp));

        var full = "data:image/webp;base64," + Convert.ToBase64String(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 5, 6 });
        var small = "data:image/webp;base64," + Convert.ToBase64String(new byte[] { 0x52, 0x49, 0x46, 0x46 });
        (await shop.PutAsJsonAsync($"/api/v1/products/{anjeer.Id}/image", new { dataUrl = full, thumbDataUrl = small }, ApiFactory.Json)).EnsureSuccessStatusCode();
        (await shop.PutAsJsonAsync($"/api/v1/products/{dates.Id}/image", new { dataUrl = full }, ApiFactory.Json)).EnsureSuccessStatusCode();

        var guest = api.CreateClient();
        async Task<int> Bytes(Guid id, string size) => (await (await guest.GetAsync($"/api/v1/images/{auth.ShopId}/{id}?v=1&size={size}")).Content.ReadAsByteArrayAsync()).Length;
        Assert.Equal((4, 10), (await Bytes(anjeer.Id, "thumb"), await Bytes(anjeer.Id, "full")));
        // No small copy yet: the grid gets the full photo instead of nothing.
        Assert.Equal(10, await Bytes(dates.Id, "thumb"));

        var store = await guest.GetJsonAsync<StoreDto>($"/api/v1/store/{slug}");
        Assert.Equal(850m, store.Items.Single(i => i.Id == anjeer.Id).Mrp!.Value);
    }

    private static async Task<ProductDto> CreateProductAsync(HttpClient client, string name, string unit, decimal price, decimal stock, bool import = false)
    {
        if (import)
        {
            await (await client.PostJsonAsync("/api/v1/products/import", new { rows = new[] { new { name, unit, openingStock = stock } } })).ReadAsync<ImportResult>();
            return (await client.GetJsonAsync<List<ProductDto>>($"/api/v1/products?search={Uri.EscapeDataString(name)}")).Single();
        }

        return await (await client.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name, unit, sellingPrice = price, openingStock = stock }))
            .ReadAsync<ProductDto>();
    }
}
