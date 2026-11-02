using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Transparency step 2: customers read their own khata, by private link or in the household app.</summary>
[Collection(ApiCollection.Name)]
public sealed class KhataViewTests(ApiFactory api)
{
    [Fact]
    public async Task The_private_link_shows_the_khata_read_only_and_a_reset_cuts_off_the_old_link()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 500m);
        await KhataTests.AddEntryAsync(shop, customer, "payment", 200m);

        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var again = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var anyone = api.CreateClient();
        var view = await anyone.GetJsonAsync<KhataViewDto>($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}");

        Assert.Equal(link.Token, again.Token); // sharing twice sends the same link
        Assert.Equal(("Sharma General Store", 300m, 2), (view.ShopName, view.Statement.ClosingBalance, view.Statement.Entries.Count));
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t=wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}")).StatusCode);

        var reset = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link/reset", new { })).ReadAsync<KhataLinkDto>();
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={reset.Token}")).StatusCode);
    }

    [Fact]
    public async Task A_household_sees_its_khata_in_every_shop_by_phone_and_nobody_elses()
    {
        var phone = ApiFactory.NewPhone();
        var (sharma, _) = await api.SignUpAsync("Sharma General Store");
        var (gupta, guptaAuth) = await api.SignUpAsync("Gupta Kirana");
        var atSharma = await CreateCustomerAsync(sharma, "Priya", phone);
        await CreateCustomerAsync(gupta, "Priya ji", phone);
        await CreateCustomerAsync(sharma, "Someone else", ApiFactory.NewPhone());
        await KhataTests.AddEntryAsync(sharma, atSharma, "credit", 640m);

        var priya = await RegisterAsync("Priya Agarwal", phone);
        // A password account hasn't proved the number is hers: it sees nothing until an SMS code does.
        Assert.Empty(await priya.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata"));
        await using (var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await db.OpenAsync();
            await using var verify = new Npgsql.NpgsqlCommand("UPDATE users SET phone_verified_at = now() WHERE phone = @p", db);
            verify.Parameters.AddWithValue("p", GroceryKhata.Api.Common.PhoneNumber.Normalize(phone)!);
            await verify.ExecuteNonQueryAsync();
        }

        var stranger = await RegisterAsync("Stranger", ApiFactory.NewPhone());

        var mine = await priya.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata");
        Assert.Equal(["Sharma General Store", "Gupta Kirana"], mine.Select(k => k.ShopName));
        Assert.Equal(640m, mine[0].Balance);

        var sharmaKhata = await priya.GetJsonAsync<KhataViewDto>($"/api/v1/me/khata/{mine[0].ShopId}/{atSharma}");
        Assert.Equal(640m, sharmaKhata.Statement.ClosingBalance);

        // Someone with another phone sees nothing, and can't open Priya's khata by its IDs.
        Assert.Empty(await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata"));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/me/khata/{mine[0].ShopId}/{atSharma}")).StatusCode);
        // Nor can Priya open her Sharma khata through another shop's ID.
        Assert.Equal(HttpStatusCode.NotFound, (await priya.GetAsync($"/api/v1/me/khata/{guptaAuth.ShopId}/{atSharma}")).StatusCode);
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient shop, string name, string phone)
    {
        var id = Guid.CreateVersion7();
        await (await shop.PostJsonAsync("/api/v1/customers", new { id, name, phone })).ReadAsync<CustomerDto>();
        return id;
    }

    private async Task<HttpClient> RegisterAsync(string name, string phone)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/register", new { fullName = name, phone, password = "secret123" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
