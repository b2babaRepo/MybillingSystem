using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

// Phase 3 of the jewellery counter: the metal side of the shop. Karigars (job workers) and bullion traders each have a
// running account kept in fine (pure) grams beside any rupees; the shop's own loose metal (old gold, scrap, bullion) is
// kept the same way. Stones are written on each tagged piece, trays are counted against what the stock says should be
// in them, and the shop's scales are listed with their stamping date.

/// <summary>A karigar (job worker) or a bullion trader the shop gives metal to and gets metal from.</summary>
public sealed class MetalParty : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public string? Phone { get; set; }

    /// <summary>karigar or trader.</summary>
    public required string Kind { get; set; }

    /// <summary>Wastage allowed to a karigar on finished work, in percent.</summary>
    public decimal AllowancePercent { get; set; }

    /// <summary>True: the allowance is a percent of the fine weight returned; false: of the gross weight returned.</summary>
    public bool AllowanceOnFine { get; set; }

    /// <summary>Labour (majuri) per gram of finished work.</summary>
    public decimal LabourPerGram { get; set; }

    /// <summary>A karigar with a GSTIN bills the shop 5% GST; one without: the shop pays it itself (reverse charge).</summary>
    public bool GstRegistered { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One movement of metal or money on a metal account. <see cref="BalanceEffect"/> is what it does to the party's fine
/// balance (positive: the party holds the shop's metal); <see cref="LooseEffect"/> to the shop's own loose metal;
/// <see cref="CashEffect"/> to the drawer.
/// </summary>
public sealed class MetalMove : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid? PartyId { get; set; }

    /// <summary>issue, receive, rate_cut, labour_paid or opening.</summary>
    public required string Kind { get; set; }

    public required string Metal { get; set; }

    public decimal Gross { get; set; }

    public decimal Reading { get; set; }

    public decimal Fine { get; set; }

    /// <summary>Finished ornaments from a karigar (they go to tagging, not to loose metal).</summary>
    public bool Finished { get; set; }

    /// <summary>Fine grams allowed to the karigar as wastage on this return.</summary>
    public decimal Allowance { get; set; }

    /// <summary>Labour owed to the karigar for this return.</summary>
    public decimal Labour { get; set; }

    public decimal? RatePerGram { get; set; }

    public decimal CashEffect { get; set; }

    public PaymentMode? Mode { get; set; }

    public decimal BalanceEffect { get; set; }

    public decimal LooseEffect { get; set; }

    public string? Note { get; set; }

    /// <summary>Metal sent for job work must come back within a year (GST s.143).</summary>
    public DateOnly? DueBack { get; set; }

    public DateTimeOffset At { get; set; }

    public Guid ByUserId { get; set; }
}

/// <summary>A diamond or other stone set in a tagged piece.</summary>
public sealed class PieceStone : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid PieceId { get; set; }

    public required string Kind { get; set; }

    public bool LabGrown { get; set; }

    public int Count { get; set; }

    public decimal Carat { get; set; }

    public string? Shape { get; set; }

    public string? Colour { get; set; }

    public string? Clarity { get; set; }

    public string? CertLab { get; set; }

    public string? CertNo { get; set; }

    public decimal RatePerCarat { get; set; }

    public decimal Value { get; set; }
}

/// <summary>A tray (or counter) counted at opening or closing: pieces found and their total weight against the stock.</summary>
public sealed class TrayCount : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Tray { get; set; }

    /// <summary>opening or closing.</summary>
    public required string Kind { get; set; }

    public int ExpectedPieces { get; set; }

    public decimal ExpectedGross { get; set; }

    public int CountedPieces { get; set; }

    public decimal? CountedGross { get; set; }

    /// <summary>Tag numbers that should be in the tray but were not found, comma separated.</summary>
    public string? Missing { get; set; }

    /// <summary>Tags found that belong elsewhere (another tray, sold, or not in stock).</summary>
    public string? Extra { get; set; }

    public Guid ByUserId { get; set; }
}

/// <summary>A weighing scale at the counter, with its legal-metrology stamping date.</summary>
public sealed class WeighingScale : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>The verification interval in grams (0.001 or 0.01): weights are billed to this.</summary>
    public decimal ReadsTo { get; set; }

    public DateOnly? StampedUntil { get; set; }

    public int Baud { get; set; } = 9600;

    public bool IsActive { get; set; } = true;
}

