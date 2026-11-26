using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// An estimate (quotation): the prices offered before the customer decides. It moves no stock, puts nothing on the
/// khata and is not a sale or a tax invoice. When the customer agrees, it is loaded into a bill and marked billed.
/// </summary>
public sealed class Estimate : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid? CustomerId { get; set; }

    /// <summary>For a walk-in with no khata: the name to print.</summary>
    public string? PartyName { get; set; }

    /// <summary>The lines (JSON): product, name, quantity, unit, rate.</summary>
    public required string Lines { get; set; }

    public decimal Total { get; set; }

    public DateOnly ValidUntil { get; set; }

    public string? Note { get; set; }

    /// <summary>The bill it became. Null: still open.</summary>
    public Guid? BillId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record EstimateLine(Guid? ProductId, string Name, decimal Quantity, Unit Unit, decimal UnitPrice, decimal Amount);

public sealed record EstimateLineRequest(Guid? ProductId, string? Name, decimal Quantity, Unit? Unit, decimal UnitPrice);

public sealed record EstimateRequest(Guid Id, Guid? CustomerId, string? PartyName, IReadOnlyList<EstimateLineRequest>? Lines, int? ValidDays = null, string? Note = null);

public sealed record EstimateBilledRequest(Guid BillId);

public sealed record EstimateDto(
    Guid Id, int Number, Guid? CustomerId, string? PartyName, IReadOnlyList<EstimateLine> Lines, decimal Total, DateOnly ValidUntil, string? Note,
    Guid? BillId, DateTimeOffset? CancelledAt, DateTimeOffset CreatedAt);

public static class Estimates
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapEstimates(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/estimates").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("", AddAsync);
        group.MapPost("/{id:guid}/billed", BilledAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync);
        return app;
    }

    private static EstimateDto ToDto(Estimate e) =>
        new(e.Id, e.Number, e.CustomerId, e.PartyName, JsonSerializer.Deserialize<List<EstimateLine>>(e.Lines, Json) ?? [], e.Total, e.ValidUntil, e.Note, e.BillId, e.CancelledAt, e.CreatedAt);

    /// <summary>The last 100, newest first; <paramref name="open"/>: only those not billed or cancelled.</summary>
    private static async Task<IReadOnlyList<EstimateDto>> ListAsync(bool? open, AppDbContext db, CancellationToken ct) =>
        (await db.Estimates.AsNoTracking().Where(e => open != true || (e.BillId == null && e.CancelledAt == null))
            .OrderByDescending(e => e.Number).Take(100).ToListAsync(ct)).Select(ToDto).ToList();

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.Estimates.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct) is { } e ? TypedResults.Ok(ToDto(e)) : TypedResults.NotFound();

    private static async Task<IResult> AddAsync(EstimateRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Estimates.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok(ToDto(existing));
        }

        var lines = request.Lines ?? [];
        var ids = lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200
            || lines.Any(l => (l.ProductId is { } pid ? !products.ContainsKey(pid) : string.IsNullOrWhiteSpace(l.Name)) || !Money.IsValidQuantity(l.Quantity) || !Money.IsValidAmount(l.UnitPrice)))
        {
            return Invalid("Lines", "Add the items with how many and the price of each.");
        }

        if (request.CustomerId is { } cid && !await db.Customers.AnyAsync(c => c.Id == cid, ct))
        {
            return Invalid("CustomerId", "That customer isn't in this shop.");
        }

        if (request.ValidDays is < 1 or > 365 || request.PartyName?.Trim().Length > 80 || request.Note?.Trim().Length > 300)
        {
            return Invalid("ValidDays", "Keep it valid for 1 to 365 days, the name under 80 letters and the note under 300.");
        }

        var built = lines.Select(l =>
        {
            var product = l.ProductId is { } pid ? products[pid] : null;
            return new EstimateLine(l.ProductId, product?.Name ?? l.Name!.Trim(), l.Quantity, product?.Unit ?? l.Unit ?? Unit.Piece, l.UnitPrice, Money.LineAmount(l.Quantity, l.UnitPrice));
        }).ToList();
        // One number at a time: two estimates saved at once would otherwise take the same number.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('estimates'))", ct);
        var estimate = new Estimate
        {
            Id = request.Id, CustomerId = request.CustomerId, PartyName = string.IsNullOrWhiteSpace(request.PartyName) ? null : request.PartyName.Trim(),
            Lines = JsonSerializer.Serialize(built, Json), Total = built.Sum(l => l.Amount), ValidUntil = IndiaTime.Today(clock).AddDays(request.ValidDays ?? 15),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), CreatedByUserId = user.UserId()!.Value,
            Number = await db.NextNumberAsync<Estimate>(ct),
        };
        db.Estimates.Add(estimate);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Created($"/api/v1/estimates/{estimate.Id}", ToDto(estimate));
    }

    /// <summary>The customer agreed and the bill was made from it. Safe to send twice.</summary>
    private static async Task<IResult> BilledAsync(Guid id, EstimateBilledRequest request, AppDbContext db, CancellationToken ct)
    {
        var estimate = await db.Estimates.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (estimate is null)
        {
            return TypedResults.NotFound();
        }

        if (estimate.CancelledAt is not null || (estimate.BillId is { } billed && billed != request.BillId))
        {
            return Invalid("Id", "This estimate is already billed or cancelled.");
        }

        // The phone sends this after the bill, in the same queue, so the bill is here first.
        if (!await db.Bills.AnyAsync(b => b.Id == request.BillId, ct))
        {
            return Invalid("BillId", "That bill isn't in this shop.");
        }

        estimate.BillId = request.BillId;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(estimate));
    }

    private static async Task<IResult> CancelAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var estimate = await db.Estimates.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (estimate is null)
        {
            return TypedResults.NotFound();
        }

        if (estimate.BillId is not null)
        {
            return Invalid("Id", "This estimate is already a bill.");
        }

        estimate.CancelledAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(estimate));
    }
}
