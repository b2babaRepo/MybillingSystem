using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Numbered papers (estimates, jobs, girvi loans...) taken at the same moment, and signed khata links.</summary>
[Collection(ApiCollection.Name)]
public sealed class DocumentNumberTests(ApiFactory api)
{
    [Fact]
    public async Task Jobs_saved_at_the_same_moment_all_save_with_their_own_numbers_per_shop()
    {
        var shop = await ServiceShopAsync();
        var other = await ServiceShopAsync();

        var saves = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => shop.PostJsonAsync("/api/v1/jobs", Job())));
        var numbers = new List<int>();
        foreach (var save in saves)
        {
            numbers.Add((await save.ReadAsync<JobDetail>()).Job.Number);
        }

        Assert.Equal(Enumerable.Range(1, 12), numbers.Order());
        Assert.Equal(13, (await (await shop.PostJsonAsync("/api/v1/jobs", Job())).ReadAsync<JobDetail>()).Job.Number);
        // Another shop counts on its own.
        Assert.Equal(1, (await (await other.PostJsonAsync("/api/v1/jobs", Job())).ReadAsync<JobDetail>()).Job.Number);
    }

    [Fact]
    public async Task A_counter_carries_on_after_the_highest_number_already_saved()
    {
        var (shop, auth) = await api.SignUpAsync("Verma Hardware");
        Assert.Equal(1, (await (await shop.PostJsonAsync("/api/v1/estimates", Estimate())).ReadAsync<EstimateDto>()).Number);
        // As if the counter were new and estimates had been numbered before it (as on a server upgraded to it).
        await AdminAsync("DELETE FROM doc_counters WHERE shop_id = @s; UPDATE estimates SET number = 40 WHERE shop_id = @s", ("s", auth.ShopId!.Value));

        Assert.Equal(41, (await (await shop.PostJsonAsync("/api/v1/estimates", Estimate())).ReadAsync<EstimateDto>()).Number);
    }

    [Fact]
    public async Task The_database_alone_cannot_open_a_khata_link()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var stored = await ScalarAsync<string>("SELECT khata_token FROM customers WHERE id = @c", ("c", customer));
        var anyone = api.CreateClient();

        Assert.NotEqual(link.Token, stored);
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={stored}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anyone.GetAsync($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}")).StatusCode);
    }

    [Fact]
    public async Task A_link_sent_before_links_were_signed_opens_until_the_shop_shares_a_new_one()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        var slug = (await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>()).ShopSlug;
        // What the migration leaves for an old link: the hash of its token, made in SQL.
        const string oldToken = "an-old-random-token-from-before";
        await AdminAsync("UPDATE customers SET khata_token = 'h:' || encode(sha256(convert_to(@t, 'UTF8')), 'base64') WHERE id = @c", ("t", oldToken), ("c", customer));
        var anyone = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anyone.GetAsync($"/api/v1/khata/{slug}/{customer}?t={oldToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{slug}/{customer}?t=wrong")).StatusCode);

        var shared = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        Assert.Equal(HttpStatusCode.OK, (await anyone.GetAsync($"/api/v1/khata/{slug}/{customer}?t={shared.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/khata/{slug}/{customer}?t={oldToken}")).StatusCode);
    }

    private static object Job() => new { id = Guid.CreateVersion7(), title = "Pump repair", price = 0m };

    private async Task<HttpClient> ServiceShopAsync()
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = $"Service {Guid.NewGuid():N}"[..30], fullName = "Suresh Verma", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "sales_service" })).ReadAsync<GroceryKhata.Api.Identity.AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static object Estimate() => new
    {
        id = Guid.CreateVersion7(), partyName = "Sharma ji",
        lines = new object[] { new { name = "Fitting labour", quantity = 1m, unit = "piece", unitPrice = 200m } },
    };

    private async Task AdminAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }
}
