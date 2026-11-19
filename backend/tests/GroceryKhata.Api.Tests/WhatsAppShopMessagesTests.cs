using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GroceryKhata.Api.Planner;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>Bills, reminders and statements from the shop's own number, delivery reports, and the inbox with Meta's 24-hour rule.</summary>
[Collection(ApiCollection.Name)]
public sealed class WhatsAppShopMessagesTests(ApiFactory api)
{
    private sealed class FakeMeta : IMetaSignup
    {
        public List<(string To, string Template, IReadOnlyList<string> Parameters, string? Button)> Templates { get; } = [];
        public List<(string To, string Text)> Texts { get; } = [];
        public List<string> Created { get; } = [];
        public bool FailNext { get; set; }
        private int n;

        public Task<string> ExchangeCodeAsync(string code, CancellationToken ct) => Task.FromResult("TOKEN");
        public Task SubscribeAppAsync(string wabaId, string token, CancellationToken ct) => Task.CompletedTask;
        public Task RegisterPhoneAsync(string phoneNumberId, string pin, string token, CancellationToken ct) => Task.CompletedTask;
        public Task<WhatsAppPhoneInfo> PhoneAsync(string phoneNumberId, string token, CancellationToken ct) => Task.FromResult(new WhatsAppPhoneInfo("+91 90000 11111", "Test Kirana", "GREEN"));

        public Task<string> SendTemplateAsync(string phoneNumberId, string token, string to, string template, string language, IReadOnlyList<string> parameters, string? buttonSuffix, CancellationToken ct)
        {
            if (FailNext) { FailNext = false; throw new HttpRequestException("Meta said: template not approved"); }
            Templates.Add((to, template, parameters, buttonSuffix));
            return Task.FromResult($"wamid.out{++n}");
        }

        public Task<string> SendTextAsync(string phoneNumberId, string token, string to, string text, CancellationToken ct)
        {
            Texts.Add((to, text));
            return Task.FromResult($"wamid.text{++n}");
        }

        public Task<IReadOnlyList<WaTemplateStatus>> TemplatesAsync(string wabaId, string token, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WaTemplateStatus>>(Created.Select(c => new WaTemplateStatus("", c, "PENDING", null)).ToList());

        public Task CreateTemplateAsync(string wabaId, string token, WaTemplateDef template, string buttonBase, CancellationToken ct)
        {
            Assert.Equal("https://app.example.in/#/", buttonBase);
            Created.Add(template.Name);
            return Task.CompletedTask;
        }
    }

    private static StringContent Signed(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var hash = System.Security.Cryptography.HMACSHA256.HashData("test-app-secret"u8.ToArray(), System.Text.Encoding.UTF8.GetBytes(json));
        content.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexStringLower(hash));
        return content;
    }

    private static object Webhook(string numberId, object value) => new { entry = new[] { new { changes = new[] { new { value } } } } };

