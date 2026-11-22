using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;

namespace GroceryKhata.Api.Tests;

/// <summary>Customer app phase 2: pay (cash, UPI, khata), the shop's extra steps, an honest bill, adding items, alerts.</summary>
[Collection(ApiCollection.Name)]
public sealed class OrderUpdatesTests(ApiFactory api)
{
    [Fact]
    public async Task The_shop_sets_a_charge_minimum_and_times_and_the_bill_shows_the_charge_it_promised()
    {
        var (shop, auth) = await api.SignUpAsync("Fee Store");
        var slug = auth.Memberships.Single().ShopSlug;
        var atta = await ProductAsync(shop, "Atta", "kg", 50m, 20m);
        (await shop.PutAsJsonAsync("/api/v1/store-settings", new { deliveryFee = 20m, freeDeliveryAbove = 500m, minOrder = 100m, slots = new[] { "Now", "Evening", " ", "Now" } })).EnsureSuccessStatusCode();

        var store = await api.CreateClient().GetJsonAsync<StoreDto>($"/api/v1/store/{slug}");
        Assert.Equal((20m, 500m, 100m), (store.Settings!.DeliveryFee, store.Settings.FreeDeliveryAbove, store.Settings.MinOrder));
        Assert.Equal(["Now", "Evening"], store.Settings.Slots);

        // Below the minimum: refused before anything is held.
        var guest = api.CreateClient();
        var small = await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 1m, "delivery"));
        Assert.Equal(HttpStatusCode.BadRequest, small.StatusCode);

        // ₹150 of atta delivered: the ₹20 charge is fixed now and goes on the bill as its own line.
        var placed = await (await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 3m, "delivery"))).ReadAsync<StoreOrderPlaced>();
        Assert.Equal(20m, placed.Order.DeliveryFee);
        var id = placed.Order.Id;
        Assert.Equal(OrderStatus.Accepted, (await (await shop.PostJsonAsync($"/api/v1/orders/{id}/accept", new { })).ReadAsync<OrderDto>()).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/orders/{id}/ready", new { })).StatusCode);
        var line = placed.Order.Lines.Single();
        var packed = await (await shop.PostJsonAsync($"/api/v1/orders/{id}/pack", new { lines = new[] { new { id = line.Id, packedQuantity = 3m, unitPrice = 50m, removed = false } } })).ReadAsync<OrderDto>();
        Assert.Equal(170m, packed.FinalAmount);
        Assert.Equal(OrderStatus.OutForDelivery, (await (await shop.PostJsonAsync($"/api/v1/orders/{id}/ready", new { })).ReadAsync<OrderDto>()).Status);
        var delivered = await (await shop.PostJsonAsync($"/api/v1/orders/{id}/deliver", new { })).ReadAsync<OrderDto>();
        Assert.Equal((OrderStatus.Delivered, id), (delivered.Status, delivered.BillId));
        var bill = (await shop.GetJsonAsync<BillView>($"/api/v1/bills/{id}")).Bill;
        Assert.Equal(170m, bill.Total);
        Assert.Contains(bill.Lines, l => l.Name == OrderEndpoints.DeliveryChargeLine && l.Amount == 20m);

        // At or above the free-delivery amount, and for pickup, there is no charge; a pickup order becomes "ready".
        var big = await (await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 10m, "delivery"))).ReadAsync<StoreOrderPlaced>();
        Assert.Equal(0m, big.Order.DeliveryFee);
        var pickup = await (await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 3m, "pickup"))).ReadAsync<StoreOrderPlaced>();
        Assert.Equal(0m, pickup.Order.DeliveryFee);
        var pl = pickup.Order.Lines.Single();
        await (await shop.PostJsonAsync($"/api/v1/orders/{pickup.Order.Id}/pack", new { lines = new[] { new { id = pl.Id, packedQuantity = 3m, unitPrice = 50m, removed = false } } })).ReadAsync<OrderDto>();
        Assert.Equal(OrderStatus.ReadyForPickup, (await (await shop.PostJsonAsync($"/api/v1/orders/{pickup.Order.Id}/ready", new { })).ReadAsync<OrderDto>()).Status);
    }

    [Fact]
    public async Task Khata_and_upi_at_checkout_follow_the_shops_choice_and_limit()
    {
        var (shop, auth) = await api.SignUpAsync("Khata Store");
        var slug = auth.Memberships.Single().ShopSlug;
        var atta = await ProductAsync(shop, "Atta", "kg", 100m, 50m);
        var phone = ApiFactory.NewPhone();
        var customerId = Guid.CreateVersion7();
        await (await shop.PostJsonAsync("/api/v1/customers", new { id = customerId, name = "Priya", phone })).ReadAsync<CustomerDto>();
        await KhataTests.AddEntryAsync(shop, customerId, "credit", 400m);
        var priya = await RegisterAsync("Priya", phone);
        await SqlAsync("UPDATE users SET phone_verified_at = now() WHERE phone = @p", GroceryKhata.Api.Common.PhoneNumber.Normalize(phone)!);
        var code = await (await shop.PostJsonAsync($"/api/v1/customers/{customerId}/phone-code", new { })).ReadAsync<PhoneCodeDto>();
        (await priya.PostJsonAsync($"/api/v1/me/khata/{auth.ShopId}/{customerId}/confirm", new { code = code.Code })).EnsureSuccessStatusCode();

        // Her khata shows at checkout, but the shop hasn't allowed app orders on it yet.
        var info = await priya.GetJsonAsync<CheckoutInfoDto>($"/api/v1/me/checkout/{auth.ShopId}");
        Assert.Equal((false, 400m), (info.Khata!.Allowed, info.Khata.Balance));
        Assert.Null(info.UpiId);
        Assert.Equal(HttpStatusCode.BadRequest, (await priya.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 2m, "pickup", "khata"))).StatusCode);
        // No UPI ID: "pay now by UPI" can't be chosen.
        Assert.Equal(HttpStatusCode.BadRequest, (await priya.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 2m, "pickup", "upi"))).StatusCode);

        // Allowed with a ₹700 limit: ₹200 fits (400 + 200), another ₹200 would not.
        (await shop.PutAsJsonAsync($"/api/v1/customers/{customerId}/online-khata", new { allowed = true })).EnsureSuccessStatusCode();
        await SqlAsync("UPDATE customers SET credit_limit = 700 WHERE id = @p", customerId);
        var placed = await (await priya.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 2m, "pickup", "khata"))).ReadAsync<StoreOrderPlaced>();
        Assert.Equal(OrderPayment.Khata, placed.Order.Payment);
        var line = placed.Order.Lines.Single();
        await (await shop.PostJsonAsync($"/api/v1/orders/{placed.Order.Id}/pack", new { lines = new[] { new { id = line.Id, packedQuantity = 2m, unitPrice = 100m, removed = false } } })).ReadAsync<OrderDto>();
        await (await shop.PostJsonAsync($"/api/v1/orders/{placed.Order.Id}/deliver", new { })).ReadAsync<OrderDto>();
        var khata = await priya.GetJsonAsync<KhataViewDto>($"/api/v1/me/khata/{auth.ShopId}/{customerId}");
        Assert.Equal(600m, khata.Statement.ClosingBalance);
        Assert.Equal(HttpStatusCode.BadRequest, (await priya.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 2m, "pickup", "khata"))).StatusCode);

        // With a UPI ID, the khata page and the checkout carry it for "Pay by UPI".
        await SqlAsync("UPDATE shops SET upi_id = 'khata@okaxis' WHERE id = @p", auth.ShopId);
        Assert.Equal("khata@okaxis", (await priya.GetJsonAsync<KhataViewDto>($"/api/v1/me/khata/{auth.ShopId}/{customerId}")).ShopUpi);
        var upi = await (await priya.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 1m, "pickup", "upi"))).ReadAsync<StoreOrderPlaced>();
        Assert.Equal(OrderPayment.Upi, upi.Order.Payment);
    }

    [Fact]
    public async Task A_customer_adds_items_until_the_shop_packs_and_can_ask_for_alerts()
    {
        var (shop, auth) = await api.SignUpAsync("Add Store");
        var slug = auth.Memberships.Single().ShopSlug;
        var atta = await ProductAsync(shop, "Atta", "kg", 44m, 10m);
        var dal = await ProductAsync(shop, "Dal", "kg", 120m, 2m);
        var priya = await RegisterAsync("Priya", ApiFactory.NewPhone());
        var placed = await (await priya.PostJsonAsync($"/api/v1/store/{slug}/orders", Body(atta.Id, 2m, "pickup"))).ReadAsync<StoreOrderPlaced>();
        var id = placed.Order.Id;

        // More atta joins its line; dal is a new line, with its stock held.
        var added = await (await priya.PostJsonAsync($"/api/v1/me/orders/{id}/add", new { lines = new object[] { new { productId = atta.Id, quantity = 1m }, new { productId = dal.Id, quantity = 1m } } })).ReadAsync<OrderDto>();
        Assert.Equal(2, added.Lines.Count);
        Assert.Equal(3m, added.Lines.Single(l => l.ProductId == atta.Id).Quantity);
        Assert.Equal(1m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{dal.Id}")).Reserved);
        Assert.Equal(HttpStatusCode.BadRequest, (await priya.PostJsonAsync($"/api/v1/me/orders/{id}/add", new { lines = new object[] { new { productId = dal.Id, quantity = 5m } } })).StatusCode);

        // Accepted still takes additions and the customer can still cancel; once packed, neither.
        await (await shop.PostJsonAsync($"/api/v1/orders/{id}/accept", new { })).ReadAsync<OrderDto>();
        var more = await (await priya.PostJsonAsync($"/api/v1/me/orders/{id}/add", new { lines = new object[] { new { productId = dal.Id, quantity = 1m } } })).ReadAsync<OrderDto>();
        Assert.Equal(2m, more.Lines.Single(l => l.ProductId == dal.Id).Quantity);
        await (await shop.PostJsonAsync($"/api/v1/orders/{id}/pack", new { lines = more.Lines.Select(l => new { id = l.Id, packedQuantity = l.Quantity, unitPrice = l.UnitPrice, removed = false }).ToArray() })).ReadAsync<OrderDto>();
        Assert.Equal(HttpStatusCode.Conflict, (await priya.PostJsonAsync($"/api/v1/me/orders/{id}/add", new { lines = new object[] { new { productId = atta.Id, quantity = 1m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await priya.PostJsonAsync($"/api/v1/me/orders/{id}/cancel", new { })).StatusCode);

        // Another person can't touch her order.
        var stranger = await RegisterAsync("Stranger", ApiFactory.NewPhone());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostJsonAsync($"/api/v1/me/orders/{id}/add", new { lines = new object[] { new { productId = atta.Id, quantity = 1m } } })).StatusCode);

        // Alerts: a phone signs up for order notifications; WhatsApp updates are her choice.
        Assert.False(string.IsNullOrEmpty((await priya.GetJsonAsync<Dictionary<string, string>>("/api/v1/me/push/key"))["key"]));
        (await priya.PostJsonAsync("/api/v1/me/push/subscribe", new { endpoint = "https://push.example.test/abc123456", p256dh = "p256dh-key-123456", auth = "auth-123", language = "hi" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await priya.PostJsonAsync("/api/v1/me/push/subscribe", new { endpoint = "http://insecure", p256dh = "x", auth = "y" })).StatusCode);
        Assert.False((await priya.GetJsonAsync<OrderAlertsDto>("/api/v1/me/order-alerts")).WhatsApp);
        (await priya.PutAsJsonAsync("/api/v1/me/order-alerts", new { whatsApp = true })).EnsureSuccessStatusCode();
        Assert.True((await priya.GetJsonAsync<OrderAlertsDto>("/api/v1/me/order-alerts")).WhatsApp);
        (await priya.PostJsonAsync("/api/v1/me/push/unsubscribe", new { endpoint = "https://push.example.test/abc123456" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public void Alerts_read_in_the_phones_language()
    {
        Assert.Equal("Sharma accepted your order", OrderUpdates.Message("en", "accepted", "Sharma", "#AB12").Title);
        Assert.Contains("रास्ते", OrderUpdates.Message("hi", "out", "Sharma", "#AB12").Title);
        Assert.Contains("બદલ્યો", OrderUpdates.Message("gu", "updated", "Sharma", "#AB12").Title);
    }

    private static object Body(Guid productId, decimal quantity, string fulfilment, string? payment = null) => new
    {
        id = Guid.CreateVersion7(),
        name = "Neha",
        phone = ApiFactory.NewPhone(),
        fulfilment,
        address = fulfilment == "delivery" ? "B-12, Raja Park" : null,
        payment,
        lines = new object[] { new { productId, quantity } },
    };

    private static async Task<ProductDto> ProductAsync(HttpClient client, string name, string unit, decimal price, decimal stock) =>
        await (await client.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name, unit, sellingPrice = price, openingStock = stock })).ReadAsync<ProductDto>();

    private async Task<HttpClient> RegisterAsync(string name, string phone)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/register", new { fullName = name, phone, password = "secret123" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task SqlAsync(string sql, object value)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, db);
        command.Parameters.AddWithValue("p", value);
        await command.ExecuteNonQueryAsync();
    }
}
