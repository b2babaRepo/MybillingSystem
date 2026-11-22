using System.Net.Http.Json;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// A shop's own WhatsApp Business number, connected through Meta's Embedded Signup. The access token Meta gives for it
/// is kept encrypted; it never leaves the server.
/// </summary>
public sealed class ShopWhatsApp : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The shop's WhatsApp Business Account.</summary>
    public required string WabaId { get; set; }

    public required string PhoneNumberId { get; set; }

    /// <summary>The number as people see it ("+91 98xxx xxx10").</summary>
    public string? DisplayNumber { get; set; }

    /// <summary>The business name Meta shows on messages.</summary>
    public string? VerifiedName { get; set; }

    /// <summary>The business token for this shop's account, encrypted with the server's data-protection keys.</summary>
    public required string AccessToken { get; set; }

    public DateTimeOffset ConnectedAt { get; set; }

    public DateTimeOffset? TestSentAt { get; set; }
}

/// <summary>
/// Which shop a WhatsApp number belongs to, for the webhook (which arrives with no shop signed in). Ids only, no
/// secrets, so it needs no row-level security.
/// </summary>
public sealed class WhatsAppNumber
{
    public required string PhoneNumberId { get; set; }

    public Guid ShopId { get; set; }
}

/// <summary>What the browser needs to open Meta's Embedded Signup. Null fields: not set up on this server.</summary>
public sealed record WhatsAppSignupConfig(bool Enabled, string? AppId, string? ConfigId, string GraphVersion);

/// <summary>What the Embedded Signup popup hands back: a one-time code, and the account and number the shop chose.</summary>
public sealed record WhatsAppConnectRequest(string? Code, string? WabaId, string? PhoneNumberId);

public sealed record WhatsAppConnectionDto(bool Connected, string? DisplayNumber, string? VerifiedName, string? Quality, DateTimeOffset? ConnectedAt, DateTimeOffset? TestSentAt);

public sealed record WhatsAppTestRequest(string? To);

/// <summary>A number's details from Meta.</summary>
public sealed record WhatsAppPhoneInfo(string? DisplayNumber, string? VerifiedName, string? Quality);

/// <summary>The Graph API calls Embedded Signup needs. Faked in tests.</summary>
public interface IMetaSignup
{
    /// <summary>The one-time code from the popup, exchanged for the shop's business token.</summary>
    Task<string> ExchangeCodeAsync(string code, CancellationToken ct);

    /// <summary>Lets Apna Kaarobar's app receive the account's messages (webhook) and act for it.</summary>
    Task SubscribeAppAsync(string wabaId, string token, CancellationToken ct);

    /// <summary>Puts the number on the Cloud API, with a two-step verification PIN of the server's choosing.</summary>
    Task RegisterPhoneAsync(string phoneNumberId, string pin, string token, CancellationToken ct);

    Task<WhatsAppPhoneInfo> PhoneAsync(string phoneNumberId, string token, CancellationToken ct);

    /// <summary>
    /// Sends one approved template: <paramref name="parameters"/> fill its body's {{1}}, {{2}}...; <paramref name="buttonSuffix"/>
    /// ends its link button's address (null: no button). Returns Meta's message id.
    /// </summary>
    Task<string> SendTemplateAsync(string phoneNumberId, string token, string to, string template, string language, IReadOnlyList<string> parameters, string? buttonSuffix, CancellationToken ct);

    /// <summary>A free-text message: allowed only within 24 hours of the customer's last message. Returns Meta's message id.</summary>
    Task<string> SendTextAsync(string phoneNumberId, string token, string to, string text, CancellationToken ct);

    /// <summary>The account's templates and Meta's verdict on each (APPROVED, PENDING, REJECTED...).</summary>
    Task<IReadOnlyList<WaTemplateStatus>> TemplatesAsync(string wabaId, string token, CancellationToken ct);

    /// <summary>Asks Meta to approve a template; its button links to <paramref name="buttonBase"/> plus a part given when sending.</summary>
    Task CreateTemplateAsync(string wabaId, string token, WaTemplateDef template, string buttonBase, CancellationToken ct);
}

