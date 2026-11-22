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

    public Guid CreatedByUserId { get; set; }
}

/// <summary><see cref="StartsOn"/> and <see cref="EndsOn"/> are whole days, India time; the offer runs to the end of its last day.</summary>
public sealed record SaleOfferRequest(Guid Id, string? Name, SaleOfferKind Kind, decimal Value, DateOnly StartsOn, DateOnly EndsOn, SaleOfferScope Scope, string? Category = null, IReadOnlyList<Guid>? ProductIds = null);

/// <summary>How an offer did: on bills not cancelled. Owners only; null for helpers.</summary>
public sealed record SaleOfferStats(int Bills, decimal Quantity, decimal Sales, decimal Saved);

public sealed record SaleOfferDto(
    Guid Id, string Name, SaleOfferKind Kind, decimal Value, DateTimeOffset StartsAt, DateTimeOffset EndsAt, SaleOfferScope Scope, string? Category,
    IReadOnlyList<Guid> ProductIds, DateTimeOffset? StoppedAt, SaleOfferStats? Stats);

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
        running.Where(o => Covers(o, product, at)).Select(o => (Offer: o, Price: PriceFor(o, product.SellingPrice)))
            .OrderBy(x => x.Price).Cast<(SaleOffer, decimal)?>().FirstOrDefault();

    public static bool Covers(SaleOffer offer, Product product, DateTimeOffset at) =>
        at >= offer.StartsAt && at < (offer.StoppedAt is { } stopped && stopped < offer.EndsAt ? stopped : offer.EndsAt)
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
        }

        return offers.Select(o => ToDto(o, LedgerEndpoints.IsOwner(user) ? stats.GetValueOrDefault(o.Id) ?? new SaleOfferStats(0, 0, 0, 0) : null)).ToList();
    }

    private static SaleOfferDto ToDto(SaleOffer o, SaleOfferStats? stats) =>
        new(o.Id, o.Name, o.Kind, o.Value, o.StartsAt, o.EndsAt, o.Scope, o.Category, o.ProductIds, o.StoppedAt, stats);

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

        if (request.Kind == SaleOfferKind.Percent ? request.Value is <= 0 or > 90 || decimal.Round(request.Value, 2) != request.Value : !Money.IsValidAmount(request.Value))
        {
            return Invalid("Value", "Enter the discount: 1 to 90 percent, or rupees off each piece.");
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

        if (request.Scope == SaleOfferScope.Items && (products.Count is 0 or > 500 || await db.Products.CountAsync(p => products.Contains(p.Id), ct) != products.Count))
        {
            return Invalid("ProductIds", "Choose the items the offer is on.");
        }

        var offer = new SaleOffer
        {
            Id = request.Id, Name = name, Kind = request.Kind, Value = request.Value, Scope = request.Scope,
            StartsAt = new DateTimeOffset(request.StartsOn.ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime(),
            EndsAt = new DateTimeOffset(request.EndsOn.AddDays(1).ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime(),
            Category = request.Scope == SaleOfferScope.Category ? category : null, ProductIds = request.Scope == SaleOfferScope.Items ? products : [],
            CreatedByUserId = user.UserId()!.Value,
        };
        db.SaleOffers.Add(offer);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(offer, new SaleOfferStats(0, 0, 0, 0)));
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
