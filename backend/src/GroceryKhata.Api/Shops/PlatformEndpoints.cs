using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>A person with platform-admin access: sees every shop and, by role, approves, blocks or helps them.</summary>
public sealed class PlatformAdmin : Entity
{
    public Guid UserId { get; set; }

    public AdminRole Role { get; set; } = AdminRole.Super;

    /// <summary>Who gave this access (null: from the server's settings at start-up).</summary>
    public Guid? AddedByUserId { get; set; }
}

/// <summary>What a platform admin may do. Everyone sees the shops; the rest depends on the role.</summary>
public enum AdminRole
{
    /// <summary>Everything, including adding, changing and removing admins.</summary>
    Super,

    /// <summary>Approve, block and unblock shops.</summary>
    Approver,

    /// <summary>Help shops: support notes and unlocking people locked out by wrong passwords.</summary>
    Support,
}

/// <summary>
/// Platform-admin access: the token says platform_admin and the person is still in platform_admins with one of the
/// allowed roles, checked on every request, so removing someone or changing their role works at once rather than
/// when their token expires. (Same pattern as SchoolErp.)
/// </summary>
public sealed class PlatformAdminRequirement(params AdminRole[] roles) : IAuthorizationRequirement
{
    public IReadOnlyList<AdminRole> Roles { get; } = roles.Length == 0 ? Enum.GetValues<AdminRole>() : roles;
}

public sealed class PlatformAdminHandler(AppDbContext db) : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        if (context.User.FindFirst(AppClaims.PlatformAdmin)?.Value != "true" || context.User.UserId() is not { } userId)
        {
            return;
        }

        var role = await db.PlatformAdmins.AsNoTracking().Where(a => a.UserId == userId).Select(a => (AdminRole?)a.Role).SingleOrDefaultAsync();
        if (role is { } r && requirement.Roles.Contains(r))
        {
            context.Succeed(requirement);
        }
    }
}

public sealed record AdminDto(Guid UserId, string Name, string Phone, AdminRole Role, DateTimeOffset AddedAt, string? AddedBy);

public sealed record AddAdminRequest(string? Phone, AdminRole? Role);

public sealed record AdminRoleRequest(AdminRole? Role);

public sealed record PlatformOverview(int Shops, int Active, int Blocked, int AwaitingApproval, int NewThisWeek, int Households);

public sealed record PlatformShopRow(
    Guid Id, string Name, string? Area, string Slug, DateTimeOffset CreatedAt, DateTimeOffset? ApprovedAt, bool IsActive,
    string? BlockedReason, string? OwnerName, string? OwnerPhone);

public sealed record PlatformPerson(Guid UserId, Guid MembershipId, string Name, string Phone, ShopRole Role, bool IsActive, bool LockedOut);

public sealed record PlatformShopDetail(
    PlatformShopRow Shop, string? Address, string? AdminNote, IReadOnlyList<PlatformPerson> People,
    int Customers, int BillsLast30Days, DateTimeOffset? LastBillAt, BalanceCheckDto? LastBalanceCheck);

public sealed record BlockRequest(string? Reason);

public sealed record NoteRequest(string? Note);

