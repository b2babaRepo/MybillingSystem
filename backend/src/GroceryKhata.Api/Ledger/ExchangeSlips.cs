using System.Security.Cryptography;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// An exchange slip: the credit (or return) note of goods taken back for an exchange, which any counter can use to pay
/// for new goods. <see cref="Status"/> is "open" (<see cref="Balance"/> left to use), "used" (all of it; last on
/// <see cref="UsedOnBillNo"/>), "expired" or "cancelled". <see cref="Code"/> is only given to the owner and the print.
/// </summary>
public sealed record ExchangeSlip(Guid Id, string NoteNo, decimal Amount, decimal Balance, DateTimeOffset IssuedAt, DateOnly ValidTill, string Status,
    string BillNo, Guid? CustomerId, string? CustomerName, string? CustomerPhone, string? UsedOnBillNo, string? Code = null);

public sealed record SlipNewCodeRequest(string? Phone);

public sealed record SlipCancelRequest(string? Reason);

public static class ExchangeSlips
{
    /// <summary>How long a slip can be used unless the shop sets its own (<see cref="Shops.Shop.ExchangeSlipDays"/>), like the big clothing chains' 90-day credit notes.</summary>
    public const int ValidDays = 90;

    private static readonly TimeSpan India = TimeSpan.FromHours(5.5);

    public static Task<int> DaysAsync(AppDbContext db, CancellationToken ct) => db.Shops.AsNoTracking().Where(s => s.Id == db.ShopIdNow).Select(s => s.ExchangeSlipDays).FirstAsync(ct);

    public static DateOnly ValidTill(DateTimeOffset issuedAt, int days = ValidDays) => DateOnly.FromDateTime(issuedAt.ToOffset(India).DateTime).AddDays(days);

    public static bool Expired(DateTimeOffset issuedAt, DateTimeOffset now, int days = ValidDays) => DateOnly.FromDateTime(now.ToOffset(India).DateTime) > ValidTill(issuedAt, days);

    public static string NewCode() => RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");

