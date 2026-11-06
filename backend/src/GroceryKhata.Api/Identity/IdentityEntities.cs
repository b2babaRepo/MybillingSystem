using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Identity;

/// <summary>One person across all shops. Not tenant-owned: always queried by user or shop explicitly.</summary>
public sealed class User : Entity
{
    /// <summary>E.164, unique. Shopkeepers sign in with their mobile number.</summary>
    public required string Phone { get; set; }

    public string? PasswordHash { get; set; }

    public required string FullName { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(160)]
    public string? Email { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(300)]
    public string? Address { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(80)]
    public string? City { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(6)]
    public string? Pincode { get; set; }

    /// <summary>"en" or "hi".</summary>
    public string PreferredLanguage { get; set; } = "hi";

    public bool IsActive { get; set; } = true;

    public int FailedSignInCount { get; set; }

    public DateTimeOffset? LockoutEndsAt { get; set; }

    /// <summary>
    /// When the person agreed to their notes, photos and voice being read by AI services (India's DPDP Act: a
    /// recorded, specific consent). Null: not agreed, or withdrawn, which also erases their AI records.
    /// </summary>
    public DateTimeOffset? AiConsentAt { get; set; }

    /// <summary>
    /// When an SMS code last proved this person holds <see cref="Phone"/>. A password sign-up alone doesn't, so anything
    /// that trusts the number itself (being made a platform admin) needs this.
    /// </summary>
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
}

/// <summary>A user's role in one shop. A user can own or work in many shops. Unique (user_id, shop_id).</summary>
public sealed class Membership : Entity
{
    public Guid UserId { get; set; }

    public Guid ShopId { get; set; }

    public ShopRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Commission on what this person sells, as a percent of their sales (the staff sales report works it out).</summary>
    public decimal? CommissionPercent { get; set; }

    /// <summary>
    /// A short PIN (4 to 6 digits) for signing in at this shop's counter, set by the owner for staff. Hashed like a
    /// password; wrong tries count towards the same lockout. Null: this membership signs in with a password only.
    /// </summary>
    public string? PinHash { get; set; }
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
