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

        var shop = new Shop { Name = request.ShopName!.Trim(), Slug = NewSlug(request.ShopName) };
        db.Shops.Add(shop);
        var membership = new Membership { UserId = user.Id, ShopId = shop.Id, Role = ShopRole.Owner };
        db.Memberships.Add(membership);
        logger.LogInformation("User {UserId} opened shop {ShopId}", user.Id, shop.Id);

        return AuthResult<AuthResponse>.Ok(await IssueAsync(user, membership, cancellationToken));
    }

    public async Task<AuthResult<AuthResponse>> SignInAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumber.Normalize(request.Phone);
        var user = phone is null ? null : await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        if (user is not { IsActive: true, PasswordHash: not null } || string.IsNullOrEmpty(request.Password))
        {
            passwordHasher.VerifyHashedPassword(new User { Phone = "-", FullName = "-" }, DummyPasswordHash, request.Password ?? string.Empty);
            return AuthResult<AuthResponse>.Fail(AuthError.InvalidCredentials);
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
        return user is null ? null : new MeResponse(Summarize(user), await ListMembershipsAsync(userId, cancellationToken));
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

    private static UserSummary Summarize(User user) => new(user.Id, user.FullName, user.PreferredLanguage);

    /// <summary>Creates an access token and a new refresh token, revoking <paramref name="replacing"/> if given, and saves.</summary>
    private async Task<AuthResponse> IssueAsync(
        User user, Membership? membership, CancellationToken cancellationToken, RefreshToken? replacing = null)
    {
        var now = clock.GetUtcNow();
        var access = tokens.Issue(new AccessTokenSubject(user.Id, membership?.ShopId, membership?.Id, membership?.Role));

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
            Summarize(user),
            await ListMembershipsAsync(user.Id, cancellationToken),
            membership?.ShopId,
            membership?.Role);
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
