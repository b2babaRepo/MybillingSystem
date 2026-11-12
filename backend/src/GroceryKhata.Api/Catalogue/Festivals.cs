using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Catalogue;

/// <summary>A shop's change to the festival calendar: a different date this year (local custom, or a wrong date), or off.</summary>
public sealed class FestivalSetting : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Key { get; set; }

    public int Year { get; set; }

    public DateOnly? Date { get; set; }

    public bool Off { get; set; }
}

public sealed record FestivalSettingRequest(string? Key, int Year, DateOnly? Date = null, bool Off = false);

/// <summary>
/// One item to stock up on. <see cref="LastYear"/>: sold in the two weeks before this festival last year;
/// <see cref="Usual"/>: a normal two weeks' sale. <see cref="Suggest"/>: what to order now, after stock and what is on order.
/// </summary>
public sealed record FestivalItemDto(Guid ProductId, string Name, string? Category, Unit Unit, decimal Stock, decimal OnOrder, decimal? LastYear, decimal Usual, decimal Suggest);

/// <summary><see cref="Basis"/>: "last_year" (from the shop's own sales) or "usual_items" (what this festival usually needs).</summary>
public sealed record FestivalDto(string Key, string Name, DateOnly Date, int DaysLeft, bool Changed, string Basis, IReadOnlyList<FestivalItemDto> Items);

public static class Festivals
{
    /// <summary>How far ahead a festival shows, and the run-up whose sales count.</summary>
    public const int LeadDays = 30;

    private const int RunUpDays = 14;

    private sealed record Festival(string Key, string Name, string[] Words, Dictionary<int, DateOnly> Dates);

    private static Dictionary<int, DateOnly> On(params (int Year, int Month, int Day)[] dates) => dates.ToDictionary(d => d.Year, d => new DateOnly(d.Year, d.Month, d.Day));

    // Dates follow the Hindu and Islamic calendars, so they move each year; a shop can change one (local custom) or turn it off.
    // Eid dates depend on the moon and may be a day out.
    private static readonly Festival[] Calendar =
    [
        new("sankranti", "Makar Sankranti / Lohri / Pongal", ["til", "तिल", "gud", "jaggery", "गुड़", "peanut", "moongfali", "मूंगफली", "rewari", "gajak", "गजक", "chikki", "kite", "patang", "पतंग", "manjha", "rice", "चावल"],
            On((2025, 1, 14), (2026, 1, 14), (2027, 1, 14))),
        new("holi", "Holi", ["gulal", "गुलाल", "colour", "color", "रंग", "pichkari", "पिचकारी", "mawa", "khoya", "मावा", "maida", "मैदा", "sugar", "चीनी", "shakkar", "dry fruit", "kaju", "badam", "ghee", "घी", "papad", "पापड़", "namkeen", "thandai", "ठंडाई"],
            On((2025, 3, 14), (2026, 3, 4), (2027, 3, 22))),
        new("eid_fitr", "Eid ul-Fitr", ["sewai", "seviyan", "vermicelli", "सेवई", "dates", "khajur", "खजूर", "dry fruit", "kaju", "badam", "pista", "kishmish", "milk", "doodh", "दूध", "sugar", "चीनी", "ghee", "घी", "rooh afza", "attar"],
            On((2025, 3, 31), (2026, 3, 20), (2027, 3, 10))),
        new("raksha_bandhan", "Raksha Bandhan", ["rakhi", "राखी", "sweet", "mithai", "मिठाई", "chocolate", "dry fruit", "kaju", "badam", "roli", "rice", "chawal", "gift"],
            On((2025, 8, 9), (2026, 8, 28), (2027, 8, 17))),
        new("janmashtami", "Janmashtami", ["makhana", "मखाना", "panjiri", "dhaniya", "धनिया", "sabudana", "साबूदाना", "mishri", "मिश्री", "butter", "makhan", "मक्खन", "milk", "doodh", "दूध", "kuttu", "sendha"],
            On((2025, 8, 16), (2026, 9, 4), (2027, 8, 25))),
        new("ganesh", "Ganesh Chaturthi", ["modak", "मोदक", "coconut", "nariyal", "नारियल", "jaggery", "gud", "गुड़", "rice flour", "chawal ka atta", "ghee", "घी", "durva", "agarbatti", "camphor", "kapur", "कपूर"],
            On((2025, 8, 27), (2026, 9, 14), (2027, 9, 4))),
        new("navratri", "Navratri", ["sabudana", "साबूदाना", "kuttu", "कुट्टू", "singhara", "सिंघाड़ा", "rajgira", "sendha", "सेंधा", "makhana", "मखाना", "samak", "vrat", "व्रत", "peanut", "moongfali", "coconut", "nariyal", "chunri", "agarbatti", "ghee", "घी"],
            On((2025, 9, 22), (2026, 10, 11), (2027, 9, 30))),
        new("karwa_chauth", "Karwa Chauth", ["mathri", "मठरी", "sindoor", "सिंदूर", "bangle", "chudi", "karwa", "करवा", "feni", "फेनी", "sweet", "mithai", "dry fruit", "mehndi", "मेहंदी"],
            On((2025, 10, 10), (2026, 10, 29), (2027, 10, 18))),
        new("diwali", "Diwali", ["diya", "दीया", "candle", "मोमबत्ती", "rangoli", "रंगोली", "dry fruit", "kaju", "काजू", "badam", "बादाम", "kishmish", "ghee", "घी", "oil", "तेल", "sugar", "चीनी", "maida", "मैदा", "besan", "बेसन", "sooji", "rava", "सूजी", "mithai", "sweet", "namkeen", "नमकीन", "chocolate", "gift", "puja", "पूजा", "agarbatti", "kheel", "batasha", "बताशा", "detergent", "phenyl"],
            On((2025, 10, 20), (2026, 11, 8), (2027, 10, 29))),
        new("chhath", "Chhath Puja", ["thekua", "ठेकुआ", "gud", "jaggery", "गुड़", "atta", "आटा", "coconut", "nariyal", "नारियल", "banana", "kela", "केला", "sugarcane", "ganna", "soop", "supali", "sindoor", "सिंदूर", "ghee", "घी", "fruit", "rice", "chawal", "चावल"],
            On((2025, 10, 27), (2026, 11, 15), (2027, 11, 4))),
        new("christmas", "Christmas / New Year", ["cake", "केक", "chocolate", "plum", "cream", "biscuit", "cold drink", "soft drink", "namkeen", "chips", "candle", "gift"],
            On((2025, 12, 25), (2026, 12, 25), (2027, 12, 25))),
    ];

