using System.Security.Claims;
using System.Security.Cryptography;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
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
}

public sealed record CreateTeamRequest(string? Name, string? YourName, bool Consent);

public sealed record JoinTeamRequest(string? Code, string? YourName, bool Consent);

public sealed record NamedMemberRequest(string? Name);

public sealed record TeamRoleRequest(TeamRole? Role);

public sealed record LeaveTeamRequest(bool DeleteMyLines);

public sealed record TeamExpenseRequest(Guid Id, decimal Amount, ExpenseCategory? Category, string? Note, PaymentMode? Mode, Guid? MemberId, DateTimeOffset? OccurredAt);

public sealed record StrikeTeamExpenseRequest(string? Reason);

public sealed record TeamSummary(Guid Id, string Name, TeamRole MyRole, int Members, decimal ThisMonth);

public sealed record TeamMemberDto(Guid Id, string Name, TeamRole Role, bool HasLogin, bool IsMe, bool HasLeft);

public sealed record TeamDto(Guid Id, string Name, TeamRole MyRole, Guid MyMemberId, IReadOnlyList<TeamMemberDto> Members, string? InviteCode, DateTimeOffset? InviteExpiresAt, int MaxMembers);

public sealed record TeamPreview(string Name, int Members, string CreatedBy, bool AlreadyIn);

public sealed record TeamLineDto(
    Guid Id, DateTimeOffset OccurredAt, decimal Amount, ExpenseCategory Category, string? Note, PaymentMode Mode,
    Guid MemberId, string MemberName, string? WrittenBy, DateTimeOffset? StruckAt, string? StruckReason, bool CanStrike);

public sealed record NameTotal(Guid MemberId, string Name, decimal Total, int Count);

public sealed record TeamDayDto(DateOnly Date, decimal Total, IReadOnlyList<NameTotal> ByName, IReadOnlyList<TeamLineDto> Lines);

public sealed record TeamMonthDto(
    string Month, bool Complete, decimal Total, decimal Cash, decimal Upi, int Count, IReadOnlyList<NameTotal> ByName,
    IReadOnlyList<ExpenseCategoryTotal> ByCategory, IReadOnlyList<ExpenseDayTotal> ByDay, IReadOnlyList<TeamLineDto> Lines);

public static class TeamKharchaEndpoints
{
    /// <summary>The wording people agree to when they join; bump it when the text changes.</summary>
    public const string ConsentVersion = "team-kharcha-v1";

    /// <summary>People (with or without a login) in a free team. Never a cap on lines.</summary>
    public const int FreeMaxMembers = 4;

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
                          db.TeamExpenses.Where(e => e.TeamId == t.Id && e.StruckAt == null && e.OccurredAt >= start && e.OccurredAt < end).Sum(e => (decimal?)e.Amount) ?? 0))
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
        return new TeamDto(team.Id, team.Name, me.Role, me.Id,
            members.Where(m => !m.HasLeft).Select(m => new TeamMemberDto(m.Id, m.DisplayName, m.Role, m.UserId != null, m.Id == me.Id, false)).ToList(),
            Manages(me) ? team.InviteCode : null, Manages(me) ? team.InviteExpiresAt : null, FreeMaxMembers);
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

        if (await db.TeamMembers.CountAsync(m => m.TeamId == team.Id && !m.HasLeft, ct) >= FreeMaxMembers)
        {
            return Problem(StatusCodes.Status402PaymentRequired, $"This team already has {FreeMaxMembers} people, the most a free team can have.");
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

        if (await db.TeamMembers.CountAsync(m => m.TeamId == id && !m.HasLeft, ct) >= FreeMaxMembers)
        {
            return Problem(StatusCodes.Status402PaymentRequired, $"A free team can have {FreeMaxMembers} people.");
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
        var memberId = request.MemberId ?? me.Id;
        if (memberId != me.Id && !Manages(me)) errors["MemberId"] = ["Only the owner or an admin writes for someone else."];
        else if (!await db.TeamMembers.AnyAsync(m => m.Id == memberId && m.TeamId == id && !m.HasLeft, ct)) errors["MemberId"] = ["That person isn't in the team."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        db.TeamExpenses.Add(new TeamExpense
        {
            Id = request.Id, TeamId = id, MemberId = memberId, CreatedByUserId = me.UserId!.Value,
            OccurredAt = request.OccurredAt is { } at && at <= now ? at.ToUniversalTime() : now,
            Amount = request.Amount, Category = request.Category ?? ExpenseReader.Guess(note ?? "").Category, Note = note, Mode = request.Mode ?? PaymentMode.Cash,
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
                          orderby e.OccurredAt, e.CreatedAt
                          select new { E = e, m.DisplayName, m.HasLeft, Writer = u == null ? null : u.FullName })
            .ToListAsync(ct);
        return rows.Select(r => new TeamLineDto(r.E.Id, r.E.OccurredAt, r.E.Amount, r.E.Category, r.E.Note, r.E.Mode, r.E.MemberId,
            r.HasLeft ? Former : r.DisplayName, r.Writer, r.E.StruckAt, r.E.StruckReason, r.E.StruckAt is null && CanStrike(me, r.E.MemberId, r.E.CreatedByUserId))).ToList();
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
        var live = lines.Where(l => l.StruckAt is null).ToList();
        return TypedResults.Ok(new TeamDayDto(day, live.Sum(l => l.Amount), ByName(live), lines));
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
        var live = (await LinesAsync(id, me, db, e => e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null, ct));
        return TypedResults.Ok(new TeamMonthDto(
            $"{m.Year:D4}-{m.Month:D2}", end <= clock.GetUtcNow(), live.Sum(l => l.Amount),
            live.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount), live.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount), live.Count,
            ByName(live),
            live.GroupBy(l => l.Category).Select(g => new ExpenseCategoryTotal(g.Key, g.Sum(l => l.Amount), g.Count())).OrderByDescending(c => c.Total).ToList(),
            live.GroupBy(l => DateOnly.FromDateTime(l.OccurredAt.ToOffset(IndiaTime.Offset).DateTime)).Select(g => new ExpenseDayTotal(g.Key, g.Sum(l => l.Amount), g.Count())).OrderBy(x => x.Date).ToList(),
            live));
    }

    private static string? Short(string? name) => name?.Trim() is { Length: >= 2 and <= 40 } n ? n : null;
}
