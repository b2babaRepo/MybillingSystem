using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One batch of a medicine on the shelf: its batch number, expiry (good to the end of that month, as packs print MM/YYYY),
/// MRP and what it cost. Selling takes the batch that expires first; an expired batch is never sold.
/// </summary>
public sealed class ProductBatch : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid ProductId { get; set; }

    public required string BatchNo { get; set; }

    /// <summary>The last day of the expiry month.</summary>
    public DateOnly Expiry { get; set; }

    public decimal Mrp { get; set; }

    /// <summary>Cost of one unit, without GST, after free goods (10+1 makes it 10/11 of the rate).</summary>
    public decimal? CostPrice { get; set; }

    public decimal Quantity { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}

/// <summary>How much of a batch a bill took, so cancelling the bill puts it back on that batch.</summary>
public sealed class BatchSale : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BillId { get; set; }

    public Guid BatchId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }
}

/// <summary><see cref="ExpiryMonth"/>: "2027-05" (the month printed on the pack).</summary>
public sealed record BatchRequest(Guid Id, string? BatchNo, string? ExpiryMonth, decimal Mrp, decimal? CostPrice, decimal Quantity, decimal FreeQuantity = 0);

public sealed record BatchDto(Guid Id, Guid ProductId, string BatchNo, DateOnly Expiry, decimal Mrp, decimal? CostPrice, decimal Quantity, bool Expired, int DaysLeft);

public sealed record ExpiringDto(Guid Id, Guid ProductId, string ProductName, string BatchNo, DateOnly Expiry, decimal Quantity, decimal Value, bool Expired, int DaysLeft);

