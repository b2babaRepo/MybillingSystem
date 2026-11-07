using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GroceryKhata.Api.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GroceryKhata.Api.Tests;

/// <summary>
/// The API against a real PostgreSQL in Docker. The app connects as khata_app, a non-superuser that owns
/// the database, exactly as in production, so row-level security is really enforced in these tests.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string AppConnectionString { get; private set; } = string.Empty;

    /// <summary>The container's superuser login: only for tests that prove the app refuses one in production.</summary>
    public string AdminConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using (var admin = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await admin.OpenAsync();
            await new NpgsqlCommand("CREATE ROLE khata_app LOGIN PASSWORD 'test-password'", admin).ExecuteNonQueryAsync();
            await new NpgsqlCommand("CREATE DATABASE grocery_khata OWNER khata_app", admin).ExecuteNonQueryAsync();
        }

        AppConnectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = "grocery_khata",
            Username = "khata_app",
            Password = "test-password",
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Added last, so these win over appsettings.json.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = AppConnectionString,
            ["Jwt:SigningKey"] = "test-signing-key-at-least-32-characters-long",
            ["Agents:AadhaarHashKey"] = "test-aadhaar-key",
            ["Billing:BillLinkKey"] = "test-bill-link-key",
            ["Database:MigrateOnStartup"] = "true",
            ["Auth:AuthRequestsPerMinutePerIp"] = "10000",
            ["Auth:StoreRequestsPerMinutePerIp"] = "10000",
            ["Auth:OtpSendsPerHourPerIp"] = "100000",
            ["Billing:RazorpayWebhookSecret"] = "test-webhook-secret",
            ["Security:RequestsPerMinutePerIp"] = "100000",
            ["Ai:Enabled"] = "false",
            ["Whatsapp:AppSecret"] = "test-app-secret",
            ["Whatsapp:VerifyToken"] = "test-verify-token",
        }));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Opens a new shop and returns a client signed in to it.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> SignUpAsync(string shopName = "Sharma General Store", string? phone = null, string password = "secret123")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/sign-up",
            new { shopName, fullName = "Ramesh Sharma", phone = phone ?? NewPhone(), password, language = "hi" }, Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    public static string NewPhone() => "9" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString("D9");
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url) => await (await client.GetAsync(url)).ReadAsync<T>();
}
