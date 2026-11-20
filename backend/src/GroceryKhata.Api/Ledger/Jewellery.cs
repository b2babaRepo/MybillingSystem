using System.Security.Claims;
using System.Text.RegularExpressions;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

// A jewellery shop's bill is a weight calculation, not a price lookup: (net weight × the day's rate for that purity)
// + wastage + making + stones + hallmark = the taxable value, then GST at 3% on all of it (CBIC FAQ: making is part of
// the value of the jewellery; 5% applies only to a karigar's job work). Each bill keeps the rate it used.

/// <summary>The rate set for one metal and purity at one moment. Every change is kept, so a bill can be checked later.</summary>
public sealed class MetalRate : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>gold or silver.</summary>
    public required string Metal { get; set; }

    /// <summary>Fineness in thousandths: 999, 995, 916 (22K), 750 (18K), 585 (14K); silver 999 or 925.</summary>
    public int Purity { get; set; }

    /// <summary>Selling rate per gram, before GST.</summary>
    public decimal SellPerGram { get; set; }

    /// <summary>What the shop pays per gram of this purity for old metal; null: the selling rate.</summary>
    public decimal? BuyPerGram { get; set; }

    public Guid SetByUserId { get; set; }
}

/// <summary>One finished ornament with its tag: weighed and described once, sold once.</summary>
public sealed class JewelleryPiece : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The shop's tag number (printed J-12 on the tag, and in its QR code).</summary>
    public int TagNo { get; set; }

    public required string Name { get; set; }

    public string? Category { get; set; }

    public required string Metal { get; set; }

    public int Purity { get; set; }

    /// <summary>Grams, to 3 decimals.</summary>
    public decimal Gross { get; set; }

    /// <summary>Stones' weight in grams (1 carat = 0.2 g): not charged at the metal rate.</summary>
    public decimal Stone { get; set; }

    public decimal WastagePercent { get; set; }

    /// <summary>per_gram, percent (of metal value) or per_piece.</summary>
    public required string MakingType { get; set; }

    public decimal MakingValue { get; set; }

    public decimal StoneValue { get; set; }

    public decimal HallmarkCharge { get; set; }

    /// <summary>The 6-character BIS hallmark unique ID.</summary>
    public string? Huid { get; set; }

    /// <summary>Sold at a fixed price instead of by weight (silver articles, small items); null: by weight.</summary>
    public decimal? FixedPrice { get; set; }

    public Guid? SoldBillId { get; set; }

    public DateTimeOffset? SoldAt { get; set; }
}

/// <summary>A jewellery bill's own facts beside the ordinary bill: PAN, what was paid in money, and old metal taken.</summary>
public sealed class JewelleryBill : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BillId { get; set; }

    /// <summary>PAN, or "FORM 60" when the buyer has none: needed from ₹2 lakh.</summary>
    public string? Pan { get; set; }

    /// <summary>Money the customer handed over (cash, UPI, card…), not counting old metal.</summary>
    public decimal MoneyPaid { get; set; }

    public PaymentMode Mode { get; set; }

    public Guid? CustomerId { get; set; }

    public decimal OldMetalValue { get; set; }

    /// <summary>The kharcha entry (stock bought) that records paying for the old metal, so the cash book nets out.</summary>
    public Guid? OldMetalExpenseId { get; set; }
}

/// <summary>One jewellery line of a bill as it was worked out: every number the printed bill shows.</summary>
public sealed class JewelleryBillLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BillId { get; set; }

    public int LineNo { get; set; }

    public Guid? PieceId { get; set; }

    public int? TagNo { get; set; }

    public required string Name { get; set; }

    public required string Metal { get; set; }

    public int Purity { get; set; }

    public decimal Gross { get; set; }

    public decimal Stone { get; set; }

    public decimal Net { get; set; }

    public decimal RatePerGram { get; set; }

    public decimal MetalValue { get; set; }

    public decimal WastagePercent { get; set; }

    public decimal WastageValue { get; set; }

    public required string MakingType { get; set; }

    public decimal MakingValue { get; set; }

    public decimal MakingAmount { get; set; }

    public decimal StoneValue { get; set; }

    public decimal HallmarkCharge { get; set; }

    public string? Huid { get; set; }

    public bool FixedPrice { get; set; }

    public decimal Taxable { get; set; }

    public decimal Gst { get; set; }
}

/// <summary>Old gold or silver taken from a customer: weighed, tested and valued at the day's buying rate.</summary>
public sealed class OldMetalPurchase : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid? BillId { get; set; }

    public Guid? CustomerId { get; set; }

    public string? Description { get; set; }

    public required string Metal { get; set; }

    public decimal Gross { get; set; }

    /// <summary>The test reading in percent (87.5 for 21K).</summary>
    public decimal Reading { get; set; }

    /// <summary>Fine (pure) weight: gross × reading.</summary>
    public decimal Fine { get; set; }

    /// <summary>The pure-metal buying rate per gram used.</summary>
    public decimal RatePerGram { get; set; }

    public decimal DeductionPercent { get; set; }

    public decimal Value { get; set; }
}

