using System.Security.Claims;
using System.Security.Cryptography;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Teams;

/// <summary>
/// A shared kharcha book: a shop with its partners and helpers, a family, a group of friends. Several app users write
/// into it; every line says whose spending it was, and the book adds up by name, day and month. Not tied to a shop, so
/// households and shopkeepers alike can make or join one, on any plan.
/// </summary>
public sealed class Team : Entity
{
    public required string Name { get; set; }

    /// <summary>The current invite code (8 letters and digits, easy to read out); null when no invite is open.</summary>
    public string? InviteCode { get; set; }

    public DateTimeOffset? InviteExpiresAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// The secret in the team's view link (/tv/{team}?t=…), for people without the app: totals by name and heading,
    /// no notes. Null: no link. Only the owner turns it on; every member sees that it is on.
    /// </summary>
    public string? ViewToken { get; set; }

    /// <summary>A member's line above this needs the owner's or an admin's OK before it counts. Null: no limit.</summary>
    public decimal? ApprovalAbove { get; set; }
}

/// <summary>
/// One person in a team. With a login (<see cref="UserId"/>) they write their own lines; without one they're a name the
/// owner writes for (a family member without a phone, a child). Leaving keeps the row: their lines then show as
/// "Former member", or are deleted if they chose so.
/// </summary>
public sealed class TeamMember : Entity
{
    public Guid TeamId { get; set; }

    public Guid? UserId { get; set; }

    public required string DisplayName { get; set; }

    public TeamRole Role { get; set; } = TeamRole.Member;

    public bool HasLeft { get; set; }

    public DateTimeOffset? LeftAt { get; set; }

    /// <summary>When they agreed their lines are seen by the team, and to which wording (data protection law asks for both).</summary>
    public DateTimeOffset? ConsentAt { get; set; }

    public string? ConsentVersion { get; set; }

    /// <summary>The most this person may spend in a month; a line past it waits for the owner's OK. Null: no limit.</summary>
    public decimal? MonthlyLimit { get; set; }
}

public enum TeamRole
{
    /// <summary>Made the team: everything, including roles and removing people.</summary>
    Owner,

    /// <summary>Invites, adds named members, writes for anyone, strikes any line.</summary>
    Admin,

    /// <summary>Writes their own lines and strikes them.</summary>
    Member,

    /// <summary>Sees the book, writes nothing.</summary>
    Viewer,
}

/// <summary>A line in a team's book. Never edited; a mistake is struck (with who and why), and struck lines stop counting.</summary>
public sealed class TeamExpense : Entity
{
    public Guid TeamId { get; set; }

    /// <summary>Whose spending it was.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Who wrote it (an owner may write for a named member).</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public decimal Amount { get; set; }

    public ExpenseCategory Category { get; set; }

    public string? Note { get; set; }

    public PaymentMode Mode { get; set; }

    public DateTimeOffset? StruckAt { get; set; }

    public Guid? StruckByUserId { get; set; }

    public string? StruckReason { get; set; }

    /// <summary>Waiting for the owner's or an admin's OK (over the approval amount or the person's monthly limit); not counted until then.</summary>
    public DateTimeOffset? PendingSince { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>A photo of the bill or receipt, shrunk on the phone (WebP/JPEG/PNG, under 150 KB).</summary>
    public byte[]? Photo { get; set; }

    public string? PhotoType { get; set; }

    /// <summary>Paid out of money the owner advanced to this person (it lowers what they still hold).</summary>
    public bool FromAdvance { get; set; }
}

/// <summary>
/// Money the owner hands a person to spend for the team (an advance), or money they hand back. What they still hold =
/// advances − lines they paid from the advance − money returned. Never edited; a mistake is struck.
/// </summary>
public sealed class TeamAdvance : Entity
{
    public Guid TeamId { get; set; }

    public Guid MemberId { get; set; }

    public AdvanceKind Kind { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }

    public Guid ByUserId { get; set; }

    public DateTimeOffset? StruckAt { get; set; }
}

public enum AdvanceKind
{
    /// <summary>The owner gave money to spend.</summary>
    Given,

