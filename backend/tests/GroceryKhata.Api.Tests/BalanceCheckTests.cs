using System.Net;
using GroceryKhata.Api.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>The nightly khata check: every customer's stored balance against the sum of their entries.</summary>
[Collection(ApiCollection.Name)]
public sealed class BalanceCheckTests(ApiFactory api)
{
    [Fact]
    public async Task A_healthy_khata_checks_clean_and_a_broken_balance_is_caught_at_night_and_shown_to_the_owner()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        var anita = await KhataTests.CreateCustomerAsync(shop, "Anita");
        await KhataTests.AddEntryAsync(shop, anita, "credit", 500m);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.GetAsync("/api/v1/ledger/checks/latest")).StatusCode); // nothing checked yet

        var now = await (await shop.PostJsonAsync("/api/v1/ledger/checks", new { })).ReadAsync<BalanceCheckDto>();
        Assert.Equal((1, 0), (now.Customers, now.Mismatches));

        // Someone edits the database by hand: the stored balance no longer matches the entries.
        await using (var admin = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await admin.OpenAsync();
            await using var edit = new NpgsqlCommand("UPDATE customers SET balance = 50 WHERE id = @id", admin);
            edit.Parameters.AddWithValue("id", anita);
            await edit.ExecuteNonQueryAsync();
        }

        var nightly = new NightlyBalanceCheck(api.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<NightlyBalanceCheck>.Instance);
        Assert.True(await nightly.RunAsync(default) >= 1);

        var latest = await shop.GetJsonAsync<BalanceCheckDto>("/api/v1/ledger/checks/latest");
        var mismatch = Assert.Single(latest.Details);
        Assert.Equal(("Anita", 50m, 500m), (mismatch.Name, mismatch.StoredBalance, mismatch.SumOfEntries));
    }

    [Fact]
    public async Task Helpers_cannot_see_or_run_the_check()
    {
        var (owner, _) = await api.SignUpAsync("Sharma General Store");
        var phone = ApiFactory.NewPhone();
        await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" });
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<Identity.AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await helper.GetAsync("/api/v1/ledger/checks/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PostJsonAsync("/api/v1/ledger/checks", new { })).StatusCode);
    }

    [Theory]
    [InlineData("2026-10-28T10:00:00+05:30", 17)] // morning: tonight at 3 am
    [InlineData("2026-10-28T02:30:00+05:30", 0.5)] // just before 3 am: in half an hour
    [InlineData("2026-10-28T03:00:00+05:30", 24)] // exactly 3 am: tomorrow
    public void The_check_runs_at_3_am_india_time(string now, double hours)
    {
        Assert.Equal(TimeSpan.FromHours(hours), NightlyBalanceCheck.UntilNextRun(DateTimeOffset.Parse(now)));
    }
}
