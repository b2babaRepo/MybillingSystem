using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A bill parked at the counter in the rush ("hold this, serve the next one"), kept on the server so any till can pick
/// it up. The bill itself is the app's own cart as it was (<see cref="Payload"/>); the server only keeps and hands it
/// back. Picking it up takes it away, so two tills can't both carry on the same bill.
/// </summary>
public sealed class HeldBill : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public string? Counter { get; set; }

    public Guid HeldByUserId { get; set; }

    public DateTimeOffset HeldAt { get; set; }

    /// <summary>The customer's name, or null for a walk-in.</summary>
    public string? Label { get; set; }

    public decimal Total { get; set; }

    public int Lines { get; set; }

    /// <summary>The cart as the app holds it (JSON), handed back as it came.</summary>
    public required string Payload { get; set; }
}

public sealed record HoldBillRequest(string? Counter, string? Label, decimal Total, int Lines, JsonElement Payload);

public sealed record HeldBillDto(Guid Id, string? Counter, string? HeldBy, DateTimeOffset HeldAt, string? Label, decimal Total, int Lines, JsonElement Payload);

public static class HeldBillEndpoints
{
    /// <summary>Bigger than any real cart (a 200-line bill is about 60 KB).</summary>
    public const int MaxPayload = 256 * 1024;

    /// <summary>Held longer than this is forgotten: the customer has long gone.</summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(2);

    public static IEndpointRouteBuilder MapHeldBills(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/held-bills").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPut("/{id:guid}", HoldAsync);
        group.MapPost("/{id:guid}/take", TakeAsync);
        return app;
    }

    private static async Task<IResult> ListAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow() - KeptFor;
        var rows = await (from h in db.HeldBills.AsNoTracking()
                          where h.HeldAt >= since
                          join u in db.Users.AsNoTracking() on h.HeldByUserId equals u.Id into users
                          from u in users.DefaultIfEmpty()
                          orderby h.HeldAt
                          select new { h, By = u == null ? null : u.FullName }).ToListAsync(ct);
        return TypedResults.Ok(rows.Select(r => Dto(r.h, r.By)).ToList());
    }

    /// <summary>Hold a bill (or hold it again, unchanged, when the app retries).</summary>
    private static async Task<IResult> HoldAsync(Guid id, HoldBillRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var payload = request.Payload.ValueKind == JsonValueKind.Object ? request.Payload.GetRawText() : null;
        if (payload is null || payload.Length > MaxPayload || request.Total < 0 || request.Total > Money.MaxAmount || request.Lines is < 0 or > 10_000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Payload"] = ["This bill can't be held."] });
        }

        var now = clock.GetUtcNow();
        // Old held bills nobody picked up go quietly.
        await db.HeldBills.Where(h => h.HeldAt < now - KeptFor).ExecuteDeleteAsync(ct);
        if (await db.HeldBills.AnyAsync(h => h.Id == id, ct))
        {
            return TypedResults.NoContent();
        }

        db.HeldBills.Add(new HeldBill
        {
            Id = id,
            Counter = Trim(request.Counter, 30),
            Label = Trim(request.Label, 120),
            HeldByUserId = user.UserId()!.Value,
            HeldAt = now,
            Total = request.Total,
            Lines = request.Lines,
            Payload = payload,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // The same hold sent twice at once: already kept.
        }

        return TypedResults.NoContent();
    }

    /// <summary>Pick up a held bill: it is handed back and no longer held. Already picked up elsewhere: 404.</summary>
    private static async Task<IResult> TakeAsync(Guid id, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        // One statement: of two tills taking it at once, only one gets it back.
        var rows = await db.HeldBills.FromSql($"DELETE FROM held_bills WHERE id = {id} AND shop_id = {tenant.ShopId} RETURNING *")
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        return rows is [var held] ? TypedResults.Ok(Dto(held, null)) : TypedResults.NotFound();
    }

    private static HeldBillDto Dto(HeldBill h, string? by) =>
        new(h.Id, h.Counter, by, h.HeldAt, h.Label, h.Total, h.Lines, JsonDocument.Parse(h.Payload).RootElement.Clone());

    private static string? Trim(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(max, text.Trim().Length)];
}
