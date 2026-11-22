using System.Net.Http.Headers;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Festival stock prompts: what to stock before a festival, from last year's sales or the festival's usual items.</summary>
[Collection(ApiCollection.Name)]
public sealed class FestivalTests(ApiFactory api)
{
    private WebApplicationFactory<Program> At(int year, int month, int day) =>
        api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            var clock = new FixedClock(new DateTimeOffset(year, month, day, 6, 0, 0, TimeSpan.Zero));
            s.AddSingleton<TimeProvider>(clock);
            // Tokens are checked against the same stopped clock, or they would look expired (or not yet valid).
            s.PostConfigureAll<JwtBearerOptions>(o =>
            {
                o.TimeProvider = clock;
                o.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) => notBefore <= clock.GetUtcNow().UtcDateTime.AddMinutes(5) && expires >= clock.GetUtcNow().UtcDateTime;
            });
        }));

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> app, string phone)
    {
        var client = app.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<(HttpClient Shop, string Phone)> ShopAsync(WebApplicationFactory<Program> app)
    {
        var phone = ApiFactory.NewPhone();
        var client = app.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Diwan Kirana", fullName = "Raj Diwan", phone, password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, phone);
    }

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal stock)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = 100m, openingStock = stock })).EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> SellAsync(HttpClient shop, Guid item, string name, decimal quantity) =>
        shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = item, name, quantity, unitPrice = 100m } }, paidAmount = 100m * quantity, paymentMode = "cash" });

    [Fact]
    public async Task Without_history_the_festivals_usual_items_are_suggested_and_a_shop_can_turn_one_off()
    {
        using var app = At(2027, 10, 10);
        var (shop, _) = await ShopAsync(app);
        var kaju = await ItemAsync(shop, "Kaju 250 g", 2m);
        await ItemAsync(shop, "Lifebuoy soap", 40m);

        var upcoming = await shop.GetJsonAsync<List<FestivalDto>>("/api/v1/festivals/upcoming");
        var diwali = upcoming.Single(f => f.Key == "diwali");
        Assert.Equal((new DateOnly(2027, 10, 29), 19, "usual_items"), (diwali.Date, diwali.DaysLeft, diwali.Basis));
        Assert.Equal([kaju], diwali.Items.Select(i => i.ProductId));

        // Turned off, it stops prompting (but still shows when asked for all); a local date moves it.
        (await shop.PostJsonAsync("/api/v1/festivals/settings", new { key = "diwali", year = 2027, off = true })).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await shop.GetJsonAsync<List<FestivalDto>>("/api/v1/festivals/upcoming"), f => f.Key == "diwali");
        Assert.Contains(await shop.GetJsonAsync<List<FestivalDto>>("/api/v1/festivals/upcoming?all=true"), f => f.Key == "diwali" && f.Changed);
        (await shop.PostJsonAsync("/api/v1/festivals/settings", new { key = "diwali", year = 2027, date = "2027-10-30" })).EnsureSuccessStatusCode();
        Assert.Equal(20, (await shop.GetJsonAsync<List<FestivalDto>>("/api/v1/festivals/upcoming")).Single(f => f.Key == "diwali").DaysLeft);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/festivals/settings", new { key = "diwali", year = 2027, date = "2027-03-01" })).StatusCode);
    }

    [Fact]
    public async Task A_clothes_shop_gets_festive_wear_for_diwali_and_a_kirana_does_not()
    {
        using var app = At(2027, 10, 10);
        var phone = ApiFactory.NewPhone();
        var fashion = app.CreateClient();
        var auth = await (await fashion.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Sitara Fashion", fullName = "Neha Sitara", phone, password = "secret123", language = "hi", type = "clothes" })).ReadAsync<AuthResponse>();
        fashion.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var saree = await ItemAsync(fashion, "Banarasi Saree", 3m);
        var kurta = await ItemAsync(fashion, "Men Kurta Pyjama Set", 5m);
        await ItemAsync(fashion, "Socks pack", 20m);

        var diwali = (await fashion.GetJsonAsync<List<FestivalDto>>("/api/v1/festivals/upcoming")).Single(f => f.Key == "diwali");
        Assert.Equal("usual_items", diwali.Basis);
        Assert.Equal(new[] { saree, kurta }.Order(), diwali.Items.Select(i => i.ProductId).Order());

        var (kirana, _) = await ShopAsync(app);
        await ItemAsync(kirana, "Banarasi Saree", 3m);
        Assert.Empty((await kirana.GetJsonAsync<List<FestivalDto>>("/api/v1/festivals/upcoming")).Single(f => f.Key == "diwali").Items);
    }

    [Fact]
    public async Task Last_years_run_up_picks_the_items_that_sold_more_and_suggests_what_to_order()
    {
        string phone;
        Guid diya, rice;
        using (var before = At(2026, 10, 5))
        {
            HttpClient shop;
            (shop, phone) = await ShopAsync(before);
            diya = await ItemAsync(shop, "Clay lamp", 500m);
            rice = await ItemAsync(shop, "Basmati", 500m);
            // A normal month before Diwali 2026 (8 Nov): 4 lamps and 40 rice over the four weeks before the run-up.
            (await SellAsync(shop, diya, "Clay lamp", 4m)).EnsureSuccessStatusCode();
            (await SellAsync(shop, rice, "Basmati", 40m)).EnsureSuccessStatusCode();
        }

        using (var runUp = At(2026, 11, 1))
        {
            var shop = await SignInAsync(runUp, phone);
            // The run-up: 120 lamps (far above usual), 22 rice (about usual).
            (await SellAsync(shop, diya, "Clay lamp", 120m)).EnsureSuccessStatusCode();
            (await SellAsync(shop, rice, "Basmati", 22m)).EnsureSuccessStatusCode();
        }

        using var now = At(2027, 10, 10);
        var me = await SignInAsync(now, phone);
        var diwali = (await me.GetJsonAsync<List<FestivalDto>>("/api/v1/festivals/upcoming")).Single(f => f.Key == "diwali");
        Assert.Equal("last_year", diwali.Basis);
        var lamp = diwali.Items.Single();
        // 19 days left is more than the 14-day run-up, so the whole 120 is needed; 376 lamps are already in stock.
        Assert.Equal((diya, 120m, 2m, 0m), (lamp.ProductId, lamp.LastYear, lamp.Usual, lamp.Suggest));
        Assert.DoesNotContain(diwali.Items, i => i.ProductId == rice);
    }
}