    /// <summary>The person handed money back.</summary>
    Returned,
}

public sealed record CreateTeamRequest(string? Name, string? YourName, bool Consent);

public sealed record JoinTeamRequest(string? Code, string? YourName, bool Consent);

public sealed record NamedMemberRequest(string? Name);

public sealed record TeamRoleRequest(TeamRole? Role);

public sealed record LeaveTeamRequest(bool DeleteMyLines);

public sealed record TeamExpenseRequest(
    Guid Id, decimal Amount, ExpenseCategory? Category, string? Note, PaymentMode? Mode, Guid? MemberId, DateTimeOffset? OccurredAt, string? PhotoDataUrl = null,
    bool FromAdvance = false);

public sealed record TeamRulesRequest(decimal? ApprovalAbove);

public sealed record AdvanceRequest(Guid MemberId, AdvanceKind? Kind, decimal Amount, string? Note);

public sealed record AdvanceEntryDto(Guid Id, DateTimeOffset At, Guid MemberId, string MemberName, AdvanceKind Kind, decimal Amount, string? Note, string? By, bool Struck);

/// <summary>One person's advance account: given, spent from it, returned, and what they still hold.</summary>
public sealed record AdvanceBalance(Guid MemberId, string Name, decimal Given, decimal Spent, decimal Returned, decimal Holds);

public sealed record AdvancesDto(IReadOnlyList<AdvanceBalance> Balances, IReadOnlyList<AdvanceEntryDto> Entries);

public sealed record MemberLimitRequest(decimal? MonthlyLimit);

public sealed record StrikeTeamExpenseRequest(string? Reason);

public sealed record TeamSummary(Guid Id, string Name, TeamRole MyRole, int Members, decimal ThisMonth);

public sealed record TeamMemberDto(Guid Id, string Name, TeamRole Role, bool HasLogin, bool IsMe, bool HasLeft, decimal? MonthlyLimit = null, decimal SpentThisMonth = 0);

public sealed record TeamDto(
    Guid Id, string Name, TeamRole MyRole, Guid MyMemberId, IReadOnlyList<TeamMemberDto> Members, string? InviteCode, DateTimeOffset? InviteExpiresAt, int MaxMembers,
    bool ViewLinkOn = false, string? ViewToken = null, decimal? ApprovalAbove = null, int Pending = 0);

/// <summary>What the view link shows: totals by name and heading for the month and today. No notes, no who-wrote-what.</summary>
public sealed record TeamPublicView(
    string Name, string Month, decimal MonthTotal, IReadOnlyList<NameTotal> MonthByName, IReadOnlyList<ExpenseCategoryTotal> MonthByCategory,
    decimal TodayTotal, IReadOnlyList<NameTotal> TodayByName);

public sealed record TeamPreview(string Name, int Members, string CreatedBy, bool AlreadyIn);

public sealed record TeamLineDto(
    Guid Id, DateTimeOffset OccurredAt, decimal Amount, ExpenseCategory Category, string? Note, PaymentMode Mode,
    Guid MemberId, string MemberName, string? WrittenBy, DateTimeOffset? StruckAt, string? StruckReason, bool CanStrike,
    bool Pending = false, bool CanApprove = false, bool HasPhoto = false, string? ApprovedBy = null, bool FromAdvance = false);

public sealed record NameTotal(Guid MemberId, string Name, decimal Total, int Count);

public sealed record TeamDayDto(DateOnly Date, decimal Total, IReadOnlyList<NameTotal> ByName, IReadOnlyList<TeamLineDto> Lines, decimal PendingTotal = 0);

public sealed record TeamMonthDto(
    string Month, bool Complete, decimal Total, decimal Cash, decimal Upi, int Count, IReadOnlyList<NameTotal> ByName,
    IReadOnlyList<ExpenseCategoryTotal> ByCategory, IReadOnlyList<ExpenseDayTotal> ByDay, IReadOnlyList<TeamLineDto> Lines);

public static class TeamKharchaEndpoints
{
    /// <summary>The wording people agree to when they join; bump it when the text changes.</summary>
    public const string ConsentVersion = "team-kharcha-v1";

    /// <summary>People (with or without a login) in a free team. Never a cap on lines.</summary>
    public const int FreeMaxMembers = 4;

    /// <summary>
    /// Team Pro: a team whose owner runs a shop on a paid plan holds more people (Basic 6, Silver 10, Gold 25; another
    /// paid plan 10). Free teams and shops on trial: <see cref="FreeMaxMembers"/>. Lines are never limited.
    /// </summary>
    public static async Task<int> MaxMembersAsync(Guid ownerUserId, AppDbContext db, CancellationToken ct)
    {
        var shopIds = await db.Memberships.IgnoreQueryFilters()
            .Where(m => m.UserId == ownerUserId && m.Role == ShopRole.Owner && m.IsActive).Select(m => m.ShopId).ToListAsync(ct);
        if (shopIds.Count == 0)
        {
            return FreeMaxMembers;
        }

        var plans = await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Code, ct);
        var subs = await db.Subscriptions.AsNoTracking().Where(x => shopIds.Contains(x.ShopId)).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var paid = subs.Select(x => Billing.Entitlements.Compute(x, plans, now, 7))
            .Where(e => e.Status is Billing.SubscriptionStatus.Active or Billing.SubscriptionStatus.Grace or Billing.SubscriptionStatus.Complimentary)
            .Select(e => e.PlanCode switch { "free" => FreeMaxMembers, "basic" => 6, "silver" => 10, "pro" => 25, _ => 10 });
        return paid.DefaultIfEmpty(FreeMaxMembers).Max();
    }

    private const string Former = "Former member";

