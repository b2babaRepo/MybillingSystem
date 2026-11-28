using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A customer's order booked ahead (a contractor's 200 bags of cement over the month, a wedding order): its items
/// are held for them until delivered, one or more bills at a time. Nothing leaves the shelf or goes on the khata
/// until a bill is made; the order only says what is promised, what went, and what is still to go.
/// </summary>
public sealed class SaleOrder : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid? CustomerId { get; set; }

    /// <summary>A name for an order from someone not on the khata.</summary>
    public string? PartyName { get; set; }

    /// <summary>The lines (<see cref="SaleOrderLine"/>), with how much of each has gone on bills, as JSON.</summary>
    public required string Lines { get; set; }

    public decimal Total { get; set; }

    /// <summary>When the customer wants it by.</summary>
    public DateOnly? DueOn { get; set; }

    public string? Note { get; set; }

    /// <summary>The bills that delivered it, so the same bill is never counted twice.</summary>
    public string BillIds { get; set; } = "[]";

    /// <summary>Closed with some still to go (the customer doesn't want the rest).</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record SaleOrderLine(Guid? ProductId, string Name, decimal Quantity, Unit Unit, decimal UnitPrice, decimal Delivered = 0, decimal? Bags = null)
{
    public decimal Pending => Math.Max(0, Quantity - Delivered);
}

public sealed record SaleOrderLineRequest(Guid? ProductId, string? Name, decimal Quantity, Unit? Unit, decimal UnitPrice, decimal? Bags = null);

public sealed record SaleOrderRequest(Guid Id, Guid? CustomerId, string? PartyName, IReadOnlyList<SaleOrderLineRequest>? Lines, DateOnly? DueOn = null, string? Note = null);

public sealed record SaleOrderDeliveredRequest(Guid BillId);

/// <summary>A line as the screen shows it: ordered, delivered, still to go, on the shelf now, and how much short.</summary>
public sealed record SaleOrderLineDto(Guid? ProductId, string Name, decimal Quantity, Unit Unit, decimal UnitPrice, decimal Delivered, decimal Pending, decimal? InStock, decimal ShortBy, decimal? Bags = null);

/// <summary><see cref="State"/>: open, part (some delivered), delivered, closed (short), cancelled.</summary>
public sealed record SaleOrderDto(
    Guid Id, int Number, Guid? CustomerId, string? Party, IReadOnlyList<SaleOrderLineDto> Lines, decimal Total, decimal PendingValue, DateOnly? DueOn,
    string? Note, string State, int Bills, DateTimeOffset CreatedAt);

