using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>A person with platform-admin (super-admin) access: sees every shop, approves, blocks and helps them.</summary>
public sealed class PlatformAdmin : Entity
{
    public Guid UserId { get; set; }
}

/// <summary>
/// Platform-admin access: the token says platform_admin and the person is still in platform_admins, so removing
/// someone takes effect at once rather than when their token expires. (Same pattern as SchoolErp.)
/// </summary>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement;

public sealed class PlatformAdminHandler(AppDbContext db) : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        if (context.User.FindFirst(AppClaims.PlatformAdmin)?.Value != "true" || context.User.UserId() is not { } userId)
        {
            return;
        }

        if (await db.PlatformAdmins.AnyAsync(a => a.UserId == userId))
        {
            context.Succeed(requirement);
        }
    }
}

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
        var group = app.MapGroup("/api/v1/platform").WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin);
        group.MapGet("/overview", OverviewAsync);
        group.MapGet("/shops", ListAsync);
        group.MapGet("/shops/{id:guid}", GetAsync);
        group.MapPost("/shops/{id:guid}/approve", ApproveAsync);
        group.MapPost("/shops/{id:guid}/block", BlockAsync);
        group.MapPost("/shops/{id:guid}/unblock", UnblockAsync);
        group.MapPut("/shops/{id:guid}/note", NoteAsync);
        group.MapPost("/users/{userId:guid}/unlock", UnlockAsync);
        return app;
    }

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
