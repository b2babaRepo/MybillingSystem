using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Planner;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Ledger;

public sealed record BillLinkDto(string ShopSlug, Guid BillId, string Token);

/// <summary>A bill as anyone holding its link sees it. <see cref="CustomerName"/> is all it says of the customer beyond the bill itself.</summary>
public sealed record PublicBillDto(string ShopName, BillDto Bill, string? CustomerName);

/// <summary>
/// A link to one bill, to send on WhatsApp, for the customer to open, print or save as a PDF. A link gets forwarded, so
/// the page shows only what is printed on the bill: never the customer's overall balance, their phone or a prescription.
/// The token is signed from the bill's ID, so nothing is stored and no one can guess another bill's link.
/// </summary>
public static class BillLinks
{
    public static IEndpointRouteBuilder MapBillLinks(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/bills/{id:guid}/link", MakeAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        app.MapGet("/api/v1/bill/{slug}/{id:guid}", ViewAsync).WithTags("Khata").AllowAnonymous().RequireRateLimiting(StoreEndpoints.StoreRateLimit);
        return app;
    }

    /// <summary>"bill:" keeps these signatures apart from the plan bills signed with the same key.</summary>
    public static string Token(string key, Guid billId) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes($"bill:{billId:N}")))[..32].ToLowerInvariant();

    /// <summary>What may leave the shop on a link.</summary>
    public static PublicBillDto Public(string shopName, BillView view) => new(
        shopName, view.Bill with { CustomerBalance = null, Prescription = null, CustomerId = null, PainterId = null }, view.Customer?.Name);

    private static async Task<IResult> MakeAsync(Guid id, LedgerService ledger, ITenantContext tenant, AppDbContext db, IOptions<BillingOptions> options, CancellationToken ct)
    {
        if (await ledger.GetBillAsync(id, ct) is null)
        {
            return TypedResults.NotFound();
        }

        var slug = db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.Slug).Single();
        return TypedResults.Ok(new BillLinkDto(slug, id, Token(options.Value.BillLinkKey, id)));
    }

    /// <summary>A wrong token looks exactly like no such bill.</summary>
    private static async Task<IResult> ViewAsync(
        string slug, Guid id, string? t, AppDbContext db, TenantContext tenant, LedgerService ledger, IOptions<BillingOptions> options, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(t) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(t), Encoding.UTF8.GetBytes(Token(options.Value.BillLinkKey, id)))
            || await StoreEndpoints.OpenShopAsync(db, tenant, slug, ct) is not { } shop || await ledger.GetBillAsync(id, ct) is not { } view)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(Public(shop.Name, view));
    }
}