public sealed record MetalPartyRequest(string Name, string? Phone, string Kind, decimal? AllowancePercent, bool? AllowanceOnFine, decimal? LabourPerGram, bool? GstRegistered, bool? IsActive);
public sealed record MetalPartyDto(
    Guid Id, string Name, string? Phone, string Kind, decimal AllowancePercent, bool AllowanceOnFine, decimal LabourPerGram, bool GstRegistered, bool IsActive,
    decimal Gold, decimal Silver, decimal LabourDue, decimal ReverseChargeGst, bool YearPassed, DateOnly? OldestDue);
public sealed record MetalMoveRequest(
    Guid? PartyId, string Kind, string Metal, decimal? Gross, decimal? Reading, bool? Finished, decimal? Fine, decimal? RatePerGram, bool? TheyPay,
    decimal? Amount, PaymentMode? Mode, string? Note);
public sealed record MetalMoveDto(
    Guid Id, int Number, Guid? PartyId, string? Party, string Kind, string Metal, decimal Gross, decimal Reading, decimal Fine, bool Finished, decimal Allowance,
    decimal Labour, decimal? RatePerGram, decimal CashEffect, PaymentMode? Mode, decimal BalanceEffect, decimal LooseEffect, decimal BalanceAfter, string? Note,
    DateOnly? DueBack, DateTimeOffset At);
public sealed record LooseMetalDto(string Metal, decimal Opening, decimal OldBought, decimal FromParties, decimal ToParties, decimal Fine);
public sealed record StoneRequest(string Kind, bool? LabGrown, int? Count, decimal? Carat, string? Shape, string? Colour, string? Clarity, string? CertLab, string? CertNo, decimal? RatePerCarat, decimal? Value);
public sealed record StoneDto(string Kind, bool LabGrown, int Count, decimal Carat, string? Shape, string? Colour, string? Clarity, string? CertLab, string? CertNo, decimal RatePerCarat, decimal Value);
public sealed record TrayDto(string Tray, int Pieces, decimal Gross, DateTimeOffset? LastCountAt, string? LastCountKind, bool LastCountClean);
public sealed record TrayCountRequest(string Tray, string Kind, IReadOnlyList<string> Tags, decimal? CountedGross);
public sealed record TrayCountDto(
    Guid Id, string Tray, string Kind, int ExpectedPieces, decimal ExpectedGross, int CountedPieces, decimal? CountedGross, decimal? GrossDifference,
    IReadOnlyList<string> Missing, IReadOnlyList<string> Extra, bool Clean, DateTimeOffset At);
public sealed record ScaleRequest(string Name, decimal ReadsTo, string? StampedUntil, int? Baud, bool? IsActive);
public sealed record ScaleDto(Guid Id, string Name, decimal ReadsTo, DateOnly? StampedUntil, int Baud, bool IsActive, int DaysLeft);

