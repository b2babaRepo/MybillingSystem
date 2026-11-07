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

    /// <summary>From a bank or UPI statement the shop imported: the bank's own record.</summary>
    Statement,

    /// <summary>The customer says they paid. Only their word, so it never moves a balance by itself.</summary>
    Claim,
}

/// <summary>Money that isn't khata money, so nobody chases it.</summary>
public enum ReceiptDisposition
{
    CounterSale,
    NotKhata,

    /// <summary>A customer said they paid, and the shop can't find the money. They are told.</summary>
    NotReceived,
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

    /// <summary>A statement row with no UTR, hashed: importing the same statement again finds it. The bank's words aren't kept.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(64)]
    public string? Fingerprint { get; set; }

    public ReceiptDisposition? Disposition { get; set; }

    /// <summary>Who said they paid this. Null unless it started as a claim.</summary>
    public Guid? ClaimedByCustomerId { get; set; }

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

public sealed record PaidClaimRequest(Guid Id, decimal Amount, string? Utr);

/// <summary>What a customer sees of a payment they reported. <see cref="Status"/>: waiting or notReceived.</summary>
public sealed record ClaimStatusDto(decimal Amount, DateTimeOffset ReportedAt, string Status);

public sealed record StatementRow(decimal Amount, string? Utr, string? PayerName, DateTimeOffset ReceivedAt, string? Narration);

public sealed record StatementImportRequest(IReadOnlyList<StatementRow>? Rows);

/// <summary><see cref="Known"/>: rows already here, from an earlier import or a payment typed with its UTR. <see cref="Confirmed"/>: rows that proved a customer's claim.</summary>
public sealed record StatementImportResult(int Added, int Known, IReadOnlyList<string> Problems, int Confirmed = 0);

public sealed record LinkRequest(Guid EntryId);

/// <summary><see cref="As"/>: what it was; null puts it back in the tray.</summary>
public sealed record DismissRequest(ReceiptDisposition? As);

/// <summary><see cref="Why"/>: amount (exactly what they owe), name (the payer's name fits only them) or claim (they said they paid).</summary>
public sealed record ReceiptSuggestion(Guid CustomerId, string Name, string Why);

/// <summary>
/// What this money may already be in the books as. <see cref="Kind"/>: entered (a UPI payment typed on a khata, so
/// matching it again would count it twice) or sale (a bill paid by UPI). The shop decides; nothing happens by itself.
/// </summary>
public sealed record ReceiptAlready(string Kind, Guid? EntryId, string? CustomerName);

/// <summary><see cref="Left"/>: what isn't matched to a customer yet.</summary>
public sealed record ReceiptDto(Guid Id, ReceiptSource Source, decimal Amount, decimal Left, DateTimeOffset ReceivedAt, string? Utr, string? PayerName, ReceiptSuggestion? Suggestion, ReceiptAlready? Already = null);

public static class Receipts
{
    public static IEndpointRouteBuilder MapReceipts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/receipts").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", NoteAsync);
        group.MapPost("/import", ImportAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/allocate", AllocateAsync);
        group.MapPost("/{id:guid}/link", LinkAsync);
        group.MapPost("/{id:guid}/dismiss", DismissAsync).RequireAuthorization(AppPolicies.ShopOwner);
        app.MapPost("/api/v1/khata/{slug}/{customerId:guid}/paid", ClaimByLinkAsync)
            .WithTags("Khata").AllowAnonymous().RequireRateLimiting(Planner.StoreEndpoints.StoreRateLimit);
        app.MapPost("/api/v1/me/khata/{shopId:guid}/{customerId:guid}/paid", ClaimMineAsync)
            .WithTags("Khata").RequireAuthorization().RequireRateLimiting(Planner.StoreEndpoints.StoreRateLimit);
        return app;
    }

    /// <summary>Waiting claims per customer: enough for real life, too few for anyone to flood the shop's tray.</summary>
    public const int MaxOpenClaims = 3;

    /// <summary>Money still needing the shop's attention, with how much of each is unmatched, newest first.</summary>
    internal static async Task<List<(IncomingReceipt Receipt, decimal Left)>> OpenAsync(AppDbContext db, CancellationToken ct) =>
        (await db.IncomingReceipts.AsNoTracking().Where(r => r.Disposition == null)
            .Select(r => new
            {
                Receipt = r,
                Matched = db.ReceiptAllocations.Where(a => a.ReceiptId == r.Id && !db.LedgerEntries.Any(e => e.ReversesEntryId == a.LedgerEntryId)).Sum(a => (decimal?)a.Amount) ?? 0m,
            })
            .Where(x => x.Matched < x.Receipt.Amount)
            .OrderByDescending(x => x.Receipt.ReceivedAt).Take(200).ToListAsync(ct))
        .Select(x => (x.Receipt, x.Receipt.Amount - x.Matched)).ToList();

