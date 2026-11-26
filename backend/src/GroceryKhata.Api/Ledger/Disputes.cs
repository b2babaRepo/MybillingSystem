using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A customer saying an entry is wrong ("I paid this", "I didn't take this"). It doesn't change the khata: the
/// entry is flagged on both sides until the shop answers. On a bill the customer can point at the exact items
/// (not received, got less, wrong) and the owner settles each one: handed over now, taken off the khata (stock
/// goes back), or disagreed with a reason. Every fix is a new entry; nothing is edited.
/// </summary>
public sealed class Dispute : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid EntryId { get; set; }

    /// <summary>The customer's words. May be empty when they ticked items instead.</summary>
    public required string Message { get; set; }

    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>The shop's answer. Null while open.</summary>
    public string? Resolution { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public List<DisputeItem> Items { get; set; } = [];
}

/// <summary>One bill item in a dispute.</summary>
public sealed class DisputeItem : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid DisputeId { get; set; }

    public Guid BillLineId { get; set; }

    public ItemProblem Problem { get; set; }

    /// <summary>For <see cref="ItemProblem.Short"/>: how much the customer says they got.</summary>
    public decimal? ReceivedQuantity { get; set; }

    public ItemOutcome? Outcome { get; set; }

    public string? OutcomeNote { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    /// <summary>The <see cref="LedgerEntryKind.Return"/> entry made when the item was taken off the khata.</summary>
    public Guid? ReturnEntryId { get; set; }
}

public enum ItemProblem
{
    /// <summary>Paid or put on khata, never got it (left on the counter, not packed).</summary>
    NotReceived,

    /// <summary>Got less than the bill says.</summary>
    Short,

    /// <summary>Wrong item or wrong price.</summary>
    Wrong,
}

public enum ItemOutcome
{
    /// <summary>The shop found it and gave it now: the khata doesn't change.</summary>
    HandedOver,

    /// <summary>The shop agrees it wasn't given: the amount comes off the khata and the stock goes back.</summary>
    Refunded,

    /// <summary>The shop disagrees, and says why.</summary>
    Disagreed,
}

public sealed record DisputeItemRequest(Guid BillLineId, ItemProblem? Problem, decimal? ReceivedQuantity);

public sealed record RaiseDisputeRequest(Guid EntryId, string? Message, IReadOnlyList<DisputeItemRequest>? Items = null);

public sealed record ResolveDisputeRequest(string? Resolution);

public sealed record ResolveItemRequest(ItemOutcome? Outcome, string? Note);

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
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<DisputeItemView> Items);

