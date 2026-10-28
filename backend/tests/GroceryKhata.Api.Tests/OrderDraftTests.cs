using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Planner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>AI phase 1: typed notes become draft lines the household checks. Tests run the rule reader (no AI calls).</summary>
[Collection(ApiCollection.Name)]
public sealed class OrderDraftTests(ApiFactory api)
{
    [Theory]
    [InlineData("do kilo aata", "Atta", 2, Unit.Kg)]
    [InlineData("1 litre sarson ka tel", "Mustard oil", 1, Unit.Litre)]
    [InlineData("ek paav jeera", "Jeera", 250, Unit.G)]
    [InlineData("aadha kilo cheeni", "Sugar", 0.5, Unit.Kg)]
    [InlineData("dhai sau gram haldi", "Turmeric powder", 250, Unit.G)]
    [InlineData("Parle-G ke 5 packet", "Biscuits", 5, Unit.Packet)]
    [InlineData("2kg chawal", "Rice", 2, Unit.Kg)]
    public async Task Hindi_and_Hinglish_quantities_and_names_are_read(string said, string item, decimal quantity, Unit unit)
    {
        var line = Assert.Single(await new RuleOrderReader().ReadAsync(new OrderInput(said, null, null), default));

        Assert.Equal(CommonCatalogue.Items.Single(i => i.NameEn == item).Id, line.CatalogueItemId);
        Assert.Equal((quantity, unit, ReadConfidence.High), (line.Quantity!.Value, line.Unit!.Value, line.Confidence));
    }

    [Fact]
    public async Task A_note_becomes_checked_draft_lines_and_the_proposal_is_kept()
    {
        var home = await RegisterAsync();

        var draft = await (await home.PostJsonAsync("/api/v1/me/orders/draft", new
        {
            text = "do kilo aata, ek litre sarson tel aur 6 ande\nkuch meetha bhi",
        })).ReadAsync<DraftDto>();

        Assert.Equal("rules", draft.Reader);
        Assert.Equal(4, draft.Lines.Count);
        Assert.Equal(["आटा", "सरसों तेल", "अंडे"], draft.Lines.Take(3).Select(l => l.Name));
        Assert.Equal((6m, Unit.Piece), (draft.Lines[2].Quantity!.Value, draft.Lines[2].Unit!.Value));
        var unclear = draft.Lines[3];
        Assert.Equal((null, ReadConfidence.Low), (unclear.CatalogueItemId, unclear.Confidence)); // "something sweet": the household decides
    }