    public static IEndpointRouteBuilder MapExchangeSlips(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/exchange-slips").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        // Any counter: the slip by its number and its secret code (typed: "CN/2026-27/0003" or "3" plus the 4 digits), or
        // its QR ("GKSLIP:<id>:<code>"). Without the right code it is "not found", so slip numbers can't be guessed.
        group.MapGet("/find", async (string? no, string? code, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var text = no?.Trim().ToUpperInvariant() ?? "";
            if (text.StartsWith("GKSLIP:", StringComparison.Ordinal) && text.Split(':') is [_, var qrId, var qrCode])
            {
                text = qrId;
                code = qrCode;
            }

            if (text.StartsWith("CN/", StringComparison.Ordinal) || text.StartsWith("RN/", StringComparison.Ordinal)) text = text[3..];
            code = code?.Trim();
            if (code is not { Length: 4 })
            {
                return Results.NotFound();
            }

            var notes = db.CreditNotes.AsNoTracking().Where(n => n.ForExchange && n.SlipCode == code);
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

            var found = await ListAsync(db, clock, notes.OrderByDescending(n => n.OccurredAt).Take(1), false, ct);
            return found.Count == 0 ? Results.NotFound() : Results.Ok(found[0]);
        });
        // The owner: one slip with its code (to print it again), and every slip of the last months, open ones first.
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            (await ListAsync(db, clock, db.CreditNotes.AsNoTracking().Where(n => n.ForExchange && n.Id == id), true, ct)) is [var slip] ? Results.Ok(slip) : Results.NotFound())
            .RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", async (AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var since = clock.GetUtcNow().AddDays(-(await DaysAsync(db, ct) + 30));
            var all = await ListAsync(db, clock, db.CreditNotes.AsNoTracking().Where(n => n.ForExchange && n.OccurredAt >= since), true, ct);
            return Results.Ok(all.OrderBy(s => s.Status != "open").ThenByDescending(s => s.IssuedAt).ToList());
        }).RequireAuthorization(AppPolicies.ShopOwner);
        // A lost slip: a new code, so the lost paper stops working. If the slip has a customer with a mobile, it must match.
        group.MapPost("/{id:guid}/new-code", async (Guid id, SlipNewCodeRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var note = await db.CreditNotes.SingleOrDefaultAsync(n => n.Id == id && n.ForExchange, ct);
            if (note is null) return Results.NotFound();
            if (note.SlipCancelledAt is not null || note.Spent >= note.Total)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Id"] = ["This slip is cancelled or used up: nothing to give a new code for."] });
            }

            var phone = note.CustomerId is null ? null : await db.Customers.Where(c => c.Id == note.CustomerId).Select(c => c.Phone).SingleOrDefaultAsync(ct);
            if (phone is { Length: > 0 } && Last10(phone) != Last10(request.Phone))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["That mobile number isn't the one on this slip."] });
            }

            note.SlipCode = NewCode();
            await db.SaveChangesAsync(ct);
            return Results.Ok((await ListAsync(db, clock, db.CreditNotes.AsNoTracking().Where(n => n.Id == id), true, ct))[0]);
        }).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/cancel", async (Guid id, SlipCancelRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (request.Reason?.Trim() is not { Length: >= 3 and <= 200 } reason)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Say why the slip is cancelled (3 to 200 letters)."] });
            }

            var updated = await db.CreditNotes.Where(n => n.Id == id && n.ForExchange && n.SlipCancelledAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.SlipCancelledAt, clock.GetUtcNow()).SetProperty(n => n.Reason, n => n.Reason + " · Slip cancelled: " + reason), ct);
            return updated == 0 ? Results.NotFound() : Results.Ok((await ListAsync(db, clock, db.CreditNotes.AsNoTracking().Where(n => n.Id == id), true, ct))[0]);
        }).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static string Last10(string? s) => new string((s ?? "").Where(char.IsAsciiDigit).ToArray()) is var d && d.Length >= 10 ? d[^10..] : d;

    private static async Task<List<ExchangeSlip>> ListAsync(AppDbContext db, TimeProvider clock, IQueryable<CreditNote> notes, bool withCode, CancellationToken ct)
    {
        var rows = await notes.Select(n => new
        {
            n.Id, n.FinancialYear, n.Number, n.TaxFree, n.Total, n.Spent, n.OccurredAt, n.CustomerId, n.ExchangeBillId, n.SlipCode, n.SlipCancelledAt,
            Bill = db.Bills.Where(b => b.Id == n.BillId).Select(b => new { b.FinancialYear, b.Number }).FirstOrDefault(),
            Used = db.Bills.Where(b => b.Id == n.ExchangeBillId).Select(b => new { b.FinancialYear, b.Number }).FirstOrDefault(),
            Customer = db.Customers.Where(c => c.Id == n.CustomerId).Select(c => new { c.Name, c.Phone }).FirstOrDefault(),
        }).ToListAsync(ct);
        var now = clock.GetUtcNow();
        var days = await DaysAsync(db, ct);
        return rows.Select(r => new ExchangeSlip(r.Id, CreditNote.No(r.FinancialYear, r.Number, r.TaxFree), r.Total, r.SlipCancelledAt is null ? r.Total - r.Spent : 0, r.OccurredAt, ValidTill(r.OccurredAt, days),
            r.SlipCancelledAt is not null ? "cancelled" : r.Spent >= r.Total ? "used" : Expired(r.OccurredAt, now, days) ? "expired" : "open",
            r.Bill is null ? "" : $"{r.Bill.FinancialYear}/{r.Bill.Number:D4}", r.CustomerId, r.Customer?.Name, r.Customer?.Phone,
            r.Used is null ? null : $"{r.Used.FinancialYear}/{r.Used.Number:D4}", withCode ? r.SlipCode : null)).ToList();
    }
}
