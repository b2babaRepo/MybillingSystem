using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Agents;

/// <summary>
/// A field agent: a person who visits shops and signs them up. Applies (or is added by an admin), an agent manager
/// checks their Aadhaar card and approves them, and they get an agent code (GKA-0042) that every shop they register carries.
/// </summary>
public sealed class FieldAgent : Entity
{
    public Guid UserId { get; set; }

    /// <summary>"GKA-0042", given on approval; null until then.</summary>
    public string? Code { get; set; }

    public required string FullName { get; set; }

    public required string Address { get; set; }

    /// <summary>The market or area they cover ("Lalpur market, Ranchi").</summary>
    public required string Market { get; set; }

    /// <summary>
    /// Only the last four digits are kept: the law doesn't allow storing Aadhaar numbers. With
    /// <see cref="AadhaarKey"/>, enough to show which card it was and to stop one card being used twice.
    /// </summary>
    public required string AadhaarLast4 { get; set; }

    /// <summary>A keyed hash of the number (HMAC-SHA256, key in server settings): matches a repeat, can't be turned back into the number.</summary>
    public required string AadhaarKey { get; set; }

    /// <summary>When an agent manager compared the card with the person and their details.</summary>
    public DateTimeOffset? KycCheckedAt { get; set; }

    public Guid? KycCheckedByUserId { get; set; }

    /// <summary>Where commission is paid.</summary>
    public string? UpiId { get; set; }

    public FieldAgentStatus Status { get; set; } = FieldAgentStatus.Applied;

    /// <summary>Why it was rejected or blocked; the agent sees it.</summary>
    public string? StatusReason { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset AgreementAcceptedAt { get; set; }

    /// <summary>The admin who entered the application, when the agent didn't apply themselves.</summary>
    public Guid? AddedByUserId { get; set; }
}

public enum FieldAgentStatus
{
    Applied,
    Approved,
    Rejected,
    Blocked,
}

public sealed class AgentOptions
{
    /// <summary>Key for <see cref="FieldAgent.AadhaarKey"/>. Set a long random value in production; changing it breaks duplicate checks.</summary>
    public string AadhaarHashKey { get; set; } = "dev-only-aadhaar-key-change-me";
}

public sealed record AgentApplyRequest(string? Address, string? Market, string? Aadhaar, string? UpiId, bool AcceptAgreement);

public sealed record AddAgentRequest(string? FullName, string? Phone, string? Address, string? Market, string? Aadhaar, string? UpiId);

public sealed record AgentApproveRequest(bool KycChecked);

public sealed record AgentReasonRequest(string? Reason);

public sealed record AgentMarketRequest(string? Market);

public sealed record FieldAgentDto(
    Guid Id, string? Code, string FullName, string Phone, string Address, string Market, string AadhaarLast4, bool KycChecked,
    string? UpiId, FieldAgentStatus Status, string? StatusReason, DateTimeOffset AppliedAt, DateTimeOffset? ApprovedAt, string? ApprovedBy);

/// <summary>An admin-added agent; a new account gets a one-time password, shown once.</summary>
public sealed record AddedAgent(FieldAgentDto Agent, string? TempPassword);

/// <summary>Aadhaar numbers: 12 digits, not starting with 0 or 1, the last one a Verhoeff check digit (catches a typed-wrong digit).</summary>
public static class Aadhaar
{
    private static readonly int[,] D =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 }, { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 }, { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 },
        { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 }, { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 }, { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 }, { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 },
        { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 }, { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
    };

    private static readonly int[,] P =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 }, { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 }, { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 },
        { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 }, { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 }, { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 }, { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
    };

    /// <summary>The 12 digits, spaces and dashes removed, or null when it isn't a valid Aadhaar number.</summary>
    public static string? Normalize(string? text)
    {
        var digits = new string((text ?? "").Where(c => c is not (' ' or '-')).ToArray());
        if (digits.Length != 12 || !digits.All(char.IsAsciiDigit) || digits[0] is '0' or '1')
        {
            return null;
        }

        var c = 0;
        for (var i = 0; i < 12; i++)
        {
            c = D[c, P[i % 8, digits[11 - i] - '0']];
        }

        return c == 0 ? digits : null;
    }

    public static string Key(string digits, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(digits))).ToLowerInvariant();
}