    [Fact]
    public async Task The_shop_sends_from_its_own_number_sees_delivery_and_answers_customers_in_the_inbox()
    {
        var meta = new FakeMeta();
        using var app = api.WithWebHostBuilder(b => b
            .UseSetting("Whatsapp:EmbeddedSignupConfigId", "cfg").UseSetting("Whatsapp:AppId", "app").UseSetting("Whatsapp:PublicAppUrl", "https://app.example.in/")
            .ConfigureTestServices(s => s.AddSingleton<IMetaSignup>(meta)));
        var (_, auth) = await api.SignUpAsync("WA Inbox Store");
        var shop = app.CreateClient();
        shop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var numberId = Random.Shared.NextInt64(10_000_000, 99_999_999).ToString();

        // Not connected yet: nothing can be sent.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/whatsapp/business/send", new { kind = "reminder", customerId = Guid.NewGuid() })).StatusCode);
        (await shop.PostJsonAsync("/api/v1/whatsapp/business/connect", new { code = "c", wabaId = "777", phoneNumberId = numberId })).EnsureSuccessStatusCode();

        // Templates: the three are asked for once, and show as waiting for Meta.
        var templates = await (await shop.PostJsonAsync("/api/v1/whatsapp/business/templates", new { })).ReadAsync<WaTemplatesDto>();
        Assert.Equal(["gk_bill_v1", "gk_payment_reminder_v1", "gk_khata_statement_v1"], meta.Created);
        Assert.All(templates.Templates, x => Assert.Equal("PENDING", x.Status));
        await shop.PostJsonAsync("/api/v1/whatsapp/business/templates", new { });
        Assert.Equal(3, meta.Created.Count);

        // A customer with a bill on khata.
        var phone = ApiFactory.NewPhone();
        var customer = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Priya", phone })).EnsureSuccessStatusCode();
        var bill = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bills", new { id = bill, customerId = customer, lines = new[] { new { name = "Atta", quantity = 2m, unitPrice = 250m } }, paidAmount = 100m, paymentMode = "cash" })).EnsureSuccessStatusCode();
        var wa = "91" + phone;

        // The bill goes with its numbers filled in and a button to its own link.
        var sent = await (await shop.PostJsonAsync("/api/v1/whatsapp/business/send", new { kind = "bill", billId = bill })).ReadAsync<WaMessageDto>();
        var billMessage = Assert.Single(meta.Templates);
        Assert.Equal((wa, "gk_bill_v1"), (billMessage.To, billMessage.Template));
        Assert.Equal(["Priya", "500.00", "400.00"], billMessage.Parameters.Where((_, i) => i is 0 or 3 or 4));
        Assert.StartsWith($"b/", billMessage.Button);
        Assert.Contains($"/{bill}?t=", billMessage.Button);
        Assert.Contains("Your bill", sent.Body);

        // The reminder carries what is due and a khata link.
        (await shop.PostJsonAsync("/api/v1/whatsapp/business/send", new { kind = "reminder", customerId = customer })).EnsureSuccessStatusCode();
        Assert.Equal("400.00", meta.Templates[1].Parameters[1]);
        Assert.StartsWith($"k/", meta.Templates[1].Button);

        // Meta refuses one: it is logged as failed with the reason.
        meta.FailNext = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await shop.PostJsonAsync("/api/v1/whatsapp/business/send", new { kind = "statement", customerId = customer })).StatusCode);

        // No reply yet: a typed message is not allowed (no customer message in the last 24 hours).
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/whatsapp/inbox/{wa}/reply", new { text = "Hello" })).StatusCode);

        // Meta reports the bill delivered then read, and the customer writes back.
        var hook = app.CreateClient();
        (await hook.PostAsync("/api/v1/whatsapp/webhook", Signed(Webhook(numberId, new
        {
            messaging_product = "whatsapp", metadata = new { phone_number_id = numberId },
            statuses = new object[] { new { id = "wamid.out1", status = "read", recipient_id = wa }, new { id = "wamid.out1", status = "delivered", recipient_id = wa } },
        })))).EnsureSuccessStatusCode();
        var inbound = new { from = wa, id = "wamid.in1", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), type = "text", text = new { body = "Please send my last bill" } };
        var message = Webhook(numberId, new { messaging_product = "whatsapp", metadata = new { phone_number_id = numberId }, messages = new[] { inbound } });
        (await hook.PostAsync("/api/v1/whatsapp/webhook", Signed(message))).EnsureSuccessStatusCode();
        (await hook.PostAsync("/api/v1/whatsapp/webhook", Signed(message))).EnsureSuccessStatusCode(); // a resend is taken once

        var inbox = await shop.GetJsonAsync<List<WaConversationDto>>("/api/v1/whatsapp/inbox");
        var talk = Assert.Single(inbox);
        Assert.Equal((wa, customer, "Priya", 1, true), (talk.Phone, talk.CustomerId!.Value, talk.Name, talk.Waiting, talk.CanReplyFreely));

        var thread = await shop.GetJsonAsync<WaThreadDto>($"/api/v1/whatsapp/inbox/{wa}");
        Assert.Equal("read", thread.Messages.First(m => m.Kind == "bill").Status); // never stepped back to delivered
        Assert.Equal("failed", thread.Messages.Single(m => m.Kind == "statement").Status);
        Assert.Single(thread.Messages, m => m.Direction == WaDirection.In && m.Body == "Please send my last bill");

        // Now a typed reply goes, and "send last bill" sends the bill template again.
        (await shop.PostJsonAsync($"/api/v1/whatsapp/inbox/{wa}/reply", new { text = "Sure, sending it now" })).EnsureSuccessStatusCode();
        Assert.Equal((wa, "Sure, sending it now"), Assert.Single(meta.Texts));
        (await shop.PostJsonAsync($"/api/v1/whatsapp/inbox/{wa}/last-bill", new { })).EnsureSuccessStatusCode();
        Assert.Equal("gk_bill_v1", meta.Templates[^1].Template);
        Assert.Equal(0, (await shop.GetJsonAsync<List<WaConversationDto>>("/api/v1/whatsapp/inbox")).Single().Waiting);

        // Another shop sees none of it.
        var (other, _) = await api.SignUpAsync("Other WA Store");
        Assert.Empty(await other.GetJsonAsync<List<WaConversationDto>>("/api/v1/whatsapp/inbox"));
    }
}
