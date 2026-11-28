using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Identity;

/// <summary>
/// A 6-digit code sent by email: either proving a recovery email is the person's own (<see cref="EmailCodes.Verify"/>)
/// or replacing a forgotten password (<see cref="EmailCodes.Reset"/>). Stored only as a hash, short-lived, few tries, used once.
/// </summary>
public sealed class EmailCode : Entity
{
    public Guid UserId { get; set; }

    /// <summary>The address it was sent to, lower case: a code for an email the account no longer has is useless.</summary>
    public required string Email { get; set; }

    public required string Purpose { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

/// <summary>The "Email" config section. With no SMTP host, development and tests log the email instead; production sends none.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>E.g. smtp.zeptomail.in (Zoho ZeptoMail), smtp-relay.brevo.com or email-smtp.ap-south-1.amazonaws.com.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string SmtpUser { get; set; } = string.Empty;

    public string SmtpPassword { get; set; } = string.Empty;

    public string From { get; set; } = "Apna Kaarobar <no-reply@apnakaarobar.com>";

    public int ValidMinutes { get; set; } = 10;

    public int MaxAttempts { get; set; } = 5;

    /// <summary>Codes one account may be sent per <see cref="WindowMinutes"/>.</summary>
    public int MaxSendsPerWindow { get; set; } = 3;

    public int WindowMinutes { get; set; } = 15;

    public int ResendSeconds { get; set; } = 30;
}

public interface IEmailSender
{
    /// <summary>Plain-text email. Throws when the provider refuses.</summary>
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

/// <summary>Any SMTP relay (ZeptoMail, Brevo, SES), over STARTTLS.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.SmtpHost, o.SmtpPort) { EnableSsl = true, Credentials = new NetworkCredential(o.SmtpUser, o.SmtpPassword) };
        using var message = new MailMessage(MailAddressParse(o.From), new MailAddress(to)) { Subject = subject, Body = body, IsBodyHtml = false };
        await client.SendMailAsync(message, ct);
    }

    private static MailAddress MailAddressParse(string from) => new(from);
}

