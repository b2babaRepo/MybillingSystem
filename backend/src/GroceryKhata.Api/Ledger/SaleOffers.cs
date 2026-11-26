using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

public enum SaleOfferKind
{
    /// <summary>A percent off each piece.</summary>
    Percent,

    /// <summary>Rupees off each piece.</summary>
    Amount,

    /// <summary>Buy <see cref="SaleOffer.BuyQty"/>, get <see cref="SaleOffer.GetQty"/> more of the same item free.</summary>
    BuyGet,

    /// <summary>A percent off the covered items once the bill has at least <see cref="SaleOffer.MinQty"/> of them.</summary>
    Slab,

    /// <summary>One of each item in <see cref="SaleOffer.ProductIds"/> together for <see cref="SaleOffer.ComboPrice"/>.</summary>
    Combo,

    /// <summary>A percent off the whole bill from <see cref="SaleOffer.MinBill"/>.</summary>
    BillPercent,

    /// <summary>Rupees off the whole bill from <see cref="SaleOffer.MinBill"/>.</summary>
    BillAmount,
}

public enum SaleOfferScope
{
    All,
    Category,
    Items,
}

/// <summary>
/// A sale the owner plans before billing ("Summer sale, 20% off kurtas, 1 to 15 May"). The bill applies it by itself to
/// the items it covers, with its name on the line, so the counter never decides the discount; a helper can sell at the
/// offer price even past their own discount limit.
/// </summary>
public sealed class SaleOffer : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public SaleOfferKind Kind { get; set; }

    public decimal Value { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public SaleOfferScope Scope { get; set; }

    public string? Category { get; set; }

    public List<Guid> ProductIds { get; set; } = [];

    /// <summary>Ended early by the owner.</summary>
    public DateTimeOffset? StoppedAt { get; set; }

    public int? BuyQty { get; set; }

    public int? GetQty { get; set; }

    public decimal? MinQty { get; set; }

    public decimal? MinBill { get; set; }

    public decimal? ComboPrice { get; set; }

    /// <summary>Weekdays it runs (bit 0 Sunday … bit 6 Saturday); null: every day.</summary>
    public int? Days { get; set; }

    /// <summary>Hours of the day it runs, as minutes after midnight India time (until is exclusive); null: all day.</summary>
    public int? FromMinute { get; set; }

    public int? ToMinute { get; set; }

    public Guid CreatedByUserId { get; set; }
}

/// <summary><see cref="StartsOn"/> and <see cref="EndsOn"/> are whole days, India time; the offer runs to the end of its last day.</summary>
public sealed record SaleOfferRequest(Guid Id, string? Name, SaleOfferKind Kind, decimal Value, DateOnly StartsOn, DateOnly EndsOn, SaleOfferScope Scope, string? Category = null, IReadOnlyList<Guid>? ProductIds = null,
    int? BuyQty = null, int? GetQty = null, decimal? MinQty = null, decimal? MinBill = null, decimal? ComboPrice = null, int? Days = null, int? FromMinute = null, int? ToMinute = null);

/// <summary>How an offer did: on bills not cancelled. Owners only; null for helpers.</summary>
public sealed record SaleOfferStats(int Bills, decimal Quantity, decimal Sales, decimal Saved);

public sealed record SaleOfferDto(
    Guid Id, string Name, SaleOfferKind Kind, decimal Value, DateTimeOffset StartsAt, DateTimeOffset EndsAt, SaleOfferScope Scope, string? Category,
    IReadOnlyList<Guid> ProductIds, DateTimeOffset? StoppedAt, SaleOfferStats? Stats,
    int? BuyQty = null, int? GetQty = null, decimal? MinQty = null, decimal? MinBill = null, decimal? ComboPrice = null, int? Days = null, int? FromMinute = null, int? ToMinute = null);

public static class SaleOffers
{
    public static IEndpointRouteBuilder MapSaleOffers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/offers").WithTags("Offers").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/stop", StopAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>The price of one piece under the offer, from the item's regular price; never below zero.</summary>
    public static decimal PriceFor(SaleOffer offer, decimal regular) =>
        Math.Max(0, offer.Kind == SaleOfferKind.Percent ? decimal.Round(regular * (1 - (offer.Value / 100)), 2, MidpointRounding.AwayFromZero) : regular - offer.Value);

    /// <summary>The offer covers this item at this moment: the moment the bill was made, so a bill queued offline keeps it.</summary>
    /// <summary>Offers running at this moment (started, not ended, not stopped).</summary>
    public static Task<List<SaleOffer>> RunningAsync(AppDbContext db, DateTimeOffset at, CancellationToken ct) =>
        db.SaleOffers.AsNoTracking().Where(o => o.StartsAt <= at && o.EndsAt > at && (o.StoppedAt == null || o.StoppedAt > at)).ToListAsync(ct);

