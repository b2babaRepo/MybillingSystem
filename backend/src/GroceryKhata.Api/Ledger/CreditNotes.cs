using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Goods taken back on a bill: a numbered document (CN/2026-27/0001) that lowers the sale and its tax. The money goes
/// back on the customer's khata, or out of the drawer for a walk-in sale.
/// </summary>
public sealed class CreditNote : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BillId { get; set; }

    public Guid? CustomerId { get; set; }

    public int Number { get; set; }

    public string FinancialYear { get; set; } = "";

    public string NoteNo => $"CN/{FinancialYear}/{Number:D4}";

    public required string Reason { get; set; }

    public decimal Total { get; set; }

    /// <summary>The goods went back on the shelf (not damaged or missing).</summary>
    public bool Restocked { get; set; }

    /// <summary>Kept as credit for an exchange: no money goes back; the new bill (<see cref="ExchangeBillId"/>) uses it.</summary>
    public bool ForExchange { get; set; }

    public Guid? ExchangeBillId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public List<CreditNoteLine> Lines { get; set; } = [];
}

/// <summary>Part of one bill line taken back, with its share of the line's tax.</summary>
public sealed class CreditNoteLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CreditNoteId { get; set; }

    public int BillLineNo { get; set; }

    public required string Name { get; set; }

    public decimal Quantity { get; set; }

    public decimal Amount { get; set; }

    public decimal? GstRate { get; set; }

    public string? HsnCode { get; set; }

    public decimal TaxableValue { get; set; }

    public decimal Cgst { get; set; }

    public decimal Sgst { get; set; }

    public decimal Igst { get; set; }
}

public sealed record CreditNoteLineRequest(int LineNo, decimal Quantity);

/// <summary><see cref="Restock"/>: the goods go back on the shelf (not damaged).</summary>
public sealed record CreditNoteRequest(Guid Id, string? Reason, IReadOnlyList<CreditNoteLineRequest>? Lines, bool Restock = true, bool Exchange = false);

public sealed record CreditNoteLineDto(int BillLineNo, string Name, decimal Quantity, decimal Amount, decimal TaxableValue, decimal Cgst, decimal Sgst, decimal Igst);

public sealed record CreditNoteDto(Guid Id, string NoteNo, Guid BillId, string Reason, decimal Total, DateTimeOffset OccurredAt, IReadOnlyList<CreditNoteLineDto> Lines);

