using System.Security.Claims;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>One earlier price: what it was, when, and on which bill (sales) or from whom (purchases).</summary>
public sealed record PastPrice(decimal Price, decimal Quantity, DateTimeOffset At, string? Reference);

/// <summary>
/// <see cref="Customer"/>: what this customer paid on their last bills (empty without a customer). <see cref="Anyone"/>:
/// the item's last sale prices to anyone. <see cref="Bought"/>: what the shop last paid suppliers, owner only (null for helpers).
/// </summary>
public sealed record LastPricesDto(IReadOnlyList<PastPrice> Customer, IReadOnlyList<PastPrice> Anyone, IReadOnlyList<PastPrice>? Bought);

/// <summary>
/// "What did I charge him last time?" while billing: the last five prices of an item to this customer and to anyone,
/// and, for the owner, its last five purchase costs. Cancelled bills don't count.
/// </summary>
public static class LastPrices
{
    public const int Count = 5;

    public static IEndpointRouteBuilder MapLastPrices(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/products/{id:guid}/last-prices", GetAsync).WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<IResult> GetAsync(Guid id, Guid? customerId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var sold = db.BillLines.AsNoTracking().Where(l => l.ProductId == id)
            .Join(db.Bills.AsNoTracking().Where(b => b.CancelledAt == null), l => l.BillId, b => b.Id, (l, b) => new { l.UnitPrice, l.Quantity, b.OccurredAt, b.Prefix, b.FinancialYear, b.Number, b.CustomerId });

        var customer = customerId is { } cid
            ? (await sold.Where(s => s.CustomerId == cid).OrderByDescending(s => s.OccurredAt).Take(Count).ToListAsync(ct))
                .Select(s => new PastPrice(s.UnitPrice, s.Quantity, s.OccurredAt, Bill.No(s.Prefix, s.FinancialYear, s.Number))).ToList()
            : [];
        var anyone = (await sold.OrderByDescending(s => s.OccurredAt).Take(Count).ToListAsync(ct))
            .Select(s => new PastPrice(s.UnitPrice, s.Quantity, s.OccurredAt, Bill.No(s.Prefix, s.FinancialYear, s.Number))).ToList();

        List<PastPrice>? bought = null;
        if (LedgerEndpoints.IsOwner(user))
        {
            bought = await db.PurchaseLines.AsNoTracking().Where(l => l.ProductId == id)
                .Join(db.Purchases.AsNoTracking(), l => l.PurchaseId, p => p.Id, (l, p) => new { l.UnitCost, l.Quantity, p.OccurredAt, p.SupplierId })
                .Join(db.Suppliers.AsNoTracking(), x => x.SupplierId, s => s.Id, (x, s) => new { x.UnitCost, x.Quantity, x.OccurredAt, s.Name })
                .OrderByDescending(x => x.OccurredAt).Take(Count)
                .Select(x => new PastPrice(x.UnitCost, x.Quantity, x.OccurredAt, x.Name)).ToListAsync(ct);
        }

        return TypedResults.Ok(new LastPricesDto(customer, anyone, bought));
    }
}
