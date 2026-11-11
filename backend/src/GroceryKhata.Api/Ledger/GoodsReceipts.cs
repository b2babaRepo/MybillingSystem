using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Goods that came in before their bill (a goods receipt note): the boxes are counted onto the shelf now, with no money
/// owed yet. When the supplier's bill comes, it is linked to the receipt, so the bill adds no stock a second time.
/// </summary>
public sealed class GoodsReceipt : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>The order the goods came against, if any: the receipt counts them off it.</summary>
    public Guid? PurchaseOrderId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>The lines (JSON): product, name and units counted in.</summary>
    public required string Lines { get; set; }

    public string? Note { get; set; }

    /// <summary>The supplier bill these goods were billed on. Null: still waiting for the bill.</summary>
    public Guid? PurchaseId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record ReceiptLine(Guid ProductId, string Name, decimal Quantity);

public sealed record ReceiptLineRequest(Guid ProductId, decimal Quantity);

public sealed record GoodsReceiptRequest(Guid Id, Guid SupplierId, IReadOnlyList<ReceiptLineRequest>? Lines, Guid? OrderId = null, string? Note = null);

public sealed record GoodsReceiptDto(
    Guid Id, int Number, Guid SupplierId, Guid? PurchaseOrderId, DateTimeOffset ReceivedAt, IReadOnlyList<ReceiptLine> Lines, string? Note, Guid? PurchaseId, DateTimeOffset? CancelledAt, string? By);

public static class GoodsReceipts
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapGoodsReceipts(this IEndpointRouteBuilder app)
    {
        // Whoever is at the counter when the boxes come counts them in; the bill and the money stay with the owner.
        var group = app.MapGroup("/api/v1/goods-receipts").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    internal static List<ReceiptLine> LinesOf(GoodsReceipt r) => JsonSerializer.Deserialize<List<ReceiptLine>>(r.Lines, Json) ?? [];

    /// <summary>A supplier's receipts (or the shop's), newest first; <paramref name="open"/>: only those still waiting for a bill.</summary>
    private static async Task<IReadOnlyList<GoodsReceiptDto>> ListAsync(Guid? supplierId, bool? open, AppDbContext db, CancellationToken ct)
    {
        var rows = await (from r in db.GoodsReceipts.AsNoTracking()
                          where (supplierId == null || r.SupplierId == supplierId) && (open != true || (r.PurchaseId == null && r.CancelledAt == null))
                          join u in db.Users.AsNoTracking() on r.CreatedByUserId equals u.Id into users
                          from u in users.DefaultIfEmpty()
                          orderby r.ReceivedAt descending
                          select new { r, By = u == null ? null : u.FullName }).Take(100).ToListAsync(ct);
        return rows.Select(x => ToDto(x.r, x.By)).ToList();
    }

    private static GoodsReceiptDto ToDto(GoodsReceipt r, string? by) =>
        new(r.Id, r.Number, r.SupplierId, r.PurchaseOrderId, r.ReceivedAt, LinesOf(r), r.Note, r.PurchaseId, r.CancelledAt, by);

    private static async Task<IResult> AddAsync(GoodsReceiptRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok(ToDto(existing, null));
        }

        var lines = request.Lines ?? [];
        var products = await db.Products.AsNoTracking().Where(p => lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200 || lines.Select(l => l.ProductId).Distinct().Count() != lines.Count
            || lines.Any(l => !products.ContainsKey(l.ProductId) || products[l.ProductId].IsService || !Money.IsValidQuantity(l.Quantity))
            || !await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct))
        {
            return Invalid("Lines", "Choose the supplier and the items that came, with how many of each.");
        }

        if (request.Note?.Trim().Length > 200)
        {
            return Invalid("Note", "Keep the note under 200 letters.");
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (request.OrderId is { } orderId
            && await PurchaseOrders.ReceiveAsync(db, orderId, request.SupplierId, lines.Select(l => (l.ProductId, l.Quantity)), 1, clock, ct) is { } orderProblem)
        {
            return Invalid("OrderId", orderProblem);
        }

        // One number at a time: two receipts saved at once would otherwise take the same number.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('goods_receipts'))", ct);
        var receipt = new GoodsReceipt
        {
            Id = request.Id, SupplierId = request.SupplierId, PurchaseOrderId = request.OrderId, ReceivedAt = now, CreatedByUserId = userId,
            Number = (await db.GoodsReceipts.MaxAsync(r => (int?)r.Number, ct) ?? 0) + 1,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Lines = JsonSerializer.Serialize(lines.Select(l => new ReceiptLine(l.ProductId, products[l.ProductId].Name, l.Quantity)).ToList(), Json),
        };
        db.GoodsReceipts.Add(receipt);
        foreach (var line in lines)
        {
            // The cost isn't known until the bill: the item keeps its cost price until then.
            db.StockMovements.Add(new StockMovement
            {
                RefType = "receipt", RefId = receipt.Id, ProductId = line.ProductId, Kind = StockMovementKind.Purchase, Quantity = line.Quantity, OccurredAt = now, CreatedByUserId = userId, Note = $"Goods receipt #{receipt.Number}",
            });
        }

        await db.SaveChangesAsync(ct);
        var ids = lines.Select(l => l.ProductId).Order().ToArray();
        var quantities = ids.Select(i => lines.Single(l => l.ProductId == i).Quantity).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock + v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Created($"/api/v1/goods-receipts/{receipt.Id}", ToDto(receipt, null));
    }

    /// <summary>A receipt made by mistake, before its bill: the goods come off the shelf again and off the order.</summary>
    private static async Task<IResult> CancelAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var receipt = await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
        {
            return TypedResults.NotFound();
        }

        if (receipt.PurchaseId is not null)
        {
            return Invalid("Id", "These goods are already on a supplier bill: cancel the bill first.");
        }

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.GoodsReceipts.Where(r => r.Id == id && r.CancelledAt == null && r.PurchaseId == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.CancelledAt, now), ct) == 0)
        {
            return TypedResults.NoContent();
        }

        var lines = LinesOf(receipt);
        if (receipt.PurchaseOrderId is { } orderId)
        {
            await PurchaseOrders.ReceiveAsync(db, orderId, receipt.SupplierId, lines.Select(l => (l.ProductId, l.Quantity)), -1, clock, ct);
        }

        foreach (var line in lines)
        {
            db.StockMovements.Add(new StockMovement
            {
                RefType = "receipt", RefId = receipt.Id, ProductId = line.ProductId, Kind = StockMovementKind.Adjustment, Reason = StockReason.Other, Quantity = -line.Quantity, OccurredAt = now,
                CreatedByUserId = userId, Note = $"Goods receipt #{receipt.Number} cancelled",
            });
        }

        await db.SaveChangesAsync(ct);
        var ids = lines.Select(l => l.ProductId).Order().ToArray();
        var quantities = ids.Select(i => lines.Single(l => l.ProductId == i).Quantity).ToArray();
        await db.Database.ExecuteSqlAsync($"UPDATE products AS p SET stock = p.stock - v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
