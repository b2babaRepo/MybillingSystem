using System.Net;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Sign-in with a code by SMS (the test SMS sender keeps the last code per phone).</summary>
[Collection(ApiCollection.Name)]
public sealed class OtpTests(ApiFactory api)
{
    private string LastCode(string phone) => api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(phone)!)!;

    [Fact]
    public async Task A_new_number_gets_a_code_gives_a_name_and_has_a_household_account()
    {
        var client = api.CreateClient();
        var phone = ApiFactory.NewPhone();
        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone })).StatusCode);
        var code = LastCode(phone);

        var noName = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.Conflict, noName.StatusCode); // right code, but who are you?

        var auth = await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code, fullName = "Priya Agarwal", language = "hi" })).ReadAsync<AuthResponse>();
        Assert.Equal("Priya Agarwal", auth.User.FullName);
        Assert.Empty(auth.Memberships);

        // A code works once.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code })).StatusCode);
    }

    [Fact]
    public async Task A_shopkeeper_signs_in_with_a_code_straight_into_their_shop()
    {
        var phone = ApiFactory.NewPhone();
        var (_, signUp) = await api.SignUpAsync("Sharma General Store", phone);
        var client = api.CreateClient();
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone });

        var auth = await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code = LastCode(phone) })).ReadAsync<AuthResponse>();
        Assert.Equal(signUp.ShopId, auth.ShopId);
    }

    [Fact]
    public async Task Wrong_codes_run_out_and_codes_cannot_be_asked_for_again_and_again()
    {
        var client = api.CreateClient();
        var phone = ApiFactory.NewPhone();
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone });
        var code = LastCode(phone);
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code = wrong })).StatusCode);
        }

        // Five wrong tries: even the right code no longer works.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code, fullName = "Anita" })).StatusCode);

        // Straight away again: wait first.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone })).StatusCode);
    }
}
