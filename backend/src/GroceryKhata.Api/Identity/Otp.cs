using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Identity;

/// <summary>
/// A one-time sign-in code sent by SMS. Stored only as a hash; good for <see cref="OtpOptions.ValidMinutes"/> minutes
/// and <see cref="OtpOptions.MaxAttempts"/> tries, then useless. Not tenant-owned: it belongs to a phone number.
/// </summary>
public sealed class OtpCode : Entity
{
    public required string Phone { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

/// <summary>The "Otp" config section. MSG91 needs an auth key and a DLT-approved OTP template (Sms__Msg91AuthKey, Sms__Msg91TemplateId).</summary>
public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public int ValidMinutes { get; set; } = 5;

    public int MaxAttempts { get; set; } = 5;

    /// <summary>Codes one phone may be sent per <see cref="WindowMinutes"/>, so nobody can flood a number with SMS.</summary>
    public int MaxSendsPerWindow { get; set; } = 3;

    public int WindowMinutes { get; set; } = 15;

    /// <summary>Wait before another code to the same phone.</summary>
    public int ResendSeconds { get; set; } = 30;

    /// <summary>All codes sent in 24 hours, across every number: a ceiling on SMS cost if someone tries many numbers.</summary>
    public int MaxSendsPerDay { get; set; } = 2000;
}

public sealed record OtpSendRequest(string? Phone);

/// <summary><see cref="FullName"/> only for a new number: the code then creates a household account.</summary>
public sealed record OtpVerifyRequest(string? Phone, string? Code, string? FullName, string? Language);

public sealed record OtpSentResponse(int ResendAfterSeconds, int ValidMinutes);

public interface ISmsSender
{
    /// <summary>Sends <paramref name="code"/> to <paramref name="phone"/> (E.164). Throws when the provider refuses.</summary>
    Task SendOtpAsync(string phone, string code, CancellationToken ct);
}

/// <summary>
/// MSG91's Flow API with a DLT-registered OTP template (India requires one). The template holds the wording and a
/// variable, by default ##OTP##, that MSG91 fills with the code.
/// </summary>
public sealed class Msg91SmsSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    public async Task SendOtpAsync(string phone, string code, CancellationToken ct)
    {
        var sms = options.Value;
        var recipient = new Dictionary<string, string> { ["mobiles"] = phone.TrimStart('+'), [sms.Msg91OtpVariable] = code };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://control.msg91.com/api/v5/flow")
        {
            Content = JsonContent.Create(new { template_id = sms.Msg91TemplateId, short_url = "0", recipients = new[] { recipient } }),
        };
        request.Headers.Add("authkey", sms.Msg91AuthKey);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Development and tests only, when no MSG91 key is set: keeps the last code per phone (tests read it) and logs it,
/// so you can sign in locally. Never registered in production.
/// </summary>
public sealed class DevSmsSender(ILogger<DevSmsSender> logger) : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _last = new();

    public string? LastCodeFor(string phone) => _last.TryGetValue(phone, out var code) ? code : null;

    public Task SendOtpAsync(string phone, string code, CancellationToken ct)
    {
        _last[phone] = code;
        logger.LogWarning("DEVELOPMENT sign-in code for {Phone}: {Code}", phone, code);
        return Task.CompletedTask;
    }
}

/// <summary>The "Sms" config section.</summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public string Msg91AuthKey { get; set; } = string.Empty;

    public string Msg91TemplateId { get; set; } = string.Empty;

    /// <summary>The variable name in the MSG91 template that takes the code.</summary>
    public string Msg91OtpVariable { get; set; } = "OTP";
}

