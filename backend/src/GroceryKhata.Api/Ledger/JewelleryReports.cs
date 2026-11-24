using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

// Phase 4 of the jewellery counter: the reports that keep a jeweller on the right side of the law, and stock by karat.
// Cash taken from each customer is added up by month and by financial year (April–March): PMLA asks a jeweller to keep
// and report cash dealings of ₹10 lakh or more with one person, and the yearly SFT return lists people who paid more
// than ₹2 lakh in cash. Kerala asks for an e-way bill to move gold worth ₹10 lakh or more inside the state.

public sealed record StockByKaratDto(string Metal, int Purity, int Pieces, int OnApproval, decimal Gross, decimal Net, decimal Fine, decimal? RatePerGram, decimal? Value);
public sealed record MetalTotalsDto(string Metal, int Pieces, decimal Gross, decimal Net, decimal Fine, decimal Value, decimal Loose, decimal WithParties, decimal OwedToParties);
public sealed record StockReportDto(IReadOnlyList<StockByKaratDto> Lines, IReadOnlyList<MetalTotalsDto> Totals, bool RatesMissing);
public sealed record SalesByKaratDto(string Metal, int Purity, int Pieces, decimal Gross, decimal Net, decimal Fine, decimal Taxable, decimal Gst);
public sealed record SalesReportDto(int Fy, DateOnly From, DateOnly To, IReadOnlyList<SalesByKaratDto> Lines, decimal OldMetalBought, decimal OldMetalFine);
public sealed record CashCustomerDto(
    Guid? CustomerId, string Name, string? Phone, string? Pan, int Bills, decimal BillsCash, decimal KhataCash, decimal GirviCash, decimal Total,
    IReadOnlyList<decimal> Months, bool PmlaMonth, bool PmlaYear, bool Sft);
public sealed record CashReportDto(int Fy, DateOnly From, DateOnly To, decimal PmlaLimit, decimal SftLimit, IReadOnlyList<CashCustomerDto> Customers, decimal WalkInCash);

public static class JewelleryReportEndpoints
{
    /// <summary>PMLA: cash dealings of ₹10 lakh or more with one person, in one go or linked (here: a month, and a year).</summary>
    public const decimal PmlaLimit = 1_000_000m;

    /// <summary>SFT (Rule 114E): cash of more than ₹2 lakh from one person for goods.</summary>
    public const decimal SftLimit = 200_000m;

    /// <summary>Kerala: an e-way bill for gold moved inside the state when the consignment is worth ₹10 lakh or more.</summary>
    public const decimal KeralaEwayFrom = 1_000_000m;

    public static bool IsKerala(string? state, string? gstin) =>
        (state is not null && state.Trim().Equals("Kerala", StringComparison.OrdinalIgnoreCase)) || (gstin is { Length: >= 2 } g && g.StartsWith("32", StringComparison.Ordinal));

    /// <summary>The financial year starting in April of <paramref name="fy"/>; by default the one running today.</summary>
    public static (int Fy, DateOnly From, DateOnly To) Year(int? fy, TimeProvider clock)
    {
        var today = IndiaTime.Today(clock);
        var year = fy ?? (today.Month >= 4 ? today.Year : today.Year - 1);
        return (year, new DateOnly(year, 4, 1), new DateOnly(year + 1, 3, 31));
    }

