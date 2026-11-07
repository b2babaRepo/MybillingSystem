using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Expired or near-expiry stock sent back to a distributor. The stock leaves at once; what the shop owes the
/// distributor only comes down when their credit note arrives (often weeks later, and not always for the full value),
/// so a return stays open until then. The gap between the stock's value and the credit is the shop's expiry loss.
/// </summary>
public sealed class SupplierReturn : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>The stock's value at cost.</summary>
    public decimal Value { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string? CreditNoteNo { get; set; }

    public decimal? CreditAmount { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public List<SupplierReturnLine> Lines { get; set; } = [];
}

public sealed class SupplierReturnLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid ReturnId { get; set; }

    public Guid ProductId { get; set; }

    public Guid BatchId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}

public sealed record ReturnLineRequest(Guid BatchId, decimal Quantity);

public sealed record SupplierReturnRequest(Guid Id, Guid SupplierId, IReadOnlyList<ReturnLineRequest>? Lines);

public sealed record ReturnCreditRequest(string? CreditNoteNo, decimal Amount);

public sealed record ReturnLineDto(string ProductName, string BatchNo, DateOnly Expiry, decimal Quantity, decimal Value);

public sealed record SupplierReturnDto(
    Guid Id, int Number, Guid SupplierId, string SupplierName, decimal Value, DateTimeOffset OccurredAt, int DaysOpen, string? CreditNoteNo, decimal? CreditAmount,
    DateTimeOffset? SettledAt, decimal Loss, IReadOnlyList<ReturnLineDto> Lines);

public static class SupplierReturns
{
    public static IEndpointRouteBuilder MapSupplierReturns(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/suppliers/returns").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapPost("", ReturnAsync);
        group.MapPost("/{id:guid}/credit-note", CreditNoteAsync);
        return app;
    }

    /// <summary>Returns still waiting for a credit note, oldest first; with <paramref name="all"/>, settled ones too (newest first).</summary>
    private static async Task<IReadOnlyList<SupplierReturnDto>> ListAsync(bool? all, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var query = db.SupplierReturns.AsNoTracking().Include(r => r.Lines).AsQueryable();
        query = all == true ? query.OrderByDescending(r => r.Number) : query.Where(r => r.SettledAt == null).OrderBy(r => r.Number);
        var list = await query.Take(200).ToListAsync(ct);
        var supplierIds = list.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var batchIds = list.SelectMany(r => r.Lines).Select(l => l.BatchId).Distinct().ToList();
        var batches = await (from b in db.ProductBatches.AsNoTracking() join p in db.Products.AsNoTracking() on b.ProductId equals p.Id where batchIds.Contains(b.Id) select new { b.Id, b.BatchNo, b.Expiry, p.Name })
            .ToDictionaryAsync(x => x.Id, ct);
        var now = clock.GetUtcNow();
        return list.Select(r => new SupplierReturnDto(
            r.Id, r.Number, r.SupplierId, suppliers.GetValueOrDefault(r.SupplierId) ?? "", r.Value, r.OccurredAt, (int)((r.SettledAt ?? now) - r.OccurredAt).TotalDays, r.CreditNoteNo, r.CreditAmount,
            r.SettledAt, r.CreditAmount is { } credit ? r.Value - credit : 0,
            r.Lines.Select(l => batches.GetValueOrDefault(l.BatchId) is { } b
                ? new ReturnLineDto(b.Name, b.BatchNo, b.Expiry, l.Quantity, Money.LineAmount(l.Quantity, l.UnitCost))
                : new ReturnLineDto("", "", default, l.Quantity, Money.LineAmount(l.Quantity, l.UnitCost))).ToList())).ToList();
    }

    /// <summary>Sends batches back: their stock leaves now (a stock movement of kind Return). Safe to repeat.</summary>
    private static async Task<IResult> ReturnAsync(SupplierReturnRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.SupplierReturns.AnyAsync(r => r.Id == request.Id, ct))
        {
            return TypedResults.Ok(new { request.Id });
        }

