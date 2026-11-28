using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Billing;

/// <summary>One shop brought by another with its referral code: when it joined, whether it has paid, whether the free month was given.</summary>
public sealed record ReferralRow(Guid ShopId, string ShopName, DateTimeOffset JoinedAt, Guid ReferrerShopId, string ReferrerName, string ReferrerCode,
    bool Paid, DateTimeOffset? FirstPaidAt, bool Rewarded);

/// <summary>Each referring shop's totals: shops brought, how many paid, free months earned.</summary>
public sealed record ReferrerTotal(Guid ShopId, string ShopName, string Code, int Joined, int Paid, int Rewarded);

public sealed record ReferralsDto(IReadOnlyList<ReferralRow> Referrals, IReadOnlyList<ReferrerTotal> Referrers);

public static class ReferralEndpoints
{
    public static IEndpointRouteBuilder MapReferrals(this IEndpointRouteBuilder app)
    {
        // The platform's record of shops bringing shops (BillingService gives both a free month at the first payment).
        app.MapGet("/api/v1/platform/referrals", ListAsync).WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin);
        return app;
    }

    private static async Task<ReferralsDto> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var referred = await db.Subscriptions.IgnoreQueryFilters().AsNoTracking().Where(s => s.ReferredByShopId != null)
            .Select(s => new { s.ShopId, Referrer = s.ReferredByShopId!.Value, s.ReferralRewarded }).ToListAsync(ct);
        var shopIds = referred.SelectMany(r => new[] { r.ShopId, r.Referrer }).Distinct().ToList();
        var shops = await db.Shops.IgnoreQueryFilters().AsNoTracking().Where(s => shopIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Slug, s.CreatedAt }).ToDictionaryAsync(s => s.Id, ct);
        var newIds = referred.Select(r => r.ShopId).ToList();
        var firstPaid = await db.SubscriptionPayments.IgnoreQueryFilters().AsNoTracking().Where(p => newIds.Contains(p.ShopId))
            .GroupBy(p => p.ShopId).Select(g => new { g.Key, First = g.Min(p => p.PaidAt) }).ToDictionaryAsync(g => g.Key, g => g.First, ct);

        var rows = referred.Where(r => shops.ContainsKey(r.ShopId) && shops.ContainsKey(r.Referrer)).Select(r =>
        {
            var shop = shops[r.ShopId];
            var by = shops[r.Referrer];
            var paidAt = firstPaid.TryGetValue(r.ShopId, out var at) ? at : (DateTimeOffset?)null;
            return new ReferralRow(shop.Id, shop.Name, shop.CreatedAt, by.Id, by.Name, by.Slug, paidAt != null, paidAt, r.ReferralRewarded);
        }).OrderByDescending(r => r.JoinedAt).ToList();
        var totals = rows.GroupBy(r => r.ReferrerShopId)
            .Select(g => new ReferrerTotal(g.Key, g.First().ReferrerName, g.First().ReferrerCode, g.Count(), g.Count(r => r.Paid), g.Count(r => r.Rewarded)))
            .OrderByDescending(t => t.Joined).ThenBy(t => t.ShopName).ToList();
        return new ReferralsDto(rows, totals);
    }
}
