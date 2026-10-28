using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A customer saying an entry is wrong ("I paid this", "I didn't take this"). It doesn't change the khata: the
/// entry is flagged on both sides until the shop answers. The answer is kept and shown to the customer; if the
/// shop agrees, it fixes the khata with a normal entry or removal.
/// </summary>
public sealed class Dispute : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid EntryId { get; set; }

    public required string Message { get; set; }

    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>The shop's answer. Null while open.</summary>
    public string? Resolution { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }
}

public sealed record RaiseDisputeRequest(Guid EntryId, string? Message);

public sealed record ResolveDisputeRequest(string? Resolution);

/// <summary>An open (or answered) dispute in the shop's list.</summary>
public sealed record DisputeDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid EntryId,
    LedgerEntryKind EntryKind,
    decimal EntryAmount,
    DateTimeOffset EntryOccurredAt,
    string Message,
    DateTimeOffset RaisedAt,
    string? Resolution,
    DateTimeOffset? ResolvedAt);

public static class DisputeEndpoints
{
    public static IEndpointRouteBuilder MapDisputes(this IEndpointRouteBuilder app)
    {
        // The customer raises one: from the private link, or from the household app.
        app.MapPost("/api/v1/khata/{slug}/{customerId:guid}/disputes", RaiseByLinkAsync)
            .WithTags("Disputes").AllowAnonymous().RequireRateLimiting(Planner.StoreEndpoints.StoreRateLimit);
        app.MapPost("/api/v1/me/khata/{shopId:guid}/{customerId:guid}/disputes", RaiseMineAsync)
            .WithTags("Disputes").RequireAuthorization();

        // The shop sees them; the owner answers.
        app.MapGet("/api/v1/disputes", ListAsync).WithTags("Disputes").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/disputes/{id:guid}/resolve", ResolveAsync).WithTags("Disputes").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> RaiseByLinkAsync(
        string slug, Guid customerId, string? t, RaiseDisputeRequest request, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (await KhataViewEndpoints.OpenByLinkAsync(slug, customerId, t, db, tenant, ct) is null)
        {
            return TypedResults.NotFound();
        }

        return await RaiseAsync(db, customerId, request, clock, ct);
    }

    private static async Task<IResult> RaiseMineAsync(
        Guid shopId, Guid customerId, RaiseDisputeRequest request, ClaimsPrincipal user, IServiceScopeFactory scopes, AppDbContext db,
        TimeProvider clock, CancellationToken ct)
    {
        if (await KhataViewEndpoints.OpenMineAsync(shopId, customerId, user, scopes, db, ct) is not { } scope)
        {
            return TypedResults.NotFound();
        }

        await using (scope)
        {
            return await RaiseAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), customerId, request, clock, ct);
        }
    }

    /// <summary>Raises a dispute on one of the customer's own entries. Asking again while it's open returns the same one.</summary>
    private static async Task<IResult> RaiseAsync(AppDbContext db, Guid customerId, RaiseDisputeRequest request, TimeProvider clock, CancellationToken ct)
    {
        var message = request.Message?.Trim();
        if (message is not { Length: >= 3 and <= 300 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Message)] = ["Say what is wrong with this entry (3 to 300 characters)."] });
        }

        if (!await db.LedgerEntries.AnyAsync(e => e.Id == request.EntryId && e.CustomerId == customerId, ct))
        {
            return TypedResults.NotFound();
        }

        if (await db.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.EntryId == request.EntryId && d.ResolvedAt == null, ct) is { } open)
        {
            return TypedResults.Ok(new DisputeView(open.Id, open.Message, open.RaisedAt, null, null));
        }

        var dispute = new Dispute { CustomerId = customerId, EntryId = request.EntryId, Message = message, RaisedAt = clock.GetUtcNow() };
        db.Disputes.Add(dispute);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return TypedResults.Conflict();
        }

        return TypedResults.Created($"/api/v1/disputes/{dispute.Id}", new DisputeView(dispute.Id, dispute.Message, dispute.RaisedAt, null, null));
    }

    private static async Task<IReadOnlyList<DisputeDto>> ListAsync(bool? open, AppDbContext db, CancellationToken ct)
    {
        var query = db.Disputes.AsNoTracking();
        if (open ?? true)
        {
            query = query.Where(d => d.ResolvedAt == null);
        }

        return await (from d in query
                      join c in db.Customers on d.CustomerId equals c.Id
                      join e in db.LedgerEntries on d.EntryId equals e.Id
                      orderby d.RaisedAt descending
                      select new DisputeDto(d.Id, c.Id, c.Name, e.Id, e.Kind, e.Amount, e.OccurredAt, d.Message, d.RaisedAt, d.Resolution, d.ResolvedAt))
            .Take(100).ToListAsync(ct);
    }

    /// <summary>The owner's answer, shown to the customer next to the entry. Fixing the khata itself is a normal entry or removal.</summary>
    private static async Task<IResult> ResolveAsync(
        Guid id, ResolveDisputeRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var resolution = request.Resolution?.Trim();
        if (resolution is not { Length: >= 3 and <= 300 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Resolution)] = ["Write the answer the customer will see (3 to 300 characters)."] });
        }

        var dispute = await db.Disputes.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (dispute is null)
        {
            return TypedResults.NotFound();
        }

        if (dispute.ResolvedAt is null)
        {
            dispute.Resolution = resolution;
            dispute.ResolvedAt = clock.GetUtcNow();
            dispute.ResolvedByUserId = user.UserId();
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(new DisputeView(dispute.Id, dispute.Message, dispute.RaisedAt, dispute.Resolution, dispute.ResolvedAt));
    }
}
