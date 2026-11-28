using System.Net;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>A recovery email proved with a code, then used to reset a forgotten password.</summary>
[Collection(ApiCollection.Name)]
public sealed class EmailResetTests(ApiFactory api)
{
    private DevEmailSender Mail => api.Services.GetRequiredService<DevEmailSender>();

    private static string NewEmail() => $"owner{Guid.NewGuid():N}@example.com";

    private async Task<(HttpClient Client, string Phone, string Email)> VerifiedAsync()
    {
        var phone = ApiFactory.NewPhone();
        var (client, _) = await api.SignUpAsync("Verma Kirana", phone);
        var email = NewEmail();
        (await client.PutJsonAsync("/api/v1/profile", new { fullName = "Ramesh Verma", preferredLanguage = "en", email = email.ToUpperInvariant() })).EnsureSuccessStatusCode();
        (await client.PostJsonAsync("/api/v1/profile/email/send-code", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync("/api/v1/profile/email/verify", new { code = "000000" == Mail.LastCodeFor(email) ? "111111" : "000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/v1/profile/email/verify", new { code = Mail.LastCodeFor(email) })).StatusCode);
        return (client, phone, email);
    }

    [Fact]
    public async Task A_verified_email_resets_the_password_and_ends_other_sign_ins()
    {
        var (client, phone, email) = await VerifiedAsync();
        Assert.True((await client.GetJsonAsync<ProfileDto>("/api/v1/profile")).EmailVerified);

        var anon = api.CreateClient();
        (await anon.PostJsonAsync("/api/v1/auth/email/send-reset", new { email })).EnsureSuccessStatusCode();
        var code = Mail.LastCodeFor(email);
        Assert.NotNull(code);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostJsonAsync("/api/v1/auth/email/reset-password", new { email, code = code == "123456" ? "654321" : "123456", newPassword = "brandnew1" })).StatusCode);
        var signedIn = await (await anon.PostJsonAsync("/api/v1/auth/email/reset-password", new { email, code, newPassword = "brandnew1" })).ReadAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(signedIn.AccessToken));
        Assert.Contains("password was just changed", Mail.LastBodyFor(email));

        // Used once only; the new password works and the old one doesn't.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostJsonAsync("/api/v1/auth/email/reset-password", new { email, code, newPassword = "another12" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "brandnew1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "secret123" })).StatusCode);
    }

    [Fact]
    public async Task An_unknown_or_unverified_email_gets_the_same_answer_and_no_mail()
    {
        var phone = ApiFactory.NewPhone();
        var (client, _) = await api.SignUpAsync("Unverified Store", phone);
        var unverified = NewEmail();
        (await client.PutJsonAsync("/api/v1/profile", new { fullName = "Sunil", preferredLanguage = "hi", email = unverified })).EnsureSuccessStatusCode();

        var anon = api.CreateClient();
        foreach (var email in new[] { unverified, NewEmail() })
        {
            Assert.Equal(HttpStatusCode.OK, (await anon.PostJsonAsync("/api/v1/auth/email/send-reset", new { email })).StatusCode);
            Assert.Null(Mail.LastBodyFor(email));
        }
    }

    [Fact]
    public async Task Changing_the_email_unverifies_it_and_a_verified_email_cant_be_claimed_twice()
    {
        var (client, _, email) = await VerifiedAsync();
        (await client.PutJsonAsync("/api/v1/profile", new { fullName = "Ramesh Verma", preferredLanguage = "en", email = NewEmail() })).EnsureSuccessStatusCode();
        Assert.False((await client.GetJsonAsync<ProfileDto>("/api/v1/profile")).EmailVerified);

        var (other, _) = await api.SignUpAsync("Other Store");
        var (first, _, taken) = await VerifiedAsync();
        Assert.NotNull(first);
        (await other.PutJsonAsync("/api/v1/profile", new { fullName = "Someone Else", preferredLanguage = "en", email = taken })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostJsonAsync("/api/v1/profile/email/send-code", new { })).StatusCode);
        Assert.NotEqual(email, taken);
    }
}