public sealed record MetalRateDto(string Metal, int Purity, decimal SellPerGram, decimal? BuyPerGram, DateTimeOffset SetAt, bool Derived);
public sealed record MetalRatesDto(IReadOnlyList<MetalRateDto> Current, IReadOnlyList<MetalRateDto> Today);
public sealed record MetalRateRequest(string Metal, int Purity, decimal SellPerGram, decimal? BuyPerGram);
public sealed record MetalRatesRequest(IReadOnlyList<MetalRateRequest> Rates);

public sealed record JewelleryPieceRequest(
    string Name, string? Category, string Metal, int Purity, decimal Gross, decimal? Stone, decimal? WastagePercent, string? MakingType,
    decimal? MakingValue, decimal? StoneValue, decimal? HallmarkCharge, string? Huid, decimal? FixedPrice);

public sealed record JewelleryPieceDto(
    Guid Id, int TagNo, string Name, string? Category, string Metal, int Purity, decimal Gross, decimal Stone, decimal Net, decimal WastagePercent,
    string MakingType, decimal MakingValue, decimal StoneValue, decimal HallmarkCharge, string? Huid, decimal? FixedPrice, bool Sold, Guid? SoldBillId,
    DateTimeOffset CreatedAt, bool OnApproval = false);

public sealed record JewelleryLineRequest(
    Guid? PieceId, string? Name, string? Metal, int? Purity, decimal? Gross, decimal? Stone, decimal? WastagePercent, string? MakingType,
    decimal? MakingValue, decimal? StoneValue, decimal? HallmarkCharge, string? Huid, decimal? RatePerGram, decimal? FixedPrice);

public sealed record OldMetalRequest(string? Description, string Metal, decimal Gross, decimal Reading, decimal? DeductionPercent, decimal? RatePerGram);

public sealed record JewelleryQuoteRequest(Guid? CustomerId, IReadOnlyList<JewelleryLineRequest>? Lines, IReadOnlyList<OldMetalRequest>? OldMetal, Guid? SchemeMemberId = null);

public sealed record JewelleryBillRequest(
    Guid BillId, Guid? CustomerId, IReadOnlyList<JewelleryLineRequest>? Lines, IReadOnlyList<OldMetalRequest>? OldMetal,
    decimal MoneyPaid, PaymentMode? Mode, string? Pan, Guid? SchemeMemberId = null);

public sealed record JewelleryLineDto(
    int LineNo, Guid? PieceId, int? TagNo, string Name, string Metal, int Purity, decimal Gross, decimal Stone, decimal Net, decimal RatePerGram,
    decimal MetalValue, decimal WastagePercent, decimal WastageValue, string MakingType, decimal MakingValue, decimal MakingAmount, decimal StoneValue,
    decimal HallmarkCharge, string? Huid, bool FixedPrice, decimal Taxable, decimal Gst);

public sealed record OldMetalDto(string? Description, string Metal, decimal Gross, decimal Reading, decimal Fine, decimal RatePerGram, decimal DeductionPercent, decimal Deduction, decimal Value);

/// <summary>
/// A bill worked out. <see cref="Total"/> is rounded to the rupee; <see cref="ToPay"/> is what is left after old
/// metal (negative: the shop pays the customer). <see cref="PanNeeded"/> from ₹2 lakh; <see cref="CashLimit"/> is the
/// most cash the shop may take from this customer today (₹2 lakh less what they paid in cash today, less a rupee).
/// </summary>
public sealed record JewelleryQuoteDto(
    IReadOnlyList<JewelleryLineDto> Lines, decimal Taxable, decimal Gst, decimal RoundOff, decimal Total, IReadOnlyList<OldMetalDto> OldMetal,
    decimal OldMetalValue, decimal ToPay, bool PanNeeded, decimal CashLimit, IReadOnlyList<string> Problems, decimal SchemeDiscount = 0, decimal KhataAdvance = 0);

public sealed record JewelleryBillDto(Guid BillId, string? Pan, decimal MoneyPaid, PaymentMode Mode, decimal OldMetalValue, IReadOnlyList<JewelleryLineDto> Lines, IReadOnlyList<OldMetalDto> OldMetal);

/// <summary>The arithmetic, kept apart so it can be checked against worked bills.</summary>
public static class JewelleryMath
{
    public const decimal GstPercent = 3m;

    /// <summary>₹2 lakh: PAN (or Form 60) needed for a sale this big, and cash of this much from one person in a day is not allowed (s.269ST).</summary>
    public const decimal PanFrom = 200_000m;

    public const decimal CashLimit = 200_000m;

    public static readonly int[] GoldPurities = [999, 995, 916, 750, 585];

    public static readonly int[] SilverPurities = [999, 925];

    public static readonly string[] MakingTypes = ["per_gram", "percent", "per_piece"];

    public static bool ValidPurity(string metal, int purity) => metal switch
    {
        "gold" => GoldPurities.Contains(purity),
        "silver" => SilverPurities.Contains(purity),
        _ => false,
    };