        var lines = request.Lines ?? [];
        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct)) return Invalid("SupplierId", "Choose the distributor.");
        if (lines.Count is 0 or > 100 || lines.Select(l => l.BatchId).Distinct().Count() != lines.Count || lines.Any(l => !Money.IsValidQuantity(l.Quantity)))
        {
            return Invalid("Lines", "Choose the batches and how many of each go back.");
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ids = lines.Select(l => l.BatchId).ToArray();
        var batches = await db.ProductBatches.FromSql($"SELECT * FROM product_batches WHERE id = ANY({ids}) FOR UPDATE").ToDictionaryAsync(b => b.Id, ct);
        var products = await db.Products.AsNoTracking().Where(p => batches.Values.Select(b => b.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Name, p.CostPrice }, ct);
        var record = new SupplierReturn
        {
            Id = request.Id, Number = (await db.SupplierReturns.MaxAsync(r => (int?)r.Number, ct) ?? 0) + 1, SupplierId = request.SupplierId, OccurredAt = now, CreatedByUserId = userId,
        };
        foreach (var line in lines)
        {
            if (batches.GetValueOrDefault(line.BatchId) is not { } batch)
            {
                return Invalid("Lines", "A batch wasn't found.");
            }

            if (line.Quantity > batch.Quantity)
            {
                return Invalid("Lines", $"{products[batch.ProductId].Name} batch {batch.BatchNo}: only {batch.Quantity:0.###} left.");
            }

            var cost = batch.CostPrice ?? products[batch.ProductId].CostPrice ?? 0;
            batch.Quantity -= line.Quantity;
            record.Lines.Add(new SupplierReturnLine { ProductId = batch.ProductId, BatchId = batch.Id, Quantity = line.Quantity, UnitCost = cost });
            db.StockMovements.Add(new StockMovement
            {
                ProductId = batch.ProductId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Return, Quantity = -line.Quantity, CostPrice = cost,
                Note = $"Returned to distributor · batch {batch.BatchNo} exp {batch.Expiry:MM/yy}", OccurredAt = now, CreatedByUserId = userId,
            });
        }

        record.Value = record.Lines.Sum(l => Money.LineAmount(l.Quantity, l.UnitCost));
        db.SupplierReturns.Add(record);
        await db.SaveChangesAsync(ct);
        var byProduct = record.Lines.GroupBy(l => l.ProductId).OrderBy(g => g.Key).ToList();
        var productIds = byProduct.Select(g => g.Key).ToArray();
        var quantities = byProduct.Select(g => g.Sum(l => l.Quantity)).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock - v.q FROM unnest({productIds}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(new { request.Id });
    }

    /// <summary>The distributor's credit note arrived: it comes off what the shop owes them, and the return is settled.</summary>
    private static async Task<IResult> CreditNoteAsync(Guid id, ReturnCreditRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var record = await db.SupplierReturns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (record is null)
        {
            return TypedResults.NotFound();
        }

        if (record.SettledAt is not null)
        {
            return TypedResults.NoContent();
        }

        // A return with no known cost has a value of 0: its credit note still has to go in.
        if (request.Amount < 0 || (record.Value > 0 && request.Amount > record.Value * 2) || request.Amount > Money.MaxAmount || decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Invalid("Amount", "Enter the credit note's amount, or 0 if the distributor gave nothing.");
        }

        var number = request.CreditNoteNo?.Trim();
        var now = clock.GetUtcNow();
        var noteNo = string.IsNullOrEmpty(number) ? null : number[..Math.Min(40, number.Length)];
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // One update decides who settles it: a second tap changes no row and adds nothing.
        var settled = await db.SupplierReturns.Where(r => r.Id == id && r.SettledAt == null).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.CreditNoteNo, noteNo).SetProperty(r => r.CreditAmount, request.Amount).SetProperty(r => r.SettledAt, now), ct);
        if (settled == 0)
        {
            return TypedResults.NoContent();
        }

        if (request.Amount > 0)
        {
            db.SupplierEntries.Add(new SupplierEntry
            {
                SupplierId = record.SupplierId, Amount = -request.Amount, Note = $"Credit note {noteNo ?? ""} · return #{record.Number}".Replace("  ", " "), OccurredAt = now,
                CreatedByUserId = user.UserId()!.Value,
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
