namespace GroceryKhata.Api.Identity;

/// <summary>
/// Opens a new shop. A new phone number creates the owner's account; a phone that already has an
/// account adds the shop to it (one person, many shops) after checking the existing password.
/// </summary>
public sealed record SignUpRequest(string? ShopName, string? FullName, string? Phone, string? Password, string? Language)
{
    public const int MinPasswordLength = 6;

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(ShopName) || ShopName.Trim().Length > 120)
        {
            errors[nameof(ShopName)] = ["Enter the shop's name."];
        }

        if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length > 120)
        {
            errors[nameof(FullName)] = ["Enter your name."];
        }

        if (Common.PhoneNumber.Normalize(Phone) is null)
        {
            errors[nameof(Phone)] = ["Enter a valid mobile number."];
        }

        if (Password is null || Password.Length < MinPasswordLength || Password.Length > 128)
        {
            errors[nameof(Password)] = [$"Use a password or PIN of at least {MinPasswordLength} characters."];
        }

        if (Language is not (null or "en" or "hi"))
        {
            errors[nameof(Language)] = ["Language must be en or hi."];
        }

        return errors;
    }
}

public sealed record SignInRequest(string? Phone, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record SelectShopRequest(Guid MembershipId);

/// <summary>
/// Returned by every sign-in step. With exactly one shop the token is already a shop token. With
/// several, <see cref="ShopId"/> is null and the token only works for /auth endpoints: pick one of
/// <see cref="Memberships"/> with select-shop.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserSummary User,
    IReadOnlyList<MembershipSummary> Memberships,
    Guid? ShopId,
    ShopRole? Role);

public sealed record UserSummary(Guid Id, string FullName, string PreferredLanguage);

public sealed record MembershipSummary(Guid MembershipId, Guid ShopId, string ShopName, string ShopSlug, ShopRole Role);

public sealed record MeResponse(UserSummary User, IReadOnlyList<MembershipSummary> Memberships);

public enum AuthError
{
    /// <summary>Wrong phone, password or token. Always the same message, so nothing is revealed. 401.</summary>
    InvalidCredentials,

    /// <summary>Too many wrong passwords; try again later. 429.</summary>
    LockedOut,

    /// <summary>Membership doesn't exist for this user or isn't active. 404.</summary>
    MembershipNotFound,
}

/// <summary>Either a response or an error, never both.</summary>
public sealed record AuthResult<T>(T? Value, AuthError? Error)
    where T : class
{
    public static AuthResult<T> Ok(T value) => new(value, null);

    public static AuthResult<T> Fail(AuthError error) => new(null, error);
}
