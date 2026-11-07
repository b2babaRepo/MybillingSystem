using System.Net.Http.Json;
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

        // The number matches, but the shop only typed it: nothing shows until a code from the shop proves it's hers.
        var unproved = await priya.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata");
        Assert.Equal(["Sharma General Store", "Gupta Kirana"], unproved.OrderBy(k => k.ShopName == "Gupta Kirana").Select(k => k.ShopName));
        Assert.All(unproved, k => Assert.Equal((false, 0m, ""), (k.Confirmed, k.Balance, k.CustomerName)));
        var sharmaId = unproved.Single(k => k.ShopName == "Sharma General Store").ShopId;
        Assert.Equal(HttpStatusCode.NotFound, (await priya.GetAsync($"/api/v1/me/khata/{sharmaId}/{atSharma}")).StatusCode);

        await ConfirmAsync(sharma, priya, sharmaId, atSharma);
        var guptaEntry = unproved.Single(k => k.ShopName == "Gupta Kirana");
        await ConfirmAsync(gupta, priya, guptaEntry.ShopId, guptaEntry.CustomerId);

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

    [Fact]
    public async Task A_stranger_on_a_mistyped_number_sees_no_amounts_and_cannot_guess_the_code()
    {
        var wrongNumber = ApiFactory.NewPhone();
        var (shop, _) = await api.SignUpAsync("Typo Store");
        var customer = await CreateCustomerAsync(shop, "Priya", wrongNumber);
        await KhataTests.AddEntryAsync(shop, customer, "credit", 640m);
        var stranger = await RegisterAsync("Stranger", wrongNumber);
        await VerifyPhoneAsync(wrongNumber);

        var seen = (await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata")).Single();
        Assert.Equal((false, 0m, ""), (seen.Confirmed, seen.Balance, seen.CustomerName));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}")).StatusCode);

        var code = (await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/phone-code", new { })).ReadAsync<PhoneCodeDto>()).Code!;
        var wrong = code == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await stranger.PostJsonAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}/confirm", new { code = wrong })).StatusCode);
        }

        // Five wrong tries and the code stops: even the right one no longer works.
        Assert.Equal(HttpStatusCode.BadRequest, (await stranger.PostJsonAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}/confirm", new { code })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/me/khata/{seen.ShopId}/{customer}")).StatusCode);
    }

    [Fact]
    public async Task Correcting_a_phone_cuts_off_the_old_number_and_its_link_and_keeps_secrets_out_of_the_audit_log()
    {
        var wrong = ApiFactory.NewPhone();
        var right = ApiFactory.NewPhone();
        var (shop, auth) = await api.SignUpAsync("Fix Store");
        var customer = await CreateCustomerAsync(shop, "Priya", wrong);
        await KhataTests.AddEntryAsync(shop, customer, "credit", 640m);
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var stranger = await RegisterAsync("Stranger", wrong);
        await VerifyPhoneAsync(wrong);
        await ConfirmAsync(shop, stranger, auth.Memberships.Single().ShopId, customer);
        Assert.Equal(640m, (await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata")).Single().Balance);

        var fixedUp = await (await shop.PutAsJsonAsync($"/api/v1/customers/{customer}", new { name = "Priya Agarwal", phone = right })).ReadAsync<GroceryKhata.Api.Ledger.CustomerDto>();
        Assert.Equal(("Priya Agarwal", 640m), (fixedUp.Name, fixedUp.Balance));

        Assert.Empty(await stranger.GetJsonAsync<List<MyKhataDto>>("/api/v1/me/khata"));
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}")).StatusCode);

        var audit = await shop.GetStringAsync($"/api/v1/audit?recordId={customer}");
        Assert.Contains("phone", audit);
        Assert.DoesNotContain(link.Token, audit);
        Assert.DoesNotContain("confirm_code_hash", audit);
    }

    private async Task VerifyPhoneAsync(string phone)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var verify = new Npgsql.NpgsqlCommand("UPDATE users SET phone_verified_at = now() WHERE phone = @p", db);
        verify.Parameters.AddWithValue("p", GroceryKhata.Api.Common.PhoneNumber.Normalize(phone)!);
        await verify.ExecuteNonQueryAsync();
    }

    private static async Task ConfirmAsync(HttpClient shop, HttpClient person, Guid shopId, Guid customer)
    {
        var made = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/phone-code", new { })).ReadAsync<PhoneCodeDto>();
        (await person.PostJsonAsync($"/api/v1/me/khata/{shopId}/{customer}/confirm", new { code = made.Code })).EnsureSuccessStatusCode();
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
