using System.Security.Claims;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Identity;

public sealed record ProfileDto(string FullName, string Phone, string PreferredLanguage, string? Email, string? Address, string? City, string? Pincode);

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
        return app;
    }

    private static ProfileDto ToDto(User u) => new(u.FullName, u.Phone, u.PreferredLanguage, u.Email, u.Address, u.City, u.Pincode);

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
        if (request.PreferredLanguage is not ("en" or "hi")) errors["PreferredLanguage"] = ["Language must be en or hi."];
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
