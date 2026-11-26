using System.Net;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Lockouts that double so a PIN can't be guessed a few tries at a time, and an SMS code that always gets the owner in.</summary>
[Collection(ApiCollection.Name)]
public sealed class LockoutTests(ApiFactory api)
{
    [Fact]
    public async Task Each_lockout_in_a_row_lasts_twice_as_long_and_a_quiet_day_starts_again()
    {
        var (owner, _) = await api.SignUpAsync();
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var counter = api.CreateClient();

        // 5 wrong PINs: locked for 15 minutes, even with the right PIN.
        await WrongAsync(counter, phone, "1111");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await counter.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).StatusCode);
        Assert.InRange((await MinutesLockedAsync(phone))!.Value, 14, 15);

        // Once it ends, 5 more: 30 minutes, then an hour.
        await EndLockoutAsync(phone);
        await WrongAsync(counter, phone, "2222");
        Assert.InRange((await MinutesLockedAsync(phone))!.Value, 29, 30);
        await EndLockoutAsync(phone);
        await WrongAsync(counter, phone, "3333");
        Assert.InRange((await MinutesLockedAsync(phone))!.Value, 59, 60);

        // Signing in between doesn't start the doubling again while a lockout is recent...
        await EndLockoutAsync(phone);
        (await counter.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).EnsureSuccessStatusCode();
        await WrongAsync(counter, phone, "4444");
        Assert.InRange((await MinutesLockedAsync(phone))!.Value, 119, 120);

        // ...but a day with no lockout does.
        await AdminAsync("UPDATE users SET lockout_ends_at = now() - interval '1 minute', last_locked_at = now() - interval '25 hours' WHERE phone = @p", phone);
        (await counter.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).EnsureSuccessStatusCode();
        await WrongAsync(counter, phone, "5555");
        Assert.InRange((await MinutesLockedAsync(phone))!.Value, 14, 15);
    }

    [Fact]
    public async Task A_lockout_from_wrong_passwords_does_not_stop_an_sms_code()
    {
        var phone = ApiFactory.NewPhone();
        var (_, signUp) = await api.SignUpAsync(phone: phone);
        var stranger = api.CreateClient();
        await WrongAsync(stranger, phone, "guessing");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await stranger.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).StatusCode);

        // The owner asks for a code to their phone: in, and the lockout is lifted.
        var owner = api.CreateClient();
        (await owner.PostJsonAsync("/api/v1/auth/otp/send", new { phone })).EnsureSuccessStatusCode();
        var code = api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(phone)!)!;
        var auth = await (await owner.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code })).ReadAsync<AuthResponse>();
        Assert.Equal(signUp.ShopId, auth.ShopId);
        Assert.True(await MinutesLockedAsync(phone) is null);

        // The stranger keeps guessing: the next lockout is longer, as it is still in a row.
        await WrongAsync(stranger, phone, "guessing");
        Assert.InRange((await MinutesLockedAsync(phone))!.Value, 29, 30);
    }

    [Fact]
    public async Task The_lockout_message_points_to_signing_in_by_sms()
    {
        var phone = ApiFactory.NewPhone();
        await api.SignUpAsync(phone: phone);
        var client = api.CreateClient();
        await WrongAsync(client, phone, "wrong-one");

        var locked = await client.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" });
        Assert.Contains("code by SMS", await locked.Content.ReadAsStringAsync());
    }

    private static async Task WrongAsync(HttpClient client, string phone, string password)
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password })).StatusCode);
        }
    }

    /// <summary>Minutes until the lockout ends; null when none is set.</summary>
    private async Task<double?> MinutesLockedAsync(string phone)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("SELECT extract(epoch FROM lockout_ends_at - now()) / 60 FROM users WHERE phone = @p", db);
        command.Parameters.AddWithValue("p", PhoneNumber.Normalize(phone)!);
        return await command.ExecuteScalarAsync() is decimal minutes ? (double)minutes : null;
    }

    /// <summary>As if the lockout had run its time (the doubling still remembers it).</summary>
    private Task EndLockoutAsync(string phone) => AdminAsync("UPDATE users SET lockout_ends_at = now() - interval '1 minute' WHERE phone = @p", phone);

    private async Task AdminAsync(string sql, string phone)
    {
        await using var db = new Npgsql.NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata");
        await db.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, db);
        command.Parameters.AddWithValue("p", PhoneNumber.Normalize(phone)!);
        await command.ExecuteNonQueryAsync();
    }
}
