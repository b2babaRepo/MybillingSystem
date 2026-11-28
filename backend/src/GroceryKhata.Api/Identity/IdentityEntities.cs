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

    /// <summary>
    /// When a code sent to <see cref="Email"/> proved it is this person's. Only a verified email can reset a password,
    /// and no two accounts share one. Cleared whenever the email changes.
    /// </summary>
    public DateTimeOffset? EmailVerifiedAt { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(300)]
    public string? Address { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(80)]
    public string? City { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(6)]
    public string? Pincode { get; set; }

    /// <summary>A business buyer's GSTIN: copied to the shop's customer record when an app order is delivered, so the bill is a tax invoice in their name.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(15)]
    public string? Gstin { get; set; }

    /// <summary>When the person read the shopping app's privacy notice and declared they are 18 or older. Null: not yet (asked on first use).</summary>
    public DateTimeOffset? PrivacyAcceptedAt { get; set; }

    /// <summary>"en", "hi", "gu" or "hn" (Hinglish, customer app only).</summary>
    public string PreferredLanguage { get; set; } = "hi";

    public bool IsActive { get; set; } = true;

    public int FailedSignInCount { get; set; }

    public DateTimeOffset? LockoutEndsAt { get; set; }

    /// <summary>
    /// Lockouts in a row: each one lasts twice as long as the one before (see AuthService.CountFailure), so a short PIN
    /// can't be worked through a few guesses at a time. Back to 0 only after a day with no lockout, so signing in now and
    /// then doesn't reset someone else's guessing.
    /// </summary>
    public int LockoutCount { get; set; }

    /// <summary>When the last lockout began.</summary>
    public DateTimeOffset? LastLockedAt { get; set; }

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

    /// <summary>Asked for order updates (accepted, changed, ready, delivered) on WhatsApp from the shops they order from.</summary>
    public bool OrderWhatsApp { get; set; }
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

    /// <summary>A helper who approves cancels, returns and prices below the helper limit with their PIN (Shops.Supervisor).</summary>
    public bool IsSupervisor { get; set; }

    /// <summary>What the owner has stopped this helper doing (<see cref="StaffLimits"/>). Empty: nothing.</summary>
    public string[] Blocked { get; set; } = [];
}

public enum ShopRole
{
    Owner,
    Staff,

    /// <summary>The shop's CA or accountant: sees what the owner sees (reports, GST, books), changes nothing.</summary>
    Accountant,
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

    /// <summary>Made with a shop's PIN: refreshing it stays as narrow as the sign-in was.</summary>
    public bool PinOnly { get; set; }
}
