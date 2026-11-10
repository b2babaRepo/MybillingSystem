using System.Security.Claims;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A customer's cheque, as the cheque list shows it.</summary>
public sealed record ChequeView(
    Guid Id, Guid CustomerId, string Customer, decimal Amount, string? ChequeNo, string? Bank, DateOnly? ChequeDate,
    ChequeStatus Status, DateTimeOffset ReceivedAt, DateTimeOffset? SettledAt, string? BounceNote);

public sealed record ChequeListDto(IReadOnlyList<ChequeView> Cheques, decimal PendingAmount, int PendingCount);

public sealed record SettleChequeRequest(string? Reason);

/// <summary>
/// Customers' cheques: handed over (pending), then cleared or bounced. A pending cheque already lowers what the
/// customer owes, but stays out of money received until the owner marks it cleared; a bounce puts it back on the khata.
/// </summary>
public static class Cheques
{
    public static IEndpointRouteBuilder MapCheques(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cheques").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        // Settling a cheque changes the books and the customer's khata: the owner's call.
        group.MapPost("/{id:guid}/clear", async (Guid id, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.SettleChequeAsync(id, true, null, user.UserId()!.Value, ct)).ToHttp($"/api/v1/cheques/{id}"))
            .RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/bounce", async (Guid id, SettleChequeRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.SettleChequeAsync(id, false, request.Reason, user.UserId()!.Value, ct)).ToHttp($"/api/v1/cheques/{id}"))
            .RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>Cheques still waiting on the bank (not removed from the khata), and what they add up to.</summary>
    public static async Task<(decimal Amount, int Count)> PendingAsync(AppDbContext db, CancellationToken ct)
    {
        var pending = await Pending(db).Select(e => -e.Amount).ToListAsync(ct);
        return (pending.Sum(), pending.Count);
    }

    private static IQueryable<LedgerEntry> Pending(AppDbContext db) =>
        db.LedgerEntries.AsNoTracking().Where(e => e.ChequeStatus == ChequeStatus.Pending && e.ReversesEntryId == null
            && !db.LedgerEntries.Any(r => r.ReversesEntryId == e.Id));

    /// <summary>Pending cheques first (oldest cheque date first), then the last 90 days' cleared and bounced ones.</summary>
    private static async Task<ChequeListDto> ListAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-90);
        var rows = await (from e in db.LedgerEntries.AsNoTracking()
                          where e.PaymentMode == PaymentMode.Cheque && e.ReversesEntryId == null && e.Kind == LedgerEntryKind.Payment
                              && !db.LedgerEntries.Any(r => r.ReversesEntryId == e.Id && r.ChequeStatus != ChequeStatus.Bounced)
                              && (e.ChequeStatus == ChequeStatus.Pending || e.ChequeSettledAt >= since)
                          join c in db.Customers.AsNoTracking() on e.CustomerId equals c.Id
                          select new { e, c.Name }).ToListAsync(ct);
        var bounceNotes = await db.LedgerEntries.AsNoTracking()
            .Where(r => r.ChequeStatus == ChequeStatus.Bounced && r.ReversesEntryId != null && r.OccurredAt >= since)
            .ToDictionaryAsync(r => r.ReversesEntryId!.Value, r => r.Note, ct);
        var list = rows.Where(r => r.e.ChequeStatus == ChequeStatus.Pending).OrderBy(r => r.e.ChequeDate ?? DateOnly.FromDateTime(r.e.OccurredAt.UtcDateTime))
            .Concat(rows.Where(r => r.e.ChequeStatus != ChequeStatus.Pending).OrderByDescending(r => r.e.ChequeSettledAt))
            .Select(r => new ChequeView(r.e.Id, r.e.CustomerId, r.Name, -r.e.Amount, r.e.ChequeNo, r.e.ChequeBank, r.e.ChequeDate, r.e.ChequeStatus ?? ChequeStatus.Pending,
                r.e.OccurredAt, r.e.ChequeSettledAt, bounceNotes.GetValueOrDefault(r.e.Id)))
            .ToList();
        var pending = list.Where(c => c.Status == ChequeStatus.Pending).ToList();
        return new ChequeListDto(list, pending.Sum(c => c.Amount), pending.Count);
    }
}
