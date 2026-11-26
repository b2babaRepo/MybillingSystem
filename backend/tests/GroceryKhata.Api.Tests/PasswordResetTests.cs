using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Forgot password: from the sign-in screen (a code and a new password) or from Profile (a code in place of the old password).</summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests(ApiFactory api)
{
    [Fact]
    public async Task A_forgotten_password_is_reset_with_an_sms_code()
    {
        var phone = ApiFactory.NewPhone();
        var anon = api.CreateClient();
        (await anon.PostJsonAsync("/api/v1/auth/sign-up", new { shopName = "Reset Kirana", fullName = "Forgetful Owner", phone, password = "old-pass-1", language = "en", type = "kirana" })).EnsureSuccessStatusCode();

        // Too short, and a wrong code: both refused, and the old password still works.
        (await anon.PostJsonAsync("/api/v1/auth/otp/send", new { phone })).EnsureSuccessStatusCode();
        var code = api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(phone)!)!;
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostJsonAsync("/api/v1/auth/otp/reset-password", new { phone, code, newPassword = "123" })).StatusCode);
        var wrong = code == "000000" ? "111111" : "000000";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostJsonAsync("/api/v1/auth/otp/reset-password", new { phone, code = wrong, newPassword = "new-pass-2" })).StatusCode);

        // The right code sets the new password and signs in.
        var reset = await anon.PostJsonAsync("/api/v1/auth/otp/reset-password", new { phone, code, newPassword = "new-pass-2" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.False(string.IsNullOrEmpty((await reset.ReadAsync<AuthResponse>()).AccessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "old-pass-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "new-pass-2" })).StatusCode);
        // The code is used up.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostJsonAsync("/api/v1/auth/otp/reset-password", new { phone, code, newPassword = "another-3" })).StatusCode);
    }

    [Fact]
    public async Task A_forgotten_password_is_replaced_with_an_sms_code_to_the_owners_own_number()
    {
        var phone = ApiFactory.NewPhone();
        var (shop, _) = await api.SignUpAsync("Jevar House", phone, "forgotten1");
        async Task<string> CodeAsync()
        {
            (await api.CreateClient().PostJsonAsync("/api/v1/auth/otp/send", new { phone })).EnsureSuccessStatusCode();
            return api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(phone)!)!;
        }

        // Without the old password or a code: refused. A wrong code: refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/profile/password", new { newPassword = "newsecret1" })).StatusCode);
        var code = await CodeAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/profile/password", new { newPassword = "newsecret1", code = code == "000000" ? "111111" : "000000" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await shop.PutAsJsonAsync("/api/v1/profile/password", new { newPassword = "newsecret1", code })).StatusCode);
        // The same code can't be used again.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PutAsJsonAsync("/api/v1/profile/password", new { newPassword = "another12", code })).StatusCode);

        var client = api.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "newsecret1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "forgotten1" })).StatusCode);
    }
}