    /// <summary>Of the running offers on this item, the one with the lowest price for the customer, and that price.</summary>
    public static (SaleOffer Offer, decimal Price)? Best(IEnumerable<SaleOffer> running, Product product, DateTimeOffset at) =>
        // Only a price per piece is a price the shelf and the online store can show; the other kinds need the whole bill.
        running.Where(o => o.Kind is SaleOfferKind.Percent or SaleOfferKind.Amount && Covers(o, product, at)).Select(o => (Offer: o, Price: PriceFor(o, product.SellingPrice)))
            .OrderBy(x => x.Price).Cast<(SaleOffer, decimal)?>().FirstOrDefault();

    public static bool Covers(SaleOffer offer, Product product, DateTimeOffset at) =>
        Promotions.RunsAt(ToRule(offer), at)
        && offer.Scope switch
        {
            SaleOfferScope.Category => product.Category is { } c && string.Equals(c, offer.Category, StringComparison.OrdinalIgnoreCase),
            SaleOfferScope.Items => offer.ProductIds.Contains(product.Id),
            _ => true,
        };

    /// <summary>Offers that are on, coming, or ended in the last 60 days, newest first.</summary>
    private static async Task<IReadOnlyList<SaleOfferDto>> ListAsync(ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-60);
        var offers = await db.SaleOffers.AsNoTracking().Where(o => o.EndsAt >= since).OrderByDescending(o => o.StartsAt).Take(100).ToListAsync(ct);
        var stats = new Dictionary<Guid, SaleOfferStats>();
        if (LedgerEndpoints.IsOwner(user) && offers.Count > 0)
        {
            var ids = offers.Select(o => o.Id).ToList();
            stats = (await (from l in db.BillLines.AsNoTracking()
                            join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                            where l.OfferId != null && ids.Contains(l.OfferId.Value) && b.CancelledAt == null
                            select new { OfferId = l.OfferId!.Value, l.BillId, l.Quantity, l.Amount, Saved = l.ListPrice == null ? 0 : (l.ListPrice.Value - l.UnitPrice) * l.Quantity }).ToListAsync(ct))
                .GroupBy(x => x.OfferId)
                .ToDictionary(g => g.Key, g => new SaleOfferStats(g.Select(x => x.BillId).Distinct().Count(), g.Sum(x => x.Quantity), g.Sum(x => x.Amount), decimal.Round(g.Sum(x => x.Saved), 2)));
            // A bill offer is on the bill, not its lines: the bills it was taken on, their sales and the money off them.
            var onBills = await (from b in db.Bills.AsNoTracking()
                                 where b.BillOfferId != null && ids.Contains(b.BillOfferId.Value) && b.CancelledAt == null
                                 // Lines an item offer priced keep that offer's saving; the bill offer's is what came off the other lines.
                                 select new { OfferId = b.BillOfferId!.Value, b.Total, Saved = b.Lines.Where(l => l.OfferId == null).Sum(l => l.ListPrice == null ? 0 : (l.ListPrice.Value - l.UnitPrice) * l.Quantity) }).ToListAsync(ct);
            foreach (var g in onBills.GroupBy(x => x.OfferId))
            {
                stats[g.Key] = new SaleOfferStats(g.Count(), 0, g.Sum(x => x.Total), decimal.Round(g.Sum(x => x.Saved), 2));
            }
        }