/// <summary>
/// Sign in with a code by SMS instead of a password: send → verify. Works for every account (households,
/// shopkeepers, helpers): owning the phone is the proof. A new number with a name becomes a household account.
/// </summary>
public static class OtpSignIn
{
    public static IServiceCollection AddOtpSignIn(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.AddOptions<OtpOptions>().BindConfiguration(OtpOptions.SectionName);
        services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName);
        var sms = config.GetSection(SmsOptions.SectionName).Get<SmsOptions>() ?? new SmsOptions();
        if (sms.Msg91AuthKey.Length > 0 && sms.Msg91TemplateId.Length > 0)
        {
            services.AddHttpClient<ISmsSender, Msg91SmsSender>(http => http.Timeout = TimeSpan.FromSeconds(15));
        }
        else if (env.IsDevelopment() || env.IsEnvironment("Testing"))
        {
            services.AddSingleton<DevSmsSender>();
            services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<DevSmsSender>());
        }

        // In production without MSG91 settings no sender is registered and send answers 503: password sign-in still works.
        return services;
    }

    public static IEndpointRouteBuilder MapOtpSignIn(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth/otp").WithTags("Sign-in").AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit);
        group.MapPost("/send", SendAsync).RequireRateLimiting(AuthSetup.OtpSendRateLimit);
        group.MapPost("/verify", VerifyAsync);
        return app;
    }

    /// <summary>Sends a code. Answers the same whether or not the number has an account, so nothing is revealed.</summary>
    private static async Task<IResult> SendAsync(
        OtpSendRequest request, AppDbContext db, IServiceProvider services, IOptions<OtpOptions> options, TimeProvider clock, ILogger<OtpCode> logger, CancellationToken ct)
    {
        if (PhoneNumber.Normalize(request.Phone) is not { } phone)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["Enter a valid mobile number."] });
        }

        if (services.GetService<ISmsSender>() is not { } sender)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Codes by SMS aren't available right now.", detail: "Please sign in with your password.");
        }

        var o = options.Value;
        var now = clock.GetUtcNow();
        var recent = await db.OtpCodes.Where(c => c.Phone == phone && c.CreatedAt > now.AddMinutes(-o.WindowMinutes)).Select(c => c.CreatedAt).ToListAsync(ct);
        if (recent.Count >= o.MaxSendsPerWindow || recent.Any(t => t > now.AddSeconds(-o.ResendSeconds)))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Please wait before asking for another code.");
        }

        // The cost brake holds back strangers' numbers only: someone flooding it must not lock real people out of their
        // accounts. Each number has its own limit above, so known numbers can't run up much either.
        if (await db.OtpCodes.CountAsync(c => c.CreatedAt > now.AddDays(-1), ct) >= o.MaxSendsPerDay && !await db.Users.AsNoTracking().AnyAsync(u => u.Phone == phone, ct))
        {
            logger.LogError("Daily sign-in code ceiling of {Max} reached: possible SMS abuse", o.MaxSendsPerDay);
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Codes by SMS aren't available right now.", detail: "Please sign in with your password.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        // Codes older than a day are only clutter.
        await db.OtpCodes.Where(c => c.Phone == phone && c.CreatedAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        // Earlier codes stop working: only the newest one counts.
        await db.OtpCodes.Where(c => c.Phone == phone && c.UsedAt == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), ct);
        db.OtpCodes.Add(new OtpCode { Phone = phone, CodeHash = Hash(phone, code), ExpiresAt = now.AddMinutes(o.ValidMinutes) });
        await db.SaveChangesAsync(ct);
        try
        {
            await sender.SendOtpAsync(phone, code, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "SMS provider refused a sign-in code");
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "The SMS couldn't be sent.", detail: "Please try again in a minute, or sign in with your password.");
        }

        return TypedResults.Ok(new OtpSentResponse(o.ResendSeconds, o.ValidMinutes));
    }

    private static async Task<IResult> VerifyAsync(
        OtpVerifyRequest request, AppDbContext db, AuthService auth, IOptions<OtpOptions> options, TimeProvider clock, CancellationToken ct)
    {
        var phone = PhoneNumber.Normalize(request.Phone);
        var codeText = request.Code?.Trim();
        var now = clock.GetUtcNow();
        var otp = phone is null || codeText is not { Length: 6 }
            ? null
            : await db.OtpCodes.Where(c => c.Phone == phone && c.UsedAt == null && c.ExpiresAt > now).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (otp is null || otp.Attempts >= options.Value.MaxAttempts)
        {
            return WrongCode();
        }

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(otp.CodeHash), Convert.FromHexString(Hash(phone!, codeText!))))
        {
            otp.Attempts++;
            await db.SaveChangesAsync(ct);
            return WrongCode();
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, ct);
        var name = request.FullName?.Trim();
        if (user is null && name is not { Length: >= 2 and <= 120 })
        {
            // Right code, new number: ask for a name and verify again with the same code (it is not used up yet).
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "name-needed", detail: "This number is new here. Tell us your name to make your account.");
        }

        otp.UsedAt = now;
        var result = await auth.SignInWithVerifiedPhoneAsync(user, phone!, name, request.Language is "en" ? "en" : "hi", ct);
        return result switch
        {
            { Value: { } value } => TypedResults.Ok(value),
            { Error: AuthError.LockedOut } => TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Account locked for a while."),
            _ => WrongCode(),
        };
    }

    private static IResult WrongCode() =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed.", detail: "The code is wrong or has expired. Ask for a new one.");

    private static string Hash(string phone, string code) => Secrets.Sha256Hex($"{phone}:{code}");
}