    // No 0/O or 1/I/L: the code is read out loud and typed by hand.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static IEndpointRouteBuilder MapTeamKharcha(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/teams").WithTags("Team kharcha").RequireAuthorization();
        g.MapGet("", ListAsync);
        g.MapPost("", CreateAsync);
        g.MapGet("/preview", PreviewAsync);
        g.MapPost("/join", JoinAsync);
        g.MapGet("/{id:guid}", GetAsync);
        g.MapPost("/{id:guid}/invite", InviteAsync);
        g.MapPost("/{id:guid}/members", AddNamedAsync);
        g.MapPut("/{id:guid}/members/{memberId:guid}/role", RoleAsync);
        g.MapPost("/{id:guid}/members/{memberId:guid}/remove", RemoveAsync);
        g.MapPost("/{id:guid}/leave", LeaveAsync);
        g.MapPost("/{id:guid}/expenses", AddExpenseAsync);
        g.MapPost("/{id:guid}/expenses/{expenseId:guid}/strike", StrikeAsync);
        g.MapGet("/{id:guid}/day", DayAsync);
        g.MapGet("/{id:guid}/month", MonthAsync);
        g.MapPost("/{id:guid}/view-link", ViewLinkAsync);
        g.MapPut("/{id:guid}/rules", RulesAsync);
        g.MapPut("/{id:guid}/members/{memberId:guid}/limit", LimitAsync);
        g.MapPost("/{id:guid}/expenses/{expenseId:guid}/approve", ApproveAsync);
        g.MapGet("/{id:guid}/expenses/{expenseId:guid}/photo", PhotoAsync);
        g.MapGet("/{id:guid}/pending", PendingAsync);
        g.MapGet("/{id:guid}/advances", AdvancesAsync);
        g.MapPost("/{id:guid}/advances", AddAdvanceAsync);
        g.MapPost("/{id:guid}/advances/{advanceId:guid}/strike", StrikeAdvanceAsync);
        app.MapGet("/api/v1/team-view/{id:guid}", PublicViewAsync).AllowAnonymous().WithTags("Team kharcha");
        return app;
    }

    private static IResult Problem(int status, string title) => TypedResults.Problem(statusCode: status, title: title);

    /// <summary>The caller's active membership in the team, or null (they aren't in it: 404, so teams can't be probed).</summary>
    private static Task<TeamMember?> MeAsync(Guid teamId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        return db.TeamMembers.SingleOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId && !m.HasLeft, ct);
    }

    private static bool Manages(TeamMember me) => me.Role is TeamRole.Owner or TeamRole.Admin;

    private static async Task<IReadOnlyList<TeamSummary>> ListAsync(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId();
        var today = IndiaTime.Today(clock);
        var (start, end) = IndiaTime.Month(today.Year, today.Month);
        return await (from m in db.TeamMembers.AsNoTracking()
                      where m.UserId == userId && !m.HasLeft
                      join t in db.Teams.AsNoTracking() on m.TeamId equals t.Id
                      orderby t.Name
                      select new TeamSummary(t.Id, t.Name, m.Role,
                          db.TeamMembers.Count(x => x.TeamId == t.Id && !x.HasLeft),
                          db.TeamExpenses.Where(e => e.TeamId == t.Id && e.StruckAt == null && e.PendingSince == null && e.OccurredAt >= start && e.OccurredAt < end).Sum(e => (decimal?)e.Amount) ?? 0))
            .ToListAsync(ct);
    }

    private static async Task<IResult> CreateAsync(CreateTeamRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (name is not { Length: >= 2 and <= 60 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Give the team a name, e.g. Sharma family or Shop staff."] });
        }

        if (!request.Consent)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Consent"] = ["Agree that the team sees the lines you write."] });
        }

        var userId = user.UserId()!.Value;
        var person = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
        var team = new Team { Name = name, CreatedByUserId = userId };
        db.Teams.Add(team);
        db.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id, UserId = userId, DisplayName = Short(request.YourName) ?? person.FullName, Role = TeamRole.Owner,
            ConsentAt = clock.GetUtcNow(), ConsentVersion = ConsentVersion,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(team.Id, userId, db, ct));
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await MeAsync(id, user, db, ct) is null ? TypedResults.NotFound() : TypedResults.Ok(await ViewAsync(id, user.UserId()!.Value, db, ct));

    private static async Task<TeamDto> ViewAsync(Guid id, Guid userId, AppDbContext db, CancellationToken ct)
    {
        var team = await db.Teams.AsNoTracking().SingleAsync(t => t.Id == id, ct);
        // Roles are stored as text, so order them here: owner, admins, members, viewers, each by when they joined.
        var members = (await db.TeamMembers.AsNoTracking().Where(m => m.TeamId == id).ToListAsync(ct)).OrderBy(m => m.Role).ThenBy(m => m.CreatedAt).ToList();
        var me = members.Single(m => m.UserId == userId && !m.HasLeft);
        var (monthStart, monthEnd) = MonthNow();
        var spent = await db.TeamExpenses.AsNoTracking()
            .Where(e => e.TeamId == id && e.StruckAt == null && e.PendingSince == null && e.OccurredAt >= monthStart && e.OccurredAt < monthEnd)
            .GroupBy(e => e.MemberId).Select(g => new { g.Key, Sum = g.Sum(e => e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var pending = await db.TeamExpenses.CountAsync(e => e.TeamId == id && e.StruckAt == null && e.PendingSince != null, ct);
        return new TeamDto(team.Id, team.Name, me.Role, me.Id,
            members.Where(m => !m.HasLeft).Select(m => new TeamMemberDto(m.Id, m.DisplayName, m.Role, m.UserId != null, m.Id == me.Id, false, m.MonthlyLimit, spent.GetValueOrDefault(m.Id))).ToList(),
            Manages(me) ? team.InviteCode : null, Manages(me) ? team.InviteExpiresAt : null, await MaxMembersAsync(team.CreatedByUserId, db, ct),
            team.ViewToken != null, me.Role == TeamRole.Owner ? team.ViewToken : null, team.ApprovalAbove, Manages(me) ? pending : 0);
    }

    /// <summary>A fresh invite code, open 48 hours; the old one stops working.</summary>
    private static async Task<IResult> InviteAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (!Manages(me))
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the team's owner or an admin can invite.");
        }

        var team = await db.Teams.SingleAsync(t => t.Id == id, ct);
        team.InviteCode = new string(RandomNumberGenerator.GetItems<char>(CodeAlphabet, 8));
        team.InviteExpiresAt = clock.GetUtcNow().AddHours(48);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(id, me.UserId!.Value, db, ct));
    }

    private static async Task<Team?> ByCodeAsync(string? code, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var clean = new string((code ?? "").ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        if (clean.Length != 8)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        return await db.Teams.SingleOrDefaultAsync(t => t.InviteCode == clean && t.InviteExpiresAt > now, ct);
    }

    /// <summary>What someone sees before joining: the team's name, its size and who made it.</summary>
    private static async Task<IResult> PreviewAsync(string? code, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await ByCodeAsync(code, db, clock, ct) is not { } team)
        {
            return Problem(StatusCodes.Status404NotFound, "This invite code is wrong or has expired. Ask for a new one.");
        }

        var userId = user.UserId();
        var creator = await db.Users.AsNoTracking().Where(u => u.Id == team.CreatedByUserId).Select(u => u.FullName).SingleAsync(ct);
        return TypedResults.Ok(new TeamPreview(team.Name, await db.TeamMembers.CountAsync(m => m.TeamId == team.Id && !m.HasLeft, ct), creator,
            await db.TeamMembers.AnyAsync(m => m.TeamId == team.Id && m.UserId == userId && !m.HasLeft, ct)));
    }

    /// <summary>Joins with an invite code. Agreeing that the team sees your lines is required; you join as a member.</summary>
    private static async Task<IResult> JoinAsync(JoinTeamRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await ByCodeAsync(request.Code, db, clock, ct) is not { } team)
        {
            return Problem(StatusCodes.Status404NotFound, "This invite code is wrong or has expired. Ask for a new one.");
        }

        if (!request.Consent)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Consent"] = ["Agree that the team sees the lines you write."] });
        }

        var userId = user.UserId()!.Value;
        var existing = await db.TeamMembers.SingleOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == userId, ct);
        if (existing is { HasLeft: false })
        {
            return TypedResults.Ok(await ViewAsync(team.Id, userId, db, ct));
        }

        var max = await MaxMembersAsync(team.CreatedByUserId, db, ct);
        if (await db.TeamMembers.CountAsync(m => m.TeamId == team.Id && !m.HasLeft, ct) >= max)
        {
            return Problem(StatusCodes.Status402PaymentRequired, $"This team already has {max} people, the most its plan allows.");
        }

        var person = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
        var member = existing ?? new TeamMember { TeamId = team.Id, UserId = userId, DisplayName = "" };
        (member.DisplayName, member.Role, member.HasLeft, member.LeftAt, member.ConsentAt, member.ConsentVersion) =
            (Short(request.YourName) ?? person.FullName, TeamRole.Member, false, null, clock.GetUtcNow(), ConsentVersion);
        if (existing is null)
        {
            db.TeamMembers.Add(member);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(team.Id, userId, db, ct));
    }

    /// <summary>A person without the app (a family member, a child): just a name the owner or an admin writes for.</summary>
    private static async Task<IResult> AddNamedAsync(Guid id, NamedMemberRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (!Manages(me))
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the team's owner or an admin can add people.");
        }

        if (Short(request.Name) is not { } name)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Enter the person's name."] });
        }

        var owner = await db.Teams.AsNoTracking().Where(t => t.Id == id).Select(t => t.CreatedByUserId).SingleAsync(ct);
        var max = await MaxMembersAsync(owner, db, ct);
        if (await db.TeamMembers.CountAsync(m => m.TeamId == id && !m.HasLeft, ct) >= max)
        {
            return Problem(StatusCodes.Status402PaymentRequired, $"This team can have {max} people on its plan.");
        }

        db.TeamMembers.Add(new TeamMember { TeamId = id, DisplayName = name, Role = TeamRole.Member });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(id, me.UserId!.Value, db, ct));
    }

    /// <summary>The owner changes someone's role (not their own, and nobody else becomes owner).</summary>
    private static async Task<IResult> RoleAsync(Guid id, Guid memberId, TeamRoleRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        var member = await db.TeamMembers.SingleOrDefaultAsync(m => m.Id == memberId && m.TeamId == id && !m.HasLeft, ct);
        if (me.Role != TeamRole.Owner || member is null || member.Id == me.Id || request.Role is null or TeamRole.Owner)
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the owner changes roles: admin, member or viewer.");
        }

        member.Role = request.Role.Value;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(id, me.UserId!.Value, db, ct));
    }

    /// <summary>The owner or an admin takes someone out of the team; their lines stay, under "Former member".</summary>
    private static async Task<IResult> RemoveAsync(Guid id, Guid memberId, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        var member = await db.TeamMembers.SingleOrDefaultAsync(m => m.Id == memberId && m.TeamId == id && !m.HasLeft, ct);
        if (!Manages(me) || member is null || member.Role == TeamRole.Owner || member.Id == me.Id)
        {
            return Problem(StatusCodes.Status403Forbidden, "The owner or an admin can remove others, never the owner.");
        }

        (member.HasLeft, member.LeftAt) = (true, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(id, me.UserId!.Value, db, ct));
    }

    /// <summary>
    /// Leaves the team: sharing stops at once. Your lines are deleted if you ask, otherwise kept as "Former member"
    /// (your name goes). The owner stays: a team always has its owner.
    /// </summary>
    private static async Task<IResult> LeaveAsync(Guid id, LeaveTeamRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (me.Role == TeamRole.Owner)
        {
            return Problem(StatusCodes.Status409Conflict, "The owner can't leave the team.");
        }

        if (request.DeleteMyLines)
        {
            await db.TeamExpenses.Where(e => e.TeamId == id && e.MemberId == me.Id).ExecuteDeleteAsync(ct);
        }

        (me.HasLeft, me.LeftAt, me.DisplayName) = (true, clock.GetUtcNow(), Former);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Writes a line. A member writes their own; the owner or an admin may write for anyone in the team (a named member
    /// without the app). The heading is guessed from the note when not given. Safe to send twice (the phone's ID).
    /// </summary>
    private static async Task<IResult> AddExpenseAsync(Guid id, TeamExpenseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (me.Role == TeamRole.Viewer)
        {
            return Problem(StatusCodes.Status403Forbidden, "Viewers can see the book but not write in it.");
        }

        if (await db.TeamExpenses.AsNoTracking().AnyAsync(e => e.Id == request.Id && e.TeamId == id, ct))
        {
            return TypedResults.Ok((await LinesAsync(id, me, db, e => e.Id == request.Id, ct)).Single());
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID."];
        if (request.Amount <= 0 || request.Amount > Money.MaxAmount || decimal.Round(request.Amount, 2) != request.Amount) errors["Amount"] = ["Enter an amount above ₹0."];
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > 120) errors["Note"] = ["Keep the note short (120 letters)."];
        if (request.Mode is not (null or PaymentMode.Cash or PaymentMode.Upi)) errors["Mode"] = ["Cash or UPI."];
        byte[]? photo = null;
        string? photoType = null;
        if (!string.IsNullOrEmpty(request.PhotoDataUrl))
        {
            var parts = request.PhotoDataUrl.Split(',', 2);
            photoType = parts is [var head, _] && head.StartsWith("data:", StringComparison.Ordinal) && head.EndsWith(";base64", StringComparison.Ordinal) ? head[5..^7] : null;
            try
            {
                photo = photoType is "image/webp" or "image/jpeg" or "image/png" ? Convert.FromBase64String(parts[1]) : null;
            }
            catch (FormatException)
            {
            }

            if (photo is not { Length: > 0 and <= 150_000 }) errors["PhotoDataUrl"] = ["Send the bill photo as WebP, JPEG or PNG under 150 KB."];
        }

        var memberId = request.MemberId ?? me.Id;
        if (memberId != me.Id && !Manages(me)) errors["MemberId"] = ["Only the owner or an admin writes for someone else."];
        else if (!await db.TeamMembers.AnyAsync(m => m.Id == memberId && m.TeamId == id && !m.HasLeft, ct)) errors["MemberId"] = ["That person isn't in the team."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        // An owner's or admin's own lines never wait. A member's line waits when it is above the team's approval amount
        // or takes them past their monthly limit.
        DateTimeOffset? pendingSince = null;
        if (!Manages(me))
        {
            var team = await db.Teams.AsNoTracking().SingleAsync(t => t.Id == id, ct);
            var member = await db.TeamMembers.AsNoTracking().SingleAsync(m => m.Id == memberId, ct);
            var (monthStart, monthEnd) = MonthNow();
            var spentSoFar = member.MonthlyLimit is null ? 0 : await db.TeamExpenses
                .Where(e => e.TeamId == id && e.MemberId == memberId && e.StruckAt == null && e.OccurredAt >= monthStart && e.OccurredAt < monthEnd)
                .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
            if (request.Amount > team.ApprovalAbove || spentSoFar + request.Amount > member.MonthlyLimit)
            {
                pendingSince = now;
            }
        }

        db.TeamExpenses.Add(new TeamExpense
        {
            Id = request.Id, TeamId = id, MemberId = memberId, CreatedByUserId = me.UserId!.Value,
            OccurredAt = request.OccurredAt is { } at && at <= now ? at.ToUniversalTime() : now,
            Amount = request.Amount, Category = request.Category ?? ExpenseReader.Guess(note ?? "").Category, Note = note, Mode = request.Mode ?? PaymentMode.Cash,
            PendingSince = pendingSince, Photo = photo, PhotoType = photo is null ? null : photoType, FromAdvance = request.FromAdvance,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/teams/{id}/expenses/{request.Id}", (await LinesAsync(id, me, db, e => e.Id == request.Id, ct)).Single());
    }

    /// <summary>Strikes a line: who wrote it, the person it was for, or the owner/an admin. It stays in view, crossed out.</summary>
    private static async Task<IResult> StrikeAsync(Guid id, Guid expenseId, StrikeTeamExpenseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        var line = await db.TeamExpenses.SingleOrDefaultAsync(e => e.Id == expenseId && e.TeamId == id, ct);
        if (line is null)
        {
            return TypedResults.NotFound();
        }

        if (!CanStrike(me, line.MemberId, line.CreatedByUserId))
        {
            return Problem(StatusCodes.Status403Forbidden, "Only who wrote it, whose it is, or the owner/an admin can strike a line.");
        }

        if (line.StruckAt is null)
        {
            (line.StruckAt, line.StruckByUserId, line.StruckReason) = (clock.GetUtcNow(), me.UserId, request.Reason?.Trim() is { Length: > 0 } r ? r[..Math.Min(r.Length, 120)] : null);
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.NoContent();
    }

    private static bool CanStrike(TeamMember me, Guid memberId, Guid createdBy) =>
        me.Role != TeamRole.Viewer && (Manages(me) || memberId == me.Id || createdBy == me.UserId);

    private static async Task<List<TeamLineDto>> LinesAsync(
        Guid teamId, TeamMember me, AppDbContext db, System.Linq.Expressions.Expression<Func<TeamExpense, bool>> filter, CancellationToken ct)
    {
        var rows = await (from e in db.TeamExpenses.AsNoTracking().Where(e => e.TeamId == teamId).Where(filter)
                          join m in db.TeamMembers.AsNoTracking() on e.MemberId equals m.Id
                          join u in db.Users.AsNoTracking() on e.CreatedByUserId equals u.Id into writers
                          from u in writers.DefaultIfEmpty()
                          join a in db.Users.AsNoTracking() on e.ApprovedByUserId equals (Guid?)a.Id into approvers
                          from a in approvers.DefaultIfEmpty()
                          orderby e.OccurredAt, e.CreatedAt
                          select new
                          {
                              e.Id, e.OccurredAt, e.Amount, e.Category, e.Note, e.Mode, e.MemberId, e.CreatedByUserId, e.StruckAt, e.StruckReason,
                              e.PendingSince, e.FromAdvance, HasPhoto = e.Photo != null, m.DisplayName, m.HasLeft, Writer = u == null ? null : u.FullName,
                              Approver = a == null ? null : a.FullName,
                          })
            .ToListAsync(ct);
        return rows.Select(r => new TeamLineDto(r.Id, r.OccurredAt, r.Amount, r.Category, r.Note, r.Mode, r.MemberId,
            r.HasLeft ? Former : r.DisplayName, r.Writer, r.StruckAt, r.StruckReason, r.StruckAt is null && CanStrike(me, r.MemberId, r.CreatedByUserId),
            r.PendingSince != null && r.StruckAt == null, r.PendingSince != null && r.StruckAt == null && Manages(me), r.HasPhoto, r.Approver, r.FromAdvance)).ToList();
    }

    private static List<NameTotal> ByName(IEnumerable<TeamLineDto> live) =>
        live.GroupBy(l => (l.MemberId, l.MemberName)).Select(g => new NameTotal(g.Key.MemberId, g.Key.MemberName, g.Sum(l => l.Amount), g.Count()))
            .OrderByDescending(n => n.Total).ToList();

    private static async Task<IResult> DayAsync(Guid id, string? date, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        var day = DateOnly.TryParse(date, out var d) ? d : IndiaTime.Today(clock);
        var (start, end) = IndiaTime.Day(day);
        var lines = await LinesAsync(id, me, db, e => e.OccurredAt >= start && e.OccurredAt < end, ct);
        var live = lines.Where(l => l.StruckAt is null && !l.Pending).ToList();
        return TypedResults.Ok(new TeamDayDto(day, live.Sum(l => l.Amount), ByName(live), lines, lines.Where(l => l.Pending).Sum(l => l.Amount)));
    }

    private static async Task<IResult> MonthAsync(Guid id, string? month, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        var today = IndiaTime.Today(clock);
        var m = string.IsNullOrEmpty(month) ? (today.Year, today.Month) : IndiaTime.ParseMonth(month) ?? (today.Year, today.Month);
        var (start, end) = IndiaTime.Month(m.Year, m.Month);
        var live = (await LinesAsync(id, me, db, e => e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null && e.PendingSince == null, ct));
        return TypedResults.Ok(new TeamMonthDto(
            $"{m.Year:D4}-{m.Month:D2}", end <= clock.GetUtcNow(), live.Sum(l => l.Amount),
            live.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount), live.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount), live.Count,
            ByName(live),
            live.GroupBy(l => l.Category).Select(g => new ExpenseCategoryTotal(g.Key, g.Sum(l => l.Amount), g.Count())).OrderByDescending(c => c.Total).ToList(),
            live.GroupBy(l => DateOnly.FromDateTime(l.OccurredAt.ToOffset(IndiaTime.Offset).DateTime)).Select(g => new ExpenseDayTotal(g.Key, g.Sum(l => l.Amount), g.Count())).OrderBy(x => x.Date).ToList(),
            live));
    }

    /// <summary>The owner turns the view link on (a new secret each time) or off (the old link stops working).</summary>
    private static async Task<IResult> ViewLinkAsync(Guid id, bool on, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (me.Role != TeamRole.Owner)
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the team's owner can share a view link.");
        }

        var team = await db.Teams.SingleAsync(t => t.Id == id, ct);
        team.ViewToken = on ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant() : null;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(id, me.UserId!.Value, db, ct));
    }

    /// <summary>The view link, no sign-in: this month's and today's totals by name and heading.</summary>
    private static async Task<IResult> PublicViewAsync(Guid id, string? t, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (team?.ViewToken is not { } token || t is null
            || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(t), System.Text.Encoding.ASCII.GetBytes(token)))
        {
            return TypedResults.NotFound();
        }

        var today = IndiaTime.Today(clock);
        var (monthStart, monthEnd) = IndiaTime.Month(today.Year, today.Month);
        var (dayStart, dayEnd) = IndiaTime.Day(today);
        var rows = await (from e in db.TeamExpenses.AsNoTracking()
                          where e.TeamId == id && e.StruckAt == null && e.PendingSince == null && e.OccurredAt >= monthStart && e.OccurredAt < monthEnd
                          join m in db.TeamMembers.AsNoTracking() on e.MemberId equals m.Id
                          select new { e.MemberId, Name = m.HasLeft ? Former : m.DisplayName, e.Amount, e.Category, e.OccurredAt })
            .ToListAsync(ct);
        var byName = rows.GroupBy(r => (r.MemberId, r.Name)).Select(g => new NameTotal(g.Key.MemberId, g.Key.Name, g.Sum(r => r.Amount), g.Count())).OrderByDescending(n => n.Total).ToList();
        var todays = rows.Where(r => r.OccurredAt >= dayStart && r.OccurredAt < dayEnd).ToList();
        return TypedResults.Ok(new TeamPublicView(
            team.Name, $"{today.Year:D4}-{today.Month:D2}", rows.Sum(r => r.Amount), byName,
            rows.GroupBy(r => r.Category).Select(g => new ExpenseCategoryTotal(g.Key, g.Sum(r => r.Amount), g.Count())).OrderByDescending(c => c.Total).ToList(),
            todays.Sum(r => r.Amount),
            todays.GroupBy(r => (r.MemberId, r.Name)).Select(g => new NameTotal(g.Key.MemberId, g.Key.Name, g.Sum(r => r.Amount), g.Count())).OrderByDescending(n => n.Total).ToList()));
    }

    private static (DateTimeOffset Start, DateTimeOffset End) MonthNow()
    {
        var now = DateTimeOffset.UtcNow.ToOffset(IndiaTime.Offset);
        return IndiaTime.Month(now.Year, now.Month);
    }

    /// <summary>The owner sets (or clears) the amount above which a member's line needs an OK.</summary>
    private static async Task<IResult> RulesAsync(Guid id, TeamRulesRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (me.Role != TeamRole.Owner)
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the owner sets the team's rules.");
        }

        if (request.ApprovalAbove is <= 0 or > 10_000_000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ApprovalAbove"] = ["Enter an amount above ₹0, or leave it empty for no approvals."] });
        }

        var team = await db.Teams.SingleAsync(t => t.Id == id, ct);
        team.ApprovalAbove = request.ApprovalAbove;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(id, me.UserId!.Value, db, ct));
    }

    /// <summary>The owner sets (or clears) how much a person may spend in a month before lines wait for an OK.</summary>
    private static async Task<IResult> LimitAsync(Guid id, Guid memberId, MemberLimitRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        var member = await db.TeamMembers.SingleOrDefaultAsync(m => m.Id == memberId && m.TeamId == id && !m.HasLeft, ct);
        if (me.Role != TeamRole.Owner || member is null)
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the owner sets monthly limits.");
        }

        if (request.MonthlyLimit is <= 0 or > 10_000_000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["MonthlyLimit"] = ["Enter an amount above ₹0, or leave it empty for no limit."] });
        }

        member.MonthlyLimit = request.MonthlyLimit;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ViewAsync(id, me.UserId!.Value, db, ct));
    }

    /// <summary>The owner or an admin says OK to a waiting line: it counts from now. (To refuse, strike it with a reason.)</summary>
    private static async Task<IResult> ApproveAsync(Guid id, Guid expenseId, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (!Manages(me))
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the owner or an admin approves lines.");
        }

        var line = await db.TeamExpenses.SingleOrDefaultAsync(e => e.Id == expenseId && e.TeamId == id, ct);
        if (line is not { PendingSince: not null, StruckAt: null })
        {
            return line is null ? TypedResults.NotFound() : Problem(StatusCodes.Status409Conflict, "This line isn't waiting for an OK.");
        }

        (line.PendingSince, line.ApprovedAt, line.ApprovedByUserId) = (null, clock.GetUtcNow(), me.UserId);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Every line waiting for an OK, any day (owner and admins).</summary>
    private static async Task<IResult> PendingAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(Manages(me) ? await LinesAsync(id, me, db, e => e.PendingSince != null && e.StruckAt == null, ct) : []);
    }

    private static async Task<IResult> PhotoAsync(Guid id, Guid expenseId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is null)
        {
            return TypedResults.NotFound();
        }

        return await db.TeamExpenses.AsNoTracking().Where(e => e.Id == expenseId && e.TeamId == id && e.Photo != null)
            .Select(e => new { e.Photo, e.PhotoType }).SingleOrDefaultAsync(ct) is { } p
            ? TypedResults.File(p.Photo!, p.PhotoType ?? "image/webp")
            : TypedResults.NotFound();
    }

    /// <summary>
    /// Everyone's advance account. The owner and admins see all; a member sees their own (it's money they're answerable for).
    /// </summary>
    private static async Task<IResult> AdvancesAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await LoadAdvancesAsync(id, me, db, ct));
    }

    private static async Task<AdvancesDto> LoadAdvancesAsync(Guid id, TeamMember me, AppDbContext db, CancellationToken ct)
    {
        var members = await db.TeamMembers.AsNoTracking().Where(m => m.TeamId == id).ToDictionaryAsync(m => m.Id, ct);
        var entries = await (from a in db.TeamAdvances.AsNoTracking()
                             where a.TeamId == id && (Manages(me) || a.MemberId == me.Id)
                             join u in db.Users.AsNoTracking() on a.ByUserId equals u.Id into users
                             from u in users.DefaultIfEmpty()
                             orderby a.CreatedAt descending
                             select new { A = a, By = u == null ? null : u.FullName }).ToListAsync(ct);
        var spent = await db.TeamExpenses.AsNoTracking()
            .Where(e => e.TeamId == id && e.FromAdvance && e.StruckAt == null && e.PendingSince == null && (Manages(me) || e.MemberId == me.Id))
            .GroupBy(e => e.MemberId).Select(g => new { g.Key, Sum = g.Sum(e => e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        string Name(Guid memberId) => members.TryGetValue(memberId, out var m) ? (m.HasLeft ? Former : m.DisplayName) : Former;
        var live = entries.Where(e => e.A.StruckAt == null).Select(e => e.A).ToList();
        var balances = live.Select(a => a.MemberId).Concat(spent.Keys).Distinct().Select(memberId =>
        {
            var given = live.Where(a => a.MemberId == memberId && a.Kind == AdvanceKind.Given).Sum(a => a.Amount);
            var returned = live.Where(a => a.MemberId == memberId && a.Kind == AdvanceKind.Returned).Sum(a => a.Amount);
            var used = spent.GetValueOrDefault(memberId);
            return new AdvanceBalance(memberId, Name(memberId), given, used, returned, given - used - returned);
        }).OrderByDescending(b => b.Holds).ToList();
        return new AdvancesDto(balances, entries.Select(e => new AdvanceEntryDto(e.A.Id, e.A.CreatedAt, e.A.MemberId, Name(e.A.MemberId), e.A.Kind, e.A.Amount, e.A.Note, e.By, e.A.StruckAt != null)).ToList());
    }

    /// <summary>The owner or an admin records money given to someone, or handed back by them.</summary>
    private static async Task<IResult> AddAdvanceAsync(Guid id, AdvanceRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (!Manages(me))
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the owner or an admin gives or takes back advances.");
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Kind is null) errors["Kind"] = ["Given or returned."];
        if (request.Amount <= 0 || request.Amount > Money.MaxAmount || decimal.Round(request.Amount, 2) != request.Amount) errors["Amount"] = ["Enter an amount above ₹0."];
        if (!await db.TeamMembers.AnyAsync(m => m.Id == request.MemberId && m.TeamId == id && !m.HasLeft, ct)) errors["MemberId"] = ["That person isn't in the team."];
        var note = request.Note?.Trim();
        if (note?.Length > 120) errors["Note"] = ["Keep the note short."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        db.TeamAdvances.Add(new TeamAdvance { TeamId = id, MemberId = request.MemberId, Kind = request.Kind!.Value, Amount = request.Amount, Note = string.IsNullOrEmpty(note) ? null : note, ByUserId = me.UserId!.Value });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await LoadAdvancesAsync(id, me, db, ct));
    }

    private static async Task<IResult> StrikeAdvanceAsync(Guid id, Guid advanceId, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(id, user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        if (!Manages(me))
        {
            return Problem(StatusCodes.Status403Forbidden, "Only the owner or an admin can strike an advance.");
        }

        var changed = await db.TeamAdvances.Where(a => a.Id == advanceId && a.TeamId == id && a.StruckAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.StruckAt, clock.GetUtcNow()), ct);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.Ok(await LoadAdvancesAsync(id, me, db, ct));
    }

    private static string? Short(string? name) => name?.Trim() is { Length: >= 2 and <= 40 } n ? n : null;
}
