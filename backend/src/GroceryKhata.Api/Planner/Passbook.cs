using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// One line of the customer's passbook: a bill from a shop (what it came to, what was paid then, what went on the
/// khata) or a payment made on a khata. <see cref="BillToken"/> opens the bill's signed page.
/// </summary>
public sealed record PassbookItemDto(Guid ShopId, string ShopName, string? ShopSlug, DateTimeOffset At, string Kind, decimal Amount, decimal Paid, decimal OnKhata,
    Guid? BillId, string? BillNo, string? BillToken, PaymentMode? Mode, string? UpiRef);

/// <summary>A shop in the passbook with what is owed there now (null: no khata there, bills only).</summary>
public sealed record PassbookShopDto(Guid ShopId, string ShopName, string? ShopSlug, decimal? Balance);

public sealed record PassbookDto(string? Month, IReadOnlyList<PassbookItemDto> Items, IReadOnlyList<PassbookShopDto> Shops, decimal Billed, decimal Paid);

/// <summary>
/// The customer's money with every shop in one place: bills (from the counter on a confirmed khata, or from their app
/// orders) and payments on khatas, newest first, for a month or the last 12 months. Only from khatas the person has
/// confirmed with the shop's code, and from their own orders.
/// </summary>
public static class Passbook
{
    public const int MaxItems = 300;

    public static IEndpointRouteBuilder MapPassbook(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/passbook", GetAsync).WithTags("Customer").RequireAuthorization();
        return app;
    }

    private static async Task<PassbookDto> GetAsync(string? month, ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, IOptions<Billing.BillingOptions> options, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var period = IndiaTime.ParseMonth(month);
        var (from, to) = period is { } p ? IndiaTime.Month(p.Year, p.Month) : (clock.GetUtcNow().AddMonths(-12), clock.GetUtcNow().AddDays(1));
        var phone = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.PhoneVerifiedAt != null).Select(u => u.Phone).SingleOrDefaultAsync(ct);
        var khatas = phone is null ? [] : await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Phone == phone && c.PhoneConfirmedAt != null).Select(c => new { c.ShopId, c.Id }).ToListAsync(ct);
        // The person's own order bills (their orders are theirs to read under row security).
        var orderBills = await db.Orders.AsNoTracking().Where(o => o.PlacedByUserId == userId && o.BillId != null)
            .Select(o => new { o.ShopId, BillId = o.BillId!.Value }).ToListAsync(ct);
        var shopIds = khatas.Select(k => k.ShopId).Concat(orderBills.Select(o => o.ShopId)).Distinct().ToList();
        var shops = await db.Shops.AsNoTracking().Where(s => shopIds.Contains(s.Id) && s.IsActive).Select(s => new { s.Id, s.Name, s.Slug }).ToListAsync(ct);
        var key = options.Value.BillLinkKey;
        var items = new List<PassbookItemDto>();
        var shopRows = new List<PassbookShopDto>();
        foreach (var s in shops)
        {
            var customerId = khatas.FirstOrDefault(k => k.ShopId == s.Id)?.Id;
            var mine = orderBills.Where(o => o.ShopId == s.Id).Select(o => o.BillId).ToList();
            var (rows, balance) = await CustomerExtras.ReadAsShopAsync(scopes, s.Id, async shopDb =>
            {
                var bills = await shopDb.Bills.AsNoTracking()
                    .Where(b => b.CancelledAt == null && b.OccurredAt >= from && b.OccurredAt < to && ((customerId != null && b.CustomerId == customerId) || mine.Contains(b.Id)))
                    .OrderByDescending(b => b.OccurredAt).Take(MaxItems)
                    .Select(b => new { b.Id, b.OccurredAt, b.Total, b.PaidAmount, b.Prefix, b.FinancialYear, b.Number, b.CustomerId }).ToListAsync(ct);
                var payments = customerId is null ? [] : await shopDb.LedgerEntries.AsNoTracking()
                    .Where(e => e.CustomerId == customerId && e.Kind == LedgerEntryKind.Payment && e.BillId == null && e.OccurredAt >= from && e.OccurredAt < to
                        && !shopDb.LedgerEntries.Any(r => r.ReversesEntryId == e.Id))
                    .OrderByDescending(e => e.OccurredAt).Take(MaxItems)
                    .Select(e => new { e.OccurredAt, e.Amount, e.PaymentMode, e.UpiRef }).ToListAsync(ct);
                var owed = customerId is null ? (decimal?)null : await shopDb.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.Balance).SingleAsync(ct);
                var list = bills.Select(b => new PassbookItemDto(s.Id, s.Name, s.Slug, b.OccurredAt, "bill", b.Total, b.PaidAmount,
                        b.CustomerId != null && b.CustomerId == customerId ? Math.Max(0, b.Total - b.PaidAmount) : 0m,
                        b.Id, Bill.No(b.Prefix, b.FinancialYear, b.Number), BillLinks.Token(key, b.Id), null, null))
                    .Concat(payments.Select(e => new PassbookItemDto(s.Id, s.Name, s.Slug, e.OccurredAt, "payment", Math.Abs(e.Amount), Math.Abs(e.Amount), 0m, null, null, null, e.PaymentMode, e.UpiRef)))
                    .ToList();
                return (list, owed);
            });
            items.AddRange(rows);
            shopRows.Add(new PassbookShopDto(s.Id, s.Name, s.Slug, balance));
        }

        var shown = items.OrderByDescending(i => i.At).Take(MaxItems).ToList();
        return new PassbookDto(period is null ? null : month, shown, shopRows.OrderByDescending(r => r.Balance ?? 0).ThenBy(r => r.ShopName).ToList(),
            shown.Where(i => i.Kind == "bill").Sum(i => i.Amount), shown.Sum(i => i.Kind == "bill" ? i.Paid : i.Amount));
    }
}
