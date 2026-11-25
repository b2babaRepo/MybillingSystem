using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A company's dealer scheme: buy this much of a brand between two dates (in rupees before GST, or in units) and get this
/// percent back. Progress is read from the shop's own supplier bills, so the owner sees how far off the target is and
/// what rebate is due, then writes down what the company actually paid (usually a credit note) to catch short payments.
/// </summary>
public sealed class BrandScheme : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>The brand as it appears in item names ("Asian Paints", "Polycab"): purchases of those items count.</summary>
    public required string Brand { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    /// <summary>"value": the target is rupees bought before GST; "quantity": units bought.</summary>
    public required string Basis { get; set; }

    public decimal Target { get; set; }

    /// <summary>Percent of the value bought paid back once the target is met.</summary>
    public decimal RebatePercent { get; set; }

    public decimal? Received { get; set; }

    public DateOnly? ReceivedOn { get; set; }

    public string? Note { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record BrandSchemeRequest(Guid Id, string? Name, string? Brand, DateOnly From, DateOnly To, string? Basis, decimal Target, decimal RebatePercent);

public sealed record BrandSchemeReceivedRequest(decimal Amount, DateOnly? On, string? Note);

/// <summary>Progress so far: bought (value and units), how far to go, the rebate earned once the target is met, and what came.</summary>
public sealed record BrandSchemeDto(
    Guid Id, string Name, string Brand, DateOnly From, DateOnly To, string Basis, decimal Target, decimal RebatePercent,
    decimal BoughtValue, decimal BoughtQuantity, decimal Progress, decimal ToGo, bool Met, decimal RebateDue, decimal? Received, DateOnly? ReceivedOn, string? Note, int DaysLeft);

public static class BrandSchemes
{
    public static IEndpointRouteBuilder MapBrandSchemes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/brand-schemes").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapPost("/{id:guid}/received", ReceivedAsync);
        return app;
    }

    private static async Task<IReadOnlyList<BrandSchemeDto>> ListAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var schemes = await db.BrandSchemes.AsNoTracking().OrderByDescending(s => s.To).Take(100).ToListAsync(ct);
        var today = IndiaTime.Today(clock);
        var list = new List<BrandSchemeDto>();
        foreach (var s in schemes)
        {
            list.Add(await ToDtoAsync(db, s, today, ct));
        }

        return list;
    }

    /// <summary>The brand's purchases in the scheme's dates, on bills not cancelled: value before GST and units (free ones too).</summary>
    private static async Task<BrandSchemeDto> ToDtoAsync(AppDbContext db, BrandScheme s, DateOnly today, CancellationToken ct)
    {
        var (start, _) = IndiaTime.Day(s.From);
        var (_, end) = IndiaTime.Day(s.To);
        var brand = s.Brand.ToLower();
        var bought = await (from l in db.PurchaseLines.AsNoTracking()
                            join pu in db.Purchases.AsNoTracking() on l.PurchaseId equals pu.Id
                            join p in db.Products.AsNoTracking() on l.ProductId equals p.Id
                            where pu.CancelledAt == null && pu.OccurredAt >= start && pu.OccurredAt < end && p.Name.ToLower().Contains(brand)
                            select new { l.Taxable, l.Quantity, l.FreeQuantity }).ToListAsync(ct);
        var value = bought.Sum(b => b.Taxable);
        var quantity = bought.Sum(b => b.Quantity + b.FreeQuantity);
        var progress = s.Basis == "quantity" ? quantity : value;
        var met = progress >= s.Target;
        return new BrandSchemeDto(s.Id, s.Name, s.Brand, s.From, s.To, s.Basis, s.Target, s.RebatePercent, value, quantity, progress,
            Math.Max(0, s.Target - progress), met, met ? decimal.Round(value * s.RebatePercent / 100, 2) : 0, s.Received, s.ReceivedOn, s.Note,
            Math.Max(0, s.To.DayNumber - today.DayNumber));
    }

    private static async Task<IResult> AddAsync(BrandSchemeRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.BrandSchemes.AsNoTracking().SingleOrDefaultAsync(s => s.Id == r.Id, ct) is { } again)
        {
            return TypedResults.Ok(await ToDtoAsync(db, again, IndiaTime.Today(clock), ct));
        }

        var name = r.Name?.Trim();
        var brand = r.Brand?.Trim();
        if (r.Id == Guid.Empty || name is not { Length: >= 2 and <= 80 } || brand is not { Length: >= 2 and <= 40 } || r.Basis is not ("value" or "quantity")
            || r.To < r.From || r.To.DayNumber - r.From.DayNumber > 400 || r.Target is <= 0 or > 100_000_000 || r.RebatePercent is <= 0 or > 50)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Name"] = ["Give the scheme a name and the brand as it is in your item names, dates up to about a year apart, a target and a rebate from 0 to 50%."],
            });
        }

        var scheme = new BrandScheme
        {
            Id = r.Id, Name = name, Brand = brand, From = r.From, To = r.To, Basis = r.Basis, Target = r.Target, RebatePercent = r.RebatePercent,
            CreatedByUserId = user.UserId()!.Value,
        };
        db.BrandSchemes.Add(scheme);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(db, scheme, IndiaTime.Today(clock), ct));
    }

    private static async Task<IResult> ReceivedAsync(Guid id, BrandSchemeReceivedRequest r, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var scheme = await db.BrandSchemes.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (scheme is null)
        {
            return TypedResults.NotFound();
        }

        if (r.Amount is < 0 or > 100_000_000 || r.Note?.Trim().Length > 120)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = ["Enter what the company paid (0 if nothing came), and a short note."] });
        }

        scheme.Received = decimal.Round(r.Amount, 2);
        scheme.ReceivedOn = r.On ?? IndiaTime.Today(clock);
        scheme.Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(db, scheme, IndiaTime.Today(clock), ct));
    }
}