public static class DisputeEndpoints
{
    public static IEndpointRouteBuilder MapDisputes(this IEndpointRouteBuilder app)
    {
        // The customer raises one: from the private link, or from the household app.
        app.MapPost("/api/v1/khata/{slug}/{customerId:guid}/disputes", RaiseByLinkAsync)
            .WithTags("Disputes").AllowAnonymous().RequireRateLimiting(Planner.StoreEndpoints.StoreRateLimit);
        app.MapPost("/api/v1/me/khata/{shopId:guid}/{customerId:guid}/disputes", RaiseMineAsync)
            .WithTags("Disputes").RequireAuthorization();

        // The shop sees them; the owner answers, or settles item by item.
        app.MapGet("/api/v1/disputes", ListAsync).WithTags("Disputes").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/disputes/{id:guid}/resolve", ResolveAsync).WithTags("Disputes").RequireAuthorization(AppPolicies.ShopOwner);
        app.MapPost("/api/v1/disputes/{id:guid}/items/{itemId:guid}/resolve", ResolveItemAsync)
            .WithTags("Disputes").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>How much of a line is in question: all of it, or the part not received.</summary>
    private static decimal Disputed(BillLine line, DisputeItem item) =>
        item.Problem == ItemProblem.Short && item.ReceivedQuantity is { } got
            ? Money.LineAmount(line.Quantity - got, line.UnitPrice)
            : line.Amount;

    /// <summary>Disputes as both sides see them, with their items named from the bill.</summary>
    internal static async Task<IReadOnlyList<DisputeView>> ViewsAsync(AppDbContext db, IReadOnlyList<Dispute> disputes, CancellationToken ct)
    {
        var ids = disputes.Select(d => d.Id).ToList();
        var items = await db.DisputeItems.AsNoTracking().Where(i => ids.Contains(i.DisputeId)).ToListAsync(ct);
        var lineIds = items.Select(i => i.BillLineId).ToList();
        var lines = await db.BillLines.AsNoTracking().Where(l => lineIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        return disputes.Select(d => new DisputeView(d.Id, d.Message, d.RaisedAt, d.Resolution, d.ResolvedAt,
            items.Where(i => i.DisputeId == d.Id).Select(i => (Item: i, Line: lines[i.BillLineId])).OrderBy(x => x.Line.LineNo)
                .Select(x => new DisputeItemView(x.Item.Id, x.Line.Id, x.Line.Name, x.Line.Quantity, x.Line.Unit, x.Line.UnitPrice,
                    x.Item.Problem, x.Item.ReceivedQuantity, Disputed(x.Line, x.Item), x.Item.Outcome, x.Item.OutcomeNote, x.Item.ResolvedAt))
                .ToList())).ToList();
    }

    private static async Task<IResult> RaiseByLinkAsync(
        string slug, Guid customerId, string? t, RaiseDisputeRequest request, AppDbContext db, TenantContext tenant, Microsoft.Extensions.Options.IOptions<Billing.BillingOptions> billing, TimeProvider clock, CancellationToken ct)
    {
        if (await KhataViewEndpoints.OpenByLinkAsync(slug, customerId, t, db, tenant, billing, ct) is null)
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

    /// <summary>
    /// Raises a dispute on one of the customer's own entries: words, ticked bill items, or both. Asking again while
    /// it's open returns the same one.
    /// </summary>
    private static async Task<IResult> RaiseAsync(AppDbContext db, Guid customerId, RaiseDisputeRequest request, TimeProvider clock, CancellationToken ct)
    {
        var message = request.Message?.Trim() ?? "";
        var wanted = request.Items ?? [];
        if (message.Length > 300 || (wanted.Count == 0 && message.Length < 3))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Message)] = ["Say what is wrong with this entry, or tick the items."] });
        }

        var entry = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.EntryId && e.CustomerId == customerId, ct);
        if (entry is null)
        {
            return TypedResults.NotFound();
        }

        if (await db.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.EntryId == request.EntryId && d.ResolvedAt == null, ct) is { } open)
        {
            return TypedResults.Ok((await ViewsAsync(db, [open], ct))[0]);
        }

        // Ticked items must be lines of this entry's bill, each once, with a sensible "got" for short ones.
        var lines = entry.BillId is { } billId
            ? await db.BillLines.AsNoTracking().Where(l => l.BillId == billId).ToDictionaryAsync(l => l.Id, ct)
            : [];
        var errors = new Dictionary<string, string[]>();
        if (wanted.Select(w => w.BillLineId).Distinct().Count() != wanted.Count)
        {
            errors[nameof(request.Items)] = ["Each item can be ticked once."];
        }

        foreach (var w in wanted)
        {
            if (!lines.TryGetValue(w.BillLineId, out var line) || w.Problem is null)
            {
                errors[nameof(request.Items)] = ["Tick items from this bill only."];
            }
            else if (w.Problem == ItemProblem.Short && (w.ReceivedQuantity is not { } got || got < 0 || got >= line.Quantity))
            {
                errors[nameof(request.Items)] = [$"For \"{line.Name}\", enter how much you got: less than {line.Quantity}."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var dispute = new Dispute
        {
            CustomerId = customerId,
            EntryId = request.EntryId,
            Message = message,
            RaisedAt = clock.GetUtcNow(),
            Items = wanted.Select(w => new DisputeItem
            {
                BillLineId = w.BillLineId,
                Problem = w.Problem!.Value,
                ReceivedQuantity = w.Problem == ItemProblem.Short ? w.ReceivedQuantity : null,
            }).ToList(),
        };
        db.Disputes.Add(dispute);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return TypedResults.Conflict();
        }

        return TypedResults.Created($"/api/v1/disputes/{dispute.Id}", (await ViewsAsync(db, [dispute], ct))[0]);
    }

    private static async Task<IReadOnlyList<DisputeDto>> ListAsync(bool? open, AppDbContext db, CancellationToken ct)
    {
        var query = db.Disputes.AsNoTracking();
        if (open ?? true)
        {
            query = query.Where(d => d.ResolvedAt == null);
        }

        var rows = await (from d in query
                          join c in db.Customers on d.CustomerId equals c.Id
                          join e in db.LedgerEntries on d.EntryId equals e.Id
                          orderby d.RaisedAt descending
                          select new { Dispute = d, CustomerName = c.Name, e.Kind, e.Amount, e.OccurredAt })
            .Take(100).ToListAsync(ct);
        var views = (await ViewsAsync(db, rows.Select(r => r.Dispute).ToList(), ct)).ToDictionary(v => v.Id);
        return rows.Select(r => new DisputeDto(r.Dispute.Id, r.Dispute.CustomerId, r.CustomerName, r.Dispute.EntryId, r.Kind, r.Amount, r.OccurredAt,
            r.Dispute.Message, r.Dispute.RaisedAt, r.Dispute.Resolution, r.Dispute.ResolvedAt, views[r.Dispute.Id].Items)).ToList();
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

        return TypedResults.Ok((await ViewsAsync(db, [dispute], ct))[0]);
    }

    /// <summary>
    /// Settles one questioned item. Handed over: nothing changes but the record. Refunded: a Return entry takes the
    /// disputed amount off the khata and the stock goes back on the shelf. Disagreed: the owner's reason is shown.
    /// When the last item is settled, the dispute closes.
    /// </summary>
    private static async Task<IResult> ResolveItemAsync(
        Guid id, Guid itemId, ResolveItemRequest request, ClaimsPrincipal user, AppDbContext db, StockService stock, TimeProvider clock, CancellationToken ct)
    {
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (request.Outcome is null || note?.Length > 300 || (request.Outcome == ItemOutcome.Disagreed && note is not { Length: >= 3 }))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Note)] = ["Choose handed over, take off khata, or disagree. To disagree, say why: the customer will see it."],
            });
        }

        var dispute = await db.Disputes.Include(d => d.Items).SingleOrDefaultAsync(d => d.Id == id, ct);
        var item = dispute?.Items.SingleOrDefault(i => i.Id == itemId);
        if (dispute is null || item is null)
        {
            return TypedResults.NotFound();
        }

        if (item.Outcome is not null)
        {
            return TypedResults.Ok((await ViewsAsync(db, [dispute], ct))[0]); // already settled: same answer
        }

        var userId = user.UserId()!.Value;
        var now = clock.GetUtcNow();
        var line = await db.BillLines.AsNoTracking().SingleAsync(l => l.Id == item.BillLineId, ct);
        var missing = item.Problem == ItemProblem.Short && item.ReceivedQuantity is { } got ? line.Quantity - got : line.Quantity;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (request.Outcome == ItemOutcome.Refunded)
            {
                var amount = Disputed(line, item);
                var entry = new LedgerEntry
                {
                    CustomerId = dispute.CustomerId,
                    Kind = LedgerEntryKind.Return,
                    Amount = -amount,
                    BillId = line.BillId,
                    Note = $"{line.Name} × {missing:0.###}",
                    OccurredAt = now,
                    CreatedByUserId = userId,
                };
                db.LedgerEntries.Add(entry);
                item.ReturnEntryId = entry.Id;
                await db.Customers.Where(c => c.Id == dispute.CustomerId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance - amount), ct);
            }

            item.Outcome = request.Outcome;
            item.OutcomeNote = note;
            item.ResolvedAt = now;
            item.ResolvedByUserId = userId;
            if (dispute.Items.All(i => i.Outcome is not null) && dispute.ResolvedAt is null)
            {
                dispute.ResolvedAt = now;
                dispute.ResolvedByUserId = userId;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        // The goods are back on the shelf (they never left the shop). Its own step: stock has its own transaction,
        // and a missing product (an item typed in at billing) simply has no stock to return.
        if (request.Outcome == ItemOutcome.Refunded && line.ProductId is { } productId)
        {
            await stock.AddMovementAsync(new StockMovementRequest(Guid.CreateVersion7(), productId, StockMovementKind.Adjustment, missing,
                StockReason.Return, null, $"Dispute: not given to the customer"), userId, ct);
        }

        return TypedResults.Ok((await ViewsAsync(db, [dispute], ct))[0]);
    }
}