public static class Batches
{
    public static IEndpointRouteBuilder MapBatches(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Products").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/products/{productId:guid}/batches", ListAsync);
        group.MapPost("/products/{productId:guid}/batches", ReceiveAsync);
        group.MapGet("/batches/expiring", ExpiringAsync);
        return app;
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);

    /// <summary>"Batch B123, exp 05/27": how a bill line names the batches it came from.</summary>
    public static string Label(IEnumerable<(string BatchNo, DateOnly Expiry)> batches)
    {
        var label = string.Join("; ", batches.Select(b => $"{b.BatchNo} exp {b.Expiry:MM/yy}"));
        return label.Length <= 200 ? label : label[..199] + "…";
    }

    private static BatchDto ToDto(ProductBatch b, DateOnly today) =>
        new(b.Id, b.ProductId, b.BatchNo, b.Expiry, b.Mrp, b.CostPrice, b.Quantity, b.Expiry < today, b.Expiry.DayNumber - today.DayNumber);

    private static async Task<IResult> ListAsync(Guid productId, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = Today(clock);
        var list = await db.ProductBatches.AsNoTracking().Where(b => b.ProductId == productId && b.Quantity > 0).OrderBy(b => b.Expiry).ThenBy(b => b.ReceivedAt).ToListAsync(ct);
        return TypedResults.Ok(list.Select(b => ToDto(b, today)).ToList());
    }

    /// <summary>
    /// Stock received in a batch (paid plus free units). The item's stock goes up; if the batch is already on the shelf
    /// (same number and expiry) it is topped up. The item's MRP and cost follow the newest batch.
    /// </summary>
    private static async Task<IResult> ReceiveAsync(Guid productId, BatchRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId && p.IsActive, ct);
        if (product is null || product.IsService)
        {
            return TypedResults.NotFound();
        }

        if (await db.StockMovements.AnyAsync(m => m.Id == request.Id, ct))
        {
            return await ListAsync(productId, db, clock, ct);
        }

        var batchNo = request.BatchNo?.Trim().ToUpperInvariant();
        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (batchNo is not { Length: >= 1 and <= 30 }) return Invalid("BatchNo", "Enter the batch number printed on the pack.");
        if (!DateOnly.TryParseExact($"{request.ExpiryMonth}-01", "yyyy-MM-dd", out var first)) return Invalid("ExpiryMonth", "Enter the expiry month (MM/YYYY).");
        var expiry = first.AddMonths(1).AddDays(-1);
        var today = Today(clock);
        if (expiry < today) return Invalid("ExpiryMonth", "This batch has already expired.");
        if (expiry > today.AddYears(10)) return Invalid("ExpiryMonth", "Check the expiry month.");
        if (request.Mrp <= 0 || !Money.IsValidAmount(request.Mrp)) return Invalid("Mrp", "Enter the MRP printed on the pack.");
        if (request.CostPrice is { } c && (c < 0 || c > request.Mrp || decimal.Round(c, 2) != c)) return Invalid("CostPrice", "The cost must be between ₹0 and the MRP.");
        if (!Money.IsValidQuantity(request.Quantity) || request.FreeQuantity < 0 || (request.FreeQuantity > 0 && !Money.IsValidQuantity(request.FreeQuantity)))
        {
            return Invalid("Quantity", "Enter how many came, with at most three decimals.");
        }

        var units = request.Quantity + request.FreeQuantity;
        // Free goods lower the real cost of every unit: 10 paid + 1 free at ₹76 is ₹69.09 each.
        var cost = request.CostPrice is { } paid ? decimal.Round(paid * request.Quantity / units, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AddToBatchAsync(db, productId, batchNo, expiry, request.Mrp, cost, units, now, ct);
        db.StockMovements.Add(new StockMovement
        {
            Id = request.Id, ProductId = productId, Kind = StockMovementKind.Purchase, Quantity = units, CostPrice = cost,
            Note = $"Batch {batchNo} exp {expiry:MM/yy}", OccurredAt = now, CreatedByUserId = user.UserId()!.Value,
        });
        product.SellingPrice = request.Mrp;
        product.CostPrice = cost ?? product.CostPrice;
        await db.SaveChangesAsync(ct);
        await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + units), ct);
        await transaction.CommitAsync(ct);
        return await ListAsync(productId, db, clock, ct);
    }

    /// <summary>"2027-05" → 31 May 2027 (packs are good to the end of the month), or null when it isn't a month.</summary>
    public static DateOnly? ParseExpiry(string? month) =>
        DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var first) ? first.AddMonths(1).AddDays(-1) : null;

    /// <summary>Adds units to a batch on the shelf (same number and expiry), or starts one. The caller moves the item's stock.</summary>
    public static async Task<ProductBatch> AddToBatchAsync(AppDbContext db, Guid productId, string batchNo, DateOnly expiry, decimal mrp, decimal? cost, decimal units, DateTimeOffset now, CancellationToken ct)
    {
        var batch = db.ProductBatches.Local.SingleOrDefault(b => b.ProductId == productId && b.BatchNo == batchNo && b.Expiry == expiry)
            ?? await db.ProductBatches.SingleOrDefaultAsync(b => b.ProductId == productId && b.BatchNo == batchNo && b.Expiry == expiry, ct);
        if (batch is null)
        {
            batch = new ProductBatch { ProductId = productId, BatchNo = batchNo, Expiry = expiry, Mrp = mrp, CostPrice = cost, ReceivedAt = now };
            db.ProductBatches.Add(batch);
        }
        else
        {
            (batch.Mrp, batch.CostPrice) = (mrp, cost ?? batch.CostPrice);
        }

        batch.Quantity += units;
        return batch;
    }

    /// <summary>Batches expired or expiring within <paramref name="days"/> (90 by default), soonest first, with their value at cost: what to return to the distributor.</summary>
    private static async Task<IReadOnlyList<ExpiringDto>> ExpiringAsync(int? days, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = Today(clock);
        var until = today.AddDays(Math.Clamp(days ?? 90, 0, 365));
        var list = await (from b in db.ProductBatches.AsNoTracking()
                          join p in db.Products.AsNoTracking() on b.ProductId equals p.Id
                          where b.Quantity > 0 && b.Expiry <= until
                          orderby b.Expiry, p.Name
                          select new { b, p.Name }).Take(500).ToListAsync(ct);
        return list.Select(x => new ExpiringDto(x.b.Id, x.b.ProductId, x.Name, x.b.BatchNo, x.b.Expiry, x.b.Quantity, Money.LineAmount(x.b.Quantity, x.b.CostPrice ?? 0),
            x.b.Expiry < today, x.b.Expiry.DayNumber - today.DayNumber)).ToList();
    }

    /// <summary>
    /// Takes a bill's items from their batches, the one expiring first first, inside the bill's transaction. Items with no
    /// batches are left alone. Returns an error when the unexpired batches don't have enough (an expired pack can't be sold).
    /// Each line gets its batch numbers and expiry, and the cost of the batches it came from.
    /// </summary>
    public static async Task<string?> AllocateAsync(AppDbContext db, Guid billId, IReadOnlyList<BillLine> lines, IReadOnlyCollection<Guid> productIds, DateOnly today, CancellationToken ct)
    {
        if (productIds.Count == 0)
        {
            return null;
        }

        var ids = productIds.ToArray();
        // Lock the batches so two counters can't sell the same strip.
        var batches = await db.ProductBatches.FromSql($"SELECT * FROM product_batches WHERE product_id = ANY({ids}) AND quantity > 0 ORDER BY expiry, received_at FOR UPDATE").ToListAsync(ct);
        if (batches.Count == 0)
        {
            return null;
        }

        foreach (var line in lines.Where(l => l.ProductId is { } id && batches.Any(b => b.ProductId == id)))
        {
            var left = line.Quantity;
            var taken = new List<(ProductBatch Batch, decimal Quantity)>();
            foreach (var batch in batches.Where(b => b.ProductId == line.ProductId && b.Expiry >= today && b.Quantity > 0).OrderBy(b => b.Expiry).ThenBy(b => b.ReceivedAt))
            {
                var take = Math.Min(left, batch.Quantity);
                batch.Quantity -= take;
                left -= take;
                taken.Add((batch, take));
                if (left == 0)
                {
                    break;
                }
            }

            if (left > 0)
            {
                var good = taken.Sum(t => t.Quantity);
                return batches.Any(b => b.ProductId == line.ProductId && b.Expiry < today)
                    ? $"{line.Name}: only {good:0.###} in stock that hasn't expired. Expired packs can't be sold."
                    : $"{line.Name}: only {good:0.###} in stock. Receive a new batch first.";
            }

            line.Batch = Label(taken.Select(t => (t.Batch.BatchNo, t.Batch.Expiry)));
            if (taken.All(t => t.Batch.CostPrice is not null))
            {
                line.UnitCost = decimal.Round(taken.Sum(t => t.Quantity * t.Batch.CostPrice!.Value) / line.Quantity, 2, MidpointRounding.AwayFromZero);
            }

            db.BatchSales.AddRange(taken.Select(t => new BatchSale { BillId = billId, BatchId = t.Batch.Id, ProductId = t.Batch.ProductId, Quantity = t.Quantity }));
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>A cancelled bill's units go back on the batches they came from.</summary>
    public static async Task RestoreAsync(AppDbContext db, Guid billId, CancellationToken ct)
    {
        var sales = await db.BatchSales.AsNoTracking().Where(s => s.BillId == billId).ToListAsync(ct);
        foreach (var s in sales)
        {
            await db.ProductBatches.Where(b => b.Id == s.BatchId).ExecuteUpdateAsync(u => u.SetProperty(b => b.Quantity, b => b.Quantity + s.Quantity), ct);
        }
    }
}
