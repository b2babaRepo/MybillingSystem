using System.Text;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Identity;

/// <summary>
/// Sign-up, password sign-in, shop selection, refresh and sign-out. Ported from SchoolErp's AuthService
/// (schools → shops, OTP and invites left for later). Logs carry user IDs only: never phone numbers or names.
/// </summary>
public sealed class AuthService(
    AppDbContext db,
    AccessTokenIssuer tokens,
    IPasswordHasher<User> passwordHasher,
    IOptions<AuthOptions> authOptions,
    TimeProvider clock,
    ILogger<AuthService> logger)
{
    // Used when the user doesn't exist, so a wrong phone takes as long as a wrong password.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(new User { Phone = "+910000000000", FullName = "-" }, "dummy-password");

    private AuthOptions Options => authOptions.Value;

    public async Task<AuthResult<AuthResponse>> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Normalize(request.Phone)!;
        var user = await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        if (user is null)
        {
            user = new User { Phone = phone, FullName = request.FullName!.Trim(), PreferredLanguage = request.Language ?? "hi" };
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            db.Users.Add(user);
        }
        else if (await CheckPasswordAsync(user, request.Password!, cancellationToken) is { } error)
        {
            // An existing account adds a shop only with its own password.
            return AuthResult<AuthResponse>.Fail(error);
        }

        var shop = new Shop
        {
            Name = request.ShopName!.Trim(),
            Slug = NewSlug(request.ShopName),
            Area = string.IsNullOrWhiteSpace(request.Area) ? null : request.Area.Trim()[..Math.Min(request.Area.Trim().Length, 120)],
        };
        db.Shops.Add(shop);
        var membership = new Membership { UserId = user.Id, ShopId = shop.Id, Role = ShopRole.Owner };
        db.Memberships.Add(membership);
        logger.LogInformation("User {UserId} opened shop {ShopId}", user.Id, shop.Id);

        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, membership, cancellationToken));
    }

    public async Task<AuthResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Normalize(request.Phone)!;
        var user = await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        if (user is null)
        {
            user = new User { Phone = phone, FullName = request.FullName!.Trim(), PreferredLanguage = request.Language ?? "hi" };
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            db.Users.Add(user);
        }
        else if (await CheckPasswordAsync(user, request.Password!, cancellationToken) is { } error)
        {
            return AuthResult<AuthResponse>.Fail(error);
        }

        // A user token, no shop: the planner works on the person's own lists.
        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, membership: null, cancellationToken));
    }

    public async Task<AuthResult<AuthResponse>> SignInAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Normalize(request.Phone);
        var user = phone is null ? null : await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        if (user is not { IsActive: true } || string.IsNullOrEmpty(request.Password))
        {
            passwordHasher.VerifyHashedPassword(new User { Phone = "-", FullName = "-" }, DummyPasswordHash, request.Password ?? string.Empty);
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        // Staff at the counter type a short PIN instead of a password: it opens the shop it was set for.
        if (Pins.LooksLikePin(request.Password) && await SignInWithPinAsync(user, request.Password, cancellationToken) is { } byPin)
        {
            return byPin;
        }

        if (await CheckPasswordAsync(user, request.Password, cancellationToken) is { } error)
        {
            return AuthResult<AuthResponse>.Fail(error);
        }

        // Most shopkeepers have one shop: give them its token straight away, no picker.
        var memberships = await ListMembershipsAsync(user.Id, cancellationToken);
        Membership? only = null;
        if (memberships.Count == 1)
        {
            only = await FindUsableMembershipAsync(memberships[0].MembershipId, user.Id, cancellationToken);
        }

        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, only, cancellationToken));
    }

    /// <summary>
    /// Sign-in after an SMS code proved the phone: an existing account opens as with a password (its one shop straight
    /// away); a new number with a name becomes a household account with no password.
    /// </summary>
    public async Task<AuthResult<AuthResponse>> SignInWithVerifiedPhoneAsync(User? user, string phone, string? fullName, string language, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            user = new User { Phone = phone, FullName = fullName!, PreferredLanguage = language };
            db.Users.Add(user);
            logger.LogInformation("User {UserId} joined with a phone code", user.Id);
            return AuthResult<AuthResponse>.Ok(await IssueAsync(user, membership: null, cancellationToken));
        }

        if (!user.IsActive)
        {
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        if (user.LockoutEndsAt > clock.GetUtcNow())
        {
            return AuthResult<AuthResponse>.Fail(AuthError.LockedOut);
        }

        user.FailedSignInCount = 0;
        var memberships = await ListMembershipsAsync(user.Id, cancellationToken);
        var only = memberships.Count == 1 ? await FindUsableMembershipAsync(memberships[0].MembershipId, user.Id, cancellationToken) : null;
        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, only, cancellationToken));
    }

    public async Task<AuthResult<AuthResponse>> SelectShopAsync(Guid userId, SelectShopRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
        var membership = user is null ? null : await FindUsableMembershipAsync(request.MembershipId, userId, cancellationToken);
        if (user is null || membership is null)
        {
            return AuthResult<AuthResponse>.Fail(AuthError.MembershipNotFound);
        }

        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, membership, cancellationToken));
    }

    public async Task<AuthResult<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        var hash = Secrets.Sha256Hex(request.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = clock.GetUtcNow();
        if (stored is null)
        {
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        if (stored.RevokedAt is not null)
        {
            // A used or signed-out token came back: it may be stolen. End every sign-in of this user.
            await RevokeAllAsync(stored.UserId, now, cancellationToken);
            logger.LogWarning("Revoked refresh token reused; all sessions of user {UserId} revoked", stored.UserId);
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        if (stored.ExpiresAt <= now)
        {
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId && u.IsActive, cancellationToken);
        Membership? membership = null;
        if (stored.MembershipId is { } membershipId)
        {
            membership = await FindUsableMembershipAsync(membershipId, stored.UserId, cancellationToken);
        }

        if (user is null || (stored.MembershipId is not null && membership is null))
        {
            stored.RevokedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, membership, cancellationToken, replacing: stored));
    }

    public async Task SignOutAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var hash = Secrets.Sha256Hex(request.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<MeResponse?> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
        return user is null ? null : new MeResponse(await SummarizeAsync(user, cancellationToken), await ListMembershipsAsync(userId, cancellationToken), await ListBlockedAsync(userId, cancellationToken));
    }

    /// <summary>
    /// A PIN sign-in, when one of the person's memberships has a PIN: right → that shop's token; wrong → counted like a
    /// wrong password. Null when no membership has a PIN, so the input is tried as a password instead.
    /// </summary>
    private async Task<AuthResult<AuthResponse>?> SignInWithPinAsync(User user, string pin, CancellationToken cancellationToken)
    {
        var withPin = await (from m in db.Memberships
                             join s in db.Shops on m.ShopId equals s.Id
                             where m.UserId == user.Id && m.IsActive && s.IsActive && m.PinHash != null
                             select m).ToListAsync(cancellationToken);
        if (withPin.Count == 0)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (user.LockoutEndsAt > now)
        {
            return AuthResult<AuthResponse>.Fail(AuthError.LockedOut);
        }

        var match = withPin.FirstOrDefault(m => passwordHasher.VerifyHashedPassword(user, m.PinHash!, pin) != PasswordVerificationResult.Failed);
        if (match is null)
        {
            if (user.PasswordHash is not null)
            {
                return null; // an owner who also has a PIN somewhere may have typed a numeric password
            }

            user.FailedSignInCount++;
            if (user.FailedSignInCount >= Options.MaxFailedSignIns)
            {
                user.FailedSignInCount = 0;
                user.LockoutEndsAt = now.AddMinutes(Options.LockoutMinutes);
                logger.LogWarning("User {UserId} locked out after too many wrong PINs", user.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
        }

        user.FailedSignInCount = 0;
        user.LockoutEndsAt = null;
        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, match, cancellationToken));
    }

    /// <summary>Null when the password is right; counts wrong ones and locks the account after too many.</summary>
    private async Task<AuthError?> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (user.LockoutEndsAt > now)
        {
            return AuthError.LockedOut;
        }

        var result = user.PasswordHash is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedSignInCount++;
            if (user.FailedSignInCount >= Options.MaxFailedSignIns)
            {
                user.FailedSignInCount = 0;
                user.LockoutEndsAt = now.AddMinutes(Options.LockoutMinutes);
                logger.LogWarning("User {UserId} locked out after too many wrong passwords", user.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            return AuthError.InvalidCredentials;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        user.FailedSignInCount = 0;
        user.LockoutEndsAt = null;
        return null;
    }

    /// <summary>An active membership of this user in a shop that is still open.</summary>
    private Task<Membership?> FindUsableMembershipAsync(Guid membershipId, Guid userId, CancellationToken cancellationToken) =>
        (from m in db.Memberships
         join s in db.Shops on m.ShopId equals s.Id
         where m.Id == membershipId && m.UserId == userId && m.IsActive && s.IsActive
         select m).SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<MembershipSummary>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken) =>
        await (from m in db.Memberships.AsNoTracking()
               join s in db.Shops.AsNoTracking() on m.ShopId equals s.Id
               where m.UserId == userId && m.IsActive && s.IsActive
               orderby s.Name
               select new MembershipSummary(m.Id, s.Id, s.Name, s.Slug, m.Role))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<BlockedShop>> ListBlockedAsync(Guid userId, CancellationToken cancellationToken) =>
        await (from m in db.Memberships.AsNoTracking()
               join s in db.Shops.AsNoTracking() on m.ShopId equals s.Id
               where m.UserId == userId && m.IsActive && !s.IsActive && s.BlockedReason != null
               orderby s.Name
               select new BlockedShop(s.Name, s.BlockedReason!))
            .ToListAsync(cancellationToken);

    private async Task<UserSummary> SummarizeAsync(User user, CancellationToken cancellationToken) =>
        new(user.Id, user.FullName, user.PreferredLanguage, user.Phone, await db.PlatformAdmins.AnyAsync(a => a.UserId == user.Id, cancellationToken));

    /// <summary>Creates an access token and a new refresh token, revoking <paramref name="replacing"/> if given, and saves.</summary>
    private async Task<AuthResponse> IssueAsync(
        User user, Membership? membership, CancellationToken cancellationToken, RefreshToken? replacing = null)
    {
        var now = clock.GetUtcNow();
        var summary = await SummarizeAsync(user, cancellationToken);
        var access = tokens.Issue(new AccessTokenSubject(user.Id, membership?.ShopId, membership?.Id, membership?.Role, summary.IsPlatformAdmin));

        var refreshValue = Secrets.NewRefreshToken();
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            MembershipId = membership?.Id,
            TokenHash = Secrets.Sha256Hex(refreshValue),
            ExpiresAt = now.AddDays(Options.RefreshTokenDays),
        };
        db.RefreshTokens.Add(refresh);

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedById = refresh.Id;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            access.Token,
            access.ExpiresAt,
            refreshValue,
            refresh.ExpiresAt,
            summary,
            await ListMembershipsAsync(user.Id, cancellationToken),
            membership?.ShopId,
            membership?.Role,
            await ListBlockedAsync(user.Id, cancellationToken));
    }

    private async Task RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    /// <summary>"Sharma General Store" → "sharma-general-store-k3f9". The suffix keeps names unique without a lookup.</summary>
    private static string NewSlug(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var stem = builder.ToString().Trim('-');
        if (stem.Length > 60)
        {
            stem = stem[..60].TrimEnd('-');
        }

        var suffix = Guid.NewGuid().ToString("N")[..4];
        return stem.Length == 0 ? $"shop-{suffix}" : $"{stem}-{suffix}";
    }
}