    public static IEndpointRouteBuilder MapFestivals(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/festivals").WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/upcoming", UpcomingAsync);
        group.MapPost("/settings", SettingAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> SettingAsync(FestivalSettingRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Calendar.FirstOrDefault(f => f.Key == request.Key) is not { } festival || request.Year is < 2024 or > 2100
            || (request.Date is { } d && (festival.Dates.TryGetValue(request.Year, out var built) ? Math.Abs(d.DayNumber - built.DayNumber) > 40 : d.Year != request.Year)))
        {
            return Invalid("Date", "Choose a festival and a date in that year.");
        }

        var setting = await db.FestivalSettings.SingleOrDefaultAsync(s => s.Key == festival.Key && s.Year == request.Year, ct);
        if (setting is null)
        {
            setting = new FestivalSetting { Key = festival.Key, Year = request.Year };
            db.FestivalSettings.Add(setting);
        }

        (setting.Date, setting.Off) = (request.Date, request.Off);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Festivals in the next <see cref="LeadDays"/> days with what to stock. With last year's sales, the items that sold
    /// clearly more than usual in the run-up; otherwise the shop's items that festival usually needs.
    /// </summary>
    private static Task<IReadOnlyList<FestivalDto>> UpcomingAsync(bool? all, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        ListAsync(db, clock, all == true, ct);

    /// <summary>
    /// For the 8 am phone note: the nearest festival within <paramref name="withinDays"/> that has items to stock up on,
    /// with how many. Null when there is none.
    /// </summary>
    internal static async Task<(string Name, int DaysLeft, int Items)?> PromptAsync(AppDbContext db, TimeProvider clock, int withinDays, CancellationToken ct) =>
        (await ListAsync(db, clock, false, ct)).Where(f => f.DaysLeft <= withinDays).Select(f => (f.Name, f.DaysLeft, Items: f.Items.Count(i => i.Suggest > 0)))
            .FirstOrDefault(f => f.Items > 0) is { Name: not null } next ? next : null;

    internal static async Task<IReadOnlyList<FestivalDto>> ListAsync(AppDbContext db, TimeProvider clock, bool all, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var settings = await db.FestivalSettings.AsNoTracking().ToListAsync(ct);
        DateOnly? DateOf(Festival f, int year)
        {
            var s = settings.FirstOrDefault(x => x.Key == f.Key && x.Year == year);
            return s?.Date ?? (f.Dates.TryGetValue(year, out var d) ? d : null);
        }

        var upcoming = Calendar.SelectMany(f => new[] { today.Year, today.Year + 1 }.Select(y => (f, y, Date: DateOf(f, y))))
            .Where(x => x.Date is { } d && d >= today && d.DayNumber - today.DayNumber <= LeadDays)
            .Where(x => all || !settings.Any(s => s.Key == x.f.Key && s.Year == x.y && s.Off))
            .OrderBy(x => x.Date).ToList();
        if (upcoming.Count == 0)
        {
            return [];
        }

        var products = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsService)
            .Select(p => new { p.Id, p.Name, p.Category, p.Unit, p.Stock }).ToListAsync(ct);
        var onOrder = (await db.PurchaseOrderLines.AsNoTracking()
                .Where(l => db.PurchaseOrders.Any(o => o.Id == l.OrderId && o.Status == PurchaseOrderStatus.Open))
                .Select(l => new { l.ProductId, l.Quantity, l.Received, l.ShortClosed }).ToListAsync(ct))
            .GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => Math.Max(0, l.Quantity - l.Received - l.ShortClosed)));

