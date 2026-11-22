using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// A store room or godown away from the counter. The item's stock stays one number for the whole shop; a godown holds
/// part of it, worked out from the transfers in and out. What is not in any godown is on the shop shelf, which is where
/// bills sell from.
/// </summary>
public sealed class Godown : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Goods moved between the shop shelf (null) and a godown, or between two godowns. Moves no money.</summary>
public sealed class StockTransfer : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid? FromGodownId { get; set; }

    public Guid? ToGodownId { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public List<StockTransferLine> Lines { get; set; } = [];
}

public sealed class StockTransferLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid TransferId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? Bags { get; set; }
}

public sealed record GodownRequest(Guid Id, string? Name, string? Address = null, bool IsActive = true);

/// <summary><see cref="Units"/>: everything held there, all items added; <see cref="Items"/>: how many items have some.</summary>
public sealed record GodownDto(Guid Id, string Name, string? Address, bool IsActive, int Items, decimal Units, decimal Value);

public sealed record TransferLineRequest(Guid ProductId, decimal Quantity, decimal? Bags = null);

public sealed record TransferRequest(Guid Id, Guid? FromGodownId, Guid? ToGodownId, IReadOnlyList<TransferLineRequest>? Lines, string? Note = null);

public sealed record TransferLineDto(Guid ProductId, string Name, decimal Quantity, decimal? Bags);

public sealed record TransferDto(Guid Id, int Number, Guid? FromGodownId, string From, Guid? ToGodownId, string To, DateTimeOffset OccurredAt,
    string? Note, bool Cancelled, string? By, IReadOnlyList<TransferLineDto> Lines);

/// <summary>Where an item's stock is: <see cref="Shelf"/> in the shop, and each godown's share.</summary>
public sealed record PlaceStockDto(Guid ProductId, string Name, string? Category, Unit Unit, decimal Total, decimal Shelf, IReadOnlyDictionary<Guid, decimal> Godowns);