public static class CreditNotes
{
    public static IEndpointRouteBuilder MapCreditNotes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/bills/{billId:guid}/credit-notes").WithTags("Khata").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        return app;
    }

    public static CreditNoteDto ToDto(CreditNote n) => new(n.Id, n.NoteNo, n.BillId, n.Reason, n.Total, n.OccurredAt,
        n.Lines.OrderBy(l => l.BillLineNo).Select(l => new CreditNoteLineDto(l.BillLineNo, l.Name, l.Quantity, l.Amount, l.TaxableValue, l.Cgst, l.Sgst, l.Igst)).ToList());

    private static async Task<IReadOnlyList<CreditNoteDto>> ListAsync(Guid billId, AppDbContext db, CancellationToken ct) =>
        (await db.CreditNotes.AsNoTracking().Include(n => n.Lines).Where(n => n.BillId == billId).OrderBy(n => n.OccurredAt).ToListAsync(ct)).Select(ToDto).ToList();

    private static async Task<IResult> AddAsync(Guid billId, CreditNoteRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.CreditNotes.AsNoTracking().Include(n => n.Lines).SingleOrDefaultAsync(n => n.Id == request.Id, ct) is { } saved)
        {
            return TypedResults.Ok(ToDto(saved));
        }

        var bill = await db.Bills.AsNoTracking().Include(b => b.Lines).SingleOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null)
        {
            return TypedResults.NotFound();
        }

        if (bill.CancelledAt is not null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This bill is cancelled: nothing to take back.");
        }

        // What each line still has to give back after earlier credit notes.
        var returned = (await db.CreditNoteLines.AsNoTracking().Where(l => db.CreditNotes.Any(n => n.Id == l.CreditNoteId && n.BillId == billId)).ToListAsync(ct))
            .GroupBy(l => l.BillLineNo).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var reason = request.Reason?.Trim();
        var asked = (request.Lines ?? []).Where(l => l.Quantity > 0).ToList();
        if (request.Id == Guid.Empty || reason is not { Length: >= 3 and <= 200 } || asked.Count == 0
            || asked.Any(a => bill.Lines.SingleOrDefault(l => l.LineNo == a.LineNo) is not { } line || !Money.IsValidQuantity(a.Quantity)
                || a.Quantity > line.Quantity - returned.GetValueOrDefault(a.LineNo)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = ["Say why, and take back no more than was sold (less what was already returned)."] });
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        var lines = asked.Select(a =>
        {
            var line = bill.Lines.Single(l => l.LineNo == a.LineNo);
            var share = a.Quantity / line.Quantity;
            var amount = decimal.Round(line.Amount * share, 2);
            // Tax in the same share as the bill line had; a line with no stored tax is worked out from its rate.
            var taxable = line.TaxableValue is { } t ? decimal.Round(t * share, 2) : bill.Gstin is not null && line.GstRate is { } r ? decimal.Round(amount / (1 + (r / 100)), 2) : amount;
            var igst = line.Igst is > 0 ? amount - taxable : 0;
            var cgst = igst == 0 ? decimal.Round((amount - taxable) / 2, 2) : 0;
            return new CreditNoteLine
            {
                BillLineNo = line.LineNo, Name = line.Name, Quantity = a.Quantity, Amount = amount, GstRate = line.GstRate, HsnCode = line.HsnCode,
                TaxableValue = taxable, Igst = igst, Cgst = cgst, Sgst = igst == 0 ? amount - taxable - cgst : 0,
            };
        }).ToList();
        var total = lines.Sum(l => l.Amount);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var india = now.ToOffset(IndiaTime.Offset);
        var startYear = india.Month >= 4 ? india.Year : india.Year - 1;
        var year = $"{startYear}-{(startYear + 1) % 100:D2}";
        var number = (await db.CreditNotes.Where(n => n.FinancialYear == year).MaxAsync(n => (int?)n.Number, ct) ?? 0) + 1;
        var note = new CreditNote
        {
            Id = request.Id, BillId = billId, CustomerId = bill.CustomerId, FinancialYear = year, Number = number, Reason = reason!,
            Total = total, Restocked = request.Restock, OccurredAt = now, CreatedByUserId = userId, Lines = lines, ForExchange = request.Exchange,
        };
        db.CreditNotes.Add(note);
        if (request.Exchange)
        {
            // An exchange: the value stays as credit for the new bill, so neither the khata nor the drawer moves now.
        }
        else if (bill.CustomerId is { } customerId)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = customerId, Kind = LedgerEntryKind.Return, Amount = -total, BillId = billId, Note = note.NoteNo, OccurredAt = now, CreatedByUserId = userId,
            });
        }
        else
        {
            // A walk-in sale was paid at the counter: the money goes back out of the drawer.
            db.Expenses.Add(new Expense { Amount = total, Category = ExpenseCategory.Other, Mode = bill.PaymentMode ?? PaymentMode.Cash, Note = $"Refund {note.NoteNo}", OccurredAt = now, CreatedByUserId = userId });
        }

        if (request.Restock)
        {
            foreach (var (line, ask) in asked.Select(a => (bill.Lines.Single(l => l.LineNo == a.LineNo), a)).Where(x => x.Item1.ProductId != null))
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = line.ProductId!.Value, Kind = StockMovementKind.Adjustment, Reason = StockReason.Return, Quantity = ask.Quantity, BillId = billId,
                    OccurredAt = now, CreatedByUserId = userId, Note = note.NoteNo,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        if (bill.CustomerId is { } owing)
        {
            await db.Customers.Where(c => c.Id == owing).ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance - total), ct);
        }

        if (request.Restock)
        {
            foreach (var group in asked.Select(a => (bill.Lines.Single(l => l.LineNo == a.LineNo), a)).Where(x => x.Item1.ProductId != null).GroupBy(x => x.Item1.ProductId!.Value))
            {
                var quantity = group.Sum(x => x.a.Quantity);
                await db.Products.Where(p => p.Id == group.Key).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity), ct);
            }
        }

        await transaction.CommitAsync(ct);
        return TypedResults.Ok(ToDto(note));
    }
}
