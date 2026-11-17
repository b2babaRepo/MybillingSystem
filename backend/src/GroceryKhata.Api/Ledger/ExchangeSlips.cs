using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// An exchange slip: the credit note of goods taken back for an exchange, which any counter can use to pay for new goods.
/// <see cref="Status"/> is "open" (<see cref="Balance"/> left to use), "used" (all of it; last on <see cref="UsedOnBillNo"/>) or "expired".
/// </summary>
public sealed record ExchangeSlip(Guid Id, string NoteNo, decimal Amount, decimal Balance, DateTimeOffset IssuedAt, DateOnly ValidTill, string Status,
    string BillNo, Guid? CustomerId, string? CustomerName, string? CustomerPhone, string? UsedOnBillNo);

public static class ExchangeSlips
{
    /// <summary>How long a slip can be used unless the shop sets its own (<see cref="Shops.Shop.ExchangeSlipDays"/>), like the big clothing chains' 90-day credit notes.</summary>
    public const int ValidDays = 90;

    public static Task<int> DaysAsync(AppDbContext db, CancellationToken ct) => db.Shops.AsNoTracking().Where(s => s.Id == db.ShopIdNow).Select(s => s.ExchangeSlipDays).FirstAsync(ct);

    private static readonly TimeSpan India = TimeSpan.FromHours(5.5);

    public static DateOnly ValidTill(DateTimeOffset issuedAt, int days = ValidDays) => DateOnly.FromDateTime(issuedAt.ToOffset(India).DateTime).AddDays(days);

    public static bool Expired(DateTimeOffset issuedAt, DateTimeOffset now, int days = ValidDays) => DateOnly.FromDateTime(now.ToOffset(India).DateTime) > ValidTill(issuedAt, days);

    public static IEndpointRouteBuilder MapExchangeSlips(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/exchange-slips").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        // Any counter: the slip by its number ("CN/2026-27/0003", "2026-27/0003" or just "3"), or by id from its QR.
        group.MapGet("/find", async (string? no, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var text = no?.Trim().ToUpperInvariant() ?? "";
            if (text.StartsWith("CN/", StringComparison.Ordinal)) text = text[3..];
            var notes = db.CreditNotes.AsNoTracking().Where(n => n.ForExchange);
            if (Guid.TryParse(text, out var id))
            {
                notes = notes.Where(n => n.Id == id);
            }
            else
            {
                var slash = text.LastIndexOf('/');
                if (!int.TryParse(slash >= 0 ? text[(slash + 1)..] : text, out var number))
                {
                    return Results.NotFound();
                }

                var year = slash > 0 ? text[..slash] : null;
                notes = notes.Where(n => n.Number == number && (year == null || n.FinancialYear == year));
            }

            var found = await ListAsync(db, clock, notes.OrderByDescending(n => n.OccurredAt).Take(1), ct);
            return found.Count == 0 ? Results.NotFound() : Results.Ok(found[0]);
        });
        // The slips register: every exchange slip of the last 120 days, open ones first.
        group.MapGet("", async (AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var since = clock.GetUtcNow().AddDays(-(await DaysAsync(db, ct) + 30));
            var all = await ListAsync(db, clock, db.CreditNotes.AsNoTracking().Where(n => n.ForExchange && n.OccurredAt >= since), ct);
            return Results.Ok(all.OrderBy(s => s.Status != "open").ThenByDescending(s => s.IssuedAt).ToList());
        });
        return app;
    }

    private static async Task<List<ExchangeSlip>> ListAsync(AppDbContext db, TimeProvider clock, IQueryable<CreditNote> notes, CancellationToken ct)
    {
        var rows = await notes.Select(n => new
        {
            n.Id, n.FinancialYear, n.Number, n.Total, n.Spent, n.OccurredAt, n.CustomerId, n.ExchangeBillId,
            Bill = db.Bills.Where(b => b.Id == n.BillId).Select(b => new { b.FinancialYear, b.Number }).FirstOrDefault(),
            Used = db.Bills.Where(b => b.Id == n.ExchangeBillId).Select(b => new { b.FinancialYear, b.Number }).FirstOrDefault(),
            Customer = db.Customers.Where(c => c.Id == n.CustomerId).Select(c => new { c.Name, c.Phone }).FirstOrDefault(),
        }).ToListAsync(ct);
        var now = clock.GetUtcNow();
        var days = await DaysAsync(db, ct);
        return rows.Select(r => new ExchangeSlip(r.Id, $"CN/{r.FinancialYear}/{r.Number:D4}", r.Total, r.Total - r.Spent, r.OccurredAt, ValidTill(r.OccurredAt, days),
            r.Spent >= r.Total ? "used" : Expired(r.OccurredAt, now, days) ? "expired" : "open",
            r.Bill is null ? "" : $"{r.Bill.FinancialYear}/{r.Bill.Number:D4}", r.CustomerId, r.Customer?.Name, r.Customer?.Phone,
            r.Used is null ? null : $"{r.Used.FinancialYear}/{r.Used.Number:D4}")).ToList();
    }
}
