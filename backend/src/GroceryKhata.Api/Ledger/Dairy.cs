using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A farmer who brings milk to the collection centre. <see cref="Code"/> is the number they say at the counter.</summary>
public sealed class DairyFarmer : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Code { get; set; }

    public required string Name { get; set; }

    public string? Phone { get; set; }

    public string? Village { get; set; }

    public MilkKind DefaultKind { get; set; }

    public bool IsActive { get; set; } = true;
}

public enum MilkKind
{
    Cow,
    Buffalo,
    Mixed,
}

public enum Shift
{
    Morning,
    Evening,
}

/// <summary>How milk is priced. FatSnf: rate per kg of fat plus rate per kg of SNF. FatOnly: rate per kg of fat. Flat: rate per litre.</summary>
public enum RateMethod
{
    FatSnf,
    FatOnly,
    Flat,
}

/// <summary>Who a rate plan is for: what the centre pays farmers, or what the dairy (Sudha or another) pays the centre.</summary>
public enum RatePlanFor
{
    Farmers,
    Dairy,
}

/// <summary>
/// A rate plan from a date: rates change once or twice a year, and old collections keep the rate they were bought at,
/// so a change is a new plan, never an edit. One set of rates per kind of milk.
/// </summary>
public sealed class DairyRatePlan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public RatePlanFor For { get; set; }

    public DateOnly From { get; set; }

    public RateMethod Method { get; set; }

    public List<DairyRate> Rates { get; set; } = [];
}

public sealed class DairyRate : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid PlanId { get; set; }

    public MilkKind Kind { get; set; }

    /// <summary>₹ per kg of fat (FatSnf, FatOnly).</summary>
    public decimal FatRate { get; set; }

    /// <summary>₹ per kg of SNF (FatSnf).</summary>
    public decimal SnfRate { get; set; }

    /// <summary>₹ per litre (Flat).</summary>
    public decimal FlatRate { get; set; }

    /// <summary>The standard: milk under it is still bought at the formula, but flagged.</summary>
    public decimal MinFat { get; set; }

    public decimal MinSnf { get; set; }
}

/// <summary>One farmer's milk in one shift: measured, tested, priced. The amount is fixed when saved.</summary>
public sealed class MilkCollection : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public DateOnly Date { get; set; }

    public Shift Shift { get; set; }

    public Guid FarmerId { get; set; }

    public MilkKind Kind { get; set; }

    public decimal Litres { get; set; }

    public decimal Fat { get; set; }

    public decimal? Snf { get; set; }

    /// <summary>The lactometer reading, when SNF was worked out from it.</summary>
    public decimal? Clr { get; set; }

    /// <summary>₹ per litre this milk worked out to.</summary>
    public decimal Rate { get; set; }

    public decimal Amount { get; set; }

    public bool BelowStandard { get; set; }

    /// <summary>Sour or adulterated milk sent back: recorded, not paid.</summary>
    public bool Rejected { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>Every change, oldest first ("10 Nov 08:12 owner: 5 L 6.5 fat → 5 L 6.2 fat"), so a reading is never changed silently.</summary>
    public string? EditLog { get; set; }
}

