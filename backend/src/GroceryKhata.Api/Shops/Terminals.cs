using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>
/// A till the owner registered: a real device record instead of a name typed on the device. Each has a short code
/// (T01, T02…) printed on its bills, and may carry the machine ID a mall issues for its sales feed. Bills made on it
/// carry its id; turning it off stops new bills naming it.
/// </summary>
public sealed class Terminal : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>T01, T02…: numbered in the order registered, never reused.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>The ID a mall gave this machine for its sales feed, when it asks for one.</summary>
    public string? MallMachineId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>When the owner turned it off: bills made on it after this are refused.</summary>
    public DateTimeOffset? TurnedOffAt { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>The last time a bill came from it.</summary>
    public DateTimeOffset? LastBillAt { get; set; }
}

public sealed record TerminalRequest(Guid Id, string? Name, string? MallMachineId = null);

public sealed record TerminalUpdate(string? Name, string? MallMachineId, bool IsActive);

public sealed record TerminalDto(Guid Id, string Code, string Name, string? MallMachineId, bool IsActive, DateTimeOffset RegisteredAt, DateTimeOffset? LastBillAt);

public static class Terminals
{
    public const int MaxPerShop = 50;

    public static IEndpointRouteBuilder MapTerminals(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/terminals").WithTags("Shop");
        group.MapGet("", ListAsync).RequireAuthorization(AppPolicies.ShopMember);
        group.MapPost("", RegisterAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static TerminalDto Dto(Terminal t) => new(t.Id, t.Code, t.Name, t.MallMachineId, t.IsActive, t.RegisteredAt, t.LastBillAt);

    private static async Task<IReadOnlyList<TerminalDto>> ListAsync(AppDbContext db, CancellationToken ct) =>
        (await db.Terminals.AsNoTracking().OrderBy(t => t.Code).ToListAsync(ct)).Select(Dto).ToList();

    /// <summary>Registers this device as a till (sent again: the same till).</summary>
    private static async Task<IResult> RegisterAsync(TerminalRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Terminals.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.Id, ct) is { } same)
        {
            return TypedResults.Ok(Dto(same));
        }

        var name = request.Name?.Trim();
        if (request.Id == Guid.Empty || name is not { Length: >= 1 and <= 30 } || request.MallMachineId?.Trim().Length > 40)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Name the till (like Counter 1), up to 30 letters."] });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('shop_terminals'))", ct);
        var count = await db.Terminals.CountAsync(ct);
        if (count >= MaxPerShop)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = [$"A shop can register up to {MaxPerShop} tills."] });
        }

        var terminal = new Terminal
        {
            Id = request.Id, Code = $"T{count + 1:00}", Name = name, MallMachineId = Clean(request.MallMachineId), RegisteredAt = clock.GetUtcNow(),
        };
        db.Terminals.Add(terminal);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(Dto(terminal));
    }

    private static async Task<IResult> UpdateAsync(Guid id, TerminalUpdate request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var terminal = await db.Terminals.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (terminal is null)
        {
            return TypedResults.NotFound();
        }

        var name = request.Name?.Trim();
        if (name is not { Length: >= 1 and <= 30 } || request.MallMachineId?.Trim().Length > 40)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Name the till (like Counter 1), up to 30 letters."] });
        }

        if (terminal.IsActive && !request.IsActive)
        {
            terminal.TurnedOffAt = clock.GetUtcNow();
        }

        (terminal.Name, terminal.MallMachineId, terminal.IsActive) = (name, Clean(request.MallMachineId), request.IsActive);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Dto(terminal));
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
