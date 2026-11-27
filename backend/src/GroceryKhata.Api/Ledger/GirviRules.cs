using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A state's money-lending law for a jeweller's girvi: its name, whether lending needs a licence, and the written
/// notice before pledged gold is sold. The interest cap is set by the shop as its CA confirms it (states notify and
/// change caps), so these are warnings, never blocks.
/// </summary>
public sealed record GirviStateLaw(string Key, string Name, string Act, bool LicenceNeeded, int NoticeDays);

/// <summary>The shop's girvi rules as set in Settings, and the warnings they raise.</summary>
public sealed record GirviRuleSet(GirviStateLaw? State, string? Licence, decimal? MaxYearlyRate, int NoticeDays);

public static class GirviRules
{
    /// <summary>States with rules so far. Gujarat first; the others follow once checked.</summary>
    public static readonly IReadOnlyDictionary<string, GirviStateLaw> States = new Dictionary<string, GirviStateLaw>
    {
        ["gujarat"] = new("gujarat", "Gujarat", "Gujarat Money-Lenders Act, 2011", LicenceNeeded: true, NoticeDays: 15),
    };

    public static async Task<GirviRuleSet> ForShopAsync(AppDbContext db, CancellationToken ct)
    {
        var s = await db.Shops.AsNoTracking().Where(x => x.Id == db.ShopIdNow)
            .Select(x => new { x.GirviState, x.GirviLicence, x.GirviMaxYearlyRate, x.GirviNoticeDays }).FirstOrDefaultAsync(ct);
        return s is null
            ? new GirviRuleSet(null, null, null, 15)
            : new GirviRuleSet(s.GirviState is { } k && States.TryGetValue(k, out var law) ? law : null, s.GirviLicence, s.GirviMaxYearlyRate, s.GirviNoticeDays);
    }

    /// <summary>What a loan at this monthly rate should be warned about under the shop's rules.</summary>
    public static IReadOnlyList<string> Warnings(GirviRuleSet rules, decimal ratePerMonth, bool overdue)
    {
        var list = new List<string>();
        if (rules.State is { } law && law.LicenceNeeded && string.IsNullOrWhiteSpace(rules.Licence))
            list.Add($"no_licence:{law.Act}");
        if (rules.MaxYearlyRate is { } cap && ratePerMonth * 12 > cap)
            list.Add($"over_cap:{ratePerMonth * 12:0.##}:{cap:0.##}");
        if (overdue && rules.State is not null)
            list.Add($"notice:{rules.NoticeDays}");
        return list;
    }
}
