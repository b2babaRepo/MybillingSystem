using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>An owner who forgot their password signs in by SMS and sets a new one with a fresh SMS code.</summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests(ApiFactory api)
{
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