    public static string Karat(string metal, int purity) => metal == "gold"
        ? purity switch { 999 => "24K", 995 => "24K", 916 => "22K", 750 => "18K", 585 => "14K", _ => $"{purity}" }
        : $"{purity}";

    /// <summary>A purity's rate from the pure (999) rate when the shop has not set it: 22K = 24K × 0.916.</summary>
    public static decimal Derive(decimal pureRate, int purity) => decimal.Round(pureRate * purity / 1000m, 2);

    public static decimal Grams(decimal value) => decimal.Round(value, 3);

    /// <summary>One line's numbers. A fixed-price piece is just its price (and hallmark).</summary>
    public static JewelleryLineDto Line(
        int lineNo, Guid? pieceId, int? tagNo, string name, string metal, int purity, decimal gross, decimal stone, decimal ratePerGram,
        decimal wastagePercent, string makingType, decimal makingValue, decimal stoneValue, decimal hallmark, string? huid, decimal? fixedPrice)
    {
        var net = Grams(gross - stone);
        decimal metalValue = 0, wastageValue = 0, making = 0;
        if (fixedPrice is null)
        {
            metalValue = decimal.Round(net * ratePerGram, 2);
            var wastageGrams = Grams(net * wastagePercent / 100m);
            wastageValue = decimal.Round(wastageGrams * ratePerGram, 2);
            making = makingType switch
            {
                "per_gram" => decimal.Round(net * makingValue, 2),
                "percent" => decimal.Round(metalValue * makingValue / 100m, 2),
                _ => decimal.Round(makingValue, 2),
            };
        }

        var taxable = fixedPrice is { } price ? decimal.Round(price + hallmark, 2) : metalValue + wastageValue + making + stoneValue + hallmark;
        var gst = decimal.Round(taxable * GstPercent / 100m, 2);
        return new JewelleryLineDto(lineNo, pieceId, tagNo, name, metal, purity, Grams(gross), Grams(stone), net, ratePerGram, metalValue, wastagePercent,
            wastageValue, makingType, makingValue, making, stoneValue, hallmark, huid, fixedPrice is not null, taxable, gst);
    }

    /// <summary>Old metal: fine weight × the pure buying rate, less the shop's deduction (melting loss, testing).</summary>
    public static OldMetalDto Old(string? description, string metal, decimal gross, decimal reading, decimal ratePerGram, decimal deductionPercent)
    {
        var fine = Grams(gross * reading / 100m);
        var value = decimal.Round(fine * ratePerGram, 2);
        var deduction = decimal.Round(value * deductionPercent / 100m, 2);
        return new OldMetalDto(description, metal, Grams(gross), reading, fine, ratePerGram, deductionPercent, deduction, value - deduction);
    }

    /// <summary>The bill's sums: total rounded to the nearest rupee.</summary>
    public static (decimal Taxable, decimal Gst, decimal RoundOff, decimal Total) Sum(IEnumerable<JewelleryLineDto> lines)
    {
        var list = lines.ToList();
        var taxable = list.Sum(l => l.Taxable);
        var gst = list.Sum(l => l.Gst);
        var exact = taxable + gst;
        var total = decimal.Round(exact, 0, MidpointRounding.AwayFromZero);
        return (taxable, gst, total - exact, total);
    }
}

public static class JewelleryEndpoints
{
    private static readonly Regex HuidPattern = new("^[A-Z0-9]{6}$", RegexOptions.Compiled);
    private static readonly Regex PanPattern = new("^([A-Z]{5}[0-9]{4}[A-Z]|FORM 60)$", RegexOptions.Compiled);

    public static IEndpointRouteBuilder MapJewellery(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/jewellery").WithTags("Jewellery").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/rates", RatesAsync);
        group.MapPost("/rates", SetRatesAsync);
        group.MapGet("/pieces", PiecesAsync);
        group.MapPost("/pieces", AddPieceAsync);
        group.MapPut("/pieces/{id:guid}", EditPieceAsync);
        group.MapDelete("/pieces/{id:guid}", RemovePieceAsync);
        group.MapPost("/quote", QuoteAsync);
        group.MapPost("/bills", BillAsync);
        group.MapGet("/bills/{billId:guid}", BillDetailAsync);
        return app;
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    // ---------- rates ----------

    /// <summary>The latest rate for every metal and purity (purities not set come from the pure rate), and today's changes.</summary>
    internal static async Task<MetalRatesDto> CurrentRatesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var all = await db.MetalRates.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync(ct);
        var latest = all.GroupBy(r => (r.Metal, r.Purity)).ToDictionary(g => g.Key, g => g.First());
        var current = new List<MetalRateDto>();
        foreach (var (metal, purities) in new[] { ("gold", JewelleryMath.GoldPurities), ("silver", JewelleryMath.SilverPurities) })
        {
            latest.TryGetValue((metal, 999), out var pure);
            foreach (var purity in purities)
            {
                if (latest.TryGetValue((metal, purity), out var set))
                {
                    current.Add(new MetalRateDto(metal, purity, set.SellPerGram, set.BuyPerGram, set.CreatedAt, false));
                }
                else if (pure is not null)
                {
                    current.Add(new MetalRateDto(metal, purity, JewelleryMath.Derive(pure.SellPerGram, purity),
                        pure.BuyPerGram is { } buy ? JewelleryMath.Derive(buy, purity) : null, pure.CreatedAt, true));
                }
            }
        }

