using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GroceryKhata.Api.Identity;

/// <summary>Claim names inside our access tokens (kept short, not mapped to .NET's long URIs).</summary>
public static class AppClaims
{
    public const string Subject = "sub";
    public const string ShopId = "shop_id";
    public const string MembershipId = "mid";
    public const string Role = "role";

    /// <summary>"true" on a platform admin's tokens; the policy checks the table again on every request.</summary>
    public const string PlatformAdmin = "platform_admin";

    /// <summary>The signed-in user's ID, or null if the token has none.</summary>
    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(Subject)?.Value, out var id) ? id : null;
}

public static class AppPolicies
{
    /// <summary>Signed in with a token for one shop. Every shop endpoint needs this.</summary>
    public const string ShopMember = nameof(ShopMember);

    /// <summary>The shop's owner (staff management, data export, settings).</summary>
    public const string ShopOwner = nameof(ShopOwner);

    /// <summary>Super admin: /api/v1/platform endpoints, every shop.</summary>
    public const string PlatformAdmin = nameof(PlatformAdmin);

    /// <summary>Platform admins who approve and block shops (approver or super).</summary>
    public const string PlatformApprover = nameof(PlatformApprover);

    /// <summary>Platform admins who help shops: notes and unlocking (support or super).</summary>
    public const string PlatformSupport = nameof(PlatformSupport);

    /// <summary>Super admins: everything, including managing admins.</summary>
    public const string PlatformSuper = nameof(PlatformSuper);

    /// <summary>Platform admins who handle money: plans' payments, offers, refunds (billing or super).</summary>
    public const string PlatformBilling = nameof(PlatformBilling);
}

/// <summary>Roles as they appear in tokens: ShopRole.Owner ↔ "owner".</summary>
public static class RoleNames
{
    public static string ToName(ShopRole role) => role.ToString().ToLowerInvariant();

    public static bool TryParse(string? name, out ShopRole role) =>
        Enum.TryParse(name, ignoreCase: true, out role) && Enum.IsDefined(role) && name == ToName(role);
}

/// <summary>The "Jwt" config section. The signing key comes from user-secrets or an environment variable outside Development.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinimumKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 30;
}

/// <summary>The "Auth" config section: token lifetimes and lockout.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Long, because a shop phone may stay offline for days; the app refreshes when it reconnects.</summary>
    public int RefreshTokenDays { get; set; } = 90;

    /// <summary>Wrong passwords in a row before the account locks.</summary>
    public int MaxFailedSignIns { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Requests per minute per IP address on sign-up, sign-in and refresh.</summary>
    public int AuthRequestsPerMinutePerIp { get; set; } = 20;

    /// <summary>Requests per minute per IP address on the public online store (browsing and checkout).</summary>
    public int StoreRequestsPerMinutePerIp { get; set; } = 60;
}

/// <summary>
/// What an access token says. <see cref="ShopId"/>, <see cref="MembershipId"/> and <see cref="Role"/>
/// are all set (a shop token) or all null (a user token, before a shop is picked).
/// </summary>
public sealed record AccessTokenSubject(Guid UserId, Guid? ShopId, Guid? MembershipId, ShopRole? Role, bool IsPlatformAdmin = false);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Creates signed JWT access tokens (HMAC-SHA256).</summary>
public sealed class AccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(AccessTokenSubject subject)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim> { new(AppClaims.Subject, subject.UserId.ToString()) };
        if (subject is { ShopId: { } shopId, MembershipId: { } membershipId, Role: { } role })
        {
            claims.Add(new Claim(AppClaims.ShopId, shopId.ToString()));
            claims.Add(new Claim(AppClaims.MembershipId, membershipId.ToString()));
            claims.Add(new Claim(AppClaims.Role, RoleNames.ToName(role)));
        }

        if (subject.IsPlatformAdmin)
        {
            claims.Add(new Claim(AppClaims.PlatformAdmin, "true"));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(CreateSigningKey(jwt.SigningKey), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }

    public static SymmetricSecurityKey CreateSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));
}

/// <summary>Random tokens and the hashes we store instead of them.</summary>
internal static class Secrets
{
    /// <summary>32 random bytes, URL-safe base64. Given to the client once; only its hash is stored.</summary>
    public static string NewRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