public sealed class GraphMetaSignup(HttpClient http, string appId, string appSecret, string version) : IMetaSignup
{
    private string Url(string path) => $"https://graph.facebook.com/{version}/{path}";

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() : null;
            throw new HttpRequestException($"Meta said: {message ?? response.StatusCode.ToString()}");
        }

        return doc;
    }

    private async Task<JsonDocument> PostAsync(string path, object body, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url(path)) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new("Bearer", token);
        return await ReadAsync(await http.SendAsync(request, ct), ct);
    }

    public async Task<string> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        var url = Url($"oauth/access_token?client_id={Uri.EscapeDataString(appId)}&client_secret={Uri.EscapeDataString(appSecret)}&code={Uri.EscapeDataString(code)}");
        using var doc = await ReadAsync(await http.GetAsync(url, ct), ct);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    public async Task SubscribeAppAsync(string wabaId, string token, CancellationToken ct) =>
        (await PostAsync($"{Uri.EscapeDataString(wabaId)}/subscribed_apps", new { }, token, ct)).Dispose();

    public async Task RegisterPhoneAsync(string phoneNumberId, string pin, string token, CancellationToken ct) =>
        (await PostAsync($"{Uri.EscapeDataString(phoneNumberId)}/register", new { messaging_product = "whatsapp", pin }, token, ct)).Dispose();

    public async Task<WhatsAppPhoneInfo> PhoneAsync(string phoneNumberId, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url($"{Uri.EscapeDataString(phoneNumberId)}?fields=display_phone_number,verified_name,quality_rating"));
        request.Headers.Authorization = new("Bearer", token);
        using var doc = await ReadAsync(await http.SendAsync(request, ct), ct);
        string? Text(string field) => doc.RootElement.TryGetProperty(field, out var v) ? v.GetString() : null;
        return new WhatsAppPhoneInfo(Text("display_phone_number"), Text("verified_name"), Text("quality_rating"));
    }

    private static string MessageId(JsonDocument doc) => doc.RootElement.GetProperty("messages")[0].GetProperty("id").GetString()!;

    public async Task<string> SendTemplateAsync(string phoneNumberId, string token, string to, string template, string language, IReadOnlyList<string> parameters, string? buttonSuffix, CancellationToken ct)
    {
        var components = new List<object>();
        if (parameters.Count > 0) components.Add(new { type = "body", parameters = parameters.Select(p => new { type = "text", text = p }).ToArray() });
        if (buttonSuffix is not null) components.Add(new { type = "button", sub_type = "url", index = "0", parameters = new[] { new { type = "text", text = buttonSuffix } } });
        using var doc = await PostAsync($"{Uri.EscapeDataString(phoneNumberId)}/messages",
            new { messaging_product = "whatsapp", to, type = "template", template = new { name = template, language = new { code = language }, components } }, token, ct);
        return MessageId(doc);
    }

    public async Task<string> SendTextAsync(string phoneNumberId, string token, string to, string text, CancellationToken ct)
    {
        using var doc = await PostAsync($"{Uri.EscapeDataString(phoneNumberId)}/messages", new { messaging_product = "whatsapp", to, type = "text", text = new { body = text } }, token, ct);
        return MessageId(doc);
    }

    public async Task<IReadOnlyList<WaTemplateStatus>> TemplatesAsync(string wabaId, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url($"{Uri.EscapeDataString(wabaId)}/message_templates?fields=name,status,rejected_reason&limit=100"));
        request.Headers.Authorization = new("Bearer", token);
        using var doc = await ReadAsync(await http.SendAsync(request, ct), ct);
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(t => new WaTemplateStatus("", t.GetProperty("name").GetString()!, t.GetProperty("status").GetString() ?? "", t.TryGetProperty("rejected_reason", out var r) ? r.GetString() : null))
            .ToList();
    }

    public async Task CreateTemplateAsync(string wabaId, string token, WaTemplateDef template, string buttonBase, CancellationToken ct)
    {
        var components = new List<object> { new { type = "BODY", text = template.Body, example = new { body_text = new[] { template.Examples } } } };
        if (template.ButtonText is not null)
        {
            components.Add(new { type = "BUTTONS", buttons = new[] { new { type = "URL", text = template.ButtonText, url = buttonBase + "{{1}}", example = new[] { buttonBase + "b/sharma-kirana/0193f1a2?t=abc" } } } });
        }

        (await PostAsync($"{Uri.EscapeDataString(wabaId)}/message_templates", new { name = template.Name, language = "en", category = "UTILITY", components }, token, ct)).Dispose();
    }
}