/// <summary>
/// The super-admin's view of every shop: an overview, a searchable list, one shop's people and activity, approve,
/// block or unblock (blocking signs everyone in that shop out on their next tap), a private support note, and
/// unlocking a person locked out by wrong passwords. Runs without a shop: shop-owned tables are only read inside a
/// scope working as that one shop, so row-level security still applies.
/// </summary>
public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatform(this IEndpointRouteBuilder app)
    {
        // Every admin sees the shops; approving and blocking is for approvers; notes and unlocking for support; managing
        // admins for super admins only. A super admin can do it all.
        var group = app.MapGroup("/api/v1/platform").WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin);
        group.MapGet("/overview", OverviewAsync);
        group.MapGet("/shops", ListAsync);
        group.MapGet("/shops/{id:guid}", GetAsync);
        group.MapPost("/shops/{id:guid}/approve", ApproveAsync).RequireAuthorization(AppPolicies.PlatformApprover);
        group.MapPost("/shops/{id:guid}/block", BlockAsync).RequireAuthorization(AppPolicies.PlatformApprover);
        group.MapPost("/shops/{id:guid}/unblock", UnblockAsync).RequireAuthorization(AppPolicies.PlatformApprover);
        group.MapPut("/shops/{id:guid}/note", NoteAsync).RequireAuthorization(AppPolicies.PlatformSupport);
        group.MapPost("/users/{userId:guid}/unlock", UnlockAsync).RequireAuthorization(AppPolicies.PlatformSupport);
        group.MapGet("/admins", ListAdminsAsync).RequireAuthorization(AppPolicies.PlatformSuper);
        group.MapPost("/admins", AddAdminAsync).RequireAuthorization(AppPolicies.PlatformSuper);
        group.MapPut("/admins/{userId:guid}", ChangeAdminAsync).RequireAuthorization(AppPolicies.PlatformSuper);
        group.MapDelete("/admins/{userId:guid}", RemoveAdminAsync).RequireAuthorization(AppPolicies.PlatformSuper);
        return app;
    }

    private static async Task<IReadOnlyList<AdminDto>> ListAdminsAsync(AppDbContext db, CancellationToken ct) =>
        await (from a in db.PlatformAdmins.AsNoTracking()
               join u in db.Users.AsNoTracking() on a.UserId equals u.Id
               join adder in db.Users.AsNoTracking() on a.AddedByUserId equals (Guid?)adder.Id into adders
               from b in adders.DefaultIfEmpty()
               orderby a.Role, u.FullName
               select new AdminDto(u.Id, u.FullName, u.Phone, a.Role, a.CreatedAt, b == null ? null : b.FullName))
            .ToListAsync(ct);

    /// <summary>
    /// Gives a person admin access by their phone number. They need an account, and (outside Development) a number an
    /// SMS code has proved: otherwise someone who signed up first with that number would get the access.
    /// </summary>
    private static async Task<IResult> AddAdminAsync(
        AddAdminRequest request, AppDbContext db, ClaimsPrincipal admin, IHostEnvironment env, ILogger<PlatformAdmin> logger, CancellationToken ct)
    {
        var phone = PhoneNumber.Normalize(request.Phone);
        if (phone is null || request.Role is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["Enter their mobile number and choose a role."] });
        }

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Phone == phone && u.IsActive, ct);
        if (user is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["No account with this number. Ask them to sign up first."] });
        }

        if (user.PhoneVerifiedAt is null && !env.IsDevelopment())
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["Ask them to sign in once with an SMS code first, so we know the number is theirs."] });
        }

        if (await db.PlatformAdmins.AnyAsync(a => a.UserId == user.Id, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "They are already an admin. Change their role instead.");
        }

        db.PlatformAdmins.Add(new PlatformAdmin { UserId = user.Id, Role = request.Role.Value, AddedByUserId = admin.UserId() });
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Admin {AdminId} made user {UserId} a platform admin ({Role})", admin.UserId(), user.Id, request.Role);
        return TypedResults.Ok(await ListAdminsAsync(db, ct));
    }

    private static async Task<IResult> ChangeAdminAsync(
        Guid userId, AdminRoleRequest request, AppDbContext db, ClaimsPrincipal admin, ILogger<PlatformAdmin> logger, CancellationToken ct)
    {
        if (request.Role is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Role"] = ["Choose a role."] });
        }

        var row = await db.PlatformAdmins.SingleOrDefaultAsync(a => a.UserId == userId, ct);
        if (row is null)
        {
            return TypedResults.NotFound();
        }

        if (row.Role == AdminRole.Super && request.Role != AdminRole.Super && await LastSuperAsync(db, userId, ct))
        {
            return LastSuper();
        }

        row.Role = request.Role.Value;
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Admin {AdminId} changed user {UserId} to {Role}", admin.UserId(), userId, request.Role);
        return TypedResults.Ok(await ListAdminsAsync(db, ct));
    }

    /// <summary>Takes the access away at once (the next request is refused). The last super admin can't be removed.</summary>
    private static async Task<IResult> RemoveAdminAsync(Guid userId, AppDbContext db, ClaimsPrincipal admin, ILogger<PlatformAdmin> logger, CancellationToken ct)
    {
        var row = await db.PlatformAdmins.SingleOrDefaultAsync(a => a.UserId == userId, ct);
        if (row is null)
        {
            return TypedResults.NotFound();
        }

        if (row.Role == AdminRole.Super && await LastSuperAsync(db, userId, ct))
        {
            return LastSuper();
        }

        db.PlatformAdmins.Remove(row);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Admin {AdminId} removed platform admin {UserId}", admin.UserId(), userId);
        return TypedResults.Ok(await ListAdminsAsync(db, ct));
    }

    private static async Task<bool> LastSuperAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        !await db.PlatformAdmins.AnyAsync(a => a.Role == AdminRole.Super && a.UserId != userId, ct);

    private static IResult LastSuper() =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "There must always be one super admin. Make someone else super admin first.");

    private static async Task<PlatformOverview> OverviewAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var weekAgo = clock.GetUtcNow().AddDays(-7);
        var shops = db.Shops.AsNoTracking();
        return new PlatformOverview(
            await shops.CountAsync(ct),
            await shops.CountAsync(s => s.IsActive, ct),
            await shops.CountAsync(s => !s.IsActive, ct),
            await shops.CountAsync(s => s.IsActive && s.ApprovedAt == null, ct),
            await shops.CountAsync(s => s.CreatedAt >= weekAgo, ct),
            await db.Users.CountAsync(u => !db.Memberships.Any(m => m.UserId == u.Id), ct));
    }

    /// <summary>Shops by newest; <paramref name="status"/> = new | active | blocked; <paramref name="q"/> matches name, area or owner phone.</summary>
    private static async Task<IReadOnlyList<PlatformShopRow>> ListAsync(AppDbContext db, string? q, string? status, CancellationToken ct)
    {
        var shops = db.Shops.AsNoTracking();
        shops = status switch
        {
            "new" => shops.Where(s => s.IsActive && s.ApprovedAt == null),
            "active" => shops.Where(s => s.IsActive),
            "blocked" => shops.Where(s => !s.IsActive),
            _ => shops,
        };
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            var digits = new string(q.Where(char.IsAsciiDigit).ToArray());
            shops = shops.Where(s => EF.Functions.ILike(s.Name, term) || (s.Area != null && EF.Functions.ILike(s.Area, term))
                || (digits.Length >= 4 && db.Memberships.Any(m => m.ShopId == s.Id && db.Users.Any(u => u.Id == m.UserId && u.Phone.Contains(digits)))));
        }

        return await Rows(db, shops.OrderByDescending(s => s.CreatedAt).Take(200)).ToListAsync(ct);
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, IServiceScopeFactory scopes, TimeProvider clock, ClaimsPrincipal admin, CancellationToken ct)
    {
        var row = await Rows(db, db.Shops.Where(s => s.Id == id)).SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return TypedResults.NotFound();
        }

        var shop = await db.Shops.AsNoTracking().SingleAsync(s => s.Id == id, ct);
        var now = clock.GetUtcNow();
        var people = await (from m in db.Memberships.AsNoTracking()
                            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                            where m.ShopId == id
                            orderby m.Role, u.FullName
                            select new PlatformPerson(u.Id, m.Id, u.FullName, u.Phone, m.Role, m.IsActive && u.IsActive, u.LockoutEndsAt > now))
            .ToListAsync(ct);

        // Shop-owned numbers: read as that shop, in its own scope.
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(id);
        var shopDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var monthAgo = now.AddDays(-30);
        var check = await shopDb.BalanceChecks.AsNoTracking().OrderByDescending(c => c.CheckedAt).FirstOrDefaultAsync(ct);
        // Logged admin access (project plan): every look at a shop leaves a row in that shop's own audit log.
        shopDb.AuditEntries.Add(Audit(id, admin, "view", "{}"));
        await shopDb.SaveChangesAsync(ct);
        return TypedResults.Ok(new PlatformShopDetail(
            row, shop.Address, shop.AdminNote, people,
            await shopDb.Customers.CountAsync(ct),
            await shopDb.Bills.CountAsync(b => b.CreatedAt >= monthAgo, ct),
            await shopDb.Bills.MaxAsync(b => (DateTimeOffset?)b.CreatedAt, ct),
            check is null ? null : BalanceChecks.ToDto(check)));
    }

    private static Task<IResult> ApproveAsync(Guid id, AppDbContext db, TimeProvider clock, ClaimsPrincipal user, IServiceScopeFactory scopes, ILogger<PlatformAdmin> logger, CancellationToken ct) =>
        ChangeAsync(id, db, ct, shop =>
        {
            shop.ApprovedAt ??= clock.GetUtcNow();
            logger.LogInformation("Admin {UserId} approved shop {ShopId}", user.UserId(), id);
        }, scopes, user, "approve");

    private static async Task<IResult> BlockAsync(Guid id, BlockRequest request, AppDbContext db, ClaimsPrincipal user, IServiceScopeFactory scopes, ILogger<PlatformAdmin> logger, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (reason is not { Length: >= 3 and <= 300 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Say why the shop is blocked (shown to its owner)."] });
        }

        return await ChangeAsync(id, db, ct, shop =>
        {
            shop.IsActive = false;
            shop.BlockedReason = reason;
            logger.LogWarning("Admin {UserId} blocked shop {ShopId}", user.UserId(), id);
        }, scopes, user, "block");
    }

    private static Task<IResult> UnblockAsync(Guid id, AppDbContext db, ClaimsPrincipal user, IServiceScopeFactory scopes, ILogger<PlatformAdmin> logger, CancellationToken ct) =>
        ChangeAsync(id, db, ct, shop =>
        {
            shop.IsActive = true;
            shop.BlockedReason = null;
            logger.LogInformation("Admin {UserId} unblocked shop {ShopId}", user.UserId(), id);
        }, scopes, user, "unblock");

    private static Task<IResult> NoteAsync(Guid id, NoteRequest request, AppDbContext db, CancellationToken ct) =>
        ChangeAsync(id, db, ct, shop => shop.AdminNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()[..Math.Min(request.Note.Trim().Length, 2000)]);

    /// <summary>Support: lifts a lockout from too many wrong passwords or PINs.</summary>
    private static async Task<IResult> UnlockAsync(Guid userId, AppDbContext db, ClaimsPrincipal admin, ILogger<PlatformAdmin> logger, CancellationToken ct)
    {
        var person = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (person is null)
        {
            return TypedResults.NotFound();
        }

        person.LockoutEndsAt = null;
        person.FailedSignInCount = 0;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Admin {AdminId} unlocked user {UserId}", admin.UserId(), userId);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ChangeAsync(
        Guid id, AppDbContext db, CancellationToken ct, Action<Shop> change, IServiceScopeFactory? scopes = null, ClaimsPrincipal? admin = null, string? action = null)
    {
        var shop = await db.Shops.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (shop is null)
        {
            return TypedResults.NotFound();
        }

        change(shop);
        await db.SaveChangesAsync(ct);
        if (scopes is not null && admin is not null && action is not null)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(id);
            var shopDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            shopDb.AuditEntries.Add(Audit(id, admin, action, System.Text.Json.JsonSerializer.Serialize(new { reason = shop.BlockedReason })));
            await shopDb.SaveChangesAsync(ct);
        }
        return TypedResults.Ok(await Rows(db, db.Shops.Where(s => s.Id == id)).SingleAsync(ct));
    }

    private static AuditEntry Audit(Guid shopId, ClaimsPrincipal admin, string action, string changes) =>
        new() { UserId = admin.UserId()!.Value, Table = "platform_admin", RecordId = shopId, Action = action, Changes = changes };

    private static IQueryable<PlatformShopRow> Rows(AppDbContext db, IQueryable<Shop> shops) =>
        from s in shops.AsNoTracking()
        let owner = (from m in db.Memberships
                     join u in db.Users on m.UserId equals u.Id
                     where m.ShopId == s.Id && m.Role == ShopRole.Owner
                     orderby m.CreatedAt
                     select u).FirstOrDefault()
        select new PlatformShopRow(s.Id, s.Name, s.Area, s.Slug, s.CreatedAt, s.ApprovedAt, s.IsActive, s.BlockedReason,
            owner == null ? null : owner.FullName, owner == null ? null : owner.Phone);
}