    [Fact]
    public async Task Photos_need_the_ai_and_empty_or_bad_input_is_refused()
    {
        var home = await RegisterAsync();
        var photo = "data:image/png;base64," + Convert.ToBase64String([137, 80, 78, 71, 1, 2, 3]);

        Assert.Equal(HttpStatusCode.BadRequest, (await home.PostJsonAsync("/api/v1/me/orders/draft", new { imageDataUrl = photo })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await home.PostJsonAsync("/api/v1/me/orders/draft", new { text = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await home.PostJsonAsync("/api/v1/me/orders/draft", new { text = new string('a', 2001) })).StatusCode);
    }

    [Fact]
    public async Task Nothing_is_read_before_consent_and_withdrawing_erases_the_ai_records()
    {
        var home = await RegisterAsync(consent: false);
        var status = await home.GetJsonAsync<AiStatusDto>("/api/v1/me/ai");
        Assert.Equal((null, false, false), (status.ConsentedAt, status.CanReadPhotos, status.CanHearVoice));
        Assert.Equal(HttpStatusCode.Forbidden, (await home.PostJsonAsync("/api/v1/me/orders/draft", new { text = "do kilo aata" })).StatusCode);

        await home.PostJsonAsync("/api/v1/me/ai/consent", new { agree = true });
        Assert.NotNull((await home.GetJsonAsync<AiStatusDto>("/api/v1/me/ai")).ConsentedAt);
        Assert.Equal(HttpStatusCode.OK, (await home.PostJsonAsync("/api/v1/me/orders/draft", new { text = "do kilo aata" })).StatusCode);

        await home.PostJsonAsync("/api/v1/me/ai/consent", new { agree = false });
        Assert.Null((await home.GetJsonAsync<AiStatusDto>("/api/v1/me/ai")).ConsentedAt);
        Assert.Equal(HttpStatusCode.Forbidden, (await home.PostJsonAsync("/api/v1/me/orders/draft", new { text = "do kilo aata" })).StatusCode);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var me = await home.GetJsonAsync<MeResponse>("/api/v1/auth/me");
        Assert.False(await db.AiProposals.AnyAsync(p => p.UserId == me.User.Id));
    }

    [Fact]
    public async Task Voice_needs_speech_to_text_switched_on()
    {
        var home = await RegisterAsync();
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent([1, 2, 3, 4]);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        form.Add(audio, "audio", "order.webm");

        var response = await home.PostAsync("/api/v1/me/orders/transcribe", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ai_records_older_than_the_keep_period_are_deleted()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var home = await RegisterAsync();
        var me = await home.GetJsonAsync<MeResponse>("/api/v1/auth/me");
        var now = DateTimeOffset.UtcNow;
        db.AiProposals.AddRange(
            new AiProposal { UserId = me.User.Id, Reader = "rules", Lines = "[]", CreatedAt = now.AddDays(-31) },
            new AiProposal { UserId = me.User.Id, Reader = "rules", Lines = "[]", CreatedAt = now.AddDays(-2) });
        await db.SaveChangesAsync();

        await AiPrivacy.PurgeAsync(db, now, 30, default);

        Assert.Equal(1, await db.AiProposals.CountAsync(p => p.UserId == me.User.Id));
    }

    [Fact]
    public void The_server_check_drops_made_up_ids_and_silly_quantities_to_low_confidence()
    {
        var madeUp = OrderDrafts.Check(new ReadLine("unicorn milk", 9999, "unicorn milk", 1, Unit.Litre, ReadConfidence.High, null))!;
        var silly = OrderDrafts.Check(new ReadLine("atta", 1, "आटा", 50000, Unit.Kg, ReadConfidence.High, null))!;

        Assert.Equal((null, ReadConfidence.Low), (madeUp.CatalogueItemId, madeUp.Confidence));
        Assert.Equal((null, ReadConfidence.Low), (silly.Quantity, silly.Confidence));
        Assert.Null(OrderDrafts.Check(new ReadLine("  ", null, "", null, null, ReadConfidence.Low, null)));
    }

    [Fact]
    public async Task A_signed_whatsapp_message_from_a_household_becomes_a_draft_waiting_in_their_app()
    {
        var phone = ApiFactory.NewPhone();
        var home = await RegisterAsync(phone: phone);
        var anyone = api.CreateClient();

        Assert.Equal("challenge-123", await anyone.GetStringAsync("/api/v1/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=test-verify-token&hub.challenge=challenge-123"));
        Assert.Equal(HttpStatusCode.Forbidden, (await anyone.GetAsync("/api/v1/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=x")).StatusCode);

        var payload = WhatsAppText("91" + phone, "do kilo aata aur ek litre sarson tel");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anyone.PostAsync("/api/v1/whatsapp/webhook", Signed(payload, "wrong-secret"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anyone.PostAsync("/api/v1/whatsapp/webhook", Signed(payload, "test-app-secret"))).StatusCode);
        // A stranger's number makes no draft for anyone.
        await anyone.PostAsync("/api/v1/whatsapp/webhook", Signed(WhatsAppText("919999999999", "atta"), "test-app-secret"));

        var pending = Assert.Single(await home.GetJsonAsync<List<PendingDraftDto>>("/api/v1/me/drafts"));
        Assert.Equal(["आटा", "सरसों तेल"], pending.Lines.Select(l => l.Name));

        await home.PostJsonAsync($"/api/v1/me/drafts/{pending.ProposalId}/done", new { });
        Assert.Empty(await home.GetJsonAsync<List<PendingDraftDto>>("/api/v1/me/drafts"));
    }

    [Fact]
    public async Task How_sure_the_ai_was_travels_with_the_item_to_the_shop()
    {
        var (shop, shopAuth) = await api.SignUpAsync("Gupta Kirana");
        var home = await RegisterAsync();
        await home.PutAsJsonAsync($"/api/v1/me/lists/weekly/items/{Guid.CreateVersion7()}",
            new { name = "kuch meetha", quantity = 1m, unit = "piece", note = "Wrote: kuch meetha", aiConfidence = "low" }, ApiFactory.Json);
        await home.PutAsJsonAsync($"/api/v1/me/lists/weekly/items/{Guid.CreateVersion7()}",
            new { name = "Toor dal", quantity = 1m, unit = "kg" }, ApiFactory.Json);

        await home.PostJsonAsync("/api/v1/me/orders", new { id = Guid.CreateVersion7(), shopId = shopAuth.ShopId, kind = "weekly" });

        var order = Assert.Single(await shop.GetJsonAsync<List<OrderDto>>("/api/v1/orders?status=placed"));
        Assert.Equal(ReadConfidence.Low, order.Lines.Single(l => l.Name == "kuch meetha").AiConfidence);
        Assert.Null(order.Lines.Single(l => l.Name != "kuch meetha").AiConfidence); // added by hand
    }

    private static string WhatsAppText(string from, string body) => System.Text.Json.JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[]
        {
            new
            {
                id = "1",
                changes = new[]
                {
                    new
                    {
                        field = "messages",
                        value = new
                        {
                            messaging_product = "whatsapp",
                            metadata = new { display_phone_number = "15550000000", phone_number_id = "106540352242922" },
                            messages = new[] { new { from, id = "wamid.test", timestamp = "1749416383", type = "text", text = new { body } } },
                        },
                    },
                },
            },
        },
    });

    private static StringContent Signed(string payload, string secret)
    {
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var hash = System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret), System.Text.Encoding.UTF8.GetBytes(payload));
        content.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexStringLower(hash));
        return content;
    }

    private async Task<HttpClient> RegisterAsync(bool consent = true, string? phone = null)
    {
        var client = api.CreateClient();
        var auth = await (await client.PostJsonAsync("/api/v1/auth/register", new { fullName = "Priya", phone = phone ?? ApiFactory.NewPhone(), password = "secret123" })).ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        if (consent)
        {
            await client.PostJsonAsync("/api/v1/me/ai/consent", new { agree = true });
        }

        return client;
    }
}