public static class Godowns
{
    public static IEndpointRouteBuilder MapGodowns(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/godowns").WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", SaveAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/stock", StockAsync);
        group.MapGet("/transfers", TransfersAsync);
        group.MapPost("/transfers", TransferAsync);
        group.MapPost("/transfers/{id:guid}/cancel", CancelAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>Each godown's holding of each item: transfers in less transfers out, cancelled ones left out.</summary>
    internal static async Task<Dictionary<(Guid Godown, Guid Product), decimal>> HeldAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await (from l in db.StockTransferLines.AsNoTracking()
                          join t in db.StockTransfers.AsNoTracking() on l.TransferId equals t.Id
                          where t.CancelledAt == null
                          select new { t.FromGodownId, t.ToGodownId, l.ProductId, l.Quantity }).ToListAsync(ct);
        var held = new Dictionary<(Guid, Guid), decimal>();
        foreach (var r in rows)
        {
            if (r.ToGodownId is { } to)
            {
                held[(to, r.ProductId)] = held.GetValueOrDefault((to, r.ProductId)) + r.Quantity;
            }

            if (r.FromGodownId is { } from)
            {
                held[(from, r.ProductId)] = held.GetValueOrDefault((from, r.ProductId)) - r.Quantity;
            }
        }

        return held;
    }

    private static async Task<IReadOnlyList<GodownDto>> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var godowns = await db.Godowns.AsNoTracking().OrderBy(g => g.Name).ToListAsync(ct);
        var held = await HeldAsync(db, ct);
        var ids = held.Keys.Select(k => k.Product).Distinct().ToList();
        var costs = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.CostPrice ?? 0, ct);
        return godowns.Select(g =>
        {
            var mine = held.Where(h => h.Key.Godown == g.Id && h.Value != 0).ToList();
            return new GodownDto(g.Id, g.Name, g.Address, g.IsActive, mine.Count, mine.Sum(h => h.Value),
                decimal.Round(mine.Sum(h => h.Value * costs.GetValueOrDefault(h.Key.Product)), 2));
        }).ToList();
    }

    /// <summary>Adds a godown, or renames / puts away one (by id). A godown still holding goods can't be put away.</summary>
    private static async Task<IResult> SaveAsync(GodownRequest request, AppDbContext db, CancellationToken ct)
    {
        var name = string.Join(' ', (request.Name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (request.Id == Guid.Empty || name.Length is 0 or > 60)
        {
            return Invalid("Name", "Give the godown a name.");
        }

        var godown = await db.Godowns.SingleOrDefaultAsync(g => g.Id == request.Id, ct);
        if (await db.Godowns.AnyAsync(g => g.Id != request.Id && g.Name.ToLower() == name.ToLower(), ct))
        {
            return Invalid("Name", "There is already a godown with this name.");
        }

        if (!request.IsActive && godown is not null && (await HeldAsync(db, ct)).Any(h => h.Key.Godown == godown.Id && h.Value > 0))
        {
            return Invalid("IsActive", "This godown still holds goods: move them out first.");
        }

        if (godown is null)
        {
            godown = new Godown { Id = request.Id, Name = name };
            db.Godowns.Add(godown);
        }

        godown.Name = name;
        godown.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim()[..Math.Min(200, request.Address.Trim().Length)];
        godown.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Every item with stock, split into the shop shelf and each godown.</summary>
    private static async Task<IReadOnlyList<PlaceStockDto>> StockAsync(AppDbContext db, CancellationToken ct)
    {
        var held = await HeldAsync(db, ct);
        var products = await db.Products.AsNoTracking().Where(p => !p.IsService && (p.Stock != 0 || p.IsActive)).OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Category, p.Unit, p.Stock }).ToListAsync(ct);
        var byProduct = held.Where(h => h.Value != 0).ToLookup(h => h.Key.Product);
        return products.Select(p =>
        {
            var places = byProduct[p.Id].ToDictionary(h => h.Key.Godown, h => h.Value);
            return new PlaceStockDto(p.Id, p.Name, p.Category, p.Unit, p.Stock, p.Stock - places.Values.Sum(), places);
        }).ToList();
    }

    private static async Task<IReadOnlyList<TransferDto>> TransfersAsync(Guid? godownId, AppDbContext db, CancellationToken ct)
    {
        var transfers = await db.StockTransfers.AsNoTracking().Include(t => t.Lines)
            .Where(t => godownId == null || t.FromGodownId == godownId || t.ToGodownId == godownId)
            .OrderByDescending(t => t.Number).Take(100).ToListAsync(ct);
        return await ToDtosAsync(db, transfers, ct);
    }

    private static async Task<List<TransferDto>> ToDtosAsync(AppDbContext db, IReadOnlyList<StockTransfer> transfers, CancellationToken ct)
    {
        var names = await db.Godowns.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var productIds = transfers.SelectMany(t => t.Lines).Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var userIds = transfers.Select(t => t.CreatedByUserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string Place(Guid? id) => id is { } g ? names.GetValueOrDefault(g) ?? "" : "Shop";
        return transfers.Select(t => new TransferDto(t.Id, t.Number, t.FromGodownId, Place(t.FromGodownId), t.ToGodownId, Place(t.ToGodownId), t.OccurredAt, t.Note,
            t.CancelledAt != null, users.GetValueOrDefault(t.CreatedByUserId),
            t.Lines.Select(l => new TransferLineDto(l.ProductId, products.GetValueOrDefault(l.ProductId) ?? "", l.Quantity, l.Bags)).ToList())).ToList();
    }

    /// <summary>Where goods can be taken from: a godown's holding, or the shelf (all stock less what the godowns hold).</summary>
    private static async Task<string?> ShortAsync(AppDbContext db, Guid? from, IEnumerable<(Guid ProductId, decimal Quantity)> goods, CancellationToken ct)
    {
        var held = await HeldAsync(db, ct);
        var wanted = goods.GroupBy(g => g.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var ids = wanted.Keys.ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.Name, p.Stock }, ct);
        foreach (var (id, quantity) in wanted)
        {
            var p = products[id];
            var there = from is { } g ? held.GetValueOrDefault((g, id)) : p.Stock - held.Where(h => h.Key.Product == id).Sum(h => h.Value);
            if (quantity > there)
            {
                return $"Only {Math.Max(0, there):0.###} of {p.Name} is there to move.";
            }
        }

        return null;
    }

    private static async Task<IResult> TransferAsync(TransferRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.StockTransfers.AsNoTracking().Include(t => t.Lines).SingleOrDefaultAsync(t => t.Id == request.Id, ct) is { } saved)
        {
            return TypedResults.Ok((await ToDtosAsync(db, [saved], ct))[0]);
        }

        var lines = request.Lines ?? [];
        if (request.Id == Guid.Empty || lines.Count is 0 or > 200 || lines.Select(l => l.ProductId).Distinct().Count() != lines.Count
            || lines.Any(l => !Money.IsValidQuantity(l.Quantity) || l.Bags is < 0))
        {
            return Invalid("Lines", "Choose the items, each once, with how many to move.");
        }

        if (request.FromGodownId == request.ToGodownId)
        {
            return Invalid("ToGodownId", "Choose where the goods go: somewhere other than where they are.");
        }

        var places = new[] { request.FromGodownId, request.ToGodownId }.OfType<Guid>().ToList();
        if (await db.Godowns.CountAsync(g => places.Contains(g.Id) && g.IsActive, ct) != places.Count)
        {
            return Invalid("ToGodownId", "That godown wasn't found.");
        }

        var ids = lines.Select(l => l.ProductId).ToList();
        if (await db.Products.CountAsync(p => ids.Contains(p.Id) && !p.IsService, ct) != ids.Count)
        {
            return Invalid("Lines", "An item wasn't found.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // One move at a time per shop, so two phones can't both take the last bags out of the same godown.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('stock_transfers'))", ct);
        if (await ShortAsync(db, request.FromGodownId, lines.Select(l => (l.ProductId, l.Quantity)), ct) is { } problem)
        {
            return Invalid("Lines", problem);
        }

        var note = request.Note?.Trim();
        var transfer = new StockTransfer
        {
            Id = request.Id, Number = (await db.StockTransfers.MaxAsync(t => (int?)t.Number, ct) ?? 0) + 1, FromGodownId = request.FromGodownId, ToGodownId = request.ToGodownId,
            Note = string.IsNullOrEmpty(note) ? null : note[..Math.Min(200, note.Length)], OccurredAt = clock.GetUtcNow(), CreatedByUserId = user.UserId()!.Value,
            Lines = lines.Select(l => new StockTransferLine { ProductId = l.ProductId, Quantity = l.Quantity, Bags = l.Bags is > 0 ? l.Bags : null }).ToList(),
        };
        db.StockTransfers.Add(transfer);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, [transfer], ct))[0]);
    }

    /// <summary>
    /// A move made by something else (goods in to a godown, a bill sold from one), with the same id as what made it so a
    /// retry adds nothing. From a godown, takes only what is there. Joins the caller's transaction; the caller saves.
    /// Returns what was moved.
    /// </summary>
    internal static async Task<decimal> MoveForAsync(AppDbContext db, Guid id, Guid? from, Guid? to, IEnumerable<(Guid ProductId, decimal Quantity)> goods, string note,
        Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.StockTransfers.AnyAsync(t => t.Id == id, ct) || db.StockTransfers.Local.Any(t => t.Id == id))
        {
            return 0;
        }

        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('stock_transfers'))", ct);
        var held = from is { } g ? await HeldAsync(db, ct) : null;
        var lines = goods.GroupBy(x => x.ProductId).Select(x => (ProductId: x.Key, Quantity: x.Sum(y => y.Quantity)))
            .Select(x => (x.ProductId, Quantity: held is null ? x.Quantity : Math.Min(x.Quantity, held.GetValueOrDefault((from!.Value, x.ProductId)))))
            .Where(x => x.Quantity > 0).ToList();
        if (lines.Count == 0)
        {
            return 0;
        }

        db.StockTransfers.Add(new StockTransfer
        {
            Id = id, Number = (await db.StockTransfers.MaxAsync(t => (int?)t.Number, ct) ?? 0) + 1, FromGodownId = from, ToGodownId = to, Note = note[..Math.Min(200, note.Length)],
            OccurredAt = now, CreatedByUserId = userId,
            Lines = lines.Select(l => new StockTransferLine { ProductId = l.ProductId, Quantity = l.Quantity }).ToList(),
        });
        return lines.Sum(l => l.Quantity);
    }

    /// <summary>Takes back a move made for something now cancelled (a receipt or supplier bill), whatever happened since.</summary>
    internal static Task<int> CancelForAsync(AppDbContext db, Guid id, DateTimeOffset now, CancellationToken ct) =>
        db.StockTransfers.Where(t => t.Id == id && t.CancelledAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.CancelledAt, now), ct);

    /// <summary>A godown of this shop that is in use.</summary>
    internal static Task<bool> IsActiveAsync(AppDbContext db, Guid id, CancellationToken ct) => db.Godowns.AnyAsync(g => g.Id == id && g.IsActive, ct);

    /// <summary>Undoes a move, if the goods are still where it put them.</summary>
    private static async Task<IResult> CancelAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('stock_transfers'))", ct);
        var transfer = await db.StockTransfers.Include(t => t.Lines).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (transfer is null)
        {
            return TypedResults.NotFound();
        }

        if (transfer.CancelledAt is not null)
        {
            return TypedResults.NoContent();
        }

        if (await ShortAsync(db, transfer.ToGodownId, transfer.Lines.Select(l => (l.ProductId, l.Quantity)), ct) is { } problem)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Some of these goods have already moved on. " + problem);
        }

        transfer.CancelledAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