    public static IEndpointRouteBuilder MapJewelleryReports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/jewellery/reports").WithTags("Jewellery").RequireAuthorization(AppPolicies.ShopMember);
        g.MapGet("/stock", StockAsync);
        g.MapGet("/sales", SalesAsync);
        // Each customer's yearly cash and PAN: the owner's to see, not the counter staff's.
        g.MapGet("/cash", CashAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static decimal Fine(decimal net, int purity) => JewelleryMath.Grams(net * purity / 1000m);

    /// <summary>Pieces in stock by metal and purity, with fine weight and today's value; then loose metal and metal with karigars and traders.</summary>
    private static async Task<IResult> StockAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var pieces = await db.JewelleryPieces.AsNoTracking().Where(p => p.SoldBillId == null).Select(p => new { p.Id, p.Metal, p.Purity, p.Gross, p.Stone }).ToListAsync(ct);
        var ids = pieces.Select(p => p.Id).ToList();
        var out_ = (await db.ApprovalItems.AsNoTracking().Where(i => ids.Contains(i.PieceId) && i.ReturnedAt == null && i.SoldBillId == null).Select(i => i.PieceId).ToListAsync(ct)).ToHashSet();
        var rates = (await JewelleryEndpoints.CurrentRatesAsync(db, clock, ct)).Current.ToDictionary(r => (r.Metal, r.Purity), r => r.SellPerGram);
        var lines = pieces.GroupBy(p => (p.Metal, p.Purity)).OrderBy(x => x.Key.Metal).ThenByDescending(x => x.Key.Purity).Select(x =>
        {
            var net = x.Sum(p => p.Gross - p.Stone);
            decimal? rate = rates.TryGetValue(x.Key, out var r) ? r : null;
            return new StockByKaratDto(x.Key.Metal, x.Key.Purity, x.Count(), x.Count(p => out_.Contains(p.Id)), JewelleryMath.Grams(x.Sum(p => p.Gross)), JewelleryMath.Grams(net), Fine(net, x.Key.Purity),
                rate, rate is { } v ? decimal.Round(net * v, 0) : null);
        }).ToList();

        var loose = (await JewelleryWorkshopEndpoints.LooseMetalAsync(db, ct)).ToDictionary(l => l.Metal, l => l.Fine);
        var balances = await db.MetalMoves.AsNoTracking().Where(m => m.PartyId != null).GroupBy(m => new { m.PartyId, m.Metal }).Select(x => new { x.Key.Metal, Fine = x.Sum(m => m.BalanceEffect) }).ToListAsync(ct);
        var totals = new[] { "gold", "silver" }.Select(metal =>
        {
            var ls = lines.Where(l => l.Metal == metal).ToList();
            return new MetalTotalsDto(metal, ls.Sum(l => l.Pieces), ls.Sum(l => l.Gross), ls.Sum(l => l.Net), ls.Sum(l => l.Fine), ls.Sum(l => l.Value ?? 0), loose.GetValueOrDefault(metal),
                JewelleryMath.Grams(balances.Where(b => b.Metal == metal && b.Fine > 0).Sum(b => b.Fine)), JewelleryMath.Grams(-balances.Where(b => b.Metal == metal && b.Fine < 0).Sum(b => b.Fine)));
        }).ToList();
        return TypedResults.Ok(new StockReportDto(lines, totals, lines.Any(l => l.RatePerGram is null)));
    }

