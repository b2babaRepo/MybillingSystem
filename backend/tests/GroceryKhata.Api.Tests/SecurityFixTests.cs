using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>What the sign-in and payments review found, kept fixed.</summary>
[Collection(ApiCollection.Name)]
public sealed class SecurityFixTests(ApiFactory api)
{
    private async Task<AuthResponse> SmsSignIn(HttpClient client, string phone)
    {
        (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone })).EnsureSuccessStatusCode();
        var code = api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(phone)!);
        return await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code, fullName = "Real Owner" })).ReadAsync<AuthResponse>();
    }

    [Fact]
    public async Task A_password_set_by_someone_else_stops_working_when_the_real_owner_proves_the_number()
    {
        var phone = ApiFactory.NewPhone();
        var attacker = api.CreateClient();
        var stolen = await (await attacker.PostJsonAsync("/api/v1/auth/register", new { fullName = "Attacker", phone, password = "attacker-pw" })).ReadAsync<AuthResponse>();

        await SmsSignIn(api.CreateClient(), phone);

        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "attacker-pw" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.PostJsonAsync("/api/v1/auth/refresh", new { stolen.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task After_proving_the_number_a_person_can_set_a_password_and_change_it_only_with_the_old_one()
    {
        var phone = ApiFactory.NewPhone();
        var client = api.CreateClient();
        var auth = await SmsSignIn(client, phone);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        (await client.PutAsJsonAsync("/api/v1/profile/password", new { newPassword = "first-pw" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/profile/password", new { currentPassword = "wrong", newPassword = "second-pw" })).StatusCode);
        (await client.PutAsJsonAsync("/api/v1/profile/password", new { currentPassword = "first-pw", newPassword = "second-pw" })).EnsureSuccessStatusCode();

        (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "second-pw" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_refresh_retried_straight_away_keeps_the_session()
    {
        var (_, auth) = await api.SignUpAsync("Two Tabs Store");
        var client = api.CreateClient();
        var first = await (await client.PostJsonAsync("/api/v1/auth/refresh", new { auth.RefreshToken })).ReadAsync<AuthResponse>();

        var retry = await client.PostJsonAsync("/api/v1/auth/refresh", new { auth.RefreshToken });

        retry.EnsureSuccessStatusCode();
        (await client.PostJsonAsync("/api/v1/auth/refresh", new { first.RefreshToken })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_second_shop_gets_no_new_trial_and_cannot_refer_itself()
    {
        var phone = ApiFactory.NewPhone();
        var client = api.CreateClient();
        var first = await (await client.PostJsonAsync("/api/v1/auth/sign-up", new { shopName = "First Shop", fullName = "Owner", phone, password = "secret123", language = "en" })).ReadAsync<AuthResponse>();
        var second = await (await client.PostJsonAsync("/api/v1/auth/sign-up", new { shopName = "Second Shop", fullName = "Owner", phone, password = "secret123", language = "en", referralCode = first.Memberships.Single().ShopSlug })).ReadAsync<AuthResponse>();

        var secondId = second.ShopId!.Value;
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.Subscriptions.AsNoTracking().SingleAsync(s => s.ShopId == secondId);
        Assert.True(subscription.TrialEndsAt <= DateTimeOffset.UtcNow);
        Assert.Null(subscription.ReferredByShopId);
    }

    [Fact]
    public async Task A_shop_without_the_store_feature_has_no_public_store()
    {
        var (_, auth) = await api.SignUpAsync("Lapsed Store");
        var slug = auth.Memberships.Single().ShopSlug;
        var shopId = auth.ShopId!.Value;
        var guest = api.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/v1/store/{slug}")).StatusCode); // on trial

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Subscriptions.Add(new Subscription { ShopId = shopId, TrialEndsAt = DateTimeOffset.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/store/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.PostJsonAsync($"/api/v1/store/{slug}/orders", new { id = Guid.CreateVersion7(), name = "N", phone = ApiFactory.NewPhone(), lines = new object[] { new { productId = Guid.NewGuid(), quantity = 1m } } })).StatusCode);
    }

    [Fact]
    public async Task Turning_a_helper_back_on_counts_against_the_plan()
    {
        var (shop, auth) = await api.SignUpAsync("Limit Store");
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Subscriptions.Add(new Subscription { ShopId = auth.ShopId!.Value, TrialEndsAt = DateTimeOffset.UtcNow.AddDays(-1), CompPlanCode = "basic" }); // one helper
            await db.SaveChangesAsync();
        }

        var first = await (await shop.PostJsonAsync("/api/v1/shop/staff", new { name = "First", phone = ApiFactory.NewPhone(), pin = "4826" })).ReadAsync<GroceryKhata.Api.Shops.StaffDto>();
        (await shop.PostAsync($"/api/v1/shop/staff/{first.MembershipId}/active?active=false", null)).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/shop/staff", new { name = "Second", phone = ApiFactory.NewPhone(), pin = "4826" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.PaymentRequired, (await shop.PostAsync($"/api/v1/shop/staff/{first.MembershipId}/active?active=true", null)).StatusCode);
    }

    [Fact]
    public async Task The_data_export_holds_no_keys_to_a_customers_khata()
    {
        var (shop, _) = await api.SignUpAsync("Export Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<GroceryKhata.Api.Ledger.KhataLinkDto>();

        var file = await shop.GetStringAsync("/api/v1/shop/export");

        Assert.Contains("Ramesh", file);
        Assert.DoesNotContain(link.Token, file);
    }

    [Fact]
    public void Paid_time_moved_to_another_plan_keeps_its_value_not_its_length()
    {
        var now = DateTimeOffset.UtcNow;
        var gold = BillingService.ConvertedEnd(now, now.AddDays(360), 999m, 1999m);
        Assert.InRange((gold - now).TotalDays, 179, 181);

        var silver = BillingService.ConvertedEnd(now, now.AddDays(180), 1999m, 999m);
        Assert.InRange((silver - now).TotalDays, 359, 361);
    }

    private static string Sign(string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-webhook-secret"), Encoding.UTF8.GetBytes(body)));

    private static string Refund(string paymentId, int refunded) =>
        "{\"event\":\"refund.processed\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId + "\",\"amount\":49900,\"amount_refunded\":" + refunded + "}}}}";

    private async Task<HttpStatusCode> Webhook(string body)
    {
        var client = api.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Razorpay-Signature", Sign(body));
        return (await client.PostAsync("/api/v1/webhooks/razorpay", content)).StatusCode;
    }

    [Fact]
    public async Task Paying_by_webhook_opens_the_plan_once_and_a_full_refund_in_razorpay_takes_it_back()
    {
        var (shop, auth) = await api.SignUpAsync("Gateway Store");
        var shopId = auth.ShopId!.Value;
        var orderId = "order_" + Guid.NewGuid().ToString("N");
        var paymentId = "pay_" + Guid.NewGuid().ToString("N");
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.GatewayOrders.Add(new GatewayOrder { OrderId = orderId, ShopId = shopId, PlanCode = "basic", Term = PlanTerm.Year1, Amount = 499m });
            await db.SaveChangesAsync();
        }

        var captured = "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId + "\",\"order_id\":\"" + orderId + "\",\"amount\":49900}}}}";
        Assert.Equal(HttpStatusCode.OK, await Webhook(captured));
        Assert.Equal(HttpStatusCode.OK, await Webhook(captured)); // Razorpay repeats itself

        async Task<(int Payments, DateTimeOffset? Until, int Refunded)> State()
        {
            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var paid = await db.SubscriptionPayments.AsNoTracking().Where(p => p.ShopId == shopId).ToListAsync();
            return (paid.Count, (await db.Subscriptions.AsNoTracking().SingleAsync(s => s.ShopId == shopId)).PaidUntil, paid.Count(p => p.RefundedAt != null));
        }

        var paidFor = await State();
        Assert.Equal((1, 0), (paidFor.Payments, paidFor.Refunded));

        var partial = Refund(paymentId, 100);
        Assert.Equal(HttpStatusCode.OK, await Webhook(partial));
        Assert.Equal(0, (await State()).Refunded);

        var full = Refund(paymentId, 49900);
        Assert.Equal(HttpStatusCode.OK, await Webhook(full));
        var after = await State();
        Assert.Equal(1, after.Refunded);
        Assert.True(after.Until is null || after.Until < paidFor.Until);
    }

    [Fact]
    public async Task Payments_at_the_same_moment_get_different_invoice_numbers()
    {
        var shops = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            shops.Add((await api.SignUpAsync($"Invoice Store {i}")).Item2.ShopId!.Value);
        }

        await Task.WhenAll(shops.Select(async shopId =>
        {
            using var scope = api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<BillingService>().RecordAsync(shopId, new Quote("basic", PlanTerm.Year1, 499m, null, null), PaymentMethod.Upi, "REF" + Guid.NewGuid().ToString("N")[..12], null, default);
        }));

        using var check = api.Services.CreateScope();
        var numbers = await check.ServiceProvider.GetRequiredService<AppDbContext>().SubscriptionPayments.AsNoTracking().Where(p => shops.Contains(p.ShopId)).Select(p => p.InvoiceNo).ToListAsync();
        Assert.Equal(6, numbers.Distinct().Count());
    }

    [Fact]
    public async Task A_demoted_owner_loses_owner_rights_at_once()
    {
        var (shop, auth) = await api.SignUpAsync("Demote Store");
        (await shop.GetAsync("/api/v1/audit")).EnsureSuccessStatusCode();
        var membership = auth.Memberships.Single().MembershipId;

        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var demote = new Npgsql.NpgsqlCommand("UPDATE memberships SET role = 'Helper' WHERE id = @id", db);
            demote.Parameters.AddWithValue("id", membership);
            await demote.ExecuteNonQueryAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.GetAsync("/api/v1/audit")).StatusCode);
    }
}
