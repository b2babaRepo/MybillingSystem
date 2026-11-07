using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>Turns a short voice recording into text. The audio is passed through, never stored.</summary>
public interface ISpeechReader
{
    Task<string> TranscribeAsync(Stream audio, string contentType, CancellationToken ct);
}

/// <summary>
/// Sarvam's speech-to-text (Saaras v3, "codemix" mode: Hindi in Devanagari with English words such as brands kept in
/// English; the language is detected). Built for Indian languages, processed in India, and Sarvam deletes the audio
/// after processing by default. REST is meant for clips under 30 seconds, which is why the app stops at 25.
/// </summary>
public sealed class SarvamSpeechReader(HttpClient http, string key) : ISpeechReader
{
    public async Task<string> TranscribeAsync(Stream audio, string contentType, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", contentType.Contains("ogg", StringComparison.Ordinal) ? "order.ogg" : "order.webm");
        form.Add(new StringContent("saaras:v3"), "model");
        form.Add(new StringContent("codemix"), "mode");
        form.Add(new StringContent("unknown"), "language_code");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sarvam.ai/speech-to-text") { Content = form };
        request.Headers.Add("api-subscription-key", key);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return body.RootElement.TryGetProperty("transcript", out var transcript) ? transcript.GetString() ?? "" : "";
    }
}

public sealed record AiStatusDto(DateTimeOffset? ConsentedAt, bool CanReadPhotos, bool CanHearVoice, int KeepDays);

public sealed record AiConsentRequest(bool Agree);

public sealed record TranscriptDto(string Transcript);

/// <summary>
/// AI phase 2: voice orders, consent kept on the server, and AI records deleted on time. Consent is required before
/// any note, photo or voice is read; withdrawing erases the person's AI records at once; everything else goes after
/// <c>Ai:KeepDays</c> (30 by default). Photos and audio are never stored at all.
/// </summary>
public static class AiPrivacy
{
    private const int MaxAudioBytes = 1_000_000; // ~25 s of Opus voice is well under this

    public static IServiceCollection AddAiPrivacy(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["Ai:SarvamKey"] ?? Environment.GetEnvironmentVariable("SARVAM_API_KEY");
        if (configuration.GetValue("Ai:Enabled", true) && !string.IsNullOrWhiteSpace(key))
        {
            services.AddHttpClient("sarvam", http => http.Timeout = TimeSpan.FromSeconds(40));
            services.AddSingleton<ISpeechReader>(sp => new SarvamSpeechReader(sp.GetRequiredService<IHttpClientFactory>().CreateClient("sarvam"), key));
        }

        services.AddHostedService<AiRecordCleanup>();
        return services;
    }

    public static int KeepDays(IConfiguration configuration) => Math.Max(1, configuration.GetValue("Ai:KeepDays", 30));

    public static IEndpointRouteBuilder MapAiPrivacy(this IEndpointRouteBuilder app)
    {
        // Helpers on a PIN read bill photos too, so this one isn't personal enough to refuse them.
        var me = app.MapGroup("/api/v1/me").WithTags("Household planner").RequireAuthorization(AppPolicies.AnySession);
        me.MapGet("/ai", StatusAsync);
        me.MapPost("/ai/consent", ConsentAsync);
        // Up to 1 MB of audio plus the form's own bytes.
        me.MapPost("/orders/transcribe", TranscribeAsync).DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1_200_000));
        return app;
    }

    /// <summary>Whether the person has agreed, and what is switched on: photos need the AI, voice needs speech-to-text.</summary>
    private static async Task<AiStatusDto> StatusAsync(
        ClaimsPrincipal user, AppDbContext db, IOrderReader reader, IServiceProvider services, IConfiguration configuration, CancellationToken ct)
    {
        var consented = await db.Users.AsNoTracking().Where(u => u.Id == user.UserId()).Select(u => u.AiConsentAt).SingleAsync(ct);
        return new AiStatusDto(consented, reader.CanReadImages, services.GetService<ISpeechReader>() is not null, KeepDays(configuration));
    }

    /// <summary>Agree (records the time), or withdraw: as easy as agreeing, and it erases the person's AI records.</summary>
    private static async Task<IResult> ConsentAsync(
        AiConsentRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, ILogger<AiProposal> logger, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        if (request.Agree)
        {
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AiConsentAt, clock.GetUtcNow()), ct);
        }
        else
        {
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.AiConsentAt, (DateTimeOffset?)null), ct);
            var erased = await db.AiProposals.Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);
            logger.LogInformation("AI consent withdrawn by {UserId}; {Count} AI records erased", userId, erased);
        }

        return TypedResults.NoContent();
    }

    /// <summary>A short voice order to text, for the household to check in the note box before it is read into items.</summary>
    private static async Task<IResult> TranscribeAsync(
        HttpRequest http, ClaimsPrincipal user, AppDbContext db, IServiceProvider services, ILogger<AiProposal> logger, CancellationToken ct)
    {
        if (!await HasConsentedAsync(db, user, ct))
        {
            return NeedsConsent();
        }

        if (services.GetService<ISpeechReader>() is not { } speech)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Audio"] = ["Voice orders need speech-to-text switched on (a Sarvam key on the server). Type the list instead."] });
        }

        var form = http.HasFormContentType ? await http.ReadFormAsync(ct) : null;
        var audio = form?.Files.GetFile("audio");
        var type = audio?.ContentType.Split(';')[0].Trim().ToLowerInvariant();
        if (audio is null || audio.Length is 0 or > MaxAudioBytes || type is not ("audio/webm" or "audio/ogg" or "audio/mp4" or "audio/wav"))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Audio"] = ["Send a voice recording under 25 seconds."] });
        }

        try
        {
            await using var stream = audio.OpenReadStream();
            return TypedResults.Ok(new TranscriptDto((await speech.TranscribeAsync(stream, type, ct)).Trim()));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Voice transcription failed");
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Couldn't hear that right now. Try again, or type it.");
        }
    }

    internal static Task<bool> HasConsentedAsync(AppDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Id == user.UserId() && u.AiConsentAt != null, ct);

    internal static IResult NeedsConsent() =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Agree to AI reading first.", detail: "Open \"Write it or snap it\" to read what it does and agree.");

    /// <summary>Deletes AI records older than the keep period. Returns how many went.</summary>
    public static Task<int> PurgeAsync(AppDbContext db, DateTimeOffset now, int keepDays, CancellationToken ct) =>
        db.AiProposals.Where(p => p.CreatedAt < now.AddDays(-keepDays)).ExecuteDeleteAsync(ct);
}

/// <summary>Once a day: delete AI records past their keep period (DPDP: erase once the purpose is served).</summary>
public sealed class AiRecordCleanup(IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider clock, ILogger<AiRecordCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var removed = await AiPrivacy.PurgeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), clock.GetUtcNow(), AiPrivacy.KeepDays(configuration), stoppingToken);
                if (removed > 0)
                {
                    logger.LogInformation("Deleted {Count} AI records past their keep period", removed);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "AI record cleanup failed; it runs again tomorrow");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
