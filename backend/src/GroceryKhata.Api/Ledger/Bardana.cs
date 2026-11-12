using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>A kind of empty bag the shop keeps count of: "Jute 50 kg", "Plastic 25 kg".</summary>
public sealed class BagType : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>What the bardana dealer usually pays for one, filled in when selling.</summary>
    public decimal? SaleRate { get; set; }

    public bool IsActive { get; set; } = true;
}

public enum BagMoveKind
{
    /// <summary>Bags on hand when the shop started counting.</summary>
    Opening,

    /// <summary>Emptied from goods that came in bags (from a supplier).</summary>
    CameWithGoods,

    /// <summary>Bought, paid for (money out).</summary>
    Bought,

    /// <summary>Sold to the bardana dealer (money in).</summary>
    Sold,

    /// <summary>Given back to the supplier they came from.</summary>
    ReturnedToSupplier,

    /// <summary>Used in the shop (repacking, deliveries).</summary>
    Used,

    Damaged,

    /// <summary>A physical count: the difference from the book is booked.</summary>
    Counted,
}

/// <summary>
/// The bardana account: empty bags in and out. <see cref="Quantity"/> is signed (in plus, out minus); a sale or a purchase
/// carries its money and shows in the day's cash book. A struck move no longer counts.
/// </summary>
public sealed class BagMove : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BagTypeId { get; set; }

    public BagMoveKind Kind { get; set; }

    public decimal Quantity { get; set; }

    public decimal? Rate { get; set; }

    public decimal? Amount { get; set; }

    public PaymentMode? Mode { get; set; }

    public Guid? SupplierId { get; set; }

    /// <summary>Who bought or sold them (the bardana dealer).</summary>
    public string? Party { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? StruckAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record BagTypeRequest(Guid Id, string? Name, decimal? SaleRate = null, bool IsActive = true);

/// <summary>
/// For <see cref="BagMoveKind.Counted"/>, <see cref="Quantity"/> is how many were counted; for every other kind, how many
/// moved (always positive).
/// </summary>
public sealed record BagMoveRequest(Guid Id, Guid BagTypeId, BagMoveKind Kind, decimal Quantity, decimal? Rate = null, PaymentMode? Mode = null,
    Guid? SupplierId = null, string? Party = null, string? Note = null);

public sealed record BagTypeDto(Guid Id, string Name, decimal? SaleRate, bool IsActive, decimal OnHand, decimal? Value);

/// <summary>A supplier's bags: how many came with their goods, how many went back, and how many of theirs the shop still holds.</summary>
public sealed record SupplierBagsDto(Guid SupplierId, string Supplier, decimal Came, decimal Returned, decimal Holding);

public sealed record BagMoveDto(Guid Id, Guid BagTypeId, string BagType, BagMoveKind Kind, decimal Quantity, decimal? Rate, decimal? Amount, PaymentMode? Mode,
    string? Supplier, string? Party, string? Note, DateTimeOffset OccurredAt, bool Struck, string? By);

/// <summary><see cref="SoldThisMonth"/> / <see cref="BoughtThisMonth"/>: money from and for bags this month.</summary>
public sealed record BardanaDto(IReadOnlyList<BagTypeDto> Types, IReadOnlyList<SupplierBagsDto> Suppliers, decimal SoldThisMonth, decimal BoughtThisMonth);

public static class Bardana
{
    private static readonly BagMoveKind[] OutKinds = [BagMoveKind.Sold, BagMoveKind.ReturnedToSupplier, BagMoveKind.Used, BagMoveKind.Damaged];

    public static IEndpointRouteBuilder MapBardana(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/bardana").WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", SummaryAsync);
        group.MapPost("/types", SaveTypeAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/moves", MovesAsync);
        group.MapPost("/moves", MoveAsync);
        group.MapPost("/moves/{id:guid}/strike", StrikeAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<Dictionary<Guid, decimal>> OnHandAsync(AppDbContext db, CancellationToken ct) =>
        await db.BagMoves.AsNoTracking().Where(m => m.StruckAt == null).GroupBy(m => m.BagTypeId)
            .Select(g => new { g.Key, Sum = g.Sum(m => m.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

    private static async Task<BardanaDto> SummaryAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var types = await db.BagTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        var onHand = await OnHandAsync(db, ct);
        var bySupplier = await (from m in db.BagMoves.AsNoTracking()
                                where m.StruckAt == null && m.SupplierId != null && (m.Kind == BagMoveKind.CameWithGoods || m.Kind == BagMoveKind.ReturnedToSupplier)
                                group m by new { m.SupplierId, m.Kind } into g
                                select new { g.Key.SupplierId, g.Key.Kind, Sum = g.Sum(m => m.Quantity) }).ToListAsync(ct);
        var supplierIds = bySupplier.Select(s => s.SupplierId!.Value).Distinct().ToList();
        var names = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var suppliers = supplierIds.Select(id =>
        {
            var came = bySupplier.Where(s => s.SupplierId == id && s.Kind == BagMoveKind.CameWithGoods).Sum(s => s.Sum);
            var back = -bySupplier.Where(s => s.SupplierId == id && s.Kind == BagMoveKind.ReturnedToSupplier).Sum(s => s.Sum);
            return new SupplierBagsDto(id, names.GetValueOrDefault(id) ?? "", came, back, came - back);
        }).OrderByDescending(s => s.Holding).ToList();

        var today = IndiaTime.Today(clock);
        var (monthStart, _) = IndiaTime.Day(new DateOnly(today.Year, today.Month, 1));
        var money = await db.BagMoves.AsNoTracking().Where(m => m.StruckAt == null && m.OccurredAt >= monthStart && m.Amount != null)
            .GroupBy(m => m.Kind).Select(g => new { g.Key, Sum = g.Sum(m => m.Amount!.Value) }).ToListAsync(ct);
        return new BardanaDto(
            types.Select(t => new BagTypeDto(t.Id, t.Name, t.SaleRate, t.IsActive, onHand.GetValueOrDefault(t.Id),
                t.SaleRate is { } r ? decimal.Round(r * Math.Max(0, onHand.GetValueOrDefault(t.Id)), 2) : null)).ToList(),
            suppliers, money.Where(m => m.Key == BagMoveKind.Sold).Sum(m => m.Sum), money.Where(m => m.Key == BagMoveKind.Bought).Sum(m => m.Sum));
    }

    private static async Task<IResult> SaveTypeAsync(BagTypeRequest request, AppDbContext db, CancellationToken ct)
    {
        var name = string.Join(' ', (request.Name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (request.Id == Guid.Empty || name.Length is 0 or > 60)
        {
            return Invalid("Name", "Name the kind of bag, e.g. Jute 50 kg.");
        }

        if (request.SaleRate is < 0 or > 10_000 || (request.SaleRate is { } r && decimal.Round(r, 2) != r))
        {
            return Invalid("SaleRate", "Give the rate per bag in rupees.");
        }

        if (await db.BagTypes.AnyAsync(t => t.Id != request.Id && t.Name.ToLower() == name.ToLower(), ct))
        {
            return Invalid("Name", "There is already a bag with this name.");
        }

        var type = await db.BagTypes.SingleOrDefaultAsync(t => t.Id == request.Id, ct);
        if (type is null)
        {
            type = new BagType { Id = request.Id, Name = name };
            db.BagTypes.Add(type);
        }

        (type.Name, type.SaleRate, type.IsActive) = (name, request.SaleRate, request.IsActive);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IReadOnlyList<BagMoveDto>> MovesAsync(Guid? bagTypeId, Guid? supplierId, AppDbContext db, CancellationToken ct)
    {
        var rows = await (from m in db.BagMoves.AsNoTracking()
                          where (bagTypeId == null || m.BagTypeId == bagTypeId) && (supplierId == null || m.SupplierId == supplierId)
                          join t in db.BagTypes.AsNoTracking() on m.BagTypeId equals t.Id
                          join s in db.Suppliers.AsNoTracking() on m.SupplierId equals s.Id into ss
                          from s in ss.DefaultIfEmpty()
                          join u in db.Users.AsNoTracking() on m.CreatedByUserId equals u.Id into us
                          from u in us.DefaultIfEmpty()
                          orderby m.OccurredAt descending
                          select new BagMoveDto(m.Id, m.BagTypeId, t.Name, m.Kind, m.Quantity, m.Rate, m.Amount, m.Mode, s == null ? null : s.Name, m.Party, m.Note,
                              m.OccurredAt, m.StruckAt != null, u == null ? null : u.FullName)).Take(200).ToListAsync(ct);
        return rows;
    }

    private static string? Short(string? text, int max) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(max, text.Trim().Length)];

    private static async Task<IResult> MoveAsync(BagMoveRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.BagMoves.AsNoTracking().AnyAsync(m => m.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        var counting = request.Kind == BagMoveKind.Counted;
        if (request.Id == Guid.Empty || !Enum.IsDefined(request.Kind) || request.Quantity < 0 || (!counting && request.Quantity == 0)
            || request.Quantity > 1_000_000 || decimal.Round(request.Quantity, 0) != request.Quantity)
        {
            return Invalid("Quantity", "Give how many bags, in whole bags.");
        }

        if (!await db.BagTypes.AnyAsync(t => t.Id == request.BagTypeId && t.IsActive, ct))
        {
            return Invalid("BagTypeId", "Choose the kind of bag.");
        }

        var paid = request.Kind is BagMoveKind.Sold or BagMoveKind.Bought;
        if (paid && (request.Rate is not > 0 || request.Rate > 10_000 || decimal.Round(request.Rate.Value, 2) != request.Rate || request.Mode is not (PaymentMode.Cash or PaymentMode.Upi)))
        {
            return Invalid("Rate", "Give the rate per bag and whether it was cash or UPI.");
        }

        var withSupplier = request.Kind is BagMoveKind.CameWithGoods or BagMoveKind.ReturnedToSupplier;
        if (request.Kind == BagMoveKind.ReturnedToSupplier && request.SupplierId is null)
        {
            return Invalid("SupplierId", "Choose the supplier the bags went back to.");
        }

        if (withSupplier && request.SupplierId is { } sid && !await db.Suppliers.AnyAsync(s => s.Id == sid, ct))
        {
            return Invalid("SupplierId", "That supplier wasn't found.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // One bag move at a time, so two phones can't both sell the last bags.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('bag_moves'))", ct);
        var onHand = (await OnHandAsync(db, ct)).GetValueOrDefault(request.BagTypeId);
        var quantity = counting ? request.Quantity - onHand : OutKinds.Contains(request.Kind) ? -request.Quantity : request.Quantity;
        if (OutKinds.Contains(request.Kind) && request.Quantity > onHand)
        {
            return Invalid("Quantity", $"Only {onHand:0} of these bags are in the book. Count them first if there are more.");
        }

        if (counting && quantity == 0)
        {
            return TypedResults.NoContent();
        }

        db.BagMoves.Add(new BagMove
        {
            Id = request.Id, BagTypeId = request.BagTypeId, Kind = request.Kind, Quantity = quantity,
            Rate = paid ? request.Rate : null, Amount = paid ? decimal.Round(request.Rate!.Value * request.Quantity, 2) : null, Mode = paid ? request.Mode : null,
            SupplierId = withSupplier ? request.SupplierId : null, Party = Short(request.Party, 80), Note = Short(request.Note, 200),
            OccurredAt = clock.GetUtcNow(), CreatedByUserId = user.UserId()!.Value,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Strikes a wrong entry: it stays on the list, crossed out, and stops counting (its money leaves the cash book).</summary>
    private static async Task<IResult> StrikeAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('bag_moves'))", ct);
        var move = await db.BagMoves.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (move is null)
        {
            return TypedResults.NotFound();
        }

        if (move.StruckAt is not null)
        {
            return TypedResults.NoContent();
        }

        // Striking an "in" can't leave the book below zero.
        if (move.Quantity > 0 && (await OnHandAsync(db, ct)).GetValueOrDefault(move.BagTypeId) - move.Quantity < 0)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Those bags have already gone out: strike the later entries first.");
        }

        move.StruckAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }
}