/// <summary>Development and tests only: keeps the last email per address (tests read the code) and logs it.</summary>
public sealed class DevEmailSender(ILogger<DevEmailSender> logger) : IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _last = new(StringComparer.OrdinalIgnoreCase);

    public string? LastBodyFor(string email) => _last.TryGetValue(email, out var body) ? body : null;

    /// <summary>The 6-digit code in the last email to this address.</summary>
    public string? LastCodeFor(string email) =>
        LastBodyFor(email) is { } body ? System.Text.RegularExpressions.Regex.Match(body, @"\b\d{6}\b") is { Success: true } m ? m.Value : null : null;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        _last[to] = body;
        logger.LogWarning("DEVELOPMENT email to {To}: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

public sealed record EmailVerifyRequest(string? Code);

public sealed record EmailResetSendRequest(string? Email);

public sealed record EmailResetRequest(string? Email, string? Code, string? NewPassword);

/// <summary>
/// A recovery email: the person adds it in Profile and proves it with a code; then "forgot password" can send a code to
/// it instead of an SMS. Answers to a reset request never say whether the email has an account.
/// </summary>
public static class EmailCodes
{
    public const string Verify = "verify";
    public const string Reset = "reset";

    public static IServiceCollection AddEmailCodes(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName);
        var email = config.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        if (email.SmtpHost.Length > 0)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else if (env.IsDevelopment() || env.IsEnvironment("Testing"))
        {
            services.AddSingleton<DevEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevEmailSender>());
        }

        return services;
    }

    public static IEndpointRouteBuilder MapEmailCodes(this IEndpointRouteBuilder app)
    {
        var profile = app.MapGroup("/api/v1/profile/email").WithTags("Sign-in").RequireAuthorization();
        profile.MapPost("/send-code", SendVerifyAsync);
        profile.MapPost("/verify", VerifyAsync);

        var auth = app.MapGroup("/api/v1/auth/email").WithTags("Sign-in").AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit);
        auth.MapPost("/send-reset", SendResetAsync).RequireRateLimiting(AuthSetup.OtpSendRateLimit);
        auth.MapPost("/reset-password", ResetPasswordAsync);
        return app;
    }

    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Sends a code to the email saved in the profile, to prove it.</summary>
    private static async Task<IResult> SendVerifyAsync(
        ClaimsPrincipal principal, AppDbContext db, IServiceProvider services, IOptions<EmailOptions> options, TimeProvider clock, ILogger<EmailCode> logger, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == principal.UserId(), ct);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var email = Normalize(user.Email);
        if (email.Length == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Email"] = ["Save an email first."] });
        }

        if (user.EmailVerifiedAt is not null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "already-verified");
        }

        if (await db.Users.AsNoTracking().AnyAsync(u => u.Id != user.Id && u.EmailVerifiedAt != null && u.Email == email, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "email-taken", detail: "Another account already uses this email.");
        }

        var sent = await SendCodeAsync(db, services, options.Value, clock, logger, user, email, Verify, ct);
        return sent ?? TypedResults.Ok(new OtpSentResponse(options.Value.ResendSeconds, options.Value.ValidMinutes));
    }

    private static async Task<IResult> VerifyAsync(
        EmailVerifyRequest request, ClaimsPrincipal principal, AppDbContext db, IOptions<EmailOptions> options, TimeProvider clock, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == principal.UserId(), ct);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var email = Normalize(user.Email);
        var now = clock.GetUtcNow();
        var code = email.Length == 0 ? null : await MatchAsync(db, user.Id, email, Verify, request.Code, options.Value, now, ct);
        if (code is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Code"] = ["The code is wrong or has expired. Ask for a new one."] });
        }

        if (await db.Users.AsNoTracking().AnyAsync(u => u.Id != user.Id && u.EmailVerifiedAt != null && u.Email == email, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "email-taken", detail: "Another account already uses this email.");
        }

        code.UsedAt = now;
        (user.Email, user.EmailVerifiedAt) = (email, now);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Forgot password: sends a code if the email is an account's verified one. The answer is the same either way.</summary>
    private static async Task<IResult> SendResetAsync(
        EmailResetSendRequest request, AppDbContext db, IServiceProvider services, IOptions<EmailOptions> options, TimeProvider clock, ILogger<EmailCode> logger, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        if (email.Length is < 3 or > 160 || !email.Contains('@') || email.Contains(' '))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Email"] = ["Enter a valid email."] });
        }

        if (services.GetService<IEmailSender>() is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Email isn't available right now.", detail: "Please reset with a code by SMS.");
        }

        var o = options.Value;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.EmailVerifiedAt != null && u.Email == email && u.IsActive, ct);
        if (user is not null)
        {
            // Too many sends or a refused email aren't reported: that would tell a stranger the email has an account.
            await SendCodeAsync(db, services, o, clock, logger, user, email, Reset, ct);
        }

        return TypedResults.Ok(new OtpSentResponse(o.ResendSeconds, o.ValidMinutes));
    }

    /// <summary>
    /// The code from the email replaces the password. Every other sign-in ends, this device is signed in, and the
    /// email gets a notice that the password changed.
    /// </summary>
    private static async Task<IResult> ResetPasswordAsync(
        EmailResetRequest request, AppDbContext db, AuthService auth, Microsoft.AspNetCore.Identity.IPasswordHasher<User> hasher, IServiceProvider services,
        IOptions<EmailOptions> options, TimeProvider clock, ILogger<EmailCode> logger, CancellationToken ct)
    {
        if (request.NewPassword is not { Length: >= SignUpRequest.MinPasswordLength and <= 128 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["NewPassword"] = [$"Use at least {SignUpRequest.MinPasswordLength} characters."] });
        }

        var email = Normalize(request.Email);
        var now = clock.GetUtcNow();
        var user = email.Length == 0 ? null : await db.Users.SingleOrDefaultAsync(u => u.EmailVerifiedAt != null && u.Email == email, ct);
        var code = user is null ? null : await MatchAsync(db, user.Id, email, Reset, request.Code, options.Value, now, ct);
        if (user is null || code is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Reset failed.", detail: "The code is wrong or has expired. Ask for a new one.");
        }

        code.UsedAt = now;
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        var result = await auth.SignInAfterEmailResetAsync(user, ct);
        if (result.Value is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Reset failed.", detail: "This account is switched off.");
        }

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(ct);
        if (services.GetService<IEmailSender>() is { } sender)
        {
            var (subject, body) = Changed(user.PreferredLanguage);
            try
            {
                await sender.SendAsync(email, subject, body, ct);
            }
            catch (Exception exception) when (exception is SmtpException or InvalidOperationException or IOException)
            {
                logger.LogError(exception, "Password-changed notice couldn't be emailed to user {UserId}", user.Id);
            }
        }

        return TypedResults.Ok(result.Value);
    }

    /// <summary>Null when sent; otherwise the answer to give (too soon, no email service, provider refused).</summary>
    internal static async Task<IResult?> SendCodeAsync(
        AppDbContext db, IServiceProvider services, EmailOptions o, TimeProvider clock, ILogger logger, User user, string email, string purpose, CancellationToken ct)
    {
        if (services.GetService<IEmailSender>() is not { } sender)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Email isn't available right now.");
        }

        var now = clock.GetUtcNow();
        var recent = await db.EmailCodes.Where(c => c.UserId == user.Id && c.Purpose == purpose && c.CreatedAt > now.AddMinutes(-o.WindowMinutes)).Select(c => c.CreatedAt).ToListAsync(ct);
        if (recent.Count >= o.MaxSendsPerWindow || recent.Any(t => t > now.AddSeconds(-o.ResendSeconds)))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Please wait before asking for another code.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await db.EmailCodes.Where(c => c.UserId == user.Id && c.CreatedAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        await db.EmailCodes.Where(c => c.UserId == user.Id && c.Purpose == purpose && c.UsedAt == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), ct);
        db.EmailCodes.Add(new EmailCode { UserId = user.Id, Email = email, Purpose = purpose, CodeHash = Hash(user.Id, purpose, code), ExpiresAt = now.AddMinutes(o.ValidMinutes) });
        await db.SaveChangesAsync(ct);
        var (subject, body) = purpose == Reset ? ResetMail(user.PreferredLanguage, code, o.ValidMinutes) : VerifyMail(user.PreferredLanguage, code, o.ValidMinutes);
        try
        {
            await sender.SendAsync(email, subject, body, ct);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or IOException or FormatException)
        {
            logger.LogError(exception, "Email provider refused a {Purpose} code for user {UserId}", purpose, user.Id);
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "The email couldn't be sent.", detail: "Please try again in a minute.");
        }

        return null;
    }

    private static async Task<EmailCode?> MatchAsync(AppDbContext db, Guid userId, string email, string purpose, string? code, EmailOptions o, DateTimeOffset now, CancellationToken ct)
    {
        var codeText = code?.Trim();
        var row = codeText is not { Length: 6 }
            ? null
            : await db.EmailCodes.Where(c => c.UserId == userId && c.Purpose == purpose && c.UsedAt == null && c.ExpiresAt > now).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (row is null || row.Attempts >= o.MaxAttempts || row.Email != email)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(row.CodeHash), Convert.FromHexString(Hash(userId, purpose, codeText!))))
        {
            row.Attempts++;
            await db.SaveChangesAsync(ct);
            return null;
        }

        return row;
    }

    private static string Hash(Guid userId, string purpose, string code) => Secrets.Sha256Hex($"{userId}:{purpose}:{code}");

    private static bool Hindi(string language) => language is "hi" or "hn";

    private static (string, string) VerifyMail(string lang, string code, int minutes) => Hindi(lang)
        ? ($"अपना कारोबार: ईमेल पक्का करने का कोड {code}", $"नमस्ते,\n\nअपना ईमेल पक्का करने के लिए यह कोड डालें: {code}\nयह {minutes} मिनट तक चलेगा।\n\nअगर आपने यह नहीं माँगा, तो इस ईमेल को अनदेखा करें।\n\nअपना कारोबार")
        : ($"Apna Kaarobar: your email code {code}", $"Hello,\n\nEnter this code to confirm your email: {code}\nIt works for {minutes} minutes.\n\nIf you didn't ask for it, ignore this email.\n\nApna Kaarobar");

    private static (string, string) ResetMail(string lang, string code, int minutes) => Hindi(lang)
        ? ($"अपना कारोबार: पासवर्ड बदलने का कोड {code}", $"नमस्ते,\n\nनया पासवर्ड रखने के लिए यह कोड डालें: {code}\nयह {minutes} मिनट तक चलेगा। यह कोड किसी को न बताएँ।\n\nअगर आपने यह नहीं माँगा, तो इस ईमेल को अनदेखा करें — आपका पासवर्ड नहीं बदलेगा।\n\nअपना कारोबार")
        : ($"Apna Kaarobar: password reset code {code}", $"Hello,\n\nEnter this code to set a new password: {code}\nIt works for {minutes} minutes. Don't share it with anyone.\n\nIf you didn't ask for it, ignore this email: your password stays the same.\n\nApna Kaarobar");

    private static (string, string) Changed(string lang) => Hindi(lang)
        ? ("अपना कारोबार: आपका पासवर्ड बदल गया", "नमस्ते,\n\nआपके खाते का पासवर्ड अभी बदला गया, और बाकी सब फ़ोन से खाता बाहर कर दिया गया।\nअगर यह आपने नहीं किया, तो तुरंत SMS कोड से पासवर्ड बदलें और हमसे संपर्क करें।\n\nअपना कारोबार")
        : ("Apna Kaarobar: your password was changed", "Hello,\n\nYour account's password was just changed, and every other device was signed out.\nIf this wasn't you, reset it now with an SMS code and contact us.\n\nApna Kaarobar");
}
