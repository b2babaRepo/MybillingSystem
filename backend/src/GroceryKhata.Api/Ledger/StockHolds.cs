using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Planner;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

public enum HoldStatus
{
    Active,
    Fulfilled,
    Released,
}

/// <summary>
/// Stock kept aside at the counter for a customer ("keep 2 bags for Sharmaji till Friday"). Holding stock moves
/// nothing: the goods stay on the shelf and in <see cref="Catalogue.Product.Stock"/>; a hold only makes them not
/// free to sell. A hold past its date stays held, and shows as overdue, so the owner decides.
/// </summary>
public sealed class StockHold : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public Guid? CustomerId { get; set; }

    public required string PartyName { get; set; }

    public DateOnly? Until { get; set; }

    public string? Note { get; set; }

    public HoldStatus Status { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

/// <summary>
/// One hold on stock, whatever made it: an online order (held at checkout), a sale order (still to deliver) or a
/// counter hold. <see cref="ShortBy"/>: how much of it the shelf can't cover, giving stock to the oldest hold first.
/// </summary>
public sealed record HoldDto(string Source, Guid SourceId, string Reference, Guid ProductId, string Product, Catalogue.Unit Unit, decimal Quantity,
    string Party, DateOnly? Until, bool Overdue, decimal ShortBy, DateTimeOffset Since, string? Note);

public sealed record HoldRequest(Guid Id, Guid ProductId, decimal Quantity, Guid? CustomerId, string? PartyName, DateOnly? Until, string? Note);

public static class StockHolds
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapStockHolds(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/holds").WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapPost("/{id:guid}/release", (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) => EndAsync(id, HoldStatus.Released, db, clock, ct));
        group.MapPost("/{id:guid}/done", (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) => EndAsync(id, HoldStatus.Fulfilled, db, clock, ct));
        return app;
    }

    /// <summary>Counter holds still active, per item: they count as promised, like sale orders.</summary>
    internal static async Task<Dictionary<Guid, decimal>> HeldAsync(AppDbContext db, CancellationToken ct) =>
        await db.StockHolds.AsNoTracking().Where(h => h.Status == HoldStatus.Active)
            .GroupBy(h => h.ProductId).Select(g => new { g.Key, Q = g.Sum(h => h.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Q, ct);

    /// <summary>Every hold on stock, oldest first, with what the shelf can't cover.</summary>
    internal static async Task<List<HoldDto>> AllAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var raw = new List<(string Source, Guid SourceId, string Reference, Guid ProductId, decimal Quantity, string Party, DateOnly? Until, DateTimeOffset Since, string? Note)>();

        var online = await (from l in db.OrderLines.AsNoTracking()
                            join o in db.Orders.AsNoTracking() on l.OrderId equals o.Id
                            where l.ProductId != null && l.ReservedQuantity > 0
                            select new { o.Id, o.CustomerName, o.CreatedAt, ProductId = l.ProductId!.Value, l.ReservedQuantity, o.Slot }).ToListAsync(ct);
        raw.AddRange(online.Select(o => ("online", o.Id, "", o.ProductId, o.ReservedQuantity, o.CustomerName, (DateOnly?)null, o.CreatedAt, o.Slot)));

        var sale = await db.SaleOrders.AsNoTracking().Where(o => o.ClosedAt == null && o.CancelledAt == null)
            .Select(o => new { o.Id, o.Number, o.PartyName, o.CustomerId, o.DueOn, o.CreatedAt, o.Lines, o.Note }).ToListAsync(ct);
        var customerIds = sale.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        foreach (var o in sale)
        {
            var party = o.CustomerId is { } cid && customers.TryGetValue(cid, out var n) ? n : o.PartyName ?? "—";
            foreach (var l in (JsonSerializer.Deserialize<List<SaleOrderLine>>(o.Lines, Json) ?? []).Where(l => l.ProductId != null && l.Pending > 0))
            {
                raw.Add(("sale_order", o.Id, o.Number.ToString(), l.ProductId!.Value, l.Pending, party, o.DueOn, o.CreatedAt, o.Note));
            }
        }

        var counter = await db.StockHolds.AsNoTracking().Where(h => h.Status == HoldStatus.Active).ToListAsync(ct);
        raw.AddRange(counter.Select(h => ("counter", h.Id, "", h.ProductId, h.Quantity, h.PartyName, h.Until, h.CreatedAt, h.Note)));

        var ids = raw.Select(r => r.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Unit, p.Stock }).ToDictionaryAsync(p => p.Id, ct);
        var left = products.ToDictionary(p => p.Key, p => Math.Max(0, p.Value.Stock));
        return raw.Where(r => products.ContainsKey(r.ProductId)).OrderBy(r => r.Since).Select(r =>
        {
            var covered = Math.Min(left[r.ProductId], r.Quantity);
            left[r.ProductId] -= covered;
            var p = products[r.ProductId];
            return new HoldDto(r.Source, r.SourceId, r.Reference, r.ProductId, p.Name, p.Unit, r.Quantity, r.Party, r.Until, r.Until is { } u && u < today, r.Quantity - covered, r.Since, r.Note);
        }).ToList();
    }

    private static async Task<IReadOnlyList<HoldDto>> ListAsync(Guid? productId, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        (await AllAsync(db, clock, ct)).Where(h => productId == null || h.ProductId == productId).ToList();

    private static async Task<IResult> AddAsync(HoldRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.StockHolds.AsNoTracking().AnyAsync(h => h.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        var party = request.CustomerId is { } cid ? await db.Customers.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Name).SingleOrDefaultAsync(ct) : request.PartyName?.Trim();
        if (request.Id == Guid.Empty || !Money.IsValidQuantity(request.Quantity) || string.IsNullOrWhiteSpace(party) || party.Length > 80
            || !await db.Products.AnyAsync(p => p.Id == request.ProductId && !p.IsService, ct) || request.Note?.Length > 200)
        {
            return Invalid("Quantity", "Pick the item, how many to keep aside, and for whom.");
        }

        if (request.Until is { } until && until < IndiaTime.Today(clock))
        {
            return Invalid("Until", "Keep it aside until today or a later day.");
        }

        // Only stock that is free can be kept aside: not what is already held, promised on a sale order or reserved
        // for an online order.
        var product = await db.Products.AsNoTracking().Where(p => p.Id == request.ProductId).Select(p => new { p.Name, p.Stock, p.Reserved }).SingleAsync(ct);
        // Promised counts sale orders and the counter holds already made.
        var free = product.Stock - product.Reserved - (await SaleOrders.PromisedAsync(db, ct)).GetValueOrDefault(request.ProductId);
        if (request.Quantity > free)
        {
            return Invalid("Quantity", $"Only {Math.Max(0, free):0.###} of {product.Name} is free to keep aside; the rest is already held or promised.");
        }

        db.StockHolds.Add(new StockHold
        {
            Id = request.Id, ProductId = request.ProductId, Quantity = request.Quantity, CustomerId = request.CustomerId, PartyName = party,
            Until = request.Until, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), CreatedByUserId = user.UserId()!.Value,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Done (the customer took it, on a bill) or released (not needed): the stock is free to sell again.</summary>
    private static async Task<IResult> EndAsync(Guid id, HoldStatus to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var changed = await db.StockHolds.Where(h => h.Id == id && h.Status == HoldStatus.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, to).SetProperty(h => h.EndedAt, clock.GetUtcNow()), ct);
        return changed > 0 || await db.StockHolds.AnyAsync(h => h.Id == id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