public static class JewelleryWorkshopEndpoints
{
    /// <summary>Fine grams within this of zero count as settled.</summary>
    private const decimal Settled = 0.0005m;

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static IResult OwnerOnly(string what) => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: $"Only the owner {what}.");

    private static string? Clip(string? text, int max) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(max, text.Trim().Length)];

    public static IEndpointRouteBuilder MapJewelleryWorkshop(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/jewellery").WithTags("Jewellery").RequireAuthorization(AppPolicies.ShopMember);
        g.MapGet("/parties", PartiesAsync);
        g.MapPost("/parties", AddPartyAsync);
        g.MapPut("/parties/{id:guid}", EditPartyAsync);
        g.MapGet("/parties/{id:guid}/moves", PartyMovesAsync);
        g.MapPost("/metal-moves", AddMoveAsync);
        g.MapGet("/loose-metal", LooseAsync);
        g.MapGet("/trays", TraysAsync);
        g.MapGet("/tray-counts", TrayCountsAsync);
        g.MapPost("/tray-counts", CountTrayAsync);
        g.MapGet("/scales", ScalesAsync);
        g.MapPost("/scales", AddScaleAsync);
        g.MapPut("/scales/{id:guid}", EditScaleAsync);
        return app;
    }

    // ---------------- karigars and traders ----------------

    private static async Task<List<MetalPartyDto>> PartyViewsAsync(AppDbContext db, TimeProvider clock, Func<IQueryable<MetalParty>, IQueryable<MetalParty>> filter, CancellationToken ct)
    {
        var parties = await filter(db.MetalParties.AsNoTracking()).OrderBy(p => p.Name).ToListAsync(ct);
        var ids = parties.Select(p => p.Id).ToList();
        var moves = (await db.MetalMoves.AsNoTracking().Where(m => m.PartyId != null && ids.Contains(m.PartyId.Value)).ToListAsync(ct)).ToLookup(m => m.PartyId!.Value);
        var today = IndiaTime.Today(clock);
        return parties.Select(p =>
        {
            var ms = moves[p.Id].ToList();
            var gold = ms.Where(m => m.Metal == "gold").Sum(m => m.BalanceEffect);
            var silver = ms.Where(m => m.Metal == "silver").Sum(m => m.BalanceEffect);
            var labour = ms.Sum(m => m.Labour) - ms.Where(m => m.Kind == "labour_paid").Sum(m => -m.CashEffect);
            var rcm = p.Kind == "karigar" && !p.GstRegistered ? decimal.Round(ms.Sum(m => m.Labour) * 0.05m, 0) : 0;
            // The oldest issue not yet covered by what came back (first in, first out): its one-year date.
            DateOnly? oldest = null;
            foreach (var metal in new[] { "gold", "silver" })
            {
                var balance = ms.Where(m => m.Metal == metal).Sum(m => m.BalanceEffect);
                if (balance <= Settled) continue;
                var left = balance;
                foreach (var issue in ms.Where(m => m.Metal == metal && m.Kind == "issue").OrderByDescending(m => m.At))
                {
                    if (issue.DueBack is { } due && (oldest is null || due < oldest)) oldest = due;
                    left -= issue.BalanceEffect;
                    if (left <= Settled) break;
                }
            }

            return new MetalPartyDto(p.Id, p.Name, p.Phone, p.Kind, p.AllowancePercent, p.AllowanceOnFine, p.LabourPerGram, p.GstRegistered, p.IsActive,
                JewelleryMath.Grams(gold), JewelleryMath.Grams(silver), decimal.Round(labour, 2), rcm, oldest is { } o && o < today, oldest);
        }).ToList();
    }

    private static async Task<IResult> PartiesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct) => TypedResults.Ok(await PartyViewsAsync(db, clock, q => q, ct));

    private static string? CheckParty(MetalPartyRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 80) return "Write the name.";
        if (r.Kind is not ("karigar" or "trader")) return "Choose karigar or trader.";
        if (r.AllowancePercent is < 0 or > 30) return "Wastage allowed is a percent from 0 to 30.";
        if (r.LabourPerGram is < 0 or > 100_000) return "Labour per gram must be zero or more.";
        return null;
    }

    private static void Apply(MetalParty p, MetalPartyRequest r)
    {
        p.Name = r.Name.Trim();
        p.Phone = Clip(r.Phone, 20);
        p.Kind = r.Kind;
        p.AllowancePercent = decimal.Round(r.AllowancePercent ?? 0, 2);
        p.AllowanceOnFine = r.AllowanceOnFine ?? false;
        p.LabourPerGram = decimal.Round(r.LabourPerGram ?? 0, 2);
        p.GstRegistered = r.GstRegistered ?? false;
        p.IsActive = r.IsActive ?? true;
    }

    private static async Task<IResult> AddPartyAsync(MetalPartyRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return OwnerOnly("adds a karigar or trader");
        if (CheckParty(r) is { } problem) return Invalid("Party", problem);
        var party = new MetalParty { Name = "", Kind = r.Kind };
        Apply(party, r);
        db.MetalParties.Add(party);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await PartyViewsAsync(db, clock, q => q.Where(p => p.Id == party.Id), ct)).Single());
    }

    private static async Task<IResult> EditPartyAsync(Guid id, MetalPartyRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return OwnerOnly("changes a karigar or trader");
        if (await db.MetalParties.SingleOrDefaultAsync(p => p.Id == id, ct) is not { } party) return TypedResults.NotFound();
        if (CheckParty(r) is { } problem) return Invalid("Party", problem);
        Apply(party, r);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await PartyViewsAsync(db, clock, q => q.Where(p => p.Id == id), ct)).Single());
    }

    private static List<MetalMoveDto> WithBalances(IEnumerable<MetalMove> moves, Func<MetalMove, decimal> effect, IReadOnlyDictionary<Guid, string> names)
    {
        var running = new Dictionary<string, decimal>();
        return moves.OrderBy(m => m.At).ThenBy(m => m.Number).Select(m =>
        {
            running[m.Metal] = running.GetValueOrDefault(m.Metal) + effect(m);
            return new MetalMoveDto(m.Id, m.Number, m.PartyId, m.PartyId is { } pid ? names.GetValueOrDefault(pid) : null, m.Kind, m.Metal, m.Gross, m.Reading, m.Fine,
                m.Finished, m.Allowance, m.Labour, m.RatePerGram, m.CashEffect, m.Mode, m.BalanceEffect, m.LooseEffect, JewelleryMath.Grams(running[m.Metal]), m.Note, m.DueBack, m.At);
        }).ToList();
    }

    private static async Task<IResult> PartyMovesAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (await db.MetalParties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) is not { } party) return TypedResults.NotFound();
        var moves = await db.MetalMoves.AsNoTracking().Where(m => m.PartyId == id).ToListAsync(ct);
        var list = WithBalances(moves, m => m.BalanceEffect, new Dictionary<Guid, string> { [id] = party.Name });
        list.Reverse();
        return TypedResults.Ok(list);
    }

    private static async Task<IResult> AddMoveAsync(MetalMoveRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (r.Kind is not ("issue" or "receive" or "rate_cut" or "labour_paid" or "opening")) return Invalid("Kind", "Choose what this entry is.");
        if (r.Metal is not ("gold" or "silver")) return Invalid("Metal", "Choose gold or silver.");
        if (r.Kind is "rate_cut" or "labour_paid" or "opening" && !LedgerEndpoints.IsOwner(user)) return OwnerOnly("settles metal accounts in money");
        MetalParty? party = null;
        if (r.Kind != "opening")
        {
            party = r.PartyId is { } pid ? await db.MetalParties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == pid, ct) : null;
            if (party is null) return Invalid("PartyId", "Choose the karigar or trader.");
        }

        var move = new MetalMove { Kind = r.Kind, Metal = r.Metal, PartyId = party?.Id, Note = Clip(r.Note, 200), At = clock.GetUtcNow(), ByUserId = user.UserId()!.Value };
        if (r.Kind is "issue" or "receive" or "opening")
        {
            if (r.Gross is not (> 0 and <= 100_000) || r.Reading is not (> 0 and <= 100)) return Invalid("Gross", "Enter the weight and its purity (touch) in percent.");
            move.Gross = JewelleryMath.Grams(r.Gross.Value);
            move.Reading = decimal.Round(r.Reading.Value, 2);
            move.Fine = JewelleryMath.Grams(move.Gross * move.Reading / 100m);
        }

        var balance = party is null ? 0 : await db.MetalMoves.Where(m => m.PartyId == party.Id && m.Metal == r.Metal).SumAsync(m => (decimal?)m.BalanceEffect, ct) ?? 0;
        switch (r.Kind)
        {
            case "issue":
                move.BalanceEffect = move.Fine;
                move.LooseEffect = -move.Fine;
                if (party!.Kind == "karigar") move.DueBack = IndiaTime.Today(clock).AddYears(1);
                break;
            case "receive":
                move.Finished = party!.Kind == "karigar" && (r.Finished ?? true);
                if (move.Finished)
                {
                    move.Allowance = JewelleryMath.Grams((party.AllowanceOnFine ? move.Fine : move.Gross) * party.AllowancePercent / 100m);
                    move.Labour = decimal.Round(move.Gross * party.LabourPerGram, 2);
                }

                move.BalanceEffect = -(move.Fine + move.Allowance);
                move.LooseEffect = move.Finished ? 0 : move.Fine;
                break;
            case "rate_cut":
                // Settle fine grams in money at an agreed rate: the party pays for metal it holds, or the shop pays for metal it owes.
                if (r.Fine is not > 0 || r.RatePerGram is not (> 0 and <= 1_000_000)) return Invalid("Fine", "Enter the fine grams and the rate per gram.");
                var fine = JewelleryMath.Grams(r.Fine.Value);
                var theyPay = r.TheyPay ?? balance > 0;
                if (theyPay ? fine > balance + Settled : fine > -balance + Settled) return Invalid("Fine", theyPay ? $"{party!.Name} holds only {Math.Max(0, balance):0.000} g fine." : $"You owe {party!.Name} only {Math.Max(0, -balance):0.000} g fine.");
                move.Fine = fine;
                move.RatePerGram = decimal.Round(r.RatePerGram.Value, 2);
                var money = decimal.Round(fine * move.RatePerGram.Value, 0);
                move.CashEffect = theyPay ? money : -money;
                move.BalanceEffect = theyPay ? -fine : fine;
                move.Mode = r.Mode ?? PaymentMode.Cash;
                break;
            case "labour_paid":
                if (r.Amount is not > 0 || r.Amount > Money.MaxAmount) return Invalid("Amount", "Enter the labour paid.");
                move.CashEffect = -decimal.Round(r.Amount.Value, 2);
                move.Mode = r.Mode ?? PaymentMode.Cash;
                break;
            case "opening":
                move.LooseEffect = move.Fine;
                break;
        }

        if (move.Mode is { } mode && mode is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Bank)) return Invalid("Mode", "Pay by cash, UPI or bank.");
        move.Number = (await db.MetalMoves.MaxAsync(m => (int?)m.Number, ct) ?? 0) + 1;
        db.MetalMoves.Add(move);
        await db.SaveChangesAsync(ct);
        var names = party is null ? new Dictionary<Guid, string>() : new Dictionary<Guid, string> { [party.Id] = party.Name };
        var all = await db.MetalMoves.AsNoTracking().Where(m => party == null ? m.PartyId == null : m.PartyId == party.Id).ToListAsync(ct);
        return TypedResults.Ok(WithBalances(all, m => party is null ? m.LooseEffect : m.BalanceEffect, names).Single(m => m.Id == move.Id));
    }

    /// <summary>The shop's own loose metal in fine grams: opening stock, old metal bought over the counter, and what went to and came from parties.</summary>
    private static async Task<IResult> LooseAsync(AppDbContext db, CancellationToken ct) => TypedResults.Ok(await LooseMetalAsync(db, ct));

    internal static async Task<List<LooseMetalDto>> LooseMetalAsync(AppDbContext db, CancellationToken ct)
    {
        var moves = await db.MetalMoves.AsNoTracking().Where(m => m.LooseEffect != 0).ToListAsync(ct);
        var old = await db.OldMetalPurchases.AsNoTracking().GroupBy(o => o.Metal).Select(x => new { x.Key, Fine = x.Sum(o => o.Fine) }).ToDictionaryAsync(x => x.Key, x => x.Fine, ct);
        return [.. new[] { "gold", "silver" }.Select(metal =>
        {
            var ms = moves.Where(m => m.Metal == metal).ToList();
            var opening = ms.Where(m => m.Kind == "opening").Sum(m => m.LooseEffect);
            var from = ms.Where(m => m.Kind != "opening" && m.LooseEffect > 0).Sum(m => m.LooseEffect);
            var to = -ms.Where(m => m.LooseEffect < 0).Sum(m => m.LooseEffect);
            var bought = old.GetValueOrDefault(metal);
            return new LooseMetalDto(metal, JewelleryMath.Grams(opening), JewelleryMath.Grams(bought), JewelleryMath.Grams(from), JewelleryMath.Grams(to), JewelleryMath.Grams(opening + bought + from - to));
        })];
    }

    // ---------------- stones (called from the piece endpoints) ----------------

    internal static string? CheckStones(IReadOnlyList<StoneRequest>? stones)
    {
        if (stones is null) return null;
        if (stones.Count > 30) return "Up to 30 stone lines on a piece.";
        foreach (var s in stones)
        {
            if (string.IsNullOrWhiteSpace(s.Kind) || s.Kind.Trim().Length > 40) return "Write what each stone is (diamond, ruby…).";
            if (s.Count is < 1 or > 10_000 || s.Carat is < 0 or > 5_000 || s.RatePerCarat is < 0 or > 100_000_000 || s.Value is < 0 or > 100_000_000) return "Enter stone count, carats and rates of zero or more.";
        }

        return null;
    }

    /// <summary>Replace a piece's stones. The piece's stone weight (1 carat = 0.2 g) and stone value then come from them.</summary>
    internal static async Task SetStonesAsync(AppDbContext db, JewelleryPiece piece, IReadOnlyList<StoneRequest>? stones, CancellationToken ct)
    {
        if (stones is null) return;
        db.PieceStones.RemoveRange(await db.PieceStones.Where(s => s.PieceId == piece.Id).ToListAsync(ct));
        var rows = stones.Select(s =>
        {
            var carat = decimal.Round(s.Carat ?? 0, 3);
            var rate = decimal.Round(s.RatePerCarat ?? 0, 2);
            return new PieceStone
            {
                PieceId = piece.Id, Kind = s.Kind.Trim(), LabGrown = s.LabGrown ?? false, Count = s.Count ?? 1, Carat = carat, Shape = Clip(s.Shape, 30), Colour = Clip(s.Colour, 20),
                Clarity = Clip(s.Clarity, 20), CertLab = Clip(s.CertLab, 20), CertNo = Clip(s.CertNo, 40), RatePerCarat = rate, Value = s.Value is { } v ? decimal.Round(v, 2) : decimal.Round(carat * rate, 2),
            };
        }).ToList();
        db.PieceStones.AddRange(rows);
        if (rows.Count > 0)
        {
            var weight = JewelleryMath.Grams(rows.Sum(s => s.Carat) * 0.2m);
            if (weight > 0 && weight < piece.Gross) piece.Stone = weight;
            piece.StoneValue = rows.Sum(s => s.Value);
        }
    }

    internal static StoneDto ToDto(PieceStone s) => new(s.Kind, s.LabGrown, s.Count, s.Carat, s.Shape, s.Colour, s.Clarity, s.CertLab, s.CertNo, s.RatePerCarat, s.Value);

    // ---------------- trays ----------------

    private static IQueryable<JewelleryPiece> InTray(AppDbContext db) =>
        db.JewelleryPieces.AsNoTracking().Where(p => p.SoldBillId == null && !db.ApprovalItems.Any(i => i.PieceId == p.Id && i.ReturnedAt == null && i.SoldBillId == null));

    private static async Task<IResult> TraysAsync(AppDbContext db, CancellationToken ct)
    {
        var groups = await InTray(db).GroupBy(p => p.Tray ?? "").Select(x => new { x.Key, N = x.Count(), Gross = x.Sum(p => p.Gross) }).ToListAsync(ct);
        var last = (await db.TrayCounts.AsNoTracking().OrderByDescending(c => c.CreatedAt).Take(500).ToListAsync(ct)).GroupBy(c => c.Tray).ToDictionary(x => x.Key, x => x.First());
        return TypedResults.Ok(groups.OrderBy(x => x.Key == "" ? "~" : x.Key).Select(x =>
        {
            var c = last.GetValueOrDefault(x.Key);
            return new TrayDto(x.Key, x.N, x.Gross, c?.CreatedAt, c?.Kind, c is not null && c.Missing is null && c.Extra is null);
        }));
    }

    private static TrayCountDto ToDto(TrayCount c) => new(c.Id, c.Tray, c.Kind, c.ExpectedPieces, c.ExpectedGross, c.CountedPieces, c.CountedGross,
        c.CountedGross is { } g ? JewelleryMath.Grams(g - c.ExpectedGross) : null, Split(c.Missing), Split(c.Extra),
        c.Missing is null && c.Extra is null && (c.CountedGross is null || Math.Abs(c.CountedGross.Value - c.ExpectedGross) <= 0.01m), c.CreatedAt);

    private static List<string> Split(string? csv) => csv is null ? [] : [.. csv.Split(',')];

    private static async Task<IResult> TrayCountsAsync(string? tray, AppDbContext db, CancellationToken ct)
    {
        var q = db.TrayCounts.AsNoTracking();
        if (tray is not null) q = q.Where(c => c.Tray == tray);
        return TypedResults.Ok((await q.OrderByDescending(c => c.CreatedAt).Take(60).ToListAsync(ct)).Select(ToDto));
    }

    /// <summary>
    /// Count a tray: the tags found (scanned or ticked) and, if weighed, their total gross. What should be there is every
    /// piece in stock with that tray that is not sold or out on approval.
    /// </summary>
    private static async Task<IResult> CountTrayAsync(TrayCountRequest r, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (r.Tray is null || r.Tray.Trim().Length > 30) return Invalid("Tray", "Name the tray.");
        if (r.Kind is not ("opening" or "closing")) return Invalid("Kind", "Opening or closing count.");
        if (r.Tags is null || r.Tags.Count > 2_000) return Invalid("Tags", "Up to 2,000 tags in one count.");
        if (r.CountedGross is < 0 or > 1_000_000) return Invalid("CountedGross", "Enter the weighed total in grams.");
        var tray = r.Tray.Trim();
        var expected = await InTray(db).Where(p => (p.Tray ?? "") == tray).Select(p => new { p.TagNo, p.Gross }).ToListAsync(ct);
        var found = r.Tags.Select(t => t.Trim()).Select(t => t.StartsWith("GKJ:", StringComparison.OrdinalIgnoreCase) ? t[4..] : t.TrimStart('J', 'j', '-'))
            .Select(t => int.TryParse(t, out var n) ? n : (int?)null).Where(n => n is not null).Select(n => n!.Value).ToHashSet();
        var want = expected.Select(e => e.TagNo).ToHashSet();
        var missing = want.Except(found).Order().ToList();
        var extra = found.Except(want).Order().ToList();
        var count = new TrayCount
        {
            Tray = tray, Kind = r.Kind, ExpectedPieces = expected.Count, ExpectedGross = expected.Sum(e => e.Gross), CountedPieces = found.Count,
            CountedGross = r.CountedGross is { } g ? JewelleryMath.Grams(g) : null, Missing = missing.Count == 0 ? null : string.Join(',', missing.Select(n => $"J-{n}")),
            Extra = extra.Count == 0 ? null : string.Join(',', extra.Select(n => $"J-{n}")), ByUserId = user.UserId()!.Value,
        };
        db.TrayCounts.Add(count);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(count));
    }

    // ---------------- scales ----------------

    private static ScaleDto ToDto(WeighingScale s, DateOnly today) => new(s.Id, s.Name, s.ReadsTo, s.StampedUntil, s.Baud, s.IsActive, s.StampedUntil is { } d ? d.DayNumber - today.DayNumber : 9999);

    private static async Task<IResult> ScalesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        return TypedResults.Ok((await db.WeighingScales.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct)).Select(s => ToDto(s, today)));
    }

    private static string? ApplyScale(WeighingScale s, ScaleRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 60) return "Name the scale.";
        if (r.ReadsTo is not (0.001m or 0.01m or 0.1m)) return "The scale is verified to 0.001 g, 0.01 g or 0.1 g.";
        if (r.Baud is { } b && b is not (1200 or 2400 or 4800 or 9600 or 19200 or 38400 or 57600 or 115200)) return "Choose the scale's baud rate.";
        DateOnly? until = null;
        if (!string.IsNullOrEmpty(r.StampedUntil))
        {
            if (!DateOnly.TryParseExact(r.StampedUntil, "yyyy-MM-dd", out var d)) return "Enter the stamping date.";
            until = d;
        }

        s.Name = r.Name.Trim();
        s.ReadsTo = r.ReadsTo;
        s.StampedUntil = until;
        s.Baud = r.Baud ?? 9600;
        s.IsActive = r.IsActive ?? true;
        return null;
    }

    private static async Task<IResult> AddScaleAsync(ScaleRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return OwnerOnly("adds a scale");
        var scale = new WeighingScale { Name = "" };
        if (ApplyScale(scale, r) is { } problem) return Invalid("Scale", problem);
        db.WeighingScales.Add(scale);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(scale, IndiaTime.Today(clock)));
    }

    private static async Task<IResult> EditScaleAsync(Guid id, ScaleRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return OwnerOnly("changes a scale");
        if (await db.WeighingScales.SingleOrDefaultAsync(s => s.Id == id, ct) is not { } scale) return TypedResults.NotFound();
        if (ApplyScale(scale, r) is { } problem) return Invalid("Scale", problem);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(scale, IndiaTime.Today(clock)));
    }
}