public static class FieldAgentEndpoints
{
    public static IServiceCollection AddFieldAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentOptions>(configuration.GetSection("Agents"));
        return services;
    }

    public static IEndpointRouteBuilder MapFieldAgents(this IEndpointRouteBuilder app)
    {
        // The person's own application: any signed-in account can apply, and see where it stands.
        var me = app.MapGroup("/api/v1/agents").WithTags("Field agents").RequireAuthorization();
        me.MapGet("/me", MineAsync);
        me.MapPost("/apply", ApplyAsync);

        var admin = app.MapGroup("/api/v1/platform/agents").WithTags("Field agents").RequireAuthorization(AppPolicies.PlatformAgents).RecordsActivity();
        admin.MapGet("", ListAsync);
        admin.MapPost("", AddAsync);
        admin.MapPost("/{id:guid}/approve", ApproveAsync);
        admin.MapPost("/{id:guid}/reject", (Guid id, AgentReasonRequest r, AppDbContext db, CancellationToken ct) => SetStatusAsync(id, FieldAgentStatus.Rejected, r.Reason, db, ct));
        admin.MapPost("/{id:guid}/block", (Guid id, AgentReasonRequest r, AppDbContext db, CancellationToken ct) => SetStatusAsync(id, FieldAgentStatus.Blocked, r.Reason, db, ct));
        admin.MapPost("/{id:guid}/unblock", UnblockAsync);
        admin.MapPut("/{id:guid}/market", MarketAsync);
        return app;
    }

    private static async Task<IResult> MineAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        return await Query(db, a => a.UserId == userId).Select(ToDto).SingleOrDefaultAsync(ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.NoContent();
    }

    private static async Task<IResult> ApplyAsync(
        AgentApplyRequest request, ClaimsPrincipal user, AppDbContext db, IOptions<AgentOptions> options, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var person = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
        var errors = Check(person.FullName, request.Address, request.Market, request.Aadhaar, request.UpiId);
        if (!request.AcceptAgreement)
        {
            errors["AcceptAgreement"] = ["Read and accept the agent agreement."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var existing = await db.FieldAgents.SingleOrDefaultAsync(a => a.UserId == userId, ct);
        if (existing is { Status: not FieldAgentStatus.Rejected })
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "You have already applied.");
        }

        var digits = Aadhaar.Normalize(request.Aadhaar)!;
        var key = Aadhaar.Key(digits, options.Value.AadhaarHashKey);
        if (await db.FieldAgents.AnyAsync(a => a.AadhaarKey == key && a.UserId != userId && a.Status != FieldAgentStatus.Rejected, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This Aadhaar is already used by another agent.");
        }

        // Rejected before: apply again with the same row, so the history stays in one place.
        var agent = existing ?? new FieldAgent { UserId = userId, FullName = person.FullName, Address = "", Market = "", AadhaarLast4 = "", AadhaarKey = "" };
        Fill(agent, person.FullName, request.Address!, request.Market!, digits, key, request.UpiId);
        (agent.Status, agent.StatusReason, agent.KycCheckedAt, agent.AgreementAcceptedAt) = (FieldAgentStatus.Applied, null, null, clock.GetUtcNow());
        if (existing is null)
        {
            db.FieldAgents.Add(agent);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Query(db, a => a.Id == agent.Id).Select(ToDto).SingleAsync(ct));
    }

    private static async Task<IReadOnlyList<FieldAgentDto>> ListAsync(string? status, AppDbContext db, CancellationToken ct)
    {
        var query = Enum.TryParse<FieldAgentStatus>(status, ignoreCase: true, out var s) ? Query(db, a => a.Status == s) : Query(db, a => true);
        return await query.OrderByDescending(a => a.CreatedAt).Take(300).Select(ToDto).ToListAsync(ct);
    }

    /// <summary>An agent manager enters an application for someone (met in person). A number with no account gets one.</summary>
    private static async Task<IResult> AddAsync(
        AddAgentRequest request, ClaimsPrincipal admin, AppDbContext db, IPasswordHasher<User> hasher, IOptions<AgentOptions> options, TimeProvider clock, CancellationToken ct)
    {
        var phone = PhoneNumber.Normalize(request.Phone);
        var errors = Check(request.FullName, request.Address, request.Market, request.Aadhaar, request.UpiId);
        if (phone is null)
        {
            errors["Phone"] = ["Enter the agent's mobile number."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var digits = Aadhaar.Normalize(request.Aadhaar)!;
        var key = Aadhaar.Key(digits, options.Value.AadhaarHashKey);
        if (await db.FieldAgents.AnyAsync(a => a.AadhaarKey == key && a.Status != FieldAgentStatus.Rejected, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This Aadhaar is already used by another agent.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is not null && await db.FieldAgents.AnyAsync(a => a.UserId == user.Id, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This number already has an agent application.");
        }

        string? tempPassword = null;
        if (user is null)
        {
            tempPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
            user = new User { Phone = phone!, FullName = request.FullName!.Trim() };
            user.PasswordHash = hasher.HashPassword(user, tempPassword);
            db.Users.Add(user);
        }

        var agent = new FieldAgent { UserId = user.Id, FullName = "", Address = "", Market = "", AadhaarLast4 = "", AadhaarKey = "", AddedByUserId = admin.UserId(), AgreementAcceptedAt = clock.GetUtcNow() };
        Fill(agent, request.FullName!, request.Address!, request.Market!, digits, key, request.UpiId);
        db.FieldAgents.Add(agent);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new AddedAgent(await Query(db, a => a.Id == agent.Id).Select(ToDto).SingleAsync(ct), tempPassword));
    }

    /// <summary>
    /// Approves an agent and gives them the next agent code. The manager must say they checked the Aadhaar card against
    /// the person: the app only knows the number is well formed, not whose it is.
    /// </summary>
    private static async Task<IResult> ApproveAsync(Guid id, AgentApproveRequest request, ClaimsPrincipal admin, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var agent = await db.FieldAgents.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null)
        {
            return TypedResults.NotFound();
        }

        if (!request.KycChecked)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["KycChecked"] = ["Check the Aadhaar card against the person first."] });
        }

        if (agent.Status != FieldAgentStatus.Applied)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a new application can be approved.");
        }

        var now = clock.GetUtcNow();
        agent.Code ??= $"GKA-{(await db.Database.SqlQuery<long>($"SELECT nextval('field_agent_code_seq') AS \"Value\"").ToListAsync(ct)).Single():D4}";
        (agent.Status, agent.StatusReason, agent.ApprovedAt, agent.ApprovedByUserId) = (FieldAgentStatus.Approved, null, now, admin.UserId());
        (agent.KycCheckedAt, agent.KycCheckedByUserId) = (now, admin.UserId());
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Query(db, a => a.Id == id).Select(ToDto).SingleAsync(ct));
    }

    private static async Task<IResult> SetStatusAsync(Guid id, FieldAgentStatus status, string? reason, AppDbContext db, CancellationToken ct)
    {
        reason = reason?.Trim();
        if (reason is not { Length: >= 3 and <= 200 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Say why (the agent sees it)."] });
        }

        var agent = await db.FieldAgents.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null)
        {
            return TypedResults.NotFound();
        }

        // Rejecting is for applications; blocking for approved agents.
        if ((status == FieldAgentStatus.Rejected) != (agent.Status == FieldAgentStatus.Applied) || agent.Status == status)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: status == FieldAgentStatus.Rejected ? "Only a new application can be rejected." : "Only an approved agent can be blocked.");
        }

        (agent.Status, agent.StatusReason) = (status, reason);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Query(db, a => a.Id == id).Select(ToDto).SingleAsync(ct));
    }

    private static async Task<IResult> UnblockAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var agent = await db.FieldAgents.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (agent is not { Status: FieldAgentStatus.Blocked })
        {
            return agent is null ? TypedResults.NotFound() : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This agent isn't blocked.");
        }

        (agent.Status, agent.StatusReason) = (FieldAgentStatus.Approved, null);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Query(db, a => a.Id == id).Select(ToDto).SingleAsync(ct));
    }

    private static async Task<IResult> MarketAsync(Guid id, AgentMarketRequest request, AppDbContext db, CancellationToken ct)
    {
        var market = request.Market?.Trim();
        if (market is not { Length: >= 2 and <= 120 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Market"] = ["Enter the market or area."] });
        }

        var changed = await db.FieldAgents.Where(a => a.Id == id).ExecuteUpdateAsync(u => u.SetProperty(a => a.Market, market), ct);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.Ok(await Query(db, a => a.Id == id).Select(ToDto).SingleAsync(ct));
    }

    private static Dictionary<string, string[]> Check(string? name, string? address, string? market, string? aadhaar, string? upi)
    {
        var errors = new Dictionary<string, string[]>();
        if (name?.Trim() is not { Length: >= 2 and <= 120 }) errors["FullName"] = ["Enter the agent's full name, as on Aadhaar."];
        if (address?.Trim() is not { Length: >= 5 and <= 300 }) errors["Address"] = ["Enter the home address."];
        if (market?.Trim() is not { Length: >= 2 and <= 120 }) errors["Market"] = ["Enter the market or area they will cover."];
        if (Aadhaar.Normalize(aadhaar) is null) errors["Aadhaar"] = ["This isn't a valid Aadhaar number. Check the 12 digits on the card."];
        if (!string.IsNullOrWhiteSpace(upi) && (upi.Trim().Length > 60 || !upi.Contains('@'))) errors["UpiId"] = ["A UPI ID looks like name@bank."];
        return errors;
    }

    private static void Fill(FieldAgent agent, string name, string address, string market, string digits, string key, string? upi)
    {
        agent.FullName = name.Trim();
        agent.Address = address.Trim();
        agent.Market = market.Trim();
        agent.AadhaarLast4 = digits[^4..];
        agent.AadhaarKey = key;
        agent.UpiId = string.IsNullOrWhiteSpace(upi) ? null : upi.Trim().ToLowerInvariant();
    }

    private sealed class Row
    {
        public required Guid Id { get; init; }
        public string? Code { get; init; }
        public required string FullName { get; init; }
        public required string Phone { get; init; }
        public required string Address { get; init; }
        public required string Market { get; init; }
        public required string AadhaarLast4 { get; init; }
        public bool KycChecked { get; init; }
        public string? UpiId { get; init; }
        public FieldAgentStatus Status { get; init; }
        public string? StatusReason { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? ApprovedAt { get; init; }
        public string? ApprovedBy { get; init; }
    }

    /// <summary>Agents matching <paramref name="filter"/>, with their phone and who approved them.</summary>
    private static IQueryable<Row> Query(AppDbContext db, System.Linq.Expressions.Expression<Func<FieldAgent, bool>> filter) =>
        from a in db.FieldAgents.AsNoTracking().Where(filter)
        join u in db.Users.AsNoTracking() on a.UserId equals u.Id
        join b in db.Users.AsNoTracking() on a.ApprovedByUserId equals (Guid?)b.Id into approvers
        from b in approvers.DefaultIfEmpty()
        select new Row
        {
            Id = a.Id, Code = a.Code, FullName = a.FullName, Phone = u.Phone, Address = a.Address, Market = a.Market, AadhaarLast4 = a.AadhaarLast4,
            KycChecked = a.KycCheckedAt != null, UpiId = a.UpiId, Status = a.Status, StatusReason = a.StatusReason, CreatedAt = a.CreatedAt,
            ApprovedAt = a.ApprovedAt, ApprovedBy = b == null ? null : b.FullName,
        };

    private static readonly System.Linq.Expressions.Expression<Func<Row, FieldAgentDto>> ToDto = r => new FieldAgentDto(
        r.Id, r.Code, r.FullName, r.Phone, r.Address, r.Market, r.AadhaarLast4, r.KycChecked, r.UpiId, r.Status, r.StatusReason, r.CreatedAt, r.ApprovedAt, r.ApprovedBy);
}
