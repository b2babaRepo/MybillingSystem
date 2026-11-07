using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

public enum ReceiptSource
{
    /// <summary>Typed by the shop, with the customer already known.</summary>
    Manual,

    /// <summary>The shop saw money arrive and noted it, not yet knowing whose it is.</summary>
    Noted,
}

/// <summary>Money that isn't khata money, so nobody chases it.</summary>
public enum ReceiptDisposition
{
    CounterSale,
    NotKhata,
}

/// <summary>
/// Proof that money arrived, kept apart from whose it is. The ledger stays the only source of balances: when a match
/// turns out wrong, the ledger entry is reversed and this receipt is free to be matched again, so the money stays
/// provably received and only its place in the books changes. One UTR, one receipt.
/// </summary>
public sealed class IncomingReceipt : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public ReceiptSource Source { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string? Utr { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string? PayerName { get; set; }

    public ReceiptDisposition? Disposition { get; set; }

    public Guid? DispositionByUserId { get; set; }

    public Guid CreatedByUserId { get; set; }
}

/// <summary>
/// Which ledger entry took how much of a receipt. Never changed or deleted: an allocation stops counting when its
/// ledger entry is reversed, so the history of every wrong match and its correction stays readable.
/// </summary>
public sealed class ReceiptAllocation : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid LedgerEntryId { get; set; }

    public decimal Amount { get; set; }

    public Guid UserId { get; set; }
}

public sealed record NewReceiptRequest(Guid Id, decimal Amount, string? Utr, string? PayerName, DateTimeOffset? ReceivedAt);

public sealed record AllocateRequest(Guid Id, Guid CustomerId, decimal Amount);

/// <summary><see cref="As"/>: what it was; null puts it back in the tray.</summary>
public sealed record DismissRequest(ReceiptDisposition? As);

/// <summary><see cref="Why"/>: amount (exactly what they owe) or name (the payer's name fits only them).</summary>
public sealed record ReceiptSuggestion(Guid CustomerId, string Name, string Why);

/// <summary><see cref="Left"/>: what isn't matched to a customer yet.</summary>
public sealed record ReceiptDto(Guid Id, ReceiptSource Source, decimal Amount, decimal Left, DateTimeOffset ReceivedAt, string? Utr, string? PayerName, ReceiptSuggestion? Suggestion);