        return offers.Select(o => ToDto(o, LedgerEndpoints.IsOwner(user) ? stats.GetValueOrDefault(o.Id) ?? new SaleOfferStats(0, 0, 0, 0) : null)).ToList();
    }

    private static SaleOfferDto ToDto(SaleOffer o, SaleOfferStats? stats) =>
        new(o.Id, o.Name, o.Kind, o.Value, o.StartsAt, o.EndsAt, o.Scope, o.Category, o.ProductIds, o.StoppedAt, stats,
            o.BuyQty, o.GetQty, o.MinQty, o.MinBill, o.ComboPrice, o.Days, o.FromMinute, o.ToMinute);

    /// <summary>The offer as the promotion engine reads it (names as in the API and the app: percent, buy_get, bill_amount…).</summary>
    public static Promotions.Rule ToRule(SaleOffer o) => new(
        o.Id.ToString(), o.Name, System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(o.Kind.ToString()), o.Value, o.StartsAt, o.EndsAt, o.StoppedAt,
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(o.Scope.ToString()), o.Category, o.ProductIds,
        o.BuyQty, o.GetQty, o.MinQty, o.MinBill, o.ComboPrice, o.Days, o.FromMinute, o.ToMinute);

    private static async Task<IResult> CreateAsync(SaleOfferRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.SaleOffers.AsNoTracking().SingleOrDefaultAsync(o => o.Id == request.Id, ct) is { } saved)
        {
            return TypedResults.Ok(ToDto(saved, null));
        }

        var name = request.Name?.Trim();
        if (request.Id == Guid.Empty || name is not { Length: >= 2 and <= 60 })
        {
            return Invalid("Name", "Give the offer a name, like Summer sale.");
        }

        if (KindProblem(request) is { } problem)
        {
            return Invalid(problem.Field, problem.Message);
        }

        if (request.Days is < 1 or > 127 || request.FromMinute is < 0 or >= 1440 || request.ToMinute is < 0 or >= 1440
            || request.FromMinute is null != request.ToMinute is null || (request.FromMinute is { } from && from == request.ToMinute))
        {
            return Invalid("Days", "Choose at least one day, and hours with a start and an end.");
        }

        if (request.EndsOn < request.StartsOn || request.EndsOn < IndiaTime.Today(clock) || request.EndsOn > request.StartsOn.AddDays(366))
        {
            return Invalid("EndsOn", "The last day must be on or after the first, not in the past, and within a year.");
        }

        var category = request.Category?.Trim();
        var products = (request.ProductIds ?? []).Distinct().ToList();
        if (request.Scope == SaleOfferScope.Category && string.IsNullOrEmpty(category))
        {
            return Invalid("Category", "Choose the category the offer is on.");
        }

        if (request.Kind == SaleOfferKind.Combo && (products.Count is < 2 or > 10 || await db.Products.CountAsync(p => products.Contains(p.Id), ct) != products.Count))
        {
            return Invalid("ProductIds", "A combo is 2 to 10 items.");
        }

        if (request.Kind != SaleOfferKind.Combo && request.Scope == SaleOfferScope.Items && (products.Count is 0 or > 500 || await db.Products.CountAsync(p => products.Contains(p.Id), ct) != products.Count))
        {
            return Invalid("ProductIds", "Choose the items the offer is on.");
        }

        var offer = new SaleOffer
        {
            Id = request.Id, Name = name, Kind = request.Kind, Value = request.Value, Scope = request.Scope,
            StartsAt = new DateTimeOffset(request.StartsOn.ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime(),
            EndsAt = new DateTimeOffset(request.EndsOn.AddDays(1).ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime(),
            Category = request.Scope == SaleOfferScope.Category ? category : null, ProductIds = request.Scope == SaleOfferScope.Items || request.Kind == SaleOfferKind.Combo ? products : [],
            CreatedByUserId = user.UserId()!.Value,
            BuyQty = request.Kind == SaleOfferKind.BuyGet ? request.BuyQty : null, GetQty = request.Kind == SaleOfferKind.BuyGet ? request.GetQty : null,
            MinQty = request.Kind == SaleOfferKind.Slab ? request.MinQty : null,
            MinBill = request.Kind is SaleOfferKind.BillPercent or SaleOfferKind.BillAmount ? request.MinBill : null,
            ComboPrice = request.Kind == SaleOfferKind.Combo ? request.ComboPrice : null,
            Days = request.Days is 127 ? null : request.Days, FromMinute = request.FromMinute, ToMinute = request.ToMinute,
        };
        if (offer.Kind == SaleOfferKind.Combo)
        {
            offer.Scope = SaleOfferScope.Items;
        }
        db.SaleOffers.Add(offer);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(offer, new SaleOfferStats(0, 0, 0, 0)));
    }

    /// <summary>What each kind needs: a sensible discount, and its own numbers.</summary>
    private static (string Field, string Message)? KindProblem(SaleOfferRequest r)
    {
        static bool Percent(decimal v) => v is > 0 and <= 90 && decimal.Round(v, 2) == v;
        return r.Kind switch
        {
            SaleOfferKind.Percent or SaleOfferKind.Slab or SaleOfferKind.BillPercent when !Percent(r.Value) => ("Value", "Enter the discount: 1 to 90 percent."),
            SaleOfferKind.Amount or SaleOfferKind.BillAmount when !Money.IsValidAmount(r.Value) => ("Value", "Enter the rupees off."),
            SaleOfferKind.BuyGet when r.BuyQty is not (>= 1 and <= 100) || r.GetQty is not (>= 1 and <= 100) => ("BuyQty", "Say how many to buy and how many come free, like buy 2 get 1."),
            SaleOfferKind.Slab when r.MinQty is not { } q || !Money.IsValidQuantity(q) || q < 2 => ("MinQty", "Say from how many pieces the discount starts (2 or more)."),
            SaleOfferKind.Combo when r.ComboPrice is not { } c || !Money.IsValidAmount(c) => ("ComboPrice", "Enter the price of the combo."),
            SaleOfferKind.BillPercent or SaleOfferKind.BillAmount when r.MinBill is not { } b || !Money.IsValidAmount(b) || (r.Kind == SaleOfferKind.BillAmount && r.Value >= b) => ("MinBill", "Enter the bill amount the offer starts from (more than the rupees off)."),
            _ => null,
        };
    }

    /// <summary>Ends an offer now. Bills already made keep it.</summary>
    private static async Task<IResult> StopAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var offer = await db.SaleOffers.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (offer is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow();
        if (offer.StoppedAt is null && offer.EndsAt > now)
        {
            offer.StoppedAt = now;
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.NoContent();
    }
}
