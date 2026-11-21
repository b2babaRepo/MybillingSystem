using System.Security.Claims;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Identity;

public sealed record ProfileDto(string FullName, string Phone, string PreferredLanguage, string? Email, string? Address, string? City, string? Pincode, bool HasPassword = false);

public sealed record PasswordChange(string? CurrentPassword, string? NewPassword);

/// <summary>Only the app language: saved with the account so it follows the person to any phone.</summary>
public sealed record LanguageChange(string? Language);

/// <summary>The phone is the login, so it isn't changed here.</summary>
public sealed record ProfileUpdate(string? FullName, string? PreferredLanguage, string? Email, string? Address, string? City, string? Pincode);

/// <summary>The signed-in person's own details: only ever their own row.</summary>
public static class Profile
{
    public static IEndpointRouteBuilder MapProfile(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/profile").WithTags("Sign-in").RequireAuthorization();
        group.MapGet("", GetAsync);
        group.MapPut("", UpdateAsync);
        group.MapPut("/password", SetPasswordAsync);
        group.MapPut("/language", LanguageAsync);
        return app;
    }

    /// <summary>
    /// Sets the password. Needs the current one if there is one; with none (SMS sign-in only), a number proved by SMS.
    /// Every other sign-in ends, so a password that got out stops working everywhere.
    /// </summary>
    private static async Task<IResult> SetPasswordAsync(
        PasswordChange request, ClaimsPrincipal principal, AppDbContext db, Microsoft.AspNetCore.Identity.IPasswordHasher<User> hasher, TimeProvider clock, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == principal.UserId(), ct);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (request.NewPassword is not { Length: >= SignUpRequest.MinPasswordLength and <= 128 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["NewPassword"] = [$"Use at least {SignUpRequest.MinPasswordLength} characters."] });
        }

        var allowed = user.PasswordHash is { } hash
            ? request.CurrentPassword is not null && hasher.VerifyHashedPassword(user, hash, request.CurrentPassword) != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed
            : user.PhoneVerifiedAt is not null;
        if (!allowed)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["CurrentPassword"] = ["That isn't your current password."] });
        }

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(ct);
        var now = clock.GetUtcNow();
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        return TypedResults.NoContent();
    }

    private static ProfileDto ToDto(User u) => new(u.FullName, u.Phone, u.PreferredLanguage, u.Email, u.Address, u.City, u.Pincode, u.PasswordHash is not null);

    /// <summary>Saves the language picked anywhere in the app, without touching the rest of the profile.</summary>
    private static async Task<IResult> LanguageAsync(LanguageChange request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (request.Language is not ("en" or "hi" or "gu"))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Language"] = ["Language must be en, hi or gu."] });
        }

        var changed = await db.Users.Where(u => u.Id == principal.UserId()).ExecuteUpdateAsync(s => s.SetProperty(u => u.PreferredLanguage, request.Language), ct);
        return changed == 0 ? TypedResults.Unauthorized() : TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == principal.UserId(), ct) is { } user
            ? TypedResults.Ok(ToDto(user))
            : TypedResults.Unauthorized();

    private static async Task<IResult> UpdateAsync(ProfileUpdate request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == principal.UserId(), ct);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>();
        var name = request.FullName?.Trim();
        var email = Clean(request.Email);
        var pincode = Clean(request.Pincode);
        var address = Clean(request.Address);
        var city = Clean(request.City);
        if (name is not { Length: >= 2 and <= 120 }) errors["FullName"] = ["Enter your name."];
        if (request.PreferredLanguage is not ("en" or "hi" or "gu")) errors["PreferredLanguage"] = ["Language must be en, hi or gu."];
        if (email is not null && (email.Length > 160 || !email.Contains('@') || email.Contains(' '))) errors["Email"] = ["Enter a valid email."];
        if (pincode is not null && (pincode.Length != 6 || !pincode.All(char.IsAsciiDigit))) errors["Pincode"] = ["A pincode has 6 digits."];
        if (address?.Length > 300 || city?.Length > 80) errors["Address"] = ["Keep the address shorter."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        (user.FullName, user.PreferredLanguage, user.Email, user.Address, user.City, user.Pincode) = (name!, request.PreferredLanguage!, email, address, city, pincode);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(user));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
