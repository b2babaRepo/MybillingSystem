using System.Globalization;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Which milk centre a phone number belongs to. Kept outside the shops' own rows on purpose: a WhatsApp message arrives
/// with only the sender's number, so this is how the bot finds the centre before it can look at that centre's data.
/// Holds no milk or money, only the link.
/// </summary>
public sealed class DairyFarmerPhone
{
    public required string Phone { get; set; }

    public Guid ShopId { get; set; }

    public Guid FarmerId { get; set; }
}

/// <summary>
/// The farmers' WhatsApp bot. A farmer messages the centre's WhatsApp number and gets their own figures back:
/// balance, today's milk, the rate, this cycle's bill. Plain rules, no AI. The number WhatsApp gives is the farmer's
/// verified number, matched to the phone saved on their record, so a farmer only ever sees their own account. It only
/// answers: it never records milk, pays, or changes anything. Replies go inside WhatsApp's free 24-hour window,
/// because the farmer wrote first.
/// </summary>
public static class DairyBot
{
    public const string Help = """
        Namaste {0} ji 🙏 {1}
        Likhkar bhejein:
        1 – Hisab (kitna baaki hai)
        2 – Aaj ka doodh
        3 – Rate
        4 – Is cycle ka bill
        """;

    /// <summary>What the farmer asked for, from a word or the menu number. Null when it isn't a question we know (the menu is sent).</summary>
    public static string? Intent(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        bool Has(params string[] words) => words.Any(w => t.Contains(w, StringComparison.Ordinal));
        return t switch
        {
            "1" => "balance",
            "2" => "today",
            "3" => "rate",
            "4" => "bill",
            _ when Has("hisab", "hisaab", "baki", "baaki", "balance", "paisa", "kitna", "हिसाब", "बाकी", "बकाया", "पैसा") => "balance",
            _ when Has("aaj", "today", "आज") => "today",
            _ when Has("rate", "bhav", "भाव", "रेट") => "rate",
            _ when Has("bill", "cycle", "statement", "बिल") => "bill",
            _ => null,
        };
    }

    /// <summary>Keeps the directory in step when a farmer's phone is saved or changed (called with the farmer's centre set).</summary>
    public static async Task LinkPhoneAsync(AppDbContext db, Guid shopId, Guid farmerId, string? phone, CancellationToken ct)
    {
        await db.DairyFarmerPhones.Where(p => p.FarmerId == farmerId).ExecuteDeleteAsync(ct);
        if (PhoneNumber.Normalize(phone) is { } normal)
        {
            db.DairyFarmerPhones.Add(new DairyFarmerPhone { Phone = normal, ShopId = shopId, FarmerId = farmerId });
        }
    }

