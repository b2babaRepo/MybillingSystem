using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Billing;

/// <summary>A plan payment's bill, as printed or opened from WhatsApp.</summary>
public sealed record InvoiceView(
    string InvoiceNo, DateTimeOffset PaidAt, string SellerName, string? SellerGstin, bool GstRegistered,
    string ShopName, string ShopCode, string? ShopGstin, string? ShopAddress, string? OwnerName, string? OwnerPhone,
    string PlanName, PlanTerm Term, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd,
    decimal Amount, decimal Taxable, decimal Gst, PaymentMethod Method, string? Reference, string? OfferCode, DateTimeOffset? RefundedAt);

/// <summary>
/// Plan bills by link: /bill/{payment}?t={token}. The token is a keyed hash of the payment's ID, so anyone with the link
/// (the owner, from WhatsApp) can open that one bill without signing in, and nobody can guess another.
/// </summary>
public static class Invoices
{
    public static string Token(Guid paymentId, BillingOptions options) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.BillLinkKey), paymentId.ToByteArray()))[..32].ToLowerInvariant();

    /// <summary>The same payments, each with its bill link token.</summary>
    public static List<PaymentDto> WithTokens(IEnumerable<PaymentDto> payments, BillingOptions options) =>
        payments.Select(p => p with { BillToken = Token(p.Id, options) }).ToList();

    public static IEndpointRouteBuilder MapInvoices(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/bills/plan/{id:guid}", GetAsync).AllowAnonymous().WithTags("Plans");
        return app;
    }

    private static async Task<IResult> GetAsync(Guid id, string? t, AppDbContext db, IOptions<BillingOptions> options, CancellationToken ct)
    {
        var o = options.Value;
        if (t is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(t), Encoding.ASCII.GetBytes(Token(id, o))))
        {
            return TypedResults.NotFound();
        }

        var row = await (from p in db.SubscriptionPayments.AsNoTracking()
                         where p.Id == id
                         join s in db.Shops.AsNoTracking() on p.ShopId equals s.Id
                         join plan in db.Plans.AsNoTracking() on p.PlanCode equals plan.Code into plans
                         from plan in plans.DefaultIfEmpty()
                         let owner = (from m in db.Memberships
                                      join u in db.Users on m.UserId equals u.Id
                                      where m.ShopId == s.Id && m.Role == ShopRole.Owner
                                      orderby m.CreatedAt
                                      select u).FirstOrDefault()
                         select new { P = p, s.Name, s.Number, s.Gstin, s.Address, s.Area, PlanName = plan == null ? p.PlanCode : plan.Name, OwnerName = owner == null ? null : owner.FullName, OwnerPhone = owner == null ? null : owner.Phone })
            .SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return TypedResults.NotFound();
        }

        // Prices include GST when Apna Kaarobar is registered: 18% inside the amount.
        var taxable = o.GstRegistered ? decimal.Round(row.P.Amount / 1.18m, 2) : row.P.Amount;
        return TypedResults.Ok(new InvoiceView(
            row.P.InvoiceNo, row.P.PaidAt, o.PayeeName, o.GstRegistered ? o.Gstin : null, o.GstRegistered,
            row.Name, Shop.FormatCode(row.Number), row.Gstin, row.Address ?? row.Area, row.OwnerName, row.OwnerPhone,
            row.PlanName, row.P.Term, row.P.PeriodStart, row.P.PeriodEnd, row.P.Amount, taxable, row.P.Amount - taxable,
            row.P.Method, row.P.Reference, row.P.OfferCode, row.P.RefundedAt));
    }
}