        var result = new List<FestivalDto>();
        foreach (var (f, year, date) in upcoming)
        {
            var day = date!.Value;
            // Last year's run-up, and a normal fortnight to compare it with: the four weeks before the run-up, halved.
            var lastDate = DateOf(f, year - 1);
            var items = new List<FestivalItemDto>();
            var basis = "usual_items";
            if (lastDate is { } last)
            {
                var (runStart, _) = IndiaTime.Day(last.AddDays(-RunUpDays));
                var (_, runEnd) = IndiaTime.Day(last);
                var (baseStart, _) = IndiaTime.Day(last.AddDays(-RunUpDays - 28));
                var sold = await db.StockMovements.AsNoTracking()
                    .Where(m => m.Kind == StockMovementKind.Sale && m.OccurredAt >= baseStart && m.OccurredAt < runEnd)
                    .GroupBy(m => m.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        RunUp = -g.Sum(m => m.OccurredAt >= runStart ? m.Quantity : 0),
                        Before = -g.Sum(m => m.OccurredAt < runStart ? m.Quantity : 0),
                    }).ToListAsync(ct);
                if (sold.Sum(s => s.RunUp) > 0)
                {
                    basis = "last_year";
                    foreach (var s in sold)
                    {
                        var usual = decimal.Round(s.Before / 2, 3);
                        // A festival item: sold at least a third more than a normal fortnight (or only then).
                        if (s.RunUp <= 0 || s.RunUp < usual * 1.33m || products.FirstOrDefault(p => p.Id == s.ProductId) is not { } p)
                        {
                            continue;
                        }

                        var coming = onOrder.GetValueOrDefault(p.Id);
                        // Sales still to come before the festival, in proportion to the days left of the run-up.
                        var share = Math.Min(1m, (decimal)Math.Max(0, day.DayNumber - today.DayNumber) / RunUpDays);
                        var need = Math.Max(s.RunUp * Math.Max(share, 0.5m), usual);
                        items.Add(new FestivalItemDto(p.Id, p.Name, p.Category, p.Unit, p.Stock, coming, s.RunUp, usual,
                            Math.Max(0, Math.Ceiling(need - Math.Max(0, p.Stock) - coming))));
                    }
                }
            }

            if (basis == "usual_items")
            {
                var since = IndiaTime.Day(today.AddDays(-28)).Start;
                var recent = await db.StockMovements.AsNoTracking().Where(m => m.Kind == StockMovementKind.Sale && m.OccurredAt >= since)
                    .GroupBy(m => m.ProductId).Select(g => new { g.Key, Sold = -g.Sum(m => m.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Sold, ct);
                foreach (var p in products.Where(p => Matches(f, p.Name)))
                {
                    var usual = decimal.Round(recent.GetValueOrDefault(p.Id) / 2, 3);
                    var coming = onOrder.GetValueOrDefault(p.Id);
                    // No history to go by: aim for about one and a half normal fortnights.
                    items.Add(new FestivalItemDto(p.Id, p.Name, p.Category, p.Unit, p.Stock, coming, null, usual,
                        Math.Max(0, Math.Ceiling(usual * 1.5m - Math.Max(0, p.Stock) - coming))));
                }
            }

            var changed = settings.Any(s => s.Key == f.Key && s.Year == year && (s.Date != null || s.Off));
            result.Add(new FestivalDto(f.Key, f.Name, day, day.DayNumber - today.DayNumber, changed, basis,
                items.OrderByDescending(i => i.Suggest).ThenBy(i => i.Name).Take(60).ToList()));
        }

        return result;
    }

    /// <summary>
    /// A word of the festival's list at the start of a word in the item's name. Not the category: "Atta, rice, dal" would
    /// pull in every pulse for Chhath.
    /// </summary>
    private static bool Matches(Festival f, string name)
    {
        var text = $" {name} ".ToLowerInvariant();
        return f.Words.Any(w => text.Contains(' ' + w.ToLowerInvariant(), StringComparison.Ordinal));
    }
}
