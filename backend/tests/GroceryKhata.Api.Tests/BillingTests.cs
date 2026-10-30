using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Identity;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>Shop subscriptions: trial, paying by UPI with an offer, Free limits, free grants, holds, refunds, the admin agent.</summary>
[Collection(ApiCollection.Name)]
public sealed class BillingTests(ApiFactory api)
{
    [Fact]
    public async Task A_new_shop_starts_on_a_30_day_pro_trial_and_sees_the_plans()
    {
        var (shop, _) = await api.SignUpAsync();
        var view = await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription");
        Assert.Equal((SubscriptionStatus.Trial, "pro"), (view.Status, view.PlanCode));
        Assert.InRange(view.DaysLeft!.Value, 29, 30);
        Assert.Equal(["free", "basic", "pro"], view.Plans.Select(p => p.Code));
        Assert.Equal(499m, view.Plans.Single(p => p.Code == "basic").PriceYear1);
    }

    [Fact]
    public async Task A_shop_pays_by_upi_with_the_welcome_offer_and_a_billing_admin_confirms_it()
    {
        var (shop, auth) = await api.SignUpAsync();
        var quote = await (await shop.PostJsonAsync("/api/v1/subscription/quote", new { planCode = "basic", term = "year1", offerCode = "welcome299" })).ReadAsync<Quote>();
        Assert.Equal((299m, "WELCOME299", (string?)null), (quote.Amount, quote.OfferCode, quote.Error));

        var utr = "4" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var claim = await (await shop.PostJsonAsync("/api/v1/subscription/claims", new { planCode = "basic", term = "year1", offerCode = "WELCOME299", upiRef = utr })).ReadAsync<ClaimDto>();
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/subscription/claims", new { planCode = "basic", term = "year1", upiRef = utr })).StatusCode);

        // Nothing changes until someone who handles money checks it. Support can't; billing can.
        var support = await AdminAsync("Support");
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PostJsonAsync($"/api/v1/platform/claims/{claim.Id}/confirm", new { })).StatusCode);
        var billing = await AdminAsync("Billing");
        var payment = await (await billing.PostJsonAsync($"/api/v1/platform/claims/{claim.Id}/confirm", new { })).ReadAsync<PaymentDto>();
        Assert.Equal(299m, payment.Amount);
        Assert.StartsWith("GK/", payment.InvoiceNo);

        var view = await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription");
        Assert.Equal((SubscriptionStatus.Active, "basic"), (view.Status, view.PlanCode));
        Assert.InRange(view.DaysLeft!.Value, 364, 366);

        // The welcome price is for the first payment only.
        var again = await (await shop.PostJsonAsync("/api/v1/subscription/quote", new { planCode = "basic", term = "year1", offerCode = "WELCOME299" })).ReadAsync<Quote>();
        Assert.NotNull(again.Error);

        // Refund within 7 days takes the year back off.
        Assert.Equal(HttpStatusCode.NoContent, (await billing.PostJsonAsync($"/api/v1/platform/payments/{payment.Id}/refund", new { })).StatusCode);
        Assert.NotEqual(SubscriptionStatus.Active, (await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription")).Status);
        _ = auth;
    }

    [Fact]
    public async Task After_the_trial_a_shop_on_free_keeps_its_khata_but_not_reports_or_trade_features()
    {
        var (shop, auth) = await api.SignUpAsync();
        await EndTrialAsync(auth.ShopId!.Value);

        var view = await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription");
        Assert.Equal(SubscriptionStatus.Free, view.Status);
        var month = await shop.GetAsync($"/api/v1/reports/month?month={DateTime.UtcNow:yyyy-MM}");
        Assert.Equal(HttpStatusCode.PaymentRequired, month.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await shop.GetAsync("/api/v1/challans")).StatusCode);

        // The shop's own khata keeps working on Free.
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 200m);
        Assert.Equal(HttpStatusCode.OK, (await shop.GetAsync("/api/v1/reports/dues")).StatusCode);
    }

    [Fact]
    public async Task Free_access_and_holds_follow_the_admin_roles()
    {
        var (shop, auth) = await api.SignUpAsync();
        var shopId = auth.ShopId!.Value;
        await EndTrialAsync(shopId);
        var approver = await AdminAsync("Approver");
        var super = await AdminAsync("Super");

        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostJsonAsync($"/api/v1/platform/shops/{shopId}/complimentary", new { planCode = "pro", months = 12, reason = "Pilot shop" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await approver.PostJsonAsync($"/api/v1/platform/shops/{shopId}/complimentary", new { planCode = "pro", months = 3, reason = "Pilot shop" })).StatusCode);
        Assert.Equal(SubscriptionStatus.Complimentary, (await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription")).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await super.PostJsonAsync($"/api/v1/platform/shops/{shopId}/complimentary", new { planCode = "pro", reason = "Founding shop, free for life" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await approver.PostJsonAsync($"/api/v1/platform/shops/{shopId}/hold", new { reason = "Disputed payment" })).StatusCode);
        var held = await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription");
        Assert.Equal((SubscriptionStatus.OnHold, "Disputed payment"), (held.Status, held.HoldReason));
        Assert.Equal(HttpStatusCode.PaymentRequired, (await shop.GetAsync("/api/v1/challans")).StatusCode);

        await approver.PostJsonAsync($"/api/v1/platform/shops/{shopId}/release", new { });
        Assert.Equal(SubscriptionStatus.Complimentary, (await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription")).Status);
    }

    [Fact]
    public async Task The_agent_suggests_extending_a_busy_shops_trial_and_only_an_approver_carries_it_out()
    {
        var (shop, auth) = await api.SignUpAsync("Busy Kirana");
        var shopId = auth.ShopId!.Value;
        for (var i = 0; i < 10; i++)
        {
            await shop.PostJsonAsync("/api/v1/bills", new
            {
                id = Guid.CreateVersion7(), lines = new[] { new { name = "Atta", quantity = 1m, unit = "kg", unitPrice = 45m } }, paidAmount = 45m,
            });
        }

        await EndTrialAsync(shopId, daysAgo: 3);
        var super = await AdminAsync("Super");
        await super.PostJsonAsync("/api/v1/platform/agent/run", new { });
        var suggestion = Assert.Single(await super.GetJsonAsync<List<ProposalDto>>("/api/v1/platform/agent/suggestions"), p => p.ShopId == shopId);
        Assert.Equal((ProposalKind.ExtendTrial, 14), (suggestion.Kind, suggestion.Days));

        var support = await AdminAsync("Support");
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PostJsonAsync($"/api/v1/platform/agent/suggestions/{suggestion.Id}/approve", new { })).StatusCode);
        var approver = await AdminAsync("Approver");
        Assert.Equal(HttpStatusCode.NoContent, (await approver.PostJsonAsync($"/api/v1/platform/agent/suggestions/{suggestion.Id}/approve", new { })).StatusCode);
        Assert.Equal(SubscriptionStatus.Trial, (await shop.GetJsonAsync<SubscriptionView>("/api/v1/subscription")).Status);

        var briefing = await super.GetJsonAsync<Briefing>("/api/v1/platform/agent/briefing");
        Assert.True(briefing.Trials >= 1);
    }

    /// <summary>Moves a shop's trial end into the past (as if 30 days went by).</summary>
    private async Task EndTrialAsync(Guid shopId, int daysAgo = 1)
    {
        await using var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var upsert = new NpgsqlCommand("""
            INSERT INTO subscriptions (id, shop_id, trial_ends_at, referral_rewarded, created_at, updated_at)
            VALUES (gen_random_uuid(), @s, now() - make_interval(days => @d), false, now(), now())
            ON CONFLICT (shop_id) DO UPDATE SET trial_ends_at = now() - make_interval(days => @d)
            """, db);
        upsert.Parameters.AddWithValue("s", shopId);
        upsert.Parameters.AddWithValue("d", daysAgo);
        await upsert.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task A_billing_admin_opens_a_shop_for_a_shopkeeper_who_then_signs_in_with_the_one_time_password()
    {
        var support = await AdminAsync("Support");
        var phone = ApiFactory.NewPhone();
        var body = new { shopName = "Gupta Kirana", ownerName = "Ramesh Gupta", phone, area = "Lalpur", type = "Kirana", language = "hi" };
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PostJsonAsync("/api/v1/platform/shops", body)).StatusCode);

        var approver = await AdminAsync("Billing");
        var created = await (await approver.PostJsonAsync("/api/v1/platform/shops", body)).ReadAsync<GroceryKhata.Api.Shops.CreatedShop>();
        Assert.True(created.NewAccount);
        Assert.NotNull(created.TempPassword);

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = created.TempPassword })).ReadAsync<AuthResponse>();
        Assert.Equal(created.ShopId, signedIn.ShopId);
        var owner = api.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        Assert.Equal(SubscriptionStatus.Trial, (await owner.GetJsonAsync<SubscriptionView>("/api/v1/subscription")).Status);

        // The same number again: a second shop for the same owner, no new password.
        var second = await (await approver.PostJsonAsync("/api/v1/platform/shops", body)).ReadAsync<GroceryKhata.Api.Shops.CreatedShop>();
        Assert.False(second.NewAccount);
        Assert.Null(second.TempPassword);
    }

    /// <summary>A new account given a platform-admin role directly in the database, then signed in.</summary>
    private async Task<HttpClient> AdminAsync(string role)
    {
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("Admin's own shop", phone);
        await using (var db = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var insert = new NpgsqlCommand("INSERT INTO platform_admins (id, user_id, role, created_at, updated_at) VALUES (gen_random_uuid(), @u, @r, now(), now())", db);
            insert.Parameters.AddWithValue("u", auth.User.Id);
            insert.Parameters.AddWithValue("r", role);
            await insert.ExecuteNonQueryAsync();
        }

        var signedIn = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        return client;
    }
}