/// <summary>The "Platform" config section. <see cref="AdminPhones"/>: numbers made platform admins at start-up (they must have an account).</summary>
public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    public string[] AdminPhones { get; set; } = [];

    /// <summary>
    /// True: households don't find a new shop, or send it lists, until a platform admin approves it. The shop itself
    /// works from the first minute (billing, khata), so holding it never stops a shopkeeper's day.
    /// </summary>
    public bool HoldNewShops { get; set; }
}

/// <summary>
/// At start-up, adds the configured phones to platform_admins. Only accounts whose number an SMS code has proved (in
/// Development, any), so nobody can claim admin by signing up with the admin's number first. Removing a phone from
/// config doesn't remove anyone.
/// </summary>
public sealed class PlatformAdminSeeder(IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptions<PlatformOptions> options, IHostEnvironment env, ILogger<PlatformAdminSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var phones = options.Value.AdminPhones.Select(PhoneNumber.Normalize).OfType<string>().ToList();
        if (phones.Count == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var anyAccount = env.IsDevelopment();
        var users = await db.Users
            .Where(u => phones.Contains(u.Phone) && (anyAccount || u.PhoneVerifiedAt != null) && !db.PlatformAdmins.Any(a => a.UserId == u.Id))
            .Select(u => u.Id).ToListAsync(cancellationToken);
        foreach (var userId in users)
        {
            db.PlatformAdmins.Add(new PlatformAdmin { UserId = userId });
            logger.LogInformation("User {UserId} made a platform admin from config", userId);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
