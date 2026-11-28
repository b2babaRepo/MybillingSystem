using System.Security.Claims;
using System.Security.Cryptography;
using GroceryKhata.Api.Agents;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Shops;

/// <summary>One role a person holds: in a shop (owner, staff, accountant), on the platform (admin), as a field agent, or none (customer).</summary>
public sealed record AccountRole(string Kind, string? Detail, Guid? ShopId = null, bool Active = true);

public sealed record AccountRow(
    Guid Id, string FullName, string Phone, string? Email, bool EmailVerified, bool PhoneVerified, bool IsActive, bool Locked,
    DateTimeOffset CreatedAt, DateTimeOffset? LastSignInAt, IReadOnlyList<AccountRole> Roles);

public sealed record AccountPage(IReadOnlyList<AccountRow> Items, int Total, int Page, int PageSize);

public sealed record AccountDetail(AccountRow Account, string PreferredLanguage, bool HasPassword, int ActiveSessions, string? Address, string? City, string? Pincode);

public sealed record AccountEdit(string? FullName, string? Email, string? PreferredLanguage);

public sealed record AccountReason(string? Reason);

/// <summary>
/// Every login account on the platform, with each person's roles gathered from shops, admins and field agents.
/// Every admin can look; support and super admins edit, suspend, sign out and send reset codes; only super admins remove.
/// Passwords are never shown or set here: a reset code goes to the person instead.
/// </summary>
public static class PlatformUsers
{
    private const int PageSize = 50;

