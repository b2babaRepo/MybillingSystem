using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;
using Npgsql;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PlannerTests(ApiFactory api)
{
    [Fact]
    public async Task A_household_plans_a_monthly_list_sends_it_and_the_shop_packs_and_delivers_onto_khata()
    {
        var area = $"Raja Park {Guid.NewGuid():N}"[..20];
        var (shop, shopAuth) = await SignUpShopAsync("Sharma General Store", area);
        var (home, homeAuth) = await RegisterHouseholdAsync("Priya Agarwal");
        Assert.Null(homeAuth.ShopId);

        // Plan: two items; editing one twice (as an offline retry would) keeps one row.
        var atta = Guid.CreateVersion7();
        var dal = Guid.CreateVersion7();
        await PutItemAsync(home, "monthly", atta, "Aashirvaad atta", 10m, "kg");
        await PutItemAsync(home, "monthly", atta, "Aashirvaad atta", 10m, "kg", isChecked: true);
        await PutItemAsync(home, "monthly", dal, "Toor dal", 2m, "kg");
        var lists = await home.GetJsonAsync<List<ListDto>>("/api/v1/me/lists");
        Assert.Equal(2, lists.Single(l => l.Kind == ListKind.Monthly).Items.Count);
        Assert.Empty(lists.Single(l => l.Kind == ListKind.Weekly).Items);

        // Find the shop by its market and send the list; sending twice makes one order.
        var found = await home.GetJsonAsync<List<ShopDirectoryDto>>($"/api/v1/shops?search={Uri.EscapeDataString(area)}");
        Assert.Equal(shopAuth.ShopId, Assert.Single(found).Id);
        var orderBody = new { id = Guid.CreateVersion7(), shopId = shopAuth.ShopId, kind = "monthly", fulfilment = "delivery", slot = "1 Nov evening", payment = "khata", note = "Pillsbury is fine" };
        var placed = await home.PostJsonAsync("/api/v1/me/orders", orderBody);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await home.PostJsonAsync("/api/v1/me/orders", orderBody)).StatusCode);

        // The list keeps its items for next month, with the ticks cleared.
        lists = await home.GetJsonAsync<List<ListDto>>("/api/v1/me/lists");
        Assert.All(lists.Single(l => l.Kind == ListKind.Monthly).Items, i => Assert.False(i.Checked));

        // The shop sees it in its inbox and packs it: atta swapped, dal weighed at 1.9 kg.
        var inbox = await shop.GetJsonAsync<List<OrderDto>>("/api/v1/orders?status=placed");
        var order = Assert.Single(inbox);
        Assert.Equal("Priya Agarwal", order.CustomerName);
        Assert.Equal(OrderPayment.Khata, order.Payment);
        var packed = await (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/pack", new
        {
            lines = new object[]
            {
                new { id = order.Lines[0].Id, packedQuantity = 10m, unitPrice = 42m, removed = false, shopNote = "Pillsbury instead of Aashirvaad" },
                new { id = order.Lines[1].Id, packedQuantity = 1.9m, unitPrice = 160m, removed = false },
            },
        })).ReadAsync<OrderDto>();
        Assert.Equal(724m, packed.FinalAmount); // 420 + 304

        // The household sees the packed order, the shop's change and the final amount.
        var mine = Assert.Single(await home.GetJsonAsync<List<OrderDto>>("/api/v1/me/orders"));
        Assert.Equal(OrderStatus.Packed, mine.Status);
        Assert.Equal("Sharma General Store", mine.ShopName);
        Assert.Equal("Pillsbury instead of Aashirvaad", mine.Lines[0].ShopNote);

        // Delivered on khata: a bill under the order's ID, and the whole amount owed by a new khata customer.
        var delivered = await (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/deliver", new { })).ReadAsync<OrderDto>();
        Assert.Equal(OrderStatus.Delivered, delivered.Status);
        Assert.Equal(HttpStatusCode.OK, (await shop.PostJsonAsync($"/api/v1/orders/{order.Id}/deliver", new { })).StatusCode);
        var customer = Assert.Single(await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers"));
        Assert.Equal(("Priya Agarwal", 724m), (customer.Name, customer.Balance));
    }

    [Fact]
    public async Task Items_picked_from_the_common_catalogue_arrive_with_each_shops_own_price()
    {
        var area = $"Bapu Bazar {Guid.NewGuid():N}"[..20];
        var (cheap, cheapAuth) = await SignUpShopAsync("Gupta Kirana", area);
        var (dear, dearAuth) = await SignUpShopAsync("Jain Provision", area);
        // Same item, own names and prices: "आटा (Aashirvaad)" is still the catalogue's atta.
        await cheap.PostJsonAsync("/api/v1/products/import", new { rows = new[] { new { name = "आटा (Aashirvaad)", unit = "kg", sellingPrice = 42m, openingStock = 20m } } });
        await dear.PostJsonAsync("/api/v1/products/import", new { rows = new[] { new { name = "Atta", unit = "kg", sellingPrice = 48m, openingStock = 20m } } });
        var (home, _) = await RegisterHouseholdAsync("Priya");

        var catalogue = await home.GetJsonAsync<List<CatalogueItemDto>>("/api/v1/catalogue");
        var atta = catalogue.Single(i => i.NameEn == "Atta");
        await home.PutAsJsonAsync($"/api/v1/me/lists/monthly/items/{Guid.CreateVersion7()}",
            new { name = atta.Name, quantity = 5m, unit = "kg", catalogueItemId = atta.Id }, ApiFactory.Json);

        Assert.Equal(42m, Assert.Single(await home.GetJsonAsync<List<ShopPriceDto>>($"/api/v1/shops/{cheapAuth.ShopId}/prices")).Price);
        Assert.Equal(48m, Assert.Single(await home.GetJsonAsync<List<ShopPriceDto>>($"/api/v1/shops/{dearAuth.ShopId}/prices")).Price);

        await home.PostJsonAsync("/api/v1/me/orders", new { id = Guid.CreateVersion7(), shopId = dearAuth.ShopId, kind = "monthly" });
        var line = Assert.Single(Assert.Single(await dear.GetJsonAsync<List<OrderDto>>("/api/v1/orders?status=placed")).Lines);
        Assert.Equal((48m, 240m), (line.UnitPrice, line.Amount));
    }

    [Fact]
    public async Task Orders_stay_between_the_household_and_the_shop_it_was_sent_to()
    {
        var (shopA, authA) = await SignUpShopAsync("Shop A", "Market A");
        var (shopB, _) = await SignUpShopAsync("Shop B", "Market B");
        var (priya, _) = await RegisterHouseholdAsync("Priya");
        var (neha, _) = await RegisterHouseholdAsync("Neha");

        var item = Guid.CreateVersion7();
        await PutItemAsync(priya, "weekly", item, "Milk", 6m, "litre");
        var order = await (await priya.PostJsonAsync("/api/v1/me/orders", new { id = Guid.CreateVersion7(), shopId = authA.ShopId, kind = "weekly" }))
            .ReadAsync<OrderDto>();

        // Another shop: nothing in its inbox, and the order's address is a 404.
        Assert.Empty(await shopB.GetJsonAsync<List<OrderDto>>("/api/v1/orders"));
        Assert.Equal(HttpStatusCode.NotFound, (await shopB.GetAsync($"/api/v1/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await shopB.PostJsonAsync($"/api/v1/orders/{order.Id}/cancel", new { })).StatusCode);

        // Another household: none of Priya's orders or list, and her item ID can't be taken over.
        Assert.Empty(await neha.GetJsonAsync<List<OrderDto>>("/api/v1/me/orders"));
        Assert.All(await neha.GetJsonAsync<List<ListDto>>("/api/v1/me/lists"), l => Assert.Empty(l.Items));
        var hijack = await neha.PutAsJsonAsync($"/api/v1/me/lists/weekly/items/{item}", new { name = "Mine now", quantity = 1m, unit = "piece" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.NotFound, hijack.StatusCode);
        Assert.Equal("Milk", (await priya.GetJsonAsync<List<ListDto>>("/api/v1/me/lists")).Single(l => l.Kind == ListKind.Weekly).Items[0].Name);

        // The shop it went to sees it.
        Assert.Single(await shopA.GetJsonAsync<List<OrderDto>>("/api/v1/orders"));
    }

    [Fact]
    public async Task Row_level_security_lets_only_the_shop_and_the_person_who_placed_it_read_an_order()
    {
        var (_, authA) = await SignUpShopAsync("Shop A", "Market A");
        var (_, authB) = await SignUpShopAsync("Shop B", "Market B");
        var (priya, priyaAuth) = await RegisterHouseholdAsync("Priya");
        var (_, nehaAuth) = await RegisterHouseholdAsync("Neha");
        await PutItemAsync(priya, "monthly", Guid.CreateVersion7(), "Rice", 5m, "kg");
        var order = await (await priya.PostJsonAsync("/api/v1/me/orders", new { id = Guid.CreateVersion7(), shopId = authA.ShopId, kind = "monthly" }))
            .ReadAsync<OrderDto>();

        await using var connection = new NpgsqlConnection(api.AppConnectionString);
        await connection.OpenAsync();
        Assert.Equal(1L, await CountOrdersAsync(connection, order.Id, authA.ShopId, null));
        Assert.Equal(1L, await CountOrdersAsync(connection, order.Id, null, priyaAuth.User.Id));
        Assert.Equal(0L, await CountOrdersAsync(connection, order.Id, authB.ShopId, null));
        Assert.Equal(0L, await CountOrdersAsync(connection, order.Id, null, nehaAuth.User.Id));
    }

    private static async Task<long> CountOrdersAsync(NpgsqlConnection connection, Guid orderId, Guid? shopId, Guid? userId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.shop_id', @shop, false), set_config('app.user_id', @user, false); SELECT count(*) FROM orders WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("shop", shopId?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("user", userId?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("id", orderId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.NextResultAsync();
        await reader.ReadAsync();
        return reader.GetInt64(0);
    }

    private async Task<(HttpClient, AuthResponse)> SignUpShopAsync(string name, string area)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = name, fullName = "Owner", phone = ApiFactory.NewPhone(), password = "secret123", area })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private async Task<(HttpClient, AuthResponse)> RegisterHouseholdAsync(string name)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/register",
            new { fullName = name, phone = ApiFactory.NewPhone(), password = "secret123" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task PutItemAsync(HttpClient client, string kind, Guid id, string name, decimal quantity, string unit, bool isChecked = false) =>
        await (await client.PutAsJsonAsync($"/api/v1/me/lists/{kind}/items/{id}",
            new { name, quantity, unit, @checked = isChecked }, ApiFactory.Json)).ReadAsync<ListItemDto>();
}