        var dayStart = IndiaTime.Day(IndiaTime.Today(clock)).Start;
        var today = all.Where(r => r.CreatedAt >= dayStart).Select(r => new MetalRateDto(r.Metal, r.Purity, r.SellPerGram, r.BuyPerGram, r.CreatedAt, false)).ToList();
        return new MetalRatesDto(current, today);
    }

    private static async Task<IResult> RatesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await CurrentRatesAsync(db, clock, ct));

    private static async Task<IResult> SetRatesAsync(MetalRatesRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the owner sets the day's rates.");
        }

        if (request.Rates is not { Count: > 0 and <= 20 })
        {
            return Invalid("Rates", "Enter at least one rate.");
        }

        foreach (var r in request.Rates)
        {
            if (!JewelleryMath.ValidPurity(r.Metal, r.Purity))
            {
                return Invalid("Rates", $"{r.Metal} {r.Purity} is not a purity the app knows.");
            }

            if (r.SellPerGram <= 0 || r.SellPerGram > 1_000_000 || r.BuyPerGram is <= 0 or > 1_000_000)
            {
                return Invalid("Rates", "Enter rates per gram above zero.");
            }

            db.MetalRates.Add(new MetalRate
            {
                Metal = r.Metal, Purity = r.Purity, SellPerGram = decimal.Round(r.SellPerGram, 2),
                BuyPerGram = r.BuyPerGram is { } buy ? decimal.Round(buy, 2) : null, SetByUserId = user.UserId()!.Value,
            });
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await CurrentRatesAsync(db, clock, ct));
    }

    // ---------- pieces ----------

    private static JewelleryPieceDto ToDto(JewelleryPiece p) => new(
        p.Id, p.TagNo, p.Name, p.Category, p.Metal, p.Purity, p.Gross, p.Stone, JewelleryMath.Grams(p.Gross - p.Stone), p.WastagePercent, p.MakingType,
        p.MakingValue, p.StoneValue, p.HallmarkCharge, p.Huid, p.FixedPrice, p.SoldBillId is not null, p.SoldBillId, p.CreatedAt);

    private static async Task<IResult> PiecesAsync(string? status, string? q, AppDbContext db, CancellationToken ct)
    {
        var query = db.JewelleryPieces.AsNoTracking();
        query = status switch
        {
            "sold" => query.Where(p => p.SoldBillId != null),
            "all" => query,
            _ => query.Where(p => p.SoldBillId == null),
        };
        var text = q?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            // A tag number (typed, or read from the tag's QR: "GKJ:12"), else part of the name or HUID.
            var tagText = text.StartsWith("GKJ:", StringComparison.OrdinalIgnoreCase) ? text[4..] : text.TrimStart('J', 'j', '-');
            if (int.TryParse(tagText, out var tag))
            {
                query = query.Where(p => p.TagNo == tag);
            }
            else
            {
                var like = $"%{text.ToLowerInvariant()}%";
                query = query.Where(p => EF.Functions.Like(p.Name.ToLower(), like) || (p.Huid != null && EF.Functions.Like(p.Huid.ToLower(), like)) || (p.Category != null && EF.Functions.Like(p.Category.ToLower(), like)));
            }
        }

        var list = await query.OrderByDescending(p => p.TagNo).Take(500).ToListAsync(ct);
        var ids = list.Select(p => p.Id).ToList();
        var out_ = (await db.ApprovalItems.AsNoTracking().Where(i => ids.Contains(i.PieceId) && i.ReturnedAt == null && i.SoldBillId == null).Select(i => i.PieceId).ToListAsync(ct)).ToHashSet();
        return TypedResults.Ok(list.Select(p => ToDto(p) with { OnApproval = out_.Contains(p.Id) }));
    }

    private static string? CheckPiece(JewelleryPieceRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 80) return "Name the piece (up to 80 letters).";
        if (!JewelleryMath.ValidPurity(r.Metal, r.Purity)) return "Choose gold or silver and its purity.";
        if (r.Gross <= 0 || r.Gross > 10_000) return "Enter the gross weight in grams.";
        if (r.Stone is < 0 || (r.Stone ?? 0) >= r.Gross) return "Stone weight must be less than the gross weight.";
        if (r.WastagePercent is < 0 or > 50) return "Wastage is a percentage from 0 to 50.";
        if (r.MakingType is not null && !JewelleryMath.MakingTypes.Contains(r.MakingType)) return "Making is per gram, a percent, or per piece.";
        if (r.MakingValue is < 0 or > 10_000_000 || r.StoneValue is < 0 or > 100_000_000 || r.HallmarkCharge is < 0 or > 10_000 || r.FixedPrice is <= 0 or > 100_000_000) return "Enter amounts of zero or more.";
        if (!string.IsNullOrWhiteSpace(r.Huid) && !HuidPattern.IsMatch(r.Huid.Trim().ToUpperInvariant())) return "HUID is 6 letters and numbers.";
        return null;
    }

    private static void Apply(JewelleryPiece p, JewelleryPieceRequest r)
    {
        p.Name = r.Name.Trim();
        p.Category = string.IsNullOrWhiteSpace(r.Category) ? null : r.Category.Trim();
        p.Metal = r.Metal;
        p.Purity = r.Purity;
        p.Gross = JewelleryMath.Grams(r.Gross);
        p.Stone = JewelleryMath.Grams(r.Stone ?? 0);
        p.WastagePercent = decimal.Round(r.WastagePercent ?? 0, 2);
        p.MakingType = r.MakingType ?? "per_gram";
        p.MakingValue = decimal.Round(r.MakingValue ?? 0, 2);
        p.StoneValue = decimal.Round(r.StoneValue ?? 0, 2);
        p.HallmarkCharge = decimal.Round(r.HallmarkCharge ?? 0, 2);
        p.Huid = string.IsNullOrWhiteSpace(r.Huid) ? null : r.Huid.Trim().ToUpperInvariant();
        p.FixedPrice = r.FixedPrice is { } f ? decimal.Round(f, 2) : null;
    }

    private static async Task<IResult> AddPieceAsync(JewelleryPieceRequest request, AppDbContext db, CancellationToken ct)
    {
        if (CheckPiece(request) is { } problem) return Invalid("Piece", problem);
        if (!string.IsNullOrWhiteSpace(request.Huid))
        {
            var huid = request.Huid.Trim().ToUpperInvariant();
            if (await db.JewelleryPieces.AnyAsync(p => p.Huid == huid, ct)) return Invalid("Huid", $"HUID {huid} is already on another piece.");
        }

        var next = (await db.JewelleryPieces.MaxAsync(p => (int?)p.TagNo, ct) ?? 0) + 1;
        var piece = new JewelleryPiece { Name = "", Metal = request.Metal, MakingType = "per_gram", TagNo = next };
        Apply(piece, request);
        db.JewelleryPieces.Add(piece);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(piece));
    }

    private static async Task<IResult> EditPieceAsync(Guid id, JewelleryPieceRequest request, AppDbContext db, CancellationToken ct)
    {
        if (await db.JewelleryPieces.SingleOrDefaultAsync(p => p.Id == id, ct) is not { } piece) return TypedResults.NotFound();
        if (piece.SoldBillId is not null) return Invalid("Piece", "This piece is sold: its bill keeps what it was sold as.");
        if (CheckPiece(request) is { } problem) return Invalid("Piece", problem);
        var huid = request.Huid?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(huid) && await db.JewelleryPieces.AnyAsync(p => p.Huid == huid && p.Id != id, ct)) return Invalid("Huid", $"HUID {huid} is already on another piece.");
        Apply(piece, request);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(piece));
    }

    private static async Task<IResult> RemovePieceAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the owner removes a piece.");
        if (await db.JewelleryPieces.SingleOrDefaultAsync(p => p.Id == id, ct) is not { } piece) return TypedResults.NotFound();
        if (piece.SoldBillId is not null) return Invalid("Piece", "A sold piece stays with its bill.");
        db.JewelleryPieces.Remove(piece);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // ---------- quote and bill ----------

    private sealed record Worked(JewelleryQuoteDto Quote, List<JewelleryPiece> Pieces);

    /// <summary>Works out a bill from tags (or typed lines) and today's rates. Problems are listed, not thrown.</summary>
    private static async Task<(Worked? Worked, string? Problem)> WorkAsync(
        Guid? customerId, IReadOnlyList<JewelleryLineRequest>? lines, IReadOnlyList<OldMetalRequest>? oldMetal, AppDbContext db, TimeProvider clock, CancellationToken ct, Guid? schemeMemberId = null)
    {
        if (lines is not { Count: > 0 and <= 50 }) return (null, "Add between 1 and 50 pieces.");
        if (oldMetal is { Count: > 20 }) return (null, "Up to 20 old pieces on one bill.");
        var rates = (await CurrentRatesAsync(db, clock, ct)).Current.ToDictionary(r => (r.Metal, r.Purity));
        var pieceIds = lines.Where(l => l.PieceId is not null).Select(l => l.PieceId!.Value).Distinct().ToList();
        var pieces = await db.JewelleryPieces.Where(p => pieceIds.Contains(p.Id)).ToListAsync(ct);
        var problems = new List<string>();
        var worked = new List<JewelleryLineDto>();
        var n = 0;
        foreach (var l in lines)
        {
            n++;
            string name, metal, makingType;
            int purity;
            decimal gross, stone, wastage, making, stoneValue, hallmark;
            decimal? fixedPrice;
            string? huid;
            int? tag = null;
            if (l.PieceId is { } pid)
            {
                if (pieces.FirstOrDefault(p => p.Id == pid) is not { } p) return (null, "A piece on this bill is no longer in stock.");
                if (p.SoldBillId is not null) return (null, $"Tag J-{p.TagNo} is already sold.");
                (name, metal, purity, gross, stone, tag, huid) = (p.Name, p.Metal, p.Purity, p.Gross, p.Stone, p.TagNo, p.Huid);
                wastage = l.WastagePercent ?? p.WastagePercent;
                makingType = l.MakingType ?? p.MakingType;
                making = l.MakingValue ?? p.MakingValue;
                stoneValue = l.StoneValue ?? p.StoneValue;
                hallmark = l.HallmarkCharge ?? p.HallmarkCharge;
                fixedPrice = l.FixedPrice ?? p.FixedPrice;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(l.Name) || l.Metal is null || l.Purity is null || l.Gross is not > 0) return (null, "Each typed line needs a name, metal, purity and gross weight.");
                (name, metal, purity, gross, stone) = (l.Name.Trim(), l.Metal, l.Purity.Value, l.Gross.Value, l.Stone ?? 0);
                (wastage, makingType, making, stoneValue, hallmark, fixedPrice) = (l.WastagePercent ?? 0, l.MakingType ?? "per_gram", l.MakingValue ?? 0, l.StoneValue ?? 0, l.HallmarkCharge ?? 0, l.FixedPrice);
                huid = string.IsNullOrWhiteSpace(l.Huid) ? null : l.Huid.Trim().ToUpperInvariant();
                if (!JewelleryMath.ValidPurity(metal, purity)) return (null, $"{name}: choose gold or silver and its purity.");
                if (stone < 0 || stone >= gross) return (null, $"{name}: stone weight must be less than gross.");
            }

            if (!JewelleryMath.MakingTypes.Contains(makingType)) return (null, $"{name}: making is per gram, a percent, or per piece.");
            if (wastage is < 0 or > 50 || making < 0 || stoneValue < 0 || hallmark < 0) return (null, $"{name}: amounts must be zero or more.");
            var rate = l.RatePerGram ?? (rates.TryGetValue((metal, purity), out var r) ? r.SellPerGram : 0);
            if (fixedPrice is null && rate <= 0) return (null, $"Set today's {metal} rate first ({JewelleryMath.Karat(metal, purity)}).");
            if (huid is null && metal == "gold" && fixedPrice is null) problems.Add($"{name}: no HUID. Gold jewellery must carry a BIS hallmark with HUID (pieces under 2 g and kundan/polki are exempt).");
            worked.Add(JewelleryMath.Line(n, l.PieceId, tag, name, metal, purity, gross, stone, decimal.Round(rate, 2), wastage, makingType, making, stoneValue, hallmark, huid, fixedPrice));
        }

        var olds = new List<OldMetalDto>();
        foreach (var o in oldMetal ?? [])
        {
            if (o.Metal is not ("gold" or "silver") || o.Gross is <= 0 or > 10_000 || o.Reading is <= 0 or > 100 || o.DeductionPercent is < 0 or > 30)
            {
                return (null, "Old metal needs gold or silver, its weight, the test reading (1–100%) and a deduction of 0–30%.");
            }

            var pure = rates.TryGetValue((o.Metal, 999), out var pr) ? pr.BuyPerGram ?? pr.SellPerGram : 0;
            var rate = o.RatePerGram ?? pure;
            if (rate <= 0) return (null, $"Set today's pure {o.Metal} buying rate first.");
            olds.Add(JewelleryMath.Old(string.IsNullOrWhiteSpace(o.Description) ? null : o.Description.Trim(), o.Metal, o.Gross, o.Reading, decimal.Round(rate, 2), o.DeductionPercent ?? 0));
        }

        // A finished savings scheme: its bonus comes off the making charges, line by line.
        var schemeDiscount = 0m;
        if (schemeMemberId is { } sid)
        {
            var member = (await JewelleryBookEndpoints.MemberViewsAsync(db, q => q.Where(m => m.Id == sid), ct)).SingleOrDefault();
            if (member is null || member.CustomerId != customerId) return (null, "That scheme belongs to another customer: choose them on the bill.");
            if (member.RedeemedBillId is not null || member.ClosedOn is not null) return (null, "This scheme was already used.");
            if (!member.Complete) return (null, $"The scheme has {member.Paid} of {member.Months} instalments paid: its bonus comes once all are paid.");
            var left = member.Bonus;
            for (var i = 0; i < worked.Count && left > 0; i++)
            {
                var l = worked[i];
                var off = Math.Min(left, l.MakingAmount);
                if (off <= 0) continue;
                var taxableNow = l.Taxable - off;
                worked[i] = l with { MakingAmount = l.MakingAmount - off, Taxable = taxableNow, Gst = decimal.Round(taxableNow * JewelleryMath.GstPercent / 100m, 2) };
                left -= off;
                schemeDiscount += off;
            }
        }

        var (taxable, gst, roundOff, total) = JewelleryMath.Sum(worked);
        var oldValue = olds.Sum(o => o.Value);
        // Cash already taken from this customer today on jewellery bills counts towards the ₹2 lakh a day.
        var cashToday = 0m;
        if (customerId is { } cid)
        {
            var dayStart = IndiaTime.Day(IndiaTime.Today(clock)).Start;
            cashToday = await db.JewelleryBills.Where(b => b.CustomerId == cid && b.Mode == PaymentMode.Cash && b.CreatedAt >= dayStart).SumAsync(b => (decimal?)b.MoneyPaid, ct) ?? 0;
        }

        var cashLimit = Math.Max(0, JewelleryMath.CashLimit - 1 - cashToday);
        // Money already on the customer's khata as an advance (scheme instalments, order advances) pays towards this bill.
        var advance = customerId is { } who ? Math.Max(0, -(await db.Customers.AsNoTracking().Where(c => c.Id == who).Select(c => (decimal?)c.Balance).SingleOrDefaultAsync(ct) ?? 0)) : 0;
        var quote = new JewelleryQuoteDto(worked, taxable, gst, roundOff, total, olds, oldValue, total - oldValue, total >= JewelleryMath.PanFrom, cashLimit, problems, schemeDiscount, advance);
        return (new Worked(quote, pieces), null);
    }

    private static async Task<IResult> QuoteAsync(JewelleryQuoteRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var (worked, problem) = await WorkAsync(request.CustomerId, request.Lines, request.OldMetal, db, clock, ct, request.SchemeMemberId);
        return worked is null ? Invalid("Lines", problem!) : TypedResults.Ok(worked.Quote);
    }

    /// <summary>
    /// Saves a jewellery bill: an ordinary GST bill (3%, HSN 7113) with one line per piece, plus the weight breakdown,
    /// the pieces marked sold, and old metal taken in. Old metal pays part of the bill: it is recorded as stock bought
    /// (kharcha "Purchase", kept out of profit) in the same payment mode, so the cash book nets to the money that changed hands.
    /// </summary>
    private static async Task<IResult> BillAsync(JewelleryBillRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.JewelleryBills.AsNoTracking().AnyAsync(b => b.BillId == request.BillId, ct)) return await BillDetailAsync(request.BillId, db, ct);
        var mode = request.Mode ?? PaymentMode.Cash;
        if (mode is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Card)) return Invalid("Mode", "Take cash, UPI or card.");
        if (request.MoneyPaid < 0 || request.MoneyPaid > Money.MaxAmount || decimal.Round(request.MoneyPaid, 2) != request.MoneyPaid) return Invalid("MoneyPaid", "Enter the money received.");

        var (worked, problem) = await WorkAsync(request.CustomerId, request.Lines, request.OldMetal, db, clock, ct, request.SchemeMemberId);
        if (worked is null) return Invalid("Lines", problem!);
        var q = worked.Quote;

        var pan = request.Pan?.Trim().ToUpperInvariant();
        if (pan is { Length: > 0 } && !PanPattern.IsMatch(pan)) return Invalid("Pan", "Enter a PAN like ABCDE1234F, or FORM 60.");
        if (q.PanNeeded && string.IsNullOrEmpty(pan)) return Invalid("Pan", "This bill is ₹2 lakh or more: enter the buyer's PAN (or FORM 60).");
        if (mode == PaymentMode.Cash && request.MoneyPaid > q.CashLimit)
        {
            return Invalid("MoneyPaid", $"Cash of ₹2 lakh or more from one person in a day is not allowed. Take at most ₹{q.CashLimit:N0} in cash today; the rest by UPI or card.");
        }

        // Old metal covers part of the bill; more old metal than the bill means the shop pays the difference.
        var covered = Math.Min(q.Total, request.MoneyPaid + q.OldMetalValue);
        if (q.OldMetalValue > q.Total && request.MoneyPaid > 0) return Invalid("MoneyPaid", "The old metal is worth more than the bill: the shop pays the customer the difference.");
        if (request.CustomerId is null && covered < q.Total) return Invalid("MoneyPaid", "Choose the customer to leave part of the bill on their khata.");

        // The bill: one line per piece at its GST-inclusive value; the rupee round-off rides on the last line.
        var billLines = q.Lines.Select((l, i) => new BillLineRequest(null, $"{l.Name} · {(l.TagNo is { } tg ? $"J-{tg} · " : "")}{JewelleryMath.Karat(l.Metal, l.Purity)} · {l.Net:0.000} g", 1, Unit.Piece,
            l.Taxable + l.Gst + (i == q.Lines.Count - 1 ? q.RoundOff : 0))).ToList();
        var taxSnapshot = q.Lines.Select(_ => ((decimal?)JewelleryMath.GstPercent, (string?)"7113")).ToList();
        var result = await ledger.CreateBillAsync(new BillRequest(request.BillId, request.CustomerId, billLines, covered, mode, null), user.UserId()!.Value, ct,
            isOwner: LedgerEndpoints.IsOwner(user), taxSnapshot: taxSnapshot);
        if (result.Value is not { } bill) return result.ToHttp("");

        Guid? expenseId = null;
        if (q.OldMetalValue > 0)
        {
            var note = $"Old metal taken on a bill: {string.Join(", ", q.OldMetal.Select(o => $"{o.Gross:0.000} g {o.Metal} at {o.Reading:0.##}%"))}";
            var expense = new Expense { Amount = q.OldMetalValue, Category = ExpenseCategory.Purchase, Mode = mode, Note = note.Length > 200 ? note[..200] : note, CreatedByUserId = user.UserId()!.Value, OccurredAt = clock.GetUtcNow() };
            db.Expenses.Add(expense);
            expenseId = expense.Id;
        }

        db.JewelleryBills.Add(new JewelleryBill { BillId = bill.Id, Pan = string.IsNullOrEmpty(pan) ? null : pan, MoneyPaid = request.MoneyPaid, Mode = mode, CustomerId = request.CustomerId, OldMetalValue = q.OldMetalValue, OldMetalExpenseId = expenseId });
        foreach (var l in q.Lines)
        {
            db.JewelleryBillLines.Add(new JewelleryBillLine
            {
                BillId = bill.Id, LineNo = l.LineNo, PieceId = l.PieceId, TagNo = l.TagNo, Name = l.Name, Metal = l.Metal, Purity = l.Purity, Gross = l.Gross, Stone = l.Stone, Net = l.Net,
                RatePerGram = l.RatePerGram, MetalValue = l.MetalValue, WastagePercent = l.WastagePercent, WastageValue = l.WastageValue, MakingType = l.MakingType, MakingValue = l.MakingValue,
                MakingAmount = l.MakingAmount, StoneValue = l.StoneValue, HallmarkCharge = l.HallmarkCharge, Huid = l.Huid, FixedPrice = l.FixedPrice, Taxable = l.Taxable, Gst = l.Gst,
            });
        }

        foreach (var o in q.OldMetal)
        {
            db.OldMetalPurchases.Add(new OldMetalPurchase { BillId = bill.Id, CustomerId = request.CustomerId, Description = o.Description, Metal = o.Metal, Gross = o.Gross, Reading = o.Reading, Fine = o.Fine, RatePerGram = o.RatePerGram, DeductionPercent = o.DeductionPercent, Value = o.Value });
        }

        foreach (var p in worked.Pieces)
        {
            (p.SoldBillId, p.SoldAt) = (bill.Id, clock.GetUtcNow());
        }

        if (request.SchemeMemberId is { } sid && await db.SchemeMembers.SingleOrDefaultAsync(m => m.Id == sid, ct) is { } member)
        {
            (member.RedeemedBillId, member.ClosedOn) = (bill.Id, IndiaTime.Today(clock));
        }

        // Pieces that were out on approval and are now bought: their memo line is sold.
        var pieceIds = worked.Pieces.Select(p => p.Id).ToList();
        var outItems = await db.ApprovalItems.Where(i => pieceIds.Contains(i.PieceId) && i.ReturnedAt == null && i.SoldBillId == null).ToListAsync(ct);
        foreach (var i in outItems) i.SoldBillId = bill.Id;
        await db.SaveChangesAsync(ct);
        foreach (var memoId in outItems.Select(i => i.MemoId).Distinct())
        {
            await JewelleryBookEndpoints.CloseIfDoneAsync(db, await db.ApprovalMemos.SingleAsync(m => m.Id == memoId, ct), clock, ct);
        }

        await db.SaveChangesAsync(ct);
        return await BillDetailAsync(bill.Id, db, ct);
    }

    private static async Task<IResult> BillDetailAsync(Guid billId, AppDbContext db, CancellationToken ct)
    {
        if (await db.JewelleryBills.AsNoTracking().SingleOrDefaultAsync(b => b.BillId == billId, ct) is not { } jb) return TypedResults.NotFound();
        var lines = await db.JewelleryBillLines.AsNoTracking().Where(l => l.BillId == billId).OrderBy(l => l.LineNo).ToListAsync(ct);
        var olds = await db.OldMetalPurchases.AsNoTracking().Where(o => o.BillId == billId).OrderBy(o => o.CreatedAt).ToListAsync(ct);
        return TypedResults.Ok(new JewelleryBillDto(billId, jb.Pan, jb.MoneyPaid, jb.Mode, jb.OldMetalValue,
            lines.Select(l => new JewelleryLineDto(l.LineNo, l.PieceId, l.TagNo, l.Name, l.Metal, l.Purity, l.Gross, l.Stone, l.Net, l.RatePerGram, l.MetalValue, l.WastagePercent,
                l.WastageValue, l.MakingType, l.MakingValue, l.MakingAmount, l.StoneValue, l.HallmarkCharge, l.Huid, l.FixedPrice, l.Taxable, l.Gst)).ToList(),
            olds.Select(o => new OldMetalDto(o.Description, o.Metal, o.Gross, o.Reading, o.Fine, o.RatePerGram, o.DeductionPercent, decimal.Round(o.Fine * o.RatePerGram, 2) - o.Value, o.Value)).ToList()));
    }
}