public static class Receipts
{
    public static IEndpointRouteBuilder MapReceipts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/receipts").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", NoteAsync);
        group.MapPost("/{id:guid}/allocate", AllocateAsync);
        group.MapPost("/{id:guid}/dismiss", DismissAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>What of a receipt is matched to customers now: reversed entries no longer count.</summary>
    internal static async Task<decimal> MatchedAsync(AppDbContext db, Guid receiptId, CancellationToken ct) =>
        await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.ReceiptId == receiptId && !db.LedgerEntries.Any(e => e.ReversesEntryId == a.LedgerEntryId))
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

    /// <summary>The one customer it most likely is, or none: with two possibilities the shop chooses, the app never guesses.</summary>
    public static ReceiptSuggestion? Suggest(decimal left, string? payerName, IReadOnlyList<(Guid Id, string Name, decimal Balance)> customers)
    {
        var byAmount = customers.Where(c => c.Balance > 0 && c.Balance == left).ToList();
        if (byAmount.Count == 1)
        {
            return new ReceiptSuggestion(byAmount[0].Id, byAmount[0].Name, "amount");
        }

        var payer = payerName?.Trim();
        if (byAmount.Count == 0 && payer is { Length: >= 3 })
        {
            var byName = customers.Where(c => c.Balance > 0 && (c.Name.Contains(payer, StringComparison.OrdinalIgnoreCase) || payer.Contains(c.Name.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
            if (byName.Count == 1)
            {
                return new ReceiptSuggestion(byName[0].Id, byName[0].Name, "name");
            }
        }

        return null;
    }

    /// <summary>The tray: money that arrived and isn't fully matched, newest first.</summary>
    private static async Task<IReadOnlyList<ReceiptDto>> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.IncomingReceipts.AsNoTracking().Where(r => r.Disposition == null)
            .Select(r => new
            {
                Receipt = r,
                Matched = db.ReceiptAllocations.Where(a => a.ReceiptId == r.Id && !db.LedgerEntries.Any(e => e.ReversesEntryId == a.LedgerEntryId)).Sum(a => (decimal?)a.Amount) ?? 0m,
            })
            .Where(x => x.Matched < x.Receipt.Amount)
            .OrderByDescending(x => x.Receipt.ReceivedAt).Take(200).ToListAsync(ct);
        var customers = (await db.Customers.AsNoTracking().Select(c => new { c.Id, c.Name, c.Balance }).ToListAsync(ct)).Select(c => (c.Id, c.Name, c.Balance)).ToList();
        return rows.Select(x =>
        {
            var left = x.Receipt.Amount - x.Matched;
            return new ReceiptDto(x.Receipt.Id, x.Receipt.Source, x.Receipt.Amount, left, x.Receipt.ReceivedAt, x.Receipt.Utr, x.Receipt.PayerName, Suggest(left, x.Receipt.PayerName, customers));
        }).ToList();
    }

    /// <summary>Money seen in the bank or payment app, whoever it turns out to be from. It touches no balance until it is matched.</summary>
    private static async Task<IResult> NoteAsync(NewReceiptRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var utr = string.IsNullOrWhiteSpace(request.Utr) ? null : request.Utr.Trim().ToUpperInvariant();
        var payer = string.IsNullOrWhiteSpace(request.PayerName) ? null : request.PayerName.Trim();
        if (request.Id == Guid.Empty) errors[nameof(request.Id)] = ["The app must send an ID for every new record."];
        if (!Money.IsValidAmount(request.Amount)) errors[nameof(request.Amount)] = ["Enter the amount received."];
        if (utr is not null && (utr.Length is < 6 or > 30 || !utr.All(char.IsAsciiLetterOrDigit))) errors[nameof(request.Utr)] = ["The UPI reference is 6 to 30 letters and digits (the UTR in the payment app)."];
        if (payer?.Length > 120) errors[nameof(request.PayerName)] = ["Keep the name under 120 letters."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await db.IncomingReceipts.AsNoTracking().AnyAsync(r => r.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        if (utr is not null && await db.IncomingReceipts.AsNoTracking().AnyAsync(r => r.Utr == utr, ct))
        {
            return TypedResults.Conflict(new { title = "This UPI reference is already recorded. The same payment can't be entered twice." });
        }

        var now = clock.GetUtcNow();
        db.IncomingReceipts.Add(new IncomingReceipt
        {
            Id = request.Id, Source = ReceiptSource.Noted, Amount = request.Amount, Utr = utr, PayerName = payer,
            ReceivedAt = request.ReceivedAt is { } at && at <= now ? at : now, CreatedByUserId = user.UserId()!.Value,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return TypedResults.Conflict(new { title = "This UPI reference is already recorded. The same payment can't be entered twice." });
        }

        return TypedResults.NoContent();
    }

    /// <summary>This money, or part of it, is this customer's: a payment goes on their khata, tied to the receipt.</summary>
    private static async Task<IResult> AllocateAsync(Guid id, AllocateRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
        (await ledger.AddKhataEntryAsync(
            new KhataEntryRequest(request.Id, request.CustomerId, KhataEntryType.Payment, request.Amount, PaymentMode.Upi, null, null),
            user.UserId()!.Value, ct, LedgerEndpoints.IsOwner(user), id)).ToHttp($"/api/v1/customers/{request.CustomerId}/statement");

    /// <summary>A counter sale or not khata money at all: out of the tray, and nobody chases it. Only money nothing is matched to.</summary>
    private static async Task<IResult> DismissAsync(Guid id, DismissRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var receipt = await db.IncomingReceipts.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
        {
            return TypedResults.NotFound();
        }

        if (request.As is not null && await MatchedAsync(db, id, ct) > 0)
        {
            return TypedResults.Conflict(new { title = "Part of this money is on a customer's khata. Remove that entry first." });
        }

        (receipt.Disposition, receipt.DispositionByUserId) = (request.As, request.As is null ? null : user.UserId());
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