    /// <summary>
    /// The reply for a message from a farmer, or null when the sender isn't a farmer at any centre (so the message goes
    /// on to the shopping-list reader). A farmer at more than one centre gets the centre they were added to first.
    /// </summary>
    public static async Task<string?> TryReplyAsync(JsonElement message, AppDbContext directory, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        if (!message.TryGetProperty("from", out var from) || PhoneNumber.Normalize("+" + from.GetString()) is not { } phone)
        {
            return null;
        }

        var link = await directory.DairyFarmerPhones.AsNoTracking().Where(p => p.Phone == phone).OrderBy(p => p.FarmerId).FirstOrDefaultAsync(ct);
        if (link is null)
        {
            return null;
        }

        var text = message.TryGetProperty("type", out var type) && type.GetString() == "text" ? message.GetProperty("text").GetProperty("body").GetString() ?? "" : "";
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(link.ShopId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await ReplyAsync(db, link.ShopId, link.FarmerId, text, clock, ct);
    }

    /// <summary>The answer itself, from this farmer's own records at the centre (the centre is already set).</summary>
    public static async Task<string?> ReplyAsync(AppDbContext db, Guid shopId, Guid farmerId, string text, TimeProvider clock, CancellationToken ct)
    {
        var farmer = await db.DairyFarmers.AsNoTracking().SingleOrDefaultAsync(f => f.Id == farmerId && f.IsActive, ct);
        if (farmer is null)
        {
            return null;
        }

        var centre = await db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => s.Name).SingleAsync(ct);
        var today = IndiaTime.Today(clock);
        string Money(decimal v) => "₹" + v.ToString("#,##0.##", CultureInfo.InvariantCulture);
        switch (Intent(text))
        {
            case "balance":
            {
                var milk = await db.MilkCollections.AsNoTracking().Where(c => c.FarmerId == farmerId && !c.Rejected).SumAsync(c => (decimal?)c.Amount, ct) ?? 0;
                var other = await db.DairyFarmerEntries.AsNoTracking().Where(e => e.FarmerId == farmerId).SumAsync(e => (decimal?)(e.Kind == FarmerEntryKind.Bonus ? e.Amount : -e.Amount), ct) ?? 0;
                var due = milk + other;
                return due >= 0
                    ? $"{centre}\n{farmer.Name} ji, aapka {Money(due)} baaki hai (aaj tak ka doodh, daana/advance kaatkar)."
                    : $"{centre}\n{farmer.Name} ji, aap par {Money(-due)} baaki hai (daana/advance doodh se zyada)."
;
            }

            case "today":
            {
                var rows = await db.MilkCollections.AsNoTracking().Where(c => c.FarmerId == farmerId && c.Date == today).OrderBy(c => c.Shift).ThenBy(c => c.CreatedAt).ToListAsync(ct);
                if (rows.Count == 0)
                {
                    return $"{centre}\n{farmer.Name} ji, aaj abhi tak aapka doodh darj nahi hua.";
                }

                var lines = rows.Select(c => c.Rejected
                    ? $"{(c.Shift == Shift.Morning ? "Subah" : "Shaam")}: {c.Litres:0.##} L lautaya gaya"
                    : $"{(c.Shift == Shift.Morning ? "Subah" : "Shaam")}: {c.Litres:0.##} L, fat {c.Fat:0.0}{(c.Snf is { } s ? $", SNF {s:0.0}" : "")}, {Money(c.Amount)}");
                return $"{centre}\n{farmer.Name} ji, aaj ({today:dd MMM}):\n{string.Join('\n', lines)}";
            }

            case "rate":
            {
                var plan = await db.DairyRatePlans.AsNoTracking().Include(p => p.Rates).Where(p => p.For == RatePlanFor.Farmers && p.From <= today).OrderByDescending(p => p.From).FirstOrDefaultAsync(ct);
                var rate = plan?.Rates.FirstOrDefault(r => r.Kind == farmer.DefaultKind) ?? plan?.Rates.FirstOrDefault();
                if (plan is null || rate is null)
                {
                    return $"{centre}\nRate abhi app mein nahi daala gaya. Kendra par poochhein.";
                }

                var kind = rate.Kind switch { MilkKind.Cow => "Gaay", MilkKind.Buffalo => "Bhains", _ => "Mix" };
                return plan.Method switch
                {
                    RateMethod.FatSnf => $"{centre}\n{kind} ka rate ({plan.From:dd MMM} se): fat {Money(rate.FatRate)}/kg + SNF {Money(rate.SnfRate)}/kg.",
                    RateMethod.FatOnly => $"{centre}\n{kind} ka rate ({plan.From:dd MMM} se): fat {Money(rate.FatRate)}/kg.",
                    RateMethod.Flat => $"{centre}\n{kind} ka rate ({plan.From:dd MMM} se): {Money(rate.FlatRate)}/litre.",
                    _ => $"{centre}\n{kind} ka rate chart se lagta hai (fat aur SNF ke hisab se). Kendra par chart dekhein.",
                };
            }

            case "bill":
            {
                var start = new DateOnly(today.Year, today.Month, today.Day <= 10 ? 1 : today.Day <= 20 ? 11 : 21);
                var milk = await db.MilkCollections.AsNoTracking().Where(c => c.FarmerId == farmerId && !c.Rejected && c.Date >= start).ToListAsync(ct);
                var entries = await db.DairyFarmerEntries.AsNoTracking().Where(e => e.FarmerId == farmerId && e.Date >= start).ToListAsync(ct);
                var litres = milk.Sum(c => c.Litres);
                decimal Of(FarmerEntryKind k) => entries.Where(e => e.Kind == k).Sum(e => e.Amount);
                return $"{centre}\n{farmer.Name} ji, {start:dd MMM} se aaj tak:\nDoodh {litres:0.##} L, fat {(litres > 0 ? milk.Sum(c => c.Litres * c.Fat) / litres : 0):0.0}: {Money(milk.Sum(c => c.Amount))}\n" +
                    $"Daana −{Money(Of(FarmerEntryKind.Feed))} · Advance −{Money(Of(FarmerEntryKind.Advance))} · Bonus +{Money(Of(FarmerEntryKind.Bonus))} · Mila {Money(Of(FarmerEntryKind.Payout))}";
            }

            default:
                return string.Format(CultureInfo.InvariantCulture, Help, farmer.Name, centre);
        }
    }
}