/// <summary>On a farmer's account besides milk: cattle feed or an advance taken (owed back), a bonus, or a payout made.</summary>
public sealed class DairyFarmerEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid FarmerId { get; set; }

    public FarmerEntryKind Kind { get; set; }

    public decimal Amount { get; set; }

    public PaymentMode? Mode { get; set; }

    public string? Note { get; set; }

    public DateOnly Date { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public enum FarmerEntryKind
{
    Feed,
    Advance,
    Bonus,
    Payout,
}

/// <summary>Milk sent to the dairy in one shift, and what the dairy accepted after its own test (filled in from its statement).</summary>
public sealed class MilkDispatch : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public DateOnly Date { get; set; }

    public Shift Shift { get; set; }

    public required string Agency { get; set; }

    public MilkKind Kind { get; set; }

    public decimal Litres { get; set; }

    public decimal Fat { get; set; }

    public decimal Snf { get; set; }

    public decimal? AcceptedLitres { get; set; }

    public decimal? AcceptedFat { get; set; }

    public decimal? AcceptedSnf { get; set; }

    /// <summary>What the dairy pays for it (its statement, or our dairy rate plan on the accepted figures).</summary>
    public decimal? Amount { get; set; }

    public DateOnly? PaidOn { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record FarmerRequest(Guid Id, string? Name, string? Phone, string? Village, MilkKind DefaultKind = MilkKind.Buffalo, bool IsActive = true);

public sealed record FarmerDto(Guid Id, int Code, string Name, string? Phone, string? Village, MilkKind DefaultKind, bool IsActive, decimal Due);

public sealed record RateRequest(MilkKind Kind, decimal FatRate, decimal SnfRate, decimal FlatRate, decimal MinFat, decimal MinSnf);

public sealed record RatePlanRequest(Guid Id, RatePlanFor For, DateOnly From, RateMethod Method, IReadOnlyList<RateRequest>? Rates);

public sealed record RatePlanDto(Guid Id, RatePlanFor For, DateOnly From, RateMethod Method, IReadOnlyList<RateRequest> Rates);

/// <summary>Either <see cref="Snf"/>, or <see cref="Clr"/> (SNF is then worked out from it). Litres, not kg: kg is litres × 1.03.</summary>
public sealed record CollectionRequest(Guid Id, DateOnly Date, Shift Shift, Guid FarmerId, MilkKind Kind, decimal Litres, decimal Fat, decimal? Snf, decimal? Clr, bool Rejected = false);

public sealed record CollectionDto(
    Guid Id, DateOnly Date, Shift Shift, Guid FarmerId, int FarmerCode, string FarmerName, string? Phone, MilkKind Kind, decimal Litres, decimal Fat, decimal? Snf, decimal? Clr,
    decimal Rate, decimal Amount, bool BelowStandard, bool Rejected, bool Edited);

public sealed record ShiftDto(DateOnly Date, Shift Shift, IReadOnlyList<CollectionDto> Collections, decimal Litres, decimal Amount, decimal AvgFat);

public sealed record FarmerEntryRequest(Guid Id, FarmerEntryKind Kind, decimal Amount, PaymentMode? Mode, string? Note);

/// <summary>A farmer's bill for a period: milk, deductions and bonus in it, and what is due now (including anything carried from before).</summary>
public sealed record FarmerBillDto(
    Guid FarmerId, int Code, string Name, string? Phone, decimal Litres, decimal AvgFat, decimal MilkAmount, decimal Feed, decimal Advance, decimal Bonus, decimal PaidInPeriod,
    decimal Opening, decimal Due, int Collections);

public sealed record DispatchRequest(Guid Id, DateOnly Date, Shift Shift, string? Agency, MilkKind Kind, decimal Litres, decimal Fat, decimal Snf);

public sealed record DispatchAcceptRequest(decimal AcceptedLitres, decimal AcceptedFat, decimal AcceptedSnf, decimal? Amount);

public sealed record DispatchDto(
    Guid Id, DateOnly Date, Shift Shift, string Agency, MilkKind Kind, decimal Litres, decimal Fat, decimal Snf, decimal? AcceptedLitres, decimal? AcceptedFat, decimal? AcceptedSnf,
    decimal? Amount, DateOnly? PaidOn);

/// <summary>
/// A period's milk balance: what came in from farmers against what went to the dairy and what it accepted, in litres,
/// kg of fat and kg of SNF. The gaps are the centre's loss (spillage, souring, a reading that differs) and show where.
/// </summary>
public sealed record DairySummaryDto(
    DateOnly From, DateOnly To, decimal CollectedLitres, decimal CollectedFatKg, decimal CollectedSnfKg, decimal PaidToFarmers,
    decimal DispatchedLitres, decimal DispatchedFatKg, decimal DispatchedSnfKg, decimal AcceptedLitres, decimal AcceptedFatKg, decimal AcceptedSnfKg,
    decimal FromDairy, decimal DairyUnpaid, decimal Margin, int Rejected, int BelowStandard, int DispatchesAwaitingDairy);

public static class Dairy
{
    /// <summary>Milk weighs a little more than water: 1 litre ≈ 1.03 kg. Fat and SNF are percentages of the weight.</summary>
    public const decimal KgPerLitre = 1.03m;

    public static IEndpointRouteBuilder MapDairy(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dairy").WithTags("Dairy").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/farmers", FarmersAsync);
        group.MapPost("/farmers", SaveFarmerAsync);
        group.MapGet("/rate-plans", PlansAsync);
        group.MapPost("/rate-plans", AddPlanAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/quote", QuoteAsync);
        group.MapGet("/collections", ShiftAsync);
        group.MapPost("/collections", CollectAsync);
        group.MapPut("/collections/{id:guid}", EditCollectionAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/farmers/{id:guid}/entries", FarmerEntryAsync);
        group.MapGet("/bills", BillsAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/dispatches", DispatchesAsync);
        group.MapPost("/dispatches", DispatchAsync);
        group.MapPut("/dispatches/{id:guid}/accepted", AcceptAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/dispatches/{id:guid}/paid", DispatchPaidAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/summary", SummaryAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static DateOnly Today(TimeProvider clock) => IndiaTime.Today(clock);

    /// <summary>SNF from the lactometer reading (the common field formula): CLR/4 + 0.21 × fat + 0.36. Dairies vary it slightly.</summary>
    public static decimal SnfFromClr(decimal clr, decimal fat) => decimal.Round((clr / 4) + (0.21m * fat) + 0.36m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The price of this milk under a rate: FatSnf = kg × (fat% × fat rate + SNF% × SNF rate); FatOnly = kg × fat% × fat
    /// rate; Flat = litres × rate. Returns the amount and the rate per litre it works out to.
    /// </summary>
    public static (decimal Amount, decimal PerLitre, bool BelowStandard) Price(RateMethod method, DairyRate rate, decimal litres, decimal fat, decimal snf)
    {
        var kg = litres * KgPerLitre;
        var amount = method switch
        {
            RateMethod.FatSnf => kg * ((fat / 100 * rate.FatRate) + (snf / 100 * rate.SnfRate)),
            RateMethod.FatOnly => kg * fat / 100 * rate.FatRate,
            _ => litres * rate.FlatRate,
        };
        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        return (amount, litres > 0 ? decimal.Round(amount / litres, 2) : 0, fat < rate.MinFat || (snf > 0 && snf < rate.MinSnf));
    }

    private static async Task<(DairyRatePlan Plan, DairyRate Rate)?> RateOnAsync(AppDbContext db, RatePlanFor who, DateOnly date, MilkKind kind, CancellationToken ct)
    {
        var plan = await db.DairyRatePlans.AsNoTracking().Include(p => p.Rates).Where(p => p.For == who && p.From <= date).OrderByDescending(p => p.From).FirstOrDefaultAsync(ct);
        return plan?.Rates.FirstOrDefault(r => r.Kind == kind) is { } rate ? (plan, rate) : null;
    }

    private static async Task<IReadOnlyList<FarmerDto>> FarmersAsync(AppDbContext db, CancellationToken ct)
    {
        var due = await DuesAsync(db, null, ct);
        return (await db.DairyFarmers.AsNoTracking().OrderBy(f => f.Code).ToListAsync(ct))
            .Select(f => new FarmerDto(f.Id, f.Code, f.Name, f.Phone, f.Village, f.DefaultKind, f.IsActive, due.GetValueOrDefault(f.Id))).ToList();
    }

    /// <summary>What the centre owes each farmer up to a day (all time when null): milk and bonuses, less feed, advances and payouts.</summary>
    private static async Task<Dictionary<Guid, decimal>> DuesAsync(AppDbContext db, DateOnly? before, CancellationToken ct)
    {
        var milk = await db.MilkCollections.AsNoTracking().Where(c => !c.Rejected && (before == null || c.Date < before))
            .GroupBy(c => c.FarmerId).Select(g => new { g.Key, Total = g.Sum(c => c.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var entries = await db.DairyFarmerEntries.AsNoTracking().Where(e => before == null || e.Date < before)
            .GroupBy(e => e.FarmerId).Select(g => new { g.Key, Total = g.Sum(e => e.Kind == FarmerEntryKind.Bonus ? e.Amount : -e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return milk.Keys.Union(entries.Keys).ToDictionary(k => k, k => milk.GetValueOrDefault(k) + entries.GetValueOrDefault(k));
    }

    private static async Task<IResult> SaveFarmerAsync(FarmerRequest request, AppDbContext db, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (request.Id == Guid.Empty || name is not { Length: >= 2 and <= 80 })
        {
            return Invalid("Name", "Enter the farmer's name.");
        }

        var phone = PhoneNumber.Normalize(request.Phone) ?? (string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim()[..Math.Min(20, request.Phone.Trim().Length)]);
        var village = string.IsNullOrWhiteSpace(request.Village) ? null : request.Village.Trim()[..Math.Min(60, request.Village.Trim().Length)];
        var farmer = await db.DairyFarmers.SingleOrDefaultAsync(f => f.Id == request.Id, ct);
        if (farmer is null)
        {
            farmer = new DairyFarmer { Id = request.Id, Name = name, Code = (await db.DairyFarmers.MaxAsync(f => (int?)f.Code, ct) ?? 0) + 1 };
            db.DairyFarmers.Add(farmer);
        }

        (farmer.Name, farmer.Phone, farmer.Village, farmer.DefaultKind, farmer.IsActive) = (name, phone, village, request.DefaultKind, request.IsActive);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new FarmerDto(farmer.Id, farmer.Code, farmer.Name, farmer.Phone, farmer.Village, farmer.DefaultKind, farmer.IsActive, 0));
    }

    private static async Task<IReadOnlyList<RatePlanDto>> PlansAsync(AppDbContext db, CancellationToken ct) =>
        (await db.DairyRatePlans.AsNoTracking().Include(p => p.Rates).OrderByDescending(p => p.From).Take(50).ToListAsync(ct))
            .Select(p => new RatePlanDto(p.Id, p.For, p.From, p.Method, p.Rates.Select(r => new RateRequest(r.Kind, r.FatRate, r.SnfRate, r.FlatRate, r.MinFat, r.MinSnf)).ToList())).ToList();

    private static async Task<IResult> AddPlanAsync(RatePlanRequest request, AppDbContext db, CancellationToken ct)
    {
        var rates = request.Rates ?? [];
        if (request.Id == Guid.Empty || rates.Count is 0 or > 3 || rates.Select(r => r.Kind).Distinct().Count() != rates.Count)
        {
            return Invalid("Rates", "Give rates for cow, buffalo and/or mixed milk, once each.");
        }

        bool Bad(decimal v, decimal max) => v < 0 || v > max || decimal.Round(v, 2) != v;
        if (rates.Any(r => Bad(r.FatRate, 5000) || Bad(r.SnfRate, 5000) || Bad(r.FlatRate, 500) || Bad(r.MinFat, 15) || Bad(r.MinSnf, 15))
            || rates.Any(r => request.Method switch { RateMethod.Flat => r.FlatRate <= 0, RateMethod.FatOnly => r.FatRate <= 0, _ => r.FatRate <= 0 || r.SnfRate <= 0 }))
        {
            return Invalid("Rates", "Enter the rates for the chosen method (₹ per kg fat and SNF, or ₹ per litre).");
        }

        if (await db.DairyRatePlans.AnyAsync(p => p.Id == request.Id, ct))
        {
            return TypedResults.Ok(new { request.Id });
        }

        db.DairyRatePlans.Add(new DairyRatePlan
        {
            Id = request.Id, For = request.For, From = request.From, Method = request.Method,
            Rates = rates.Select(r => new DairyRate { Kind = r.Kind, FatRate = r.FatRate, SnfRate = r.SnfRate, FlatRate = r.FlatRate, MinFat = r.MinFat, MinSnf = r.MinSnf }).ToList(),
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new { request.Id });
    }

    /// <summary>What milk would be paid, before saving: the counter shows it as the numbers are typed.</summary>
    private static async Task<IResult> QuoteAsync(DateOnly? date, string? kind, decimal litres, decimal fat, decimal? snf, decimal? clr, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        // Query values come as "buffalo"; enums bind case-sensitively, so read them by hand.
        if (!Enum.TryParse<MilkKind>(kind, ignoreCase: true, out var milk)) return Invalid("Kind", "Choose cow, buffalo or mixed milk.");
        var day = date ?? Today(clock);
        var s = snf ?? (clr is { } c ? SnfFromClr(c, fat) : 0);
        if (await RateOnAsync(db, RatePlanFor.Farmers, day, milk, ct) is not { } r)
        {
            return Invalid("Rate", $"Set the farmers' rate for {milk.ToString().ToLowerInvariant()} milk first.");
        }

        var (amount, perLitre, below) = Price(r.Plan.Method, r.Rate, litres, fat, s);
        return TypedResults.Ok(new { amount, perLitre, snf = s, belowStandard = below });
    }

    private static async Task<List<CollectionDto>> ToDtosAsync(AppDbContext db, IQueryable<MilkCollection> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var ids = rows.Select(r => r.FarmerId).Distinct().ToList();
        var farmers = await db.DairyFarmers.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        return rows.Select(c => new CollectionDto(c.Id, c.Date, c.Shift, c.FarmerId, farmers[c.FarmerId].Code, farmers[c.FarmerId].Name, farmers[c.FarmerId].Phone, c.Kind, c.Litres, c.Fat,
            c.Snf, c.Clr, c.Rate, c.Amount, c.BelowStandard, c.Rejected, c.EditLog != null)).ToList();
    }

    /// <summary>A shift's collections (today's current shift by default), in the order they came.</summary>
    private static async Task<ShiftDto> ShiftAsync(DateOnly? date, string? shift, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow().ToOffset(IndiaTime.Offset);
        var day = date ?? DateOnly.FromDateTime(now.DateTime);
        var which = Enum.TryParse<Shift>(shift, ignoreCase: true, out var asked) ? asked : now.Hour < 14 ? Shift.Morning : Shift.Evening;
        var list = await ToDtosAsync(db, db.MilkCollections.Where(c => c.Date == day && c.Shift == which), ct);
        var kept = list.Where(c => !c.Rejected).ToList();
        var litres = kept.Sum(c => c.Litres);
        return new ShiftDto(day, which, list, litres, kept.Sum(c => c.Amount), litres > 0 ? decimal.Round(kept.Sum(c => c.Litres * c.Fat) / litres, 2) : 0);
    }

    private static string? CheckReading(decimal litres, decimal fat, decimal? snf, decimal? clr)
    {
        if (litres <= 0 || litres > 500 || decimal.Round(litres, 2) != litres) return "Enter the litres (up to 500, two decimals).";
        if (fat is < 0.5m or > 15 || decimal.Round(fat, 2) != fat) return "Enter the fat % (0.5 to 15).";
        if (snf is { } s && (s is < 3 or > 15 || decimal.Round(s, 2) != s)) return "Enter the SNF % (3 to 15).";
        if (clr is { } c && (c is < 15 or > 40 || decimal.Round(c, 1) != c)) return "Enter the lactometer reading (15 to 40).";
        return null;
    }

    /// <summary>Saves one farmer's milk for the shift, priced by the farmers' rate on that day. Safe to repeat.</summary>
    private static async Task<IResult> CollectAsync(CollectionRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.MilkCollections.AnyAsync(c => c.Id == request.Id, ct))
        {
            return TypedResults.Ok((await ToDtosAsync(db, db.MilkCollections.Where(c => c.Id == request.Id), ct)).Single());
        }

        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (request.Date > Today(clock) || request.Date < Today(clock).AddDays(-3)) return Invalid("Date", "Enter today's milk (or up to 3 days back).");
        if (!await db.DairyFarmers.AnyAsync(f => f.Id == request.FarmerId && f.IsActive, ct)) return Invalid("FarmerId", "Choose the farmer.");
        if (CheckReading(request.Litres, request.Fat, request.Snf, request.Clr) is { } problem) return Invalid("Litres", problem);
        var snf = request.Snf ?? (request.Clr is { } clr ? SnfFromClr(clr, request.Fat) : (decimal?)null);
        if (await RateOnAsync(db, RatePlanFor.Farmers, request.Date, request.Kind, ct) is not { } r)
        {
            return Invalid("Rate", $"Set the farmers' rate for {request.Kind.ToString().ToLowerInvariant()} milk first.");
        }

        if (r.Plan.Method == RateMethod.FatSnf && snf is null) return Invalid("Snf", "This rate needs the SNF (or the lactometer reading).");
        var (amount, perLitre, below) = Price(r.Plan.Method, r.Rate, request.Litres, request.Fat, snf ?? 0);
        db.MilkCollections.Add(new MilkCollection
        {
            Id = request.Id, Date = request.Date, Shift = request.Shift, FarmerId = request.FarmerId, Kind = request.Kind, Litres = request.Litres, Fat = request.Fat, Snf = snf,
            Clr = request.Clr, Rate = request.Rejected ? 0 : perLitre, Amount = request.Rejected ? 0 : amount, BelowStandard = below, Rejected = request.Rejected,
            CreatedByUserId = user.UserId()!.Value,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, db.MilkCollections.Where(c => c.Id == request.Id), ct)).Single());
    }

    /// <summary>The owner corrects a reading. The old and new figures go in the edit log; the amount is worked out again.</summary>
    private static async Task<IResult> EditCollectionAsync(Guid id, CollectionRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var c = await db.MilkCollections.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return TypedResults.NotFound();
        if (CheckReading(request.Litres, request.Fat, request.Snf, request.Clr) is { } problem) return Invalid("Litres", problem);
        var snf = request.Snf ?? (request.Clr is { } clr ? SnfFromClr(clr, request.Fat) : (decimal?)null);
        if (await RateOnAsync(db, RatePlanFor.Farmers, c.Date, request.Kind, ct) is not { } r) return Invalid("Rate", "No farmers' rate for this milk.");
        var (amount, perLitre, below) = Price(r.Plan.Method, r.Rate, request.Litres, request.Fat, snf ?? 0);
        var when = clock.GetUtcNow().ToOffset(IndiaTime.Offset);
        string Show(MilkCollection x) => $"{x.Kind} {x.Litres:0.##} L {x.Fat:0.##} fat {x.Snf:0.##} snf{(x.Rejected ? " rejected" : "")} ₹{x.Amount:0.##}";
        var before = Show(c);
        (c.Kind, c.Litres, c.Fat, c.Snf, c.Clr, c.Rejected, c.BelowStandard) = (request.Kind, request.Litres, request.Fat, snf, request.Clr, request.Rejected, below);
        (c.Rate, c.Amount) = request.Rejected ? (0, 0) : (perLitre, amount);
        var line = $"{when:dd MMM HH:mm}: {before} → {Show(c)}";
        c.EditLog = c.EditLog is null ? line : $"{c.EditLog}\n{line}"[^Math.Min(2000, $"{c.EditLog}\n{line}".Length)..];
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, db.MilkCollections.Where(x => x.Id == id), ct)).Single());
    }

    /// <summary>Feed or an advance given (owed back from milk money), a bonus added, or a payout made (cash out: also a kharcha line).</summary>
    private static async Task<IResult> FarmerEntryAsync(Guid id, FarmerEntryRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.DairyFarmerEntries.AnyAsync(e => e.Id == request.Id, ct)) return TypedResults.NoContent();
        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (!await db.DairyFarmers.AnyAsync(f => f.Id == id, ct)) return TypedResults.NotFound();
        if (request.Amount <= 0 || !Money.IsValidAmount(request.Amount)) return Invalid("Amount", "Enter the amount.");
        var (userId, now) = (user.UserId()!.Value, clock.GetUtcNow());
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()[..Math.Min(120, request.Note.Trim().Length)];
        db.DairyFarmerEntries.Add(new DairyFarmerEntry
        {
            Id = request.Id, FarmerId = id, Kind = request.Kind, Amount = request.Amount, Mode = request.Kind is FarmerEntryKind.Payout or FarmerEntryKind.Advance ? request.Mode ?? PaymentMode.Cash : null,
            Note = note, Date = Today(clock), CreatedByUserId = userId,
        });
        // Money out of the cash box (a payout or a cash advance) shows in the kharcha book as milk bought, so the day's cash adds up.
        if (request.Kind is FarmerEntryKind.Payout or FarmerEntryKind.Advance)
        {
            db.Expenses.Add(new Expense
            {
                Amount = request.Amount, Category = ExpenseCategory.Purchase, Mode = request.Mode ?? PaymentMode.Cash, Note = request.Kind == FarmerEntryKind.Payout ? "Milk payment to farmer" : "Advance to farmer",
                OccurredAt = now, CreatedByUserId = userId,
            });
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Every farmer's bill for a period (a 10-day cycle, a week or a month): milk in it, deductions, and what's due now.</summary>
    private static async Task<IReadOnlyList<FarmerBillDto>> BillsAsync(DateOnly from, DateOnly to, AppDbContext db, CancellationToken ct)
    {
        var opening = await DuesAsync(db, from, ct);
        var milk = await db.MilkCollections.AsNoTracking().Where(c => !c.Rejected && c.Date >= from && c.Date <= to)
            .GroupBy(c => c.FarmerId).Select(g => new { g.Key, Litres = g.Sum(c => c.Litres), FatL = g.Sum(c => c.Litres * c.Fat), Amount = g.Sum(c => c.Amount), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, ct);
        var entries = await db.DairyFarmerEntries.AsNoTracking().Where(e => e.Date >= from && e.Date <= to)
            .GroupBy(e => new { e.FarmerId, e.Kind }).Select(g => new { g.Key.FarmerId, g.Key.Kind, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        decimal Of(Guid f, FarmerEntryKind k) => entries.Where(e => e.FarmerId == f && e.Kind == k).Sum(e => e.Total);
        var farmers = await db.DairyFarmers.AsNoTracking().OrderBy(f => f.Code).ToListAsync(ct);
        return farmers.Where(f => milk.ContainsKey(f.Id) || entries.Any(e => e.FarmerId == f.Id) || opening.GetValueOrDefault(f.Id) != 0).Select(f =>
        {
            var m = milk.GetValueOrDefault(f.Id);
            var (feed, advance, bonus, paid) = (Of(f.Id, FarmerEntryKind.Feed), Of(f.Id, FarmerEntryKind.Advance), Of(f.Id, FarmerEntryKind.Bonus), Of(f.Id, FarmerEntryKind.Payout));
            var amount = m?.Amount ?? 0;
            var o = opening.GetValueOrDefault(f.Id);
            return new FarmerBillDto(f.Id, f.Code, f.Name, f.Phone, m?.Litres ?? 0, m is { Litres: > 0 } ? decimal.Round(m.FatL / m.Litres, 2) : 0, amount, feed, advance, bonus, paid, o,
                o + amount + bonus - feed - advance - paid, m?.Count ?? 0);
        }).ToList();
    }

    private static DispatchDto ToDto(MilkDispatch d) =>
        new(d.Id, d.Date, d.Shift, d.Agency, d.Kind, d.Litres, d.Fat, d.Snf, d.AcceptedLitres, d.AcceptedFat, d.AcceptedSnf, d.Amount, d.PaidOn);

    private static async Task<IReadOnlyList<DispatchDto>> DispatchesAsync(AppDbContext db, CancellationToken ct) =>
        (await db.MilkDispatches.AsNoTracking().OrderByDescending(d => d.Date).ThenByDescending(d => d.Shift).Take(60).ToListAsync(ct)).Select(ToDto).ToList();

    /// <summary>Milk handed to the dairy's tanker or chilling centre, with our own reading of it.</summary>
    private static async Task<IResult> DispatchAsync(DispatchRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.MilkDispatches.SingleOrDefaultAsync(d => d.Id == request.Id, ct) is { } existing) return TypedResults.Ok(ToDto(existing));
        var agency = request.Agency?.Trim();
        if (request.Id == Guid.Empty || agency is not { Length: >= 2 and <= 60 }) return Invalid("Agency", "Enter the dairy (Sudha or another).");
        if (request.Litres <= 0 || request.Litres > 100_000 || decimal.Round(request.Litres, 2) != request.Litres) return Invalid("Litres", "Enter the litres sent.");
        if (request.Fat is < 0.5m or > 15 || request.Snf is < 3 or > 15) return Invalid("Fat", "Enter the fat and SNF of the milk sent.");
        if (request.Date > Today(clock)) return Invalid("Date", "The date can't be in the future.");
        var d = new MilkDispatch
        {
            Id = request.Id, Date = request.Date, Shift = request.Shift, Agency = agency, Kind = request.Kind, Litres = request.Litres, Fat = request.Fat, Snf = request.Snf,
            CreatedByUserId = user.UserId()!.Value,
        };
        db.MilkDispatches.Add(d);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(d));
    }

    /// <summary>
    /// The dairy's own figures from its statement. Without an amount, it's worked out from our dairy rate plan on the
    /// accepted figures, so the owner can check the dairy's bill against it.
    /// </summary>
    private static async Task<IResult> AcceptAsync(Guid id, DispatchAcceptRequest request, AppDbContext db, CancellationToken ct)
    {
        var d = await db.MilkDispatches.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return TypedResults.NotFound();
        if (request.AcceptedLitres < 0 || request.AcceptedLitres > d.Litres * 1.1m || request.AcceptedFat is < 0 or > 15 || request.AcceptedSnf is < 0 or > 15)
        {
            return Invalid("AcceptedLitres", "Enter what the dairy accepted, from its statement.");
        }

        var amount = request.Amount;
        if (amount is null && await RateOnAsync(db, RatePlanFor.Dairy, d.Date, d.Kind, ct) is { } r)
        {
            amount = Price(r.Plan.Method, r.Rate, request.AcceptedLitres, request.AcceptedFat, request.AcceptedSnf).Amount;
        }

        if (amount is < 0 || (amount is > 0 && !Money.IsValidAmount(amount.Value))) return Invalid("Amount", "Enter the amount on the dairy's statement.");
        (d.AcceptedLitres, d.AcceptedFat, d.AcceptedSnf, d.Amount) = (request.AcceptedLitres, request.AcceptedFat, request.AcceptedSnf, amount);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(d));
    }

    private static async Task<IResult> DispatchPaidAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var d = await db.MilkDispatches.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return TypedResults.NotFound();
        d.PaidOn ??= Today(clock);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(d));
    }

    private static async Task<DairySummaryDto> SummaryAsync(DateOnly? from, DateOnly? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = Today(clock);
        var (start, end) = (from ?? new DateOnly(today.Year, today.Month, 1), to ?? today);
        var c = await db.MilkCollections.AsNoTracking().Where(x => x.Date >= start && x.Date <= end).ToListAsync(ct);
        var d = await db.MilkDispatches.AsNoTracking().Where(x => x.Date >= start && x.Date <= end).ToListAsync(ct);
        var kept = c.Where(x => !x.Rejected).ToList();
        decimal Kg(decimal litres, decimal pct) => decimal.Round(litres * KgPerLitre * pct / 100, 2);
        var accepted = d.Where(x => x.AcceptedLitres is not null).ToList();
        var paid = kept.Sum(x => x.Amount);
        var fromDairy = accepted.Sum(x => x.Amount ?? 0);
        return new DairySummaryDto(start, end,
            kept.Sum(x => x.Litres), kept.Sum(x => Kg(x.Litres, x.Fat)), kept.Sum(x => Kg(x.Litres, x.Snf ?? 0)), paid,
            d.Sum(x => x.Litres), d.Sum(x => Kg(x.Litres, x.Fat)), d.Sum(x => Kg(x.Litres, x.Snf)),
            accepted.Sum(x => x.AcceptedLitres!.Value), accepted.Sum(x => Kg(x.AcceptedLitres!.Value, x.AcceptedFat ?? 0)), accepted.Sum(x => Kg(x.AcceptedLitres!.Value, x.AcceptedSnf ?? 0)),
            fromDairy, accepted.Where(x => x.PaidOn is null).Sum(x => x.Amount ?? 0), fromDairy - paid,
            c.Count(x => x.Rejected), kept.Count(x => x.BelowStandard), d.Count(x => x.AcceptedLitres is null));
    }
}