    public static IEndpointRouteBuilder MapPlatformUsers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/platform/users").WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin).RecordsActivity();
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", EditAsync).RequireAuthorization(AppPolicies.PlatformSupport);
        group.MapPost("/{id:guid}/suspend", SuspendAsync).RequireAuthorization(AppPolicies.PlatformSupport);
        group.MapPost("/{id:guid}/activate", ActivateAsync).RequireAuthorization(AppPolicies.PlatformSupport);
        group.MapPost("/{id:guid}/sign-out", SignOutAsync).RequireAuthorization(AppPolicies.PlatformSupport);
        group.MapPost("/{id:guid}/send-reset", SendResetAsync).RequireAuthorization(AppPolicies.PlatformSupport);
        group.MapDelete("/{id:guid}", RemoveAsync).RequireAuthorization(AppPolicies.PlatformRemoveAccounts);
        return app;
    }

    private static async Task<AccountPage> ListAsync(string? q, string? role, string? status, int? page, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            var digits = new string(q.Where(char.IsAsciiDigit).ToArray());
            users = users.Where(u => EF.Functions.ILike(u.FullName, term) || (u.Email != null && EF.Functions.ILike(u.Email, term)) || (digits.Length >= 3 && u.Phone.Contains(digits)));
        }

        users = role switch
        {
            "owner" => users.Where(u => db.Memberships.Any(m => m.UserId == u.Id && m.Role == ShopRole.Owner)),
            "staff" => users.Where(u => db.Memberships.Any(m => m.UserId == u.Id && m.Role == ShopRole.Staff)),
            "accountant" => users.Where(u => db.Memberships.Any(m => m.UserId == u.Id && m.Role == ShopRole.Accountant)),
            "admin" => users.Where(u => db.PlatformAdmins.Any(a => a.UserId == u.Id)),
            "agent" => users.Where(u => db.FieldAgents.Any(a => a.UserId == u.Id)),
            "customer" => users.Where(u => !db.Memberships.Any(m => m.UserId == u.Id) && !db.PlatformAdmins.Any(a => a.UserId == u.Id) && !db.FieldAgents.Any(a => a.UserId == u.Id)),
            _ => users,
        };
        users = status switch
        {
            "active" => users.Where(u => u.IsActive),
            "suspended" => users.Where(u => !u.IsActive),
            _ => users,
        };

        var number = Math.Max(1, page ?? 1);
        var total = await users.CountAsync(ct);
        var found = await users.OrderByDescending(u => u.CreatedAt).Skip((number - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new AccountPage(await RowsAsync(found, db, clock, ct), total, number, PageSize);
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow();
        var sessions = await db.RefreshTokens.CountAsync(t => t.UserId == id && t.RevokedAt == null && t.ExpiresAt > now, ct);
        var row = (await RowsAsync([user], db, clock, ct))[0];
        return TypedResults.Ok(new AccountDetail(row, user.PreferredLanguage, user.PasswordHash is not null, sessions, user.Address, user.City, user.Pincode));
    }

    /// <summary>Name, email and language. A new email isn't verified: the person proves it in their profile. The phone is the login and changes only by its owner with a code.</summary>
    private static async Task<IResult> EditAsync(Guid id, AccountEdit request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var name = request.FullName?.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : EmailCodes.Normalize(request.Email);
        var errors = new Dictionary<string, string[]>();
        if (name is not { Length: >= 2 and <= 120 }) errors["FullName"] = ["Enter a name."];
        if (email is not null && (email.Length > 160 || !email.Contains('@') || email.Contains(' '))) errors["Email"] = ["Enter a valid email."];
        if (request.PreferredLanguage is not (null or "en" or "hi" or "gu" or "hn")) errors["PreferredLanguage"] = ["Language must be en, hi, gu or hn."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            (user.Email, user.EmailVerifiedAt) = (email, null);
        }

        user.FullName = name!;
        user.PreferredLanguage = request.PreferredLanguage ?? user.PreferredLanguage;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, clock, ct);
    }

    /// <summary>Switches the account off: no sign-in, every session ends. Shops and records stay. Admins lose their admin role first.</summary>
    private static async Task<IResult> SuspendAsync(Guid id, AccountReason request, ClaimsPrincipal admin, AppDbContext db, TimeProvider clock, ILogger<PlatformAdmin> logger, CancellationToken ct)
    {
        if (await GuardAsync(id, admin, db, ct) is { } refused)
        {
            return refused;
        }

        var now = clock.GetUtcNow();
        if (await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false), ct) == 0)
        {
            return TypedResults.NotFound();
        }

        await RevokeAsync(db, id, now, ct);
        logger.LogInformation("Admin {AdminId} suspended user {UserId}: {Reason}", admin.UserId(), id, request.Reason);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ActivateAsync(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true), ct) == 0 ? TypedResults.NotFound() : TypedResults.NoContent();

    private static async Task<IResult> SignOutAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        await RevokeAsync(db, id, clock.GetUtcNow(), ct);
        return TypedResults.NoContent();
    }

    /// <summary>Sends the person a reset code to their verified email; the admin never sees or sets the password.</summary>
    private static async Task<IResult> SendResetAsync(
        Guid id, AppDbContext db, IServiceProvider services, IOptions<EmailOptions> options, TimeProvider clock, ILogger<EmailCode> logger, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (user.EmailVerifiedAt is null || user.Email is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "no-verified-email", detail: "This account has no verified email. Ask them to use 'Forgot password' with an SMS code.");
        }

        return await EmailCodes.SendCodeAsync(db, services, options.Value, clock, logger, user, user.Email, EmailCodes.Reset, ct)
            ?? TypedResults.NoContent();
    }

    /// <summary>
    /// Removes a person: switched off and their personal details wiped (name, phone, email, address). Bills and khata
    /// entries they made stay with the shop (GST law keeps them for six years), now under "Removed user".
    /// An owner of a working shop can't be removed: block or hand over the shop first.
    /// </summary>
    private static async Task<IResult> RemoveAsync(Guid id, ClaimsPrincipal admin, AppDbContext db, TimeProvider clock, ILogger<PlatformAdmin> logger, CancellationToken ct)
    {
        if (await GuardAsync(id, admin, db, ct) is { } refused)
        {
            return refused;
        }

        if (await db.Memberships.AnyAsync(m => m.UserId == id && m.IsActive && m.Role == ShopRole.Owner && db.Shops.IgnoreQueryFilters().Any(s => s.Id == m.ShopId && s.IsActive), ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "owns-active-shop", detail: "This person owns a working shop. Block the shop or hand it to another owner first.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow();
        user.FullName = "Removed user";
        user.Phone = "+0" + string.Concat(Enumerable.Range(0, 14).Select(_ => (char)('0' + RandomNumberGenerator.GetInt32(10))));
        (user.Email, user.EmailVerifiedAt, user.Address, user.City, user.Pincode, user.Gstin) = (null, null, null, null, null, null);
        (user.PasswordHash, user.PhoneVerifiedAt, user.IsActive, user.OrderWhatsApp, user.AiConsentAt) = (null, null, false, false, null);
        await db.SaveChangesAsync(ct);
        await db.Memberships.Where(m => m.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.IsActive, false).SetProperty(m => m.PinHash, (string?)null), ct);
        await RevokeAsync(db, id, now, ct);
        logger.LogInformation("Admin {AdminId} removed user {UserId}", admin.UserId(), id);
        return TypedResults.NoContent();
    }

    /// <summary>Not yourself, and not a platform admin (take their admin role away first, on the admins page).</summary>
    private static async Task<IResult?> GuardAsync(Guid id, ClaimsPrincipal admin, AppDbContext db, CancellationToken ct)
    {
        if (id == admin.UserId())
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "self", detail: "You can't do this to your own account.");
        }

        return await db.PlatformAdmins.AnyAsync(a => a.UserId == id, ct)
            ? TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "is-admin", detail: "This person is a platform admin. Remove their admin role first.")
            : null;
    }

    private static Task RevokeAsync(AppDbContext db, Guid id, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    private static async Task<List<AccountRow>> RowsAsync(IReadOnlyList<User> users, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();
        var memberships = await (from m in db.Memberships.AsNoTracking()
                                 join s in db.Shops.IgnoreQueryFilters().AsNoTracking() on m.ShopId equals s.Id
                                 where ids.Contains(m.UserId)
                                 select new { m.UserId, m.Role, m.IsActive, s.Id, s.Name }).ToListAsync(ct);
        var admins = await db.PlatformAdmins.AsNoTracking().Where(a => ids.Contains(a.UserId)).Select(a => new { a.UserId, a.Role }).ToListAsync(ct);
        var agents = await db.FieldAgents.AsNoTracking().Where(a => ids.Contains(a.UserId)).Select(a => new { a.UserId, a.Status, a.Code }).ToListAsync(ct);
        var lastSignIn = await db.RefreshTokens.AsNoTracking().Where(t => ids.Contains(t.UserId)).GroupBy(t => t.UserId)
            .Select(g => new { g.Key, Last = g.Max(t => t.CreatedAt) }).ToDictionaryAsync(g => g.Key, g => g.Last, ct);
        var now = clock.GetUtcNow();
        return users.Select(u =>
        {
            var roles = new List<AccountRole>();
            roles.AddRange(admins.Where(a => a.UserId == u.Id).Select(a => new AccountRole("admin", a.Role.ToString())));
            roles.AddRange(agents.Where(a => a.UserId == u.Id).Select(a => new AccountRole("agent", a.Code ?? a.Status.ToString().ToLowerInvariant(), Active: a.Status == FieldAgentStatus.Approved)));
            roles.AddRange(memberships.Where(m => m.UserId == u.Id).Select(m => new AccountRole(m.Role.ToString().ToLowerInvariant(), m.Name, m.Id, m.IsActive)));
            if (roles.Count == 0)
            {
                roles.Add(new AccountRole("customer", null));
            }

            return new AccountRow(u.Id, u.FullName, u.Phone, u.Email, u.EmailVerifiedAt is not null, u.PhoneVerifiedAt is not null, u.IsActive,
                u.LockoutEndsAt > now, u.CreatedAt, lastSignIn.TryGetValue(u.Id, out var at) ? at : null, roles);
        }).ToList();
    }
}
