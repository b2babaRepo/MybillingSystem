using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Planner;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>A shop connects its own WhatsApp Business number through Embedded Signup; the token stays on the server.</summary>
[Collection(ApiCollection.Name)]
public sealed class WhatsAppSignupTests(ApiFactory api)
{
    private sealed class FakeMeta : IMetaSignup
    {
        public List<string> Calls { get; } = [];

        public Task<string> ExchangeCodeAsync(string code, CancellationToken ct)
        {
            Calls.Add($"exchange:{code}");
            return code == "bad" ? throw new HttpRequestException("Meta said: code expired") : Task.FromResult("SECRET-TOKEN-" + code);
        }

        public Task SubscribeAppAsync(string wabaId, string token, CancellationToken ct) { Calls.Add($"subscribe:{wabaId}:{token}"); return Task.CompletedTask; }

        public Task RegisterPhoneAsync(string phoneNumberId, string pin, string token, CancellationToken ct) { Calls.Add($"register:{phoneNumberId}"); return Task.CompletedTask; }

        public Task<WhatsAppPhoneInfo> PhoneAsync(string phoneNumberId, string token, CancellationToken ct) =>
            Task.FromResult(new WhatsAppPhoneInfo("+91 98765 43210", "Sharma Kirana", "GREEN"));

        public Task SendTemplateAsync(string phoneNumberId, string token, string to, string template, string language, CancellationToken ct)
        {
            Calls.Add($"send:{phoneNumberId}:{to}:{template}:{token}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_shop_connects_its_own_number_tests_it_and_disconnects_and_the_token_never_comes_back()
    {
        var meta = new FakeMeta();
        using var app = api.WithWebHostBuilder(b => b
            .UseSetting("Whatsapp:EmbeddedSignupConfigId", "cfg-1").UseSetting("Whatsapp:AppId", "app-1")
            .ConfigureTestServices(s => s.AddSingleton<IMetaSignup>(meta)));
        HttpClient Client(string token) { var c = app.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return c; }
        var phone = ApiFactory.NewPhone();
        var (_, auth) = await api.SignUpAsync("WhatsApp Business Store", phone);
        var shop = Client(auth.AccessToken);

        var config = await shop.GetJsonAsync<WhatsAppSignupConfig>("/api/v1/whatsapp/business/config");
        Assert.Equal((true, "app-1", "cfg-1"), (config.Enabled, config.AppId, config.ConfigId));
        Assert.False((await shop.GetJsonAsync<WhatsAppConnectionDto>("/api/v1/whatsapp/business")).Connected);

        // A failed exchange changes nothing; a good one connects.
        Assert.Equal(HttpStatusCode.BadGateway, (await shop.PostJsonAsync("/api/v1/whatsapp/business/connect", new { code = "bad", wabaId = "111", phoneNumberId = "222" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/whatsapp/business/connect", new { code = "c1", wabaId = "abc", phoneNumberId = "222" })).StatusCode);
        var connected = await (await shop.PostJsonAsync("/api/v1/whatsapp/business/connect", new { code = "c1", wabaId = "111", phoneNumberId = "222" })).Content.ReadAsStringAsync();
        Assert.Contains("+91 98765 43210", connected);
        Assert.DoesNotContain("SECRET-TOKEN", connected);
        Assert.Contains("subscribe:111:SECRET-TOKEN-c1", meta.Calls);
        Assert.Contains("register:222", meta.Calls);

        var status = await shop.GetStringAsync("/api/v1/whatsapp/business");
        Assert.Contains("Sharma Kirana", status);
        Assert.Contains("GREEN", status);
        Assert.DoesNotContain("SECRET-TOKEN", status);

        // The test goes from the shop's number to the owner's phone, with the token decrypted on the server only.
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync("/api/v1/whatsapp/business/test", new { })).StatusCode);
        Assert.Contains(meta.Calls, c => c.StartsWith("send:222:") && c.EndsWith(":hello_world:SECRET-TOKEN-c1") && c.Contains(phone));

        // Another shop can't take the same number.
        var (_, other) = await api.SignUpAsync("Other WhatsApp Store");
        Assert.Equal(HttpStatusCode.Conflict, (await Client(other.AccessToken).PostJsonAsync("/api/v1/whatsapp/business/connect", new { code = "c2", wabaId = "333", phoneNumberId = "222" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await shop.DeleteAsync("/api/v1/whatsapp/business")).StatusCode);
        Assert.False((await shop.GetJsonAsync<WhatsAppConnectionDto>("/api/v1/whatsapp/business")).Connected);
        // Once let go, the other shop may connect it.
        Assert.Equal(HttpStatusCode.OK, (await Client(other.AccessToken).PostJsonAsync("/api/v1/whatsapp/business/connect", new { code = "c2", wabaId = "333", phoneNumberId = "222" })).StatusCode);
    }

    [Fact]
    public async Task Without_the_Meta_app_set_up_the_connect_button_stays_off()
    {
        var (shop, _) = await api.SignUpAsync("No Meta Store");
        Assert.False((await shop.GetJsonAsync<WhatsAppSignupConfig>("/api/v1/whatsapp/business/config")).Enabled);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await shop.PostJsonAsync("/api/v1/whatsapp/business/connect", new { code = "c", wabaId = "1", phoneNumberId = "2" })).StatusCode);
    }
}