public static class SaleOrders
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapSaleOrders(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sale-orders").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            await db.SaleOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct) is { } o ? Results.Ok((await ToDtosAsync(db, [o], ct))[0]) : Results.NotFound());
        group.MapPost("", AddAsync);
        group.MapPost("/{id:guid}/delivered", DeliveredAsync);
        group.MapPost("/{id:guid}/close", (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) => FinishAsync(id, close: true, db, clock, ct));
        group.MapPost("/{id:guid}/cancel", (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) => FinishAsync(id, close: false, db, clock, ct));
        return app;
    }

    internal static List<SaleOrderLine> LinesOf(SaleOrder o) => JsonSerializer.Deserialize<List<SaleOrderLine>>(o.Lines, Json) ?? [];

    private static bool IsOpen(SaleOrder o) => o.ClosedAt == null && o.CancelledAt == null;

    /// <summary>What open sale orders still hold of each item: kept apart from the shelf as "promised".</summary>
    /// <summary>Promised per item: still to deliver on open sale orders, plus counter holds (see <see cref="StockHolds"/>).</summary>
    internal static async Task<Dictionary<Guid, decimal>> PromisedAsync(AppDbContext db, CancellationToken ct)
    {
        var open = await db.SaleOrders.AsNoTracking().Where(o => o.ClosedAt == null && o.CancelledAt == null).Select(o => o.Lines).ToListAsync(ct);
        var promised = open.SelectMany(l => JsonSerializer.Deserialize<List<SaleOrderLine>>(l, Json) ?? [])
            .Where(l => l.ProductId is not null && l.Pending > 0)
            .GroupBy(l => l.ProductId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Pending));
        foreach (var (id, q) in await StockHolds.HeldAsync(db, ct))
        {
            promised[id] = promised.GetValueOrDefault(id) + q;
        }

        return promised;
    }

    private static string StateOf(SaleOrder o, List<SaleOrderLine> lines) =>
        o.CancelledAt is not null ? "cancelled"
        : lines.All(l => l.Pending == 0) ? "delivered"
        : o.ClosedAt is not null ? "closed"
        : lines.Any(l => l.Delivered > 0) ? "part"
        : "open";

    /// <summary>
    /// Shortage is shown oldest order first: the shelf goes to the order booked first, so a later order shows short
    /// before an earlier one does.
    /// </summary>
    private static async Task<List<SaleOrderDto>> ToDtosAsync(AppDbContext db, List<SaleOrder> orders, CancellationToken ct)
    {
        var stock = await db.Products.AsNoTracking().Select(p => new { p.Id, p.Stock, p.Reserved }).ToDictionaryAsync(p => p.Id, p => p.Stock - p.Reserved, ct);
        var earlier = await db.SaleOrders.AsNoTracking().Where(o => o.ClosedAt == null && o.CancelledAt == null).OrderBy(o => o.Number)
            .Select(o => new { o.Number, o.Lines }).ToListAsync(ct);
        var names = await db.Customers.AsNoTracking().Where(c => orders.Select(o => o.CustomerId).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return orders.Select(o =>
        {
            // Stock left for this order once the open orders before it have taken theirs.
            var before = earlier.Where(e => e.Number < o.Number).SelectMany(e => JsonSerializer.Deserialize<List<SaleOrderLine>>(e.Lines, Json) ?? [])
                .Where(l => l.ProductId is not null).GroupBy(l => l.ProductId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Pending));
            var lines = LinesOf(o);
            var open = IsOpen(o);
            var dtoLines = lines.Select(l =>
            {
                decimal? onShelf = l.ProductId is { } pid ? stock.GetValueOrDefault(pid) : null;
                var left = onShelf is { } s && l.ProductId is { } p2 ? Math.Max(0, s - before.GetValueOrDefault(p2)) : (decimal?)null;
                return new SaleOrderLineDto(l.ProductId, l.Name, l.Quantity, l.Unit, l.UnitPrice, l.Delivered, l.Pending, onShelf,
                    open && left is { } avail ? Math.Max(0, l.Pending - avail) : 0, l.Bags);
            }).ToList();
            return new SaleOrderDto(o.Id, o.Number, o.CustomerId, o.CustomerId is { } c ? names.GetValueOrDefault(c) : o.PartyName, dtoLines, o.Total,
                lines.Sum(l => Money.LineAmount(l.Pending, l.UnitPrice)), o.DueOn, o.Note, StateOf(o, lines),
                (JsonSerializer.Deserialize<List<Guid>>(o.BillIds, Json) ?? []).Count, o.CreatedAt);
        }).ToList();
    }

    /// <summary>The last 100, newest first; <paramref name="open"/>: only those with something still to deliver.</summary>
    private static async Task<IReadOnlyList<SaleOrderDto>> ListAsync(bool? open, AppDbContext db, CancellationToken ct)
    {
        var orders = await db.SaleOrders.AsNoTracking().Where(o => open != true || (o.ClosedAt == null && o.CancelledAt == null))
            .OrderByDescending(o => o.Number).Take(100).ToListAsync(ct);
        var dtos = await ToDtosAsync(db, orders, ct);
        return open == true ? dtos.Where(d => d.State is "open" or "part").ToList() : dtos;
    }

    private static async Task<IResult> AddAsync(SaleOrderRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await db.SaleOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok((await ToDtosAsync(db, [existing], ct))[0]);
        }

        var lines = request.Lines ?? [];
        var ids = lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200
            || lines.Any(l => (l.ProductId is { } pid ? !products.ContainsKey(pid) : string.IsNullOrWhiteSpace(l.Name)) || !Money.IsValidQuantity(l.Quantity) || !Money.IsValidAmount(l.UnitPrice)))
        {
            return Invalid("Lines", "Add the items with how many and the price of each.");
        }

        if (request.CustomerId is null && string.IsNullOrWhiteSpace(request.PartyName))
        {
            return Invalid("CustomerId", "Choose whose order it is.");
        }

        if (request.CustomerId is { } cid && !await db.Customers.AnyAsync(c => c.Id == cid, ct))
        {
            return Invalid("CustomerId", "That customer isn't in this shop.");
        }

        if (request.PartyName?.Trim().Length > 80 || request.Note?.Trim().Length > 300)
        {
            return Invalid("Note", "Keep the name under 80 letters and the note under 300.");
        }

        var built = lines.Select(l =>
        {
            var product = l.ProductId is { } pid ? products[pid] : null;
            return new SaleOrderLine(l.ProductId, product?.Name ?? l.Name!.Trim(), l.Quantity, product?.Unit ?? l.Unit ?? Unit.Piece, l.UnitPrice,
                Bags: l.Bags is > 0 and <= 100_000 && product?.BagSize != null ? l.Bags : null);
        }).ToList();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('sale_orders'))", ct);
        var order = new SaleOrder
        {
            Id = request.Id, CustomerId = request.CustomerId, PartyName = string.IsNullOrWhiteSpace(request.PartyName) ? null : request.PartyName.Trim(),
            Lines = JsonSerializer.Serialize(built, Json), Total = built.Sum(l => Money.LineAmount(l.Quantity, l.UnitPrice)), DueOn = request.DueOn,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), CreatedByUserId = user.UserId()!.Value,
            Number = await db.NextNumberAsync<SaleOrder>(ct),
        };
        db.SaleOrders.Add(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Created($"/api/v1/sale-orders/{order.Id}", (await ToDtosAsync(db, [order], ct))[0]);
    }

    /// <summary>
    /// A bill delivered (some of) the order: each of its items counts off what is still to go, never past it. Read from
    /// the bill itself, so the phone can't claim more than was billed; the same bill twice changes nothing.
    /// </summary>
    private static async Task<IResult> DeliveredAsync(Guid id, SaleOrderDeliveredRequest request, AppDbContext db, CancellationToken ct)
    {
        var order = await db.SaleOrders.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        var bills = JsonSerializer.Deserialize<List<Guid>>(order.BillIds, Json) ?? [];
        if (bills.Contains(request.BillId))
        {
            return TypedResults.Ok((await ToDtosAsync(db, [order], ct))[0]);
        }

        if (!IsOpen(order))
        {
            return Invalid("Id", "This order is closed or cancelled.");
        }

        var billed = await (from l in db.BillLines.AsNoTracking()
                            join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                            where b.Id == request.BillId && b.CancelledAt == null
                            select new { l.ProductId, l.Name, l.Quantity }).ToListAsync(ct);
        if (billed.Count == 0)
        {
            return Invalid("BillId", "That bill isn't in this shop.");
        }

        var lines = LinesOf(order);
        foreach (var item in billed)
        {
            var left = item.Quantity;
            for (var i = 0; i < lines.Count && left > 0; i++)
            {
                var l = lines[i];
                var same = item.ProductId is { } pid ? l.ProductId == pid : l.ProductId is null && string.Equals(l.Name, item.Name, StringComparison.OrdinalIgnoreCase);
                if (!same || l.Pending <= 0)
                {
                    continue;
                }

                var take = Math.Min(left, l.Pending);
                lines[i] = l with { Delivered = l.Delivered + take };
                left -= take;
            }
        }

        bills.Add(request.BillId);
        order.Lines = JsonSerializer.Serialize(lines, Json);
        order.BillIds = JsonSerializer.Serialize(bills, Json);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, [order], ct))[0]);
    }

    private static async Task<IResult> FinishAsync(Guid id, bool close, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var order = await db.SaleOrders.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (!close && LinesOf(order).Any(l => l.Delivered > 0))
        {
            return Invalid("Id", "Some of this order is delivered: close it instead, so the bills stay with it.");
        }

        if (close)
        {
            order.ClosedAt ??= clock.GetUtcNow();
        }
        else
        {
            order.CancelledAt ??= clock.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, [order], ct))[0]);
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
