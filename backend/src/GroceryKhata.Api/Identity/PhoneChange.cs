using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Identity;

public sealed record PhoneChangeSend(string? NewPhone, bool ToOld = false);

/// <summary><see cref="Password"/> or <see cref="OldCode"/> proves it is the account's owner; <see cref="Code"/> proves the new number.</summary>
public sealed record PhoneChangeRequest(string? NewPhone, string? Code, string? Password, string? OldCode);

/// <summary>
/// Moving an account to a new mobile number. The new number is proved by an SMS code sent to it; the person is proved
/// by their password, or (an account with no password) by a code sent to the old number. Someone who lost the old SIM
/// and has no password asks support. Every sign-in ends; this phone gets a fresh one. Khatas at shops stay under the
/// old number until each shop updates it and the person confirms again with the shop's code.
/// </summary>
public static class PhoneChange
{
    public static IEndpointRouteBuilder MapPhoneChange(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/profile/phone").WithTags("Profile").RequireAuthorization();
        group.MapPost("/send", SendAsync).RequireRateLimiting(AuthSetup.OtpSendRateLimit);
        group.MapPost("", ChangeAsync).RequireRateLimiting(AuthSetup.AuthRateLimit);
        return app;
    }

    private static readonly Dictionary<string, string[]> Taken = new() { ["NewPhone"] = ["This number can't be used. If it's yours on another account, sign in with it instead."] };

    /// <summary>A code to the new number, or (<c>ToOld</c>) to the account's own number for an account without a password.</summary>
    private static async Task<IResult> SendAsync(PhoneChangeSend request, ClaimsPrincipal principal, AppDbContext db, IServiceProvider services, IOptions<OtpOptions> options,
        TimeProvider clock, ILogger<OtpCode> logger, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == principal.UserId(), ct);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (request.ToOld)
        {
            return await OtpSignIn.SendAsync(new OtpSendRequest(user.Phone), db, services, options, clock, logger, ct);
        }

        if (PhoneNumber.Normalize(request.NewPhone) is not { } phone)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["NewPhone"] = ["Enter a valid mobile number."] });
        }

        if (phone == user.Phone || await db.Users.AnyAsync(u => u.Phone == phone, ct))
        {
            return TypedResults.ValidationProblem(Taken);
        }

        return await OtpSignIn.SendAsync(new OtpSendRequest(phone), db, services, options, clock, logger, ct);
    }

    private static async Task<IResult> ChangeAsync(PhoneChangeRequest request, ClaimsPrincipal principal, AppDbContext db, AuthService auth, IPasswordHasher<User> hasher,
        IOptions<OtpOptions> options, TimeProvider clock, ILogger<OtpCode> logger, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == principal.UserId(), ct);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (PhoneNumber.Normalize(request.NewPhone) is not { } phone)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["NewPhone"] = ["Enter a valid mobile number."] });
        }

        if (phone == user.Phone || await db.Users.AnyAsync(u => u.Phone == phone, ct))
        {
            return TypedResults.ValidationProblem(Taken);
        }

        var now = clock.GetUtcNow();
        // Who they are: the password, else a code on the old number.
        OtpCode? oldCode = null;
        var owner = user.PasswordHash is { } hash
            ? request.Password is not null && hasher.VerifyHashedPassword(user, hash, request.Password) != PasswordVerificationResult.Failed
            : (oldCode = await OtpSignIn.MatchAsync(db, user.Phone, request.OldCode, options.Value, now, ct)) is not null;
        if (!owner)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [user.PasswordHash is null ? "OldCode" : "Password"] = [user.PasswordHash is null ? "The code sent to your old number is wrong or has expired." : "That isn't your password."],
            });
        }

        var newCode = await OtpSignIn.MatchAsync(db, phone, request.Code, options.Value, now, ct);
        if (newCode is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Code"] = ["The code sent to the new number is wrong or has expired."] });
        }

        newCode.UsedAt = now;
        if (oldCode is not null)
        {
            oldCode.UsedAt = now;
        }

        var old = user.Phone;
        user.Phone = phone;
        await db.SaveChangesAsync(ct);
        // Every sign-in ends; this phone carries on with a fresh one under the new number.
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        logger.LogInformation("User {UserId} moved to a new phone number (was ending {Old})", user.Id, old.Length >= 4 ? old[^4..] : old);
        var result = await auth.SignInWithVerifiedPhoneAsync(user, phone, null, user.PreferredLanguage, ct);
        return result.Value is { } session ? TypedResults.Ok(session) : TypedResults.Unauthorized();
    }
}