/// <summary>
/// Embedded Signup: the shop connects its own WhatsApp Business number from Settings, so messages to its customers go
/// from its own number through Meta's official Cloud API, never through a linked WhatsApp Web device. Needs a Meta app
/// set up as a Tech Provider: Whatsapp:AppId, Whatsapp:AppSecret and Whatsapp:EmbeddedSignupConfigId.
/// </summary>
public static class WhatsAppSignup
{
    private const string Purpose = "WhatsApp.ShopToken";

    public static IServiceCollection AddWhatsAppSignup(this IServiceCollection services, IConfiguration configuration)
    {
        // The keys that encrypt shops' tokens must outlive a restart or a new container, or every shop would have to
        // connect again: DataProtection:KeysPath puts them on a kept disk (a mounted volume in production).
        var keys = services.AddDataProtection().SetApplicationName("GroceryKhata");
        if (configuration["DataProtection:KeysPath"] is { Length: > 0 } path)
        {
            keys.PersistKeysToFileSystem(new DirectoryInfo(path));
        }
        var appId = configuration["Whatsapp:AppId"];
        var secret = configuration["Whatsapp:AppSecret"];
        if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(secret))
        {
            var version = configuration["Whatsapp:GraphVersion"] ?? "v23.0";
            services.AddHttpClient("meta-signup", http => http.Timeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<IMetaSignup>(sp => new GraphMetaSignup(sp.GetRequiredService<IHttpClientFactory>().CreateClient("meta-signup"), appId, secret, version));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapWhatsAppSignup(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/whatsapp/business").WithTags("WhatsApp").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/config", (IConfiguration c, IServiceProvider s) => TypedResults.Ok(Config(c, s)));
        group.MapGet("", GetAsync);
        group.MapPost("/connect", ConnectAsync);
        group.MapPost("/test", TestAsync);
        group.MapDelete("", DisconnectAsync);
        return app;
    }

    private static WhatsAppSignupConfig Config(IConfiguration configuration, IServiceProvider services)
    {
        var configId = configuration["Whatsapp:EmbeddedSignupConfigId"];
        var on = services.GetService<IMetaSignup>() is not null && !string.IsNullOrWhiteSpace(configId);
        return new WhatsAppSignupConfig(on, on ? configuration["Whatsapp:AppId"] : null, on ? configId : null, configuration["Whatsapp:GraphVersion"] ?? "v23.0");
    }

    /// <summary>True when the webhook's number is a shop's own: its messages are the shop's, not household orders.</summary>
    public static Task<bool> IsShopNumberAsync(AppDbContext db, string phoneNumberId, CancellationToken ct) =>
        db.WhatsAppNumbers.AsNoTracking().AnyAsync(n => n.PhoneNumberId == phoneNumberId, ct);

    private static async Task<IResult> GetAsync(AppDbContext db, IServiceProvider services, IDataProtectionProvider protection, CancellationToken ct)
    {
        var row = await db.ShopWhatsApps.AsNoTracking().SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return TypedResults.Ok(new WhatsAppConnectionDto(false, null, null, null, null, null));
        }

        // The quality rating changes with how customers react: fetched live, and left out if Meta can't be reached.
        string? quality = null;
        if (services.GetService<IMetaSignup>() is { } meta)
        {
            try { quality = (await meta.PhoneAsync(row.PhoneNumberId, protection.CreateProtector(Purpose).Unprotect(row.AccessToken), ct)).Quality; }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Security.Cryptography.CryptographicException) { }
        }

