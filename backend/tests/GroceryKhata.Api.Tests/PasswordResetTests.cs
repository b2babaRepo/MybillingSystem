using System.Net;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Forgot password: a code to the mobile replaces the password without the old one, and signs in.</summary>
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
}
