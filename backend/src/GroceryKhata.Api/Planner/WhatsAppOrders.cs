using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>Talks to the WhatsApp Business Platform (Cloud API): fetch a customer's media, send a reply.</summary>
public interface IWhatsAppClient
{
    Task<(byte[] Data, string MediaType)> DownloadMediaAsync(string mediaId, CancellationToken ct);

    Task SendTextAsync(string phoneNumberId, string to, string body, CancellationToken ct);

    /// <summary>
    /// A message the business starts (outside a customer's 24-hour reply window) must use a template Meta approved.
    /// <paramref name="parameters"/> fill the template body's {{1}}, {{2}}… in order.
    /// </summary>
    Task SendTemplateAsync(string phoneNumberId, string to, string template, string language, IReadOnlyList<string> parameters, CancellationToken ct);
}

/// <summary>Graph API calls with the business's access token. The version is a setting (Whatsapp:GraphVersion).</summary>
public sealed class GraphWhatsAppClient(HttpClient http, string token, string version) : IWhatsAppClient
{
    public async Task<(byte[] Data, string MediaType)> DownloadMediaAsync(string mediaId, CancellationToken ct)
    {
        // Two steps: the media id gives a short-lived URL, which is fetched with the same token.
        using var info = new HttpRequestMessage(HttpMethod.Get, $"https://graph.facebook.com/{version}/{Uri.EscapeDataString(mediaId)}");
        info.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var infoResponse = await http.SendAsync(info, ct);
        infoResponse.EnsureSuccessStatusCode();
        using var meta = await JsonDocument.ParseAsync(await infoResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var url = meta.RootElement.GetProperty("url").GetString()!;
        var type = meta.RootElement.TryGetProperty("mime_type", out var m) ? m.GetString() ?? "" : "";

        using var file = new HttpRequestMessage(HttpMethod.Get, url);
        file.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var fileResponse = await http.SendAsync(file, ct);
        fileResponse.EnsureSuccessStatusCode();
        return (await fileResponse.Content.ReadAsByteArrayAsync(ct), type);
    }

    public async Task SendTemplateAsync(string phoneNumberId, string to, string template, string language, IReadOnlyList<string> parameters, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{version}/{Uri.EscapeDataString(phoneNumberId)}/messages")
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp", to, type = "template",
                template = new
                {
                    name = template, language = new { code = language },
                    components = new[] { new { type = "body", parameters = parameters.Select(p => new { type = "text", text = p }).ToArray() } },
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendTextAsync(string phoneNumberId, string to, string body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{version}/{Uri.EscapeDataString(phoneNumberId)}/messages")
        {
            Content = JsonContent.Create(new { messaging_product = "whatsapp", to, type = "text", text = new { body } }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// AI phase 3: households send their list to the Grocery Khata WhatsApp number as text, a photo or a voice note.
/// Each message is read like an in-app order and lands as a draft in their app ("From WhatsApp") for them to check;
/// nothing is added or sent to a shop until they do. A short reply says what was understood. Only people who have
/// an account with that phone and have agreed to AI reading are served; anyone else gets a pointer to the app.
/// Media is downloaded, read and dropped; only the read text and lines are kept, like any AI record.
/// </summary>
public static class WhatsAppOrders
{
    public static IServiceCollection AddWhatsAppOrders(this IServiceCollection services, IConfiguration configuration)
    {
        var token = configuration["Whatsapp:AccessToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            var version = configuration["Whatsapp:GraphVersion"] ?? "v23.0";
            services.AddHttpClient("whatsapp", http => http.Timeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<IWhatsAppClient>(sp => new GraphWhatsAppClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("whatsapp"), token, version));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapWhatsAppOrders(this IEndpointRouteBuilder app)
    {
        var hook = app.MapGroup("/api/v1/whatsapp/webhook").WithTags("WhatsApp").AllowAnonymous().DisableRateLimiting();
        hook.MapGet("", Verify);
        hook.MapPost("", ReceiveAsync);
        return app;
    }

    /// <summary>Meta's one-time check when the webhook is set up: echo the challenge if the verify token matches.</summary>
    private static IResult Verify(HttpRequest request, IConfiguration configuration)
    {
        var expected = configuration["Whatsapp:VerifyToken"];
        return !string.IsNullOrEmpty(expected) && request.Query["hub.mode"] == "subscribe" && request.Query["hub.verify_token"] == expected
            ? TypedResults.Text(request.Query["hub.challenge"].ToString())
            : TypedResults.StatusCode(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Incoming messages. The body must carry Meta's signature (HMAC-SHA256 with the app secret): anything else is
    /// refused, so no one can post fake orders here. Answers 200 quickly; a failure on one message doesn't fail the rest.
    /// </summary>
    private static async Task<IResult> ReceiveAsync(
        HttpRequest request, IConfiguration configuration, IServiceProvider services, AppDbContext db, IOrderReader reader,
        TimeProvider clock, ILogger<AiProposal> logger, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();
        if (!SignatureMatches(body, request.Headers["X-Hub-Signature-256"], configuration["Whatsapp:AppSecret"]))
        {
            return TypedResults.StatusCode(StatusCodes.Status401Unauthorized);
        }

        using var doc = JsonDocument.Parse(body);
        var whatsapp = services.GetService<IWhatsAppClient>();
        var speech = services.GetService<ISpeechReader>();
        foreach (var (phoneNumberId, message) in Messages(doc.RootElement))
        {
            try
            {
                // A message to a shop's own connected number is between that shop and its customer, not a household order.
                if (await WhatsAppSignup.IsShopNumberAsync(db, phoneNumberId, ct))
                {
                    continue;
                }

                // Taken once: a resent message changes no row and is skipped.
                var messageId = message.TryGetProperty("id", out var id) ? id.GetString() : null;
                var now = clock.GetUtcNow();
                if (messageId is not null && await db.Database.ExecuteSqlAsync($"INSERT INTO seen_messages (message_id, seen_at) VALUES ({messageId}, {now}) ON CONFLICT DO NOTHING", ct) == 0)
                {
                    continue;
                }

                // A farmer of a milk centre asking for their figures is answered first; anyone else goes to the list reader.
                var reply = await Ledger.DairyBot.TryReplyAsync(message, db, services.GetRequiredService<IServiceScopeFactory>(), clock, ct)
                    ?? await HandleAsync(message, db, reader, whatsapp, speech, clock, ct);
                if (reply is not null && whatsapp is not null)
                {
                    await whatsapp.SendTextAsync(phoneNumberId, message.GetProperty("from").GetString()!, reply, ct);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "WhatsApp message could not be handled");
            }
        }

        return TypedResults.Ok();
    }

    /// <summary>Reads one message into a draft for its sender. Returns the reply to send, or null for nothing.</summary>
    internal static async Task<string?> HandleAsync(
        JsonElement message, AppDbContext db, IOrderReader reader, IWhatsAppClient? whatsapp, ISpeechReader? speech, TimeProvider clock, CancellationToken ct)
    {
        var phone = PhoneNumber.Normalize("+" + message.GetProperty("from").GetString());
        var person = await db.Users.AsNoTracking().Where(u => u.Phone == phone && u.IsActive).Select(u => new { u.Id, u.AiConsentAt }).FirstOrDefaultAsync(ct);
        if (person is null)
        {
            return "Namaste! Grocery Khata app mein isi number se khata banayein, phir yahan apni list bhejein. 🙏";
        }

        if (person.AiConsentAt is null)
        {
            return "Namaste! App mein \"Likhein, bolein ya photo lein\" kholkar AI se list padhwane ki anumati dein, phir yahan list bhejein.";
        }

        string? text = null;
        string? imageType = null;
        string? imageBase64 = null;
        switch (message.GetProperty("type").GetString())
        {
            case "text":
                text = message.GetProperty("text").GetProperty("body").GetString();
                break;
            case "image" when whatsapp is not null && reader.CanReadImages:
                var (picture, pictureType) = await whatsapp.DownloadMediaAsync(message.GetProperty("image").GetProperty("id").GetString()!, ct);
                (imageType, imageBase64) = (pictureType.Split(';')[0], Convert.ToBase64String(picture));
                text = message.GetProperty("image").TryGetProperty("caption", out var caption) ? caption.GetString() : null;
                break;
            case "audio" when whatsapp is not null && speech is not null:
                var (audio, audioType) = await whatsapp.DownloadMediaAsync(message.GetProperty("audio").GetProperty("id").GetString()!, ct);
                using (var stream = new MemoryStream(audio))
                {
                    text = await speech.TranscribeAsync(stream, audioType.Split(';')[0], ct);
                }

                break;
            default:
                return "Abhi hum likhi hui list padh sakte hain. Kripya list type karke bhejein.";
        }

        if (string.IsNullOrWhiteSpace(text) && imageBase64 is null)
        {
            return "Kuch samajh nahi aaya. Kripya list dobara bhejein.";
        }

        var lines = (await reader.ReadAsync(new OrderInput(text, imageType, imageBase64), ct)).Select(OrderDrafts.Check).OfType<DraftLineDto>().Take(60).ToList();
        db.AiProposals.Add(new AiProposal
        {
            UserId = person.Id,
            InputText = text?.Length > 2000 ? text[..2000] : text,
            HadImage = imageBase64 is not null,
            Reader = reader.Name,
            Source = ProposalSource.WhatsApp,
            Lines = JsonSerializer.Serialize(lines),
            CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        var sure = lines.Count(l => l.Confidence == ReadConfidence.High);
        return lines.Count == 0
            ? "Koi saaman samajh nahi aaya. Kripya alag tarah se likhkar bhejein."
            : $"Mil gaya! {lines.Count} saaman ({sure} pakke). Grocery Khata app mein \"WhatsApp se aaye\" dekhkar jaanch lein aur list mein jodein.";
    }

    private static IEnumerable<(string PhoneNumberId, JsonElement Message)> Messages(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries))
        {
            yield break;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            foreach (var change in entry.GetProperty("changes").EnumerateArray())
            {
                var value = change.GetProperty("value");
                if (!value.TryGetProperty("messages", out var messages))
                {
                    continue; // status updates (sent, delivered, read)
                }

                var phoneNumberId = value.GetProperty("metadata").GetProperty("phone_number_id").GetString()!;
                foreach (var message in messages.EnumerateArray())
                {
                    yield return (phoneNumberId, message);
                }
            }
        }
    }

    internal static bool SignatureMatches(byte[] body, string? header, string? appSecret)
    {
        if (string.IsNullOrEmpty(appSecret) || header is not { Length: > 7 } || !header.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(header[7..]));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