    /// <summary>The customer's reports from the last 30 days that aren't on their khata, so they know where each one stands.</summary>
    internal static async Task<IReadOnlyList<ClaimStatusDto>> ClaimsForAsync(AppDbContext db, Guid customerId, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-30);
        var rows = await db.IncomingReceipts.AsNoTracking()
            .Where(r => r.ClaimedByCustomerId == customerId && r.ReceivedAt >= since && (r.Disposition == null || r.Disposition == ReceiptDisposition.NotReceived))
            .Select(r => new
            {
                r.Amount, r.ReceivedAt, r.Disposition,
                Matched = db.ReceiptAllocations.Where(a => a.ReceiptId == r.Id && !db.LedgerEntries.Any(e => e.ReversesEntryId == a.LedgerEntryId)).Sum(a => (decimal?)a.Amount) ?? 0m,
            })
            .ToListAsync(ct);
        return rows.Where(x => x.Matched < x.Amount).OrderByDescending(x => x.ReceivedAt)
            .Select(x => new ClaimStatusDto(x.Amount, x.ReceivedAt, x.Disposition is null ? "waiting" : "notReceived")).ToList();
    }

    /// <summary>The customer's own "I have paid", from their private link.</summary>
    private static async Task<IResult> ClaimByLinkAsync(
        string slug, Guid customerId, string? t, PaidClaimRequest request, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct) =>
        await KhataViewEndpoints.OpenByLinkAsync(slug, customerId, t, db, tenant, ct) is null ? TypedResults.NotFound() : await ClaimAsync(db, customerId, Guid.Empty, request, clock, ct);

    /// <summary>The same from the household app, for a khata they have confirmed is theirs.</summary>
    private static async Task<IResult> ClaimMineAsync(
        Guid shopId, Guid customerId, PaidClaimRequest request, ClaimsPrincipal user, IServiceScopeFactory scopes, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await KhataViewEndpoints.OpenMineAsync(shopId, customerId, user, scopes, db, ct) is not { } scope)
        {
            return TypedResults.NotFound();
        }

        await using (scope)
        {
            return await ClaimAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), customerId, user.UserId()!.Value, request, clock, ct);
        }
    }

    private static async Task<IResult> ClaimAsync(AppDbContext db, Guid customerId, Guid userId, PaidClaimRequest request, TimeProvider clock, CancellationToken ct)
    {
        var utr = string.IsNullOrWhiteSpace(request.Utr) ? null : request.Utr.Trim().ToUpperInvariant();
        if (request.Id == Guid.Empty || !Money.IsValidAmount(request.Amount) || (utr is not null && (utr.Length is < 6 or > 30 || !utr.All(char.IsAsciiLetterOrDigit))))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = ["Check the amount and the UPI reference."] });
        }

        if (await db.IncomingReceipts.AsNoTracking().AnyAsync(r => r.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        var waiting = (await OpenAsync(db, ct)).Count(x => x.Receipt.ClaimedByCustomerId == customerId);
        if (waiting >= MaxOpenClaims)
        {
            return TypedResults.Conflict(new { title = "The shop hasn't looked at your earlier messages yet. Please call them." });
        }

        if (utr is not null && await db.IncomingReceipts.AsNoTracking().AnyAsync(r => r.Utr == utr, ct))
        {
            return TypedResults.Conflict(new { title = "The shop already has this UPI reference." });
        }

        db.IncomingReceipts.Add(new IncomingReceipt
        {
            Id = request.Id, Source = ReceiptSource.Claim, Amount = request.Amount, Utr = utr, ClaimedByCustomerId = customerId, ReceivedAt = clock.GetUtcNow(), CreatedByUserId = userId,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>What of a receipt is matched to customers now: reversed entries no longer count.</summary>
    internal static async Task<decimal> MatchedAsync(AppDbContext db, Guid receiptId, CancellationToken ct) =>
        await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.ReceiptId == receiptId && !db.LedgerEntries.Any(e => e.ReversesEntryId == a.LedgerEntryId))
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

    /// <summary>How far apart a statement's date and an entry typed at the counter can be: statements only give the day.</summary>
    private static readonly TimeSpan SameMoney = TimeSpan.FromHours(72);

    /// <summary>
    /// An unlinked UPI payment of exactly this amount near this time, if there is only one: with two the shop must look.
    /// Else a bill paid by UPI for this amount. Entries already claimed by an earlier receipt in the list are skipped.
    /// </summary>
    public static ReceiptAlready? Already(
        decimal left, DateTimeOffset at, IReadOnlyList<(Guid Id, string Customer, decimal Amount, DateTimeOffset At)> entries,
        IReadOnlyList<(decimal Paid, DateTimeOffset At)> upiBills, ISet<Guid> claimed)
    {
        var typed = entries.Where(e => e.Amount == left && !claimed.Contains(e.Id) && (e.At - at).Duration() <= SameMoney).ToList();
        if (typed.Count == 1)
        {
            claimed.Add(typed[0].Id);
            return new ReceiptAlready("entered", typed[0].Id, typed[0].Customer);
        }

        return typed.Count == 0 && upiBills.Any(b => b.Paid == left && (b.At - at).Duration() <= SameMoney) ? new ReceiptAlready("sale", null, null) : null;
    }

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
        var rows = await OpenAsync(db, ct);
        var customers = (await db.Customers.AsNoTracking().Select(c => new { c.Id, c.Name, c.Balance }).ToListAsync(ct)).Select(c => (c.Id, c.Name, c.Balance)).ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        var since = rows.Min(x => x.Receipt.ReceivedAt) - SameMoney;
        var entries = (await (from e in db.LedgerEntries.AsNoTracking()
                              join c in db.Customers.AsNoTracking() on e.CustomerId equals c.Id
                              where e.Kind == LedgerEntryKind.Payment && e.PaymentMode == PaymentMode.Upi && e.BillId == null && e.OccurredAt >= since
                                  && !db.LedgerEntries.Any(r => r.ReversesEntryId == e.Id) && !db.ReceiptAllocations.Any(a => a.LedgerEntryId == e.Id)
                              select new { e.Id, c.Name, e.Amount, e.OccurredAt }).ToListAsync(ct)).Select(e => (e.Id, e.Name, -e.Amount, e.OccurredAt)).ToList();
        var bills = (await db.Bills.AsNoTracking().Where(b => b.PaymentMode == PaymentMode.Upi && b.CancelledAt == null && b.OccurredAt >= since)
            .Select(b => new { b.PaidAmount, b.OccurredAt }).ToListAsync(ct)).Select(b => (b.PaidAmount, b.OccurredAt)).ToList();
        var claimed = new HashSet<Guid>();
        return rows.Select(x =>
        {
            var already = Already(x.Left, x.Receipt.ReceivedAt, entries, bills, claimed);
            var who = customers.FirstOrDefault(c => c.Id == x.Receipt.ClaimedByCustomerId);
            var suggestion = x.Receipt.ClaimedByCustomerId is { } claimant && who.Id == claimant
                ? new ReceiptSuggestion(claimant, who.Name, "claim")
                : Suggest(x.Left, x.Receipt.PayerName, customers);
            return new ReceiptDto(x.Receipt.Id, x.Receipt.Source, x.Receipt.Amount, x.Left, x.Receipt.ReceivedAt, x.Receipt.Utr, x.Receipt.PayerName, already is null ? suggestion : null, already);
        }).ToList();
    }

    /// <summary>
    /// Money in from a statement, into the tray. It moves no balance. A row already here is skipped by its UTR, or by a
    /// fingerprint when it has none, so importing overlapping statements is safe. Identical rows in one file are told
    /// apart by their order, so two equal payments on one day both count, and both are found again next time.
    /// </summary>
    private static async Task<IResult> ImportAsync(StatementImportRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (request.Rows is not { Count: > 0 and <= 5000 } rows)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Rows"] = ["Send between 1 and 5,000 rows."] });
        }

        var utrs = (await db.IncomingReceipts.AsNoTracking().Where(r => r.Utr != null).Select(r => r.Utr!).ToListAsync(ct)).ToHashSet();
        var prints = (await db.IncomingReceipts.AsNoTracking().Where(r => r.Fingerprint != null).Select(r => r.Fingerprint!).ToListAsync(ct)).ToHashSet();
        var seen = new Dictionary<string, int>();
        var problems = new List<string>();
        var known = 0;
        var added = 0;
        var confirmed = 0;
        // Claims the bank can now prove, kept as one receipt rather than a second one beside them.
        var claims = await db.IncomingReceipts.Where(r => r.Source == ReceiptSource.Claim).ToListAsync(ct);
        var userId = user.UserId()!.Value;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var utr = string.IsNullOrWhiteSpace(row.Utr) ? null : row.Utr.Trim().ToUpperInvariant();
            if (!Money.IsValidAmount(row.Amount) || (utr is not null && (utr.Length is < 6 or > 30 || !utr.All(char.IsAsciiLetterOrDigit))))
            {
                problems.Add($"Row {i + 1}: the amount or reference isn't valid.");
                continue;
            }

            string? print = null;
            if (utr is null)
            {
                var plain = $"{row.ReceivedAt:yyyy-MM-dd}|{row.Amount:0.00}|{row.Narration?.Trim()}";
                seen[plain] = seen.GetValueOrDefault(plain) + 1;
                print = Secrets.Sha256Hex($"{plain}|{seen[plain]}");
            }

            var claimedUtr = utr is null ? null : claims.FirstOrDefault(c => c.Utr == utr);
            if (utr is not null ? !utrs.Add(utr) : !prints.Add(print!))
            {
                if (claimedUtr is null)
                {
                    known++;
                    continue;
                }
            }

            var payer = row.PayerName?.Trim();
            var cleanPayer = string.IsNullOrEmpty(payer) ? null : payer[..Math.Min(payer.Length, 120)];
            // The same money the customer said they paid: by its UTR, or by being the only claim of this amount around then.
            // A claim the shop turned down isn't proof of anything, so it can't take this money in by amount.
            var fits = claims.Where(c => c.Utr is null && c.Disposition is null && c.Amount == row.Amount && (c.ReceivedAt - row.ReceivedAt).Duration() <= SameMoney).ToList();
            if ((claimedUtr ?? (fits.Count == 1 ? fits[0] : null)) is { } claim)
            {
                // From here it is the bank's record, so the bank's figure and date replace the customer's, and a claim turned
                // down but now proved by its UTR comes back to the tray.
                if (claim.Amount != row.Amount)
                {
                    problems.Add($"Row {i + 1}: the customer said {claim.Amount:0.##} but the bank shows {row.Amount:0.##}. The bank's figure is kept.");
                }

                (claim.Source, claim.Utr, claim.Fingerprint, claim.PayerName, claim.Amount) = (ReceiptSource.Statement, utr, print, cleanPayer, row.Amount);
                (claim.ReceivedAt, claim.Disposition, claim.DispositionByUserId) = (row.ReceivedAt.ToUniversalTime(), null, null);
                claims.Remove(claim);
                confirmed++;
                continue;
            }

            db.IncomingReceipts.Add(new IncomingReceipt
            {
                Source = ReceiptSource.Statement, Amount = row.Amount, ReceivedAt = row.ReceivedAt.ToUniversalTime(), Utr = utr, Fingerprint = print,
                PayerName = cleanPayer, CreatedByUserId = userId,
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new StatementImportResult(added, known, problems, confirmed));
    }

    /// <summary>This money is a payment already typed on a khata: tie the two together, with no second entry and no balance change.</summary>
    private static async Task<IResult> LinkAsync(Guid id, LinkRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var receipt = await db.IncomingReceipts.FromSql($"SELECT * FROM incoming_receipts WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        var entry = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.EntryId, ct);
        if (receipt is null || entry is null)
        {
            return TypedResults.NotFound();
        }

        if (entry.Kind != LedgerEntryKind.Payment || await db.LedgerEntries.AnyAsync(e => e.ReversesEntryId == entry.Id, ct) || await db.ReceiptAllocations.AnyAsync(a => a.LedgerEntryId == entry.Id, ct)
            || receipt.Disposition is not null || await MatchedAsync(db, id, ct) - entry.Amount > receipt.Amount)
        {
            return TypedResults.Conflict(new { title = "This entry can't be tied to that money." });
        }

        db.ReceiptAllocations.Add(new ReceiptAllocation { ReceiptId = id, CustomerId = entry.CustomerId, LedgerEntryId = entry.Id, Amount = -entry.Amount, UserId = user.UserId()!.Value });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
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
            ReceivedAt = request.ReceivedAt is { } at && at <= now ? at.ToUniversalTime() : now, CreatedByUserId = user.UserId()!.Value,
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

    /// <summary>A counter sale or not khata money at all: out of the tray, and nobody chases it. On a part-matched receipt this is what's left over.</summary>
    private static async Task<IResult> DismissAsync(Guid id, DismissRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var receipt = await db.IncomingReceipts.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
        {
            return TypedResults.NotFound();
        }

        (receipt.Disposition, receipt.DispositionByUserId) = (request.As, request.As is null ? null : user.UserId());
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
