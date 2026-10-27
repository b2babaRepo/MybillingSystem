using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Identity;

/// <summary>One person across all shops. Not tenant-owned: always queried by user or shop explicitly.</summary>
public sealed class User : Entity
{
    /// <summary>E.164, unique. Shopkeepers sign in with their mobile number.</summary>
    public required string Phone { get; set; }

    public string? PasswordHash { get; set; }

    public required string FullName { get; set; }

    /// <summary>"en" or "hi".</summary>
    public string PreferredLanguage { get; set; } = "hi";

    public bool IsActive { get; set; } = true;

    public int FailedSignInCount { get; set; }

    public DateTimeOffset? LockoutEndsAt { get; set; }
}

/// <summary>A user's role in one shop. A user can own or work in many shops. Unique (user_id, shop_id).</summary>
public sealed class Membership : Entity
{
    public Guid UserId { get; set; }

    public Guid ShopId { get; set; }

    public ShopRole Role { get; set; }

    public bool IsActive { get; set; } = true;
}

public enum ShopRole
{
    Owner,
    Staff,
}

/// <summary>
/// A long-lived sign-in, stored only as a hash. Rotated on every use: the old one is revoked
/// and points at its replacement. Using a revoked token again revokes all of the user's tokens.
/// </summary>
public sealed class RefreshToken : Entity
{
    public Guid UserId { get; set; }

    /// <summary>The shop membership this sign-in is for; null before a shop is picked.</summary>
    public Guid? MembershipId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }
}