        return TypedResults.Ok(new WhatsAppConnectionDto(true, row.DisplayNumber, row.VerifiedName, quality, row.ConnectedAt, row.TestSentAt));
    }

    private static async Task<IResult> ConnectAsync(
        WhatsAppConnectRequest request, AppDbContext db, ITenantContext tenant, IServiceProvider services, IDataProtectionProvider protection,
        TimeProvider clock, ILogger<ShopWhatsApp> logger, CancellationToken ct)
    {
        if (services.GetService<IMetaSignup>() is not { } meta)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "WhatsApp Business connection isn't set up on this server.");
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.WabaId) || string.IsNullOrWhiteSpace(request.PhoneNumberId)
            || !request.WabaId.All(char.IsAsciiDigit) || !request.PhoneNumberId.All(char.IsAsciiDigit))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Code"] = ["Finish the steps in the WhatsApp window, then try again."] });
        }

        // Another shop already uses this number: a number belongs to one shop.
        if (await db.WhatsAppNumbers.AnyAsync(n => n.PhoneNumberId == request.PhoneNumberId && n.ShopId != tenant.ShopId, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This WhatsApp number is connected to another shop.");
        }

        string token;
        WhatsAppPhoneInfo info;
        try
        {
            token = await meta.ExchangeCodeAsync(request.Code, ct);
            await meta.SubscribeAppAsync(request.WabaId, token, ct);
            // A random two-step PIN the shop never needs: it only guards the number's registration.
            await meta.RegisterPhoneAsync(request.PhoneNumberId, Random.Shared.Next(100000, 999999).ToString(System.Globalization.CultureInfo.InvariantCulture), token, ct);
            info = await meta.PhoneAsync(request.PhoneNumberId, token, ct);
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning(e, "WhatsApp Embedded Signup failed for shop {ShopId}", tenant.ShopId);
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "WhatsApp could not finish connecting the number.", detail: e.Message);
        }

        var row = await db.ShopWhatsApps.SingleOrDefaultAsync(ct);
        if (row is not null)
        {
            db.WhatsAppNumbers.RemoveRange(db.WhatsAppNumbers.Where(n => n.ShopId == tenant.ShopId));
            db.ShopWhatsApps.Remove(row);
        }

        db.ShopWhatsApps.Add(new ShopWhatsApp
        {
            WabaId = request.WabaId, PhoneNumberId = request.PhoneNumberId, DisplayNumber = info.DisplayNumber, VerifiedName = info.VerifiedName,
            AccessToken = protection.CreateProtector(Purpose).Protect(token), ConnectedAt = clock.GetUtcNow(),
        });
        db.WhatsAppNumbers.Add(new WhatsAppNumber { PhoneNumberId = request.PhoneNumberId, ShopId = tenant.ShopId });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Shop {ShopId} connected WhatsApp number {Number}", tenant.ShopId, info.DisplayNumber);
        return TypedResults.Ok(new WhatsAppConnectionDto(true, info.DisplayNumber, info.VerifiedName, info.Quality, clock.GetUtcNow(), null));
    }

    /// <summary>Meta's own "hello_world" template to the owner's phone: proof that the number sends.</summary>
    private static async Task<IResult> TestAsync(
        WhatsAppTestRequest request, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IServiceProvider services, IDataProtectionProvider protection,
        TimeProvider clock, CancellationToken ct)
    {
        var row = await db.ShopWhatsApps.SingleOrDefaultAsync(ct);
        if (row is null || services.GetService<IMetaSignup>() is not { } meta)
        {
            return TypedResults.NotFound();
        }

        var to = PhoneNumber.Normalize(request.To) ?? (user.UserId() is { } id ? await db.Users.Where(u => u.Id == id).Select(u => u.Phone).SingleOrDefaultAsync(ct) : null);
        if (to is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["To"] = ["Enter a 10-digit mobile number."] });
        }

        try
        {
            await meta.SendTemplateAsync(row.PhoneNumberId, protection.CreateProtector(Purpose).Unprotect(row.AccessToken), to.TrimStart('+'), "hello_world", "en_US", [], null, ct);
        }
        catch (HttpRequestException e)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "WhatsApp did not send the test message.", detail: e.Message);
        }

        row.TestSentAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Forgets the number here. The shop can also remove Apna Kaarobar in its Meta Business settings.</summary>
    private static async Task<IResult> DisconnectAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var row = await db.ShopWhatsApps.SingleOrDefaultAsync(ct);
        if (row is not null)
        {
            db.ShopWhatsApps.Remove(row);
        }

        db.WhatsAppNumbers.RemoveRange(db.WhatsAppNumbers.Where(n => n.ShopId == tenant.ShopId));
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
