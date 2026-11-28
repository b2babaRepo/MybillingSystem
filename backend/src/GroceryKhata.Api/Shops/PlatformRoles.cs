using System.Security.Claims;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>
/// What a platform admin may do, one code each. The five built-in roles have fixed sets (<see cref="BuiltIn"/>);
/// a super admin can make custom roles from any of these except <see cref="AdminsManage"/>, which stays super-only.
/// </summary>
public static class Permissions
{
    public const string ShopsApprove = "shops.approve";
    public const string ShopsOpen = "shops.open";
    public const string ShopsReview = "shops.review";
    public const string Support = "support";
    public const string Billing = "billing";
    public const string AgentsManage = "agents.manage";
    public const string AgentsMoney = "agents.money";
    public const string Catalogue = "catalogue";
    public const string AccountsRemove = "accounts.remove";
    public const string AdminsManage = "admins.manage";

    /// <summary>In the order the grid shows them.</summary>
    public static readonly string[] All = [ShopsApprove, ShopsOpen, ShopsReview, Support, Billing, AgentsManage, AgentsMoney, Catalogue, AccountsRemove, AdminsManage];

    /// <summary>What a custom role may be given: everything but managing admins and roles.</summary>
    public static readonly string[] Grantable = All.Where(p => p != AdminsManage).ToArray();

    /// <summary>The built-in roles, exactly as they worked before roles became editable.</summary>
    public static readonly IReadOnlyDictionary<AdminRole, string[]> BuiltIn = new Dictionary<AdminRole, string[]>
    {
        [AdminRole.Super] = All,
        [AdminRole.Approver] = [ShopsApprove, ShopsOpen, ShopsReview, Catalogue],
        [AdminRole.Support] = [Support],
        [AdminRole.Billing] = [Billing, ShopsOpen, AgentsMoney, Catalogue],
        [AdminRole.AgentManager] = [AgentsManage, AgentsMoney, ShopsReview, Catalogue],
        [AdminRole.Custom] = [],
    };

    /// <summary>The signed-in admin's permissions, read fresh from the database (null: not an admin).</summary>
    public static async Task<string[]?> ForUserAsync(AppDbContext db, Guid userId, CancellationToken ct = default)
    {
        var admin = await db.PlatformAdmins.AsNoTracking().Where(a => a.UserId == userId).Select(a => new { a.Role, a.CustomRoleId }).SingleOrDefaultAsync(ct);
        if (admin is null)
        {
            return null;
        }

        if (admin.Role == AdminRole.Custom && admin.CustomRoleId is { } roleId)
        {
            return await db.PlatformRoles.AsNoTracking().Where(r => r.Id == roleId).Select(r => r.Permissions).SingleOrDefaultAsync(ct) ?? [];
        }

        return BuiltIn[admin.Role];
    }
}

/// <summary>A role a super admin made: a name and a set of <see cref="Permissions"/>.</summary>
public sealed class PlatformRole : Common.Entity
{
    public required string Name { get; set; }

    public string[] Permissions { get; set; } = [];
}

public sealed record RoleRequest(string? Name, string[]? Permissions);

public sealed record BuiltInRoleDto(AdminRole Key, string[] Permissions, int Admins);

public sealed record CustomRoleDto(Guid Id, string Name, string[] Permissions, int Admins);

public sealed record RolesDto(string[] Permissions, string[] Grantable, IReadOnlyList<BuiltInRoleDto> BuiltIn, IReadOnlyList<CustomRoleDto> Custom);

public sealed record MyPermissionsDto(AdminRole Role, string? CustomRoleName, string[] Permissions);

public static class PlatformRoles
{
    public static IEndpointRouteBuilder MapPlatformRoles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/platform/me", MeAsync).WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin);
        var group = app.MapGroup("/api/v1/platform/roles").WithTags("Platform").RequireAuthorization(AppPolicies.PlatformSuper).RecordsActivity();
        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return app;
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var admin = await db.PlatformAdmins.AsNoTracking().Where(a => a.UserId == principal.UserId())
            .Select(a => new { a.Role, Name = db.PlatformRoles.Where(r => r.Id == a.CustomRoleId).Select(r => r.Name).FirstOrDefault() }).SingleOrDefaultAsync(ct);
        var permissions = await Permissions.ForUserAsync(db, principal.UserId()!.Value, ct);
        return admin is null || permissions is null ? TypedResults.Forbid() : TypedResults.Ok(new MyPermissionsDto(admin.Role, admin.Name, permissions));
    }

    internal static async Task<RolesDto> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var counts = await db.PlatformAdmins.AsNoTracking().GroupBy(a => new { a.Role, a.CustomRoleId }).Select(g => new { g.Key.Role, g.Key.CustomRoleId, Count = g.Count() }).ToListAsync(ct);
        var builtIn = Permissions.BuiltIn.Where(b => b.Key != AdminRole.Custom)
            .Select(b => new BuiltInRoleDto(b.Key, b.Value, counts.Where(c => c.Role == b.Key).Sum(c => c.Count))).ToList();
        var custom = (await db.PlatformRoles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct))
            .Select(r => new CustomRoleDto(r.Id, r.Name, r.Permissions, counts.Where(c => c.CustomRoleId == r.Id).Sum(c => c.Count))).ToList();
        return new RolesDto(Permissions.All, Permissions.Grantable, builtIn, custom);
    }

    private static async Task<IResult> CreateAsync(RoleRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        var name = request.Name!.Trim();
        if (await db.PlatformRoles.AnyAsync(r => r.Name.ToLower() == name.ToLower(), ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A role with this name already exists.");
        }

        db.PlatformRoles.Add(new PlatformRole { Name = name, Permissions = Clean(request.Permissions!) });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ListAsync(db, ct));
    }

    /// <summary>Takes effect on the next request of everyone holding the role: access is checked against the database every time.</summary>
    private static async Task<IResult> UpdateAsync(Guid id, RoleRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        var role = await db.PlatformRoles.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role is null)
        {
            return TypedResults.NotFound();
        }

        var name = request.Name!.Trim();
        if (await db.PlatformRoles.AnyAsync(r => r.Id != id && r.Name.ToLower() == name.ToLower(), ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A role with this name already exists.");
        }

        (role.Name, role.Permissions) = (name, Clean(request.Permissions!));
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ListAsync(db, ct));
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (await db.PlatformAdmins.AnyAsync(a => a.CustomRoleId == id, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Admins still have this role. Give them another role first.");
        }

        await db.PlatformRoles.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
        return TypedResults.Ok(await ListAsync(db, ct));
    }

    private static IResult? Validate(RoleRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Name?.Trim() is not { Length: >= 2 and <= 40 }) errors["Name"] = ["Give the role a name (2 to 40 letters)."];
        if (request.Permissions is null || request.Permissions.Length == 0) errors["Permissions"] = ["Tick at least one permission."];
        else if (request.Permissions.Any(p => !Permissions.Grantable.Contains(p))) errors["Permissions"] = ["Only super admins manage admins and roles; that can't be given to a custom role."];
        return errors.Count > 0 ? TypedResults.ValidationProblem(errors) : null;
    }

    private static string[] Clean(string[] permissions) => Permissions.All.Where(permissions.Contains).ToArray();
}
