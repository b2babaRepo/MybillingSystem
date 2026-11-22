using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// A medicine the signed-in person buys regularly at a chemist and is due again, by the same rule the shop's own
/// refill list uses. <see cref="InStock"/>: the shop has it now (so it can be ordered from its page).
/// </summary>
public sealed record MyRefillDto(Guid ShopId, string ShopName, string? ShopSlug, Guid ProductId, string Medicine,
    decimal LastQuantity, DateOnly LastBought, int EveryDays, DateOnly DueOn, int DaysLate, bool InStock);

public static class MyRefills
{
    public static IEndpointRouteBuilder MapMyRefills(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/refills", RefillsAsync).WithTags("Customer").RequireAuthorization();
        return app;
    }

    /// <summary>
    /// Only from chemists where the person has confirmed their khata (their SMS-proved phone, confirmed with the
    /// shop's code): medicine history is health data, so nothing shows from a number they haven't proved is theirs.
    /// </summary>
    private static async Task<IReadOnlyList<MyRefillDto>> RefillsAsync(ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var phone = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.PhoneVerifiedAt != null).Select(u => u.Phone).SingleOrDefaultAsync(ct);
        if (phone is null)
        {
            return [];
        }

        var mine = await (from c in db.Customers.IgnoreQueryFilters().AsNoTracking()
                          join s in db.Shops.AsNoTracking() on c.ShopId equals s.Id
                          where c.Phone == phone && c.PhoneConfirmedAt != null && s.IsActive
                              && (s.Type == Shops.ShopType.Medical || s.ExtraTypes.Contains("medical"))
                          select new { CustomerId = c.Id, s.Id, s.Name, s.Slug }).ToListAsync(ct);
        var today = IndiaTime.Today(clock);
        var since = new DateTimeOffset(today.AddDays(-240).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var result = new List<MyRefillDto>();
        foreach (var k in mine)
        {
            result.AddRange(await CustomerExtras.ReadAsShopAsync(scopes, k.Id, async shopDb =>
            {
                var rows = await (from l in shopDb.BillLines.AsNoTracking()
                                  join b in shopDb.Bills.AsNoTracking() on l.BillId equals b.Id
                                  where b.CustomerId == k.CustomerId && l.ProductId != null && b.CancelledAt == null && b.OccurredAt >= since
                                  select new { ProductId = l.ProductId!.Value, b.OccurredAt, l.Quantity }).ToListAsync(ct);
                var due = Ledger.PharmacyAssist.Refills(rows.Select(r => (k.CustomerId, r.ProductId, DateOnly.FromDateTime(r.OccurredAt.ToOffset(IndiaTime.Offset).DateTime), r.Quantity)), today);
                var ids = due.Select(d => d.ProductId).ToList();
                var products = await shopDb.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive && !p.IsService)
                    .ToDictionaryAsync(p => p.Id, p => new { p.Name, InStock = p.Uncounted || p.Stock - p.Reserved > 0 }, ct);
                return due.Where(d => products.ContainsKey(d.ProductId))
                    .Select(d => new MyRefillDto(k.Id, k.Name, k.Slug, d.ProductId, products[d.ProductId].Name, d.LastQuantity, d.LastBought, d.EveryDays, d.DueOn, d.DaysLate, products[d.ProductId].InStock))
                    .ToList();
            }));
        }

        return result.OrderByDescending(r => r.DaysLate).ThenBy(r => r.Medicine).ToList();
    }
}