    /// <summary>Jewellery sold in a financial year by metal and purity (cancelled bills left out), and the old metal bought in.</summary>
    private static async Task<IResult> SalesAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var (year, from, to) = Year(fy, clock);
        var start = IndiaTime.Day(from).Start;
        var end = IndiaTime.Day(to).End;
        var sold = await (from l in db.JewelleryBillLines.AsNoTracking()
                          join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                          where b.CancelledAt == null && b.CreatedAt >= start && b.CreatedAt < end
                          select new { l.Metal, l.Purity, l.Gross, l.Net, l.Taxable, l.Gst }).ToListAsync(ct);
        var lines = sold.GroupBy(l => (l.Metal, l.Purity)).OrderBy(x => x.Key.Metal).ThenByDescending(x => x.Key.Purity)
            .Select(x => new SalesByKaratDto(x.Key.Metal, x.Key.Purity, x.Count(), x.Sum(l => l.Gross), x.Sum(l => l.Net), Fine(x.Sum(l => l.Net), x.Key.Purity), x.Sum(l => l.Taxable), x.Sum(l => l.Gst))).ToList();
        var old = await db.OldMetalPurchases.AsNoTracking().Where(o => o.CreatedAt >= start && o.CreatedAt < end).ToListAsync(ct);
        return TypedResults.Ok(new SalesReportDto(year, from, to, lines, old.Sum(o => o.Value), JewelleryMath.Grams(old.Where(o => o.Metal == "gold").Sum(o => o.Fine))));
    }

    /// <summary>
    /// Cash from each customer in a financial year: cash paid on jewellery bills, cash khata payments (scheme
    /// instalments, order advances, udhaar paid back) and cash paid back on girvi. Bills without a customer count by PAN.
    /// </summary>
    private static async Task<IResult> CashAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var (year, from, to) = Year(fy, clock);
        var start = IndiaTime.Day(from).Start;
        var end = IndiaTime.Day(to).End;
        var bills = await (from j in db.JewelleryBills.AsNoTracking()
                           join b in db.Bills.AsNoTracking() on j.BillId equals b.Id
                           where j.Mode == PaymentMode.Cash && j.MoneyPaid > 0 && b.CancelledAt == null && j.CreatedAt >= start && j.CreatedAt < end
                           select new { j.CustomerId, j.Pan, j.MoneyPaid, j.CreatedAt }).ToListAsync(ct);
        var khata = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PaymentMode == PaymentMode.Cash && e.BillId == null && (e.Kind == LedgerEntryKind.Payment || e.Kind == LedgerEntryKind.Reversal) && e.OccurredAt >= start && e.OccurredAt < end)
            .Select(e => new { e.CustomerId, e.Amount, e.OccurredAt }).ToListAsync(ct);
        var girvi = await (from p in db.GirviPayments.AsNoTracking()
                           join l in db.GirviLoans.AsNoTracking() on p.LoanId equals l.Id
                           where p.Mode == PaymentMode.Cash && p.Amount > 0 && p.At >= start && p.At < end
                           select new { l.CustomerId, p.Amount, p.At }).ToListAsync(ct);

        // Who: a customer, or a PAN on a bill with no customer.
        var rows = new Dictionary<string, CashRow>();
        var walkIn = 0m;
        int Month(DateTimeOffset at) { var d = IndiaTime.DayOf(at); return (d.Month + 8) % 12; } // April = 0 … March = 11
        void Add(string key, Guid? customer, string? pan, DateTimeOffset at, decimal amount, int kind)
        {
            if (!rows.TryGetValue(key, out var r)) rows[key] = r = new CashRow { Customer = customer };
            r.Months[Month(at)] += amount;
            if (kind == 0) { r.Bills++; r.Bill += amount; } else if (kind == 1) r.Khata += amount; else r.Girvi += amount;
            if (pan is not null) r.Pan = pan;
        }

        foreach (var b in bills)
        {
            var pan = string.IsNullOrWhiteSpace(b.Pan) ? null : b.Pan.Trim().ToUpperInvariant();
            if (b.CustomerId is { } c) Add(c.ToString(), c, pan, b.CreatedAt, b.MoneyPaid, 0);
            else if (pan is not null) Add("PAN:" + pan, null, pan, b.CreatedAt, b.MoneyPaid, 0);
            else walkIn += b.MoneyPaid;
        }

        foreach (var e in khata) Add(e.CustomerId.ToString(), e.CustomerId, null, e.OccurredAt, -e.Amount, 1);
        foreach (var p in girvi) Add(p.CustomerId.ToString(), p.CustomerId, null, p.At, p.Amount, 2);

        var custIds = rows.Values.Where(r => r.Customer is not null).Select(r => r.Customer!.Value).Distinct().ToList();
        var people = await db.Customers.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        var list = rows.Values.Select(r =>
        {
            var total = r.Bill + r.Khata + r.Girvi;
            var who = r.Customer is { } id ? people.GetValueOrDefault(id) : null;
            return new CashCustomerDto(r.Customer, who?.Name ?? (r.Pan is null ? "—" : $"PAN {r.Pan}"), who?.Phone, r.Pan, r.Bills, r.Bill, r.Khata, r.Girvi, total, r.Months,
                r.Months.Any(m => m >= PmlaLimit), total >= PmlaLimit, total > SftLimit);
        }).Where(c => c.Total > 0).OrderByDescending(c => c.Total).Take(1000).ToList();
        return TypedResults.Ok(new CashReportDto(year, from, to, PmlaLimit, SftLimit, list, walkIn));
    }

    private sealed class CashRow
    {
        public Guid? Customer;
        public string? Pan;
        public int Bills;
        public decimal Bill, Khata, Girvi;
        public decimal[] Months = new decimal[12];
    }
}
