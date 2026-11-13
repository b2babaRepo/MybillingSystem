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

    /// <summary>The batch it came from, for medicines; null for goods kept without batches.</summary>
    public Guid? BatchId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}

/// <summary>A batch going back (medicines), or an item kept without batches (<see cref="ProductId"/>).</summary>
public sealed record ReturnLineRequest(Guid? BatchId, decimal Quantity, Guid? ProductId = null);

public sealed record SupplierReturnRequest(Guid Id, Guid SupplierId, IReadOnlyList<ReturnLineRequest>? Lines);

/// <summary><see cref="Gst"/>: the GST shown on the supplier's credit note; left out, it is worked out from the goods' GST rates.</summary>
public sealed record ReturnCreditRequest(string? CreditNoteNo, decimal Amount, decimal? Gst = null);

public sealed record ReturnLineDto(string ProductName, string BatchNo, DateOnly? Expiry, decimal Quantity, decimal Value);

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

    /// <summary>Returns still waiting for a credit note, oldest first; with <paramref name="all"/>, settled ones too (newest first). One supplier's, or every one.</summary>
    private static async Task<IReadOnlyList<SupplierReturnDto>> ListAsync(bool? all, Guid? supplierId, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var query = db.SupplierReturns.AsNoTracking().Include(r => r.Lines).Where(r => supplierId == null || r.SupplierId == supplierId);
        query = all == true ? query.OrderByDescending(r => r.Number) : query.Where(r => r.SettledAt == null).OrderBy(r => r.Number);
        var list = await query.Take(200).ToListAsync(ct);
        var supplierIds = list.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var batchIds = list.SelectMany(r => r.Lines).Where(l => l.BatchId != null).Select(l => l.BatchId!.Value).Distinct().ToList();
        var looseIds = list.SelectMany(r => r.Lines).Where(l => l.BatchId == null).Select(l => l.ProductId).Distinct().ToList();
        var loose = await db.Products.AsNoTracking().Where(p => looseIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var batches = await (from b in db.ProductBatches.AsNoTracking() join p in db.Products.AsNoTracking() on b.ProductId equals p.Id where batchIds.Contains(b.Id) select new { b.Id, b.BatchNo, b.Expiry, p.Name })
            .ToDictionaryAsync(x => x.Id, ct);
        var now = clock.GetUtcNow();
        return list.Select(r => new SupplierReturnDto(
            r.Id, r.Number, r.SupplierId, suppliers.GetValueOrDefault(r.SupplierId) ?? "", r.Value, r.OccurredAt, (int)((r.SettledAt ?? now) - r.OccurredAt).TotalDays, r.CreditNoteNo, r.CreditAmount,
            r.SettledAt, r.CreditAmount is { } credit ? r.Value - credit : 0,
            r.Lines.Select(l => l.BatchId is { } batchId && batches.GetValueOrDefault(batchId) is { } b
                ? new ReturnLineDto(b.Name, b.BatchNo, b.Expiry, l.Quantity, Money.LineAmount(l.Quantity, l.UnitCost))
                : new ReturnLineDto(loose.GetValueOrDefault(l.ProductId) ?? "", "", null, l.Quantity, Money.LineAmount(l.Quantity, l.UnitCost))).ToList())).ToList();
    }

    /// <summary>Sends goods back, by batch for medicines or by item for everything else: their stock leaves now (a stock movement of kind Return). Safe to repeat.</summary>
    private static async Task<IResult> ReturnAsync(SupplierReturnRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.SupplierReturns.AnyAsync(r => r.Id == request.Id, ct))
        {
            return TypedResults.Ok(new { request.Id });
        }

        var lines = request.Lines ?? [];
        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct)) return Invalid("SupplierId", "Choose the distributor.");
        if (lines.Count is 0 or > 100 || lines.Any(l => (l.BatchId is null) == (l.ProductId is null) || !Money.IsValidQuantity(l.Quantity))
            || lines.Select(l => l.BatchId ?? l.ProductId).Distinct().Count() != lines.Count)
        {
            return Invalid("Lines", "Choose the goods and how many of each go back.");
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ids = lines.Where(l => l.BatchId != null).Select(l => l.BatchId!.Value).ToArray();
        var batches = await db.ProductBatches.FromSql($"SELECT * FROM product_batches WHERE id = ANY({ids}) FOR UPDATE").ToDictionaryAsync(b => b.Id, ct);
        var involved = batches.Values.Select(b => b.ProductId).Concat(lines.Where(l => l.ProductId != null).Select(l => l.ProductId!.Value)).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => involved.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Name, p.CostPrice, p.IsService }, ct);
        var batched = (await db.ProductBatches.AsNoTracking().Where(b => involved.Contains(b.ProductId)).Select(b => b.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        var record = new SupplierReturn
        {
            Id = request.Id, Number = (await db.SupplierReturns.MaxAsync(r => (int?)r.Number, ct) ?? 0) + 1, SupplierId = request.SupplierId, OccurredAt = now, CreatedByUserId = userId,
        };
        foreach (var line in lines)
        {
            if (line.ProductId is { } productId)
            {
                if (products.GetValueOrDefault(productId) is not { IsService: false } item)
                {
                    return Invalid("Lines", "An item wasn't found.");
                }

                // Kept by batch, it goes back by batch, or the batches and the shelf would stop agreeing.
                if (batched.Contains(productId))
                {
                    return Invalid("Lines", $"{item.Name} is kept by batch: send it back from expiry returns, batch by batch.");
                }

                var each = item.CostPrice ?? 0;
                record.Lines.Add(new SupplierReturnLine { ProductId = productId, Quantity = line.Quantity, UnitCost = each });
                db.StockMovements.Add(new StockMovement
                {
                    RefType = "supplier_return", RefId = record.Id, ProductId = productId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Return, Quantity = -line.Quantity, CostPrice = each,
                    Note = "Returned to supplier", OccurredAt = now, CreatedByUserId = userId,
                });
                continue;
            }

            if (batches.GetValueOrDefault(line.BatchId!.Value) is not { } batch)
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
                RefType = "supplier_return", RefId = record.Id, ProductId = batch.ProductId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Return, Quantity = -line.Quantity, CostPrice = cost,
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
    /// <summary>The GST inside a credit of <paramref name="amount"/> for a return: each line's share by value, at its item's rate.</summary>
    private static async Task<decimal> GstInAsync(AppDbContext db, Guid returnId, decimal amount, CancellationToken ct)
    {
        var lines = await (from l in db.SupplierReturnLines.AsNoTracking()
                           where l.ReturnId == returnId
                           join p in db.Products.AsNoTracking() on l.ProductId equals p.Id
                           select new { Value = l.Quantity * l.UnitCost, Rate = p.GstRate ?? 0 }).ToListAsync(ct);
        var total = lines.Sum(l => l.Value);
        return total <= 0 ? 0 : decimal.Round(lines.Sum(l => amount * (l.Value / total) * l.Rate / (100 + l.Rate)), 2);
    }

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
            // The GST inside the credit: as shown on the note, else from each returned item's rate, shared by value.
            var gst = request.Gst is { } given && given >= 0 && given <= request.Amount ? decimal.Round(given, 2) : await GstInAsync(db, id, request.Amount, ct);
            db.SupplierEntries.Add(new SupplierEntry
            {
                Gst = gst,
                SupplierId = record.SupplierId, Kind = SupplierEntryKind.CreditNote, Amount = -request.Amount, Note = (noteNo is null ? $"Return #{record.Number}" : $"{noteNo} · return #{record.Number}")[..Math.Min(40, (noteNo is null ? $"Return #{record.Number}" : $"{noteNo} · return #{record.Number}").Length)], OccurredAt = now,
                CreatedByUserId = user.UserId()!.Value,
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
