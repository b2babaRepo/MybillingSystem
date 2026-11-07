using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Billing;

public enum GatewayOrderStatus
{
    Created,
    Paid,
}

/// <summary>A Razorpay order: what was quoted, kept here so a payment can only ever buy what was asked for.</summary>
public sealed class GatewayOrder : Entity
{
    public required string OrderId { get; set; }

    public Guid ShopId { get; set; }

    public required string PlanCode { get; set; }

    public PlanTerm Term { get; set; }

    public decimal Amount { get; set; }

    public string? OfferCode { get; set; }

    public GatewayOrderStatus Status { get; set; }

    public string? PaymentId { get; set; }
}

public sealed record CheckoutRequest(string? PlanCode, PlanTerm? Term, string? OfferCode);

public sealed record CheckoutDto(string OrderId, string KeyId, int AmountPaise);

public sealed record CheckoutVerifyRequest(string? OrderId, string? PaymentId, string? Signature);

/// <summary>
/// Paying a yearly plan online. The server sets the price, and a plan opens only on Razorpay's signature: from the
/// browser's return or from the webhook, whichever comes first (the other changes nothing).
/// </summary>
public static class RazorpayCheckout
{
    public static IEndpointRouteBuilder MapRazorpay(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1/subscription/checkout").WithTags("Subscription").RequireAuthorization(AppPolicies.ShopOwner);
        shop.MapPost("", CreateAsync);
        shop.MapPost("/verify", VerifyAsync);
        app.MapPost("/api/v1/webhooks/razorpay", WebhookAsync).AllowAnonymous().WithTags("Subscription");
        return app;
    }

    public static bool IsOn(BillingOptions o) => !string.IsNullOrWhiteSpace(o.RazorpayKeyId) && !string.IsNullOrWhiteSpace(o.RazorpayKeySecret);

    /// <summary>Lower-case hex HMAC-SHA256, compared in constant time.</summary>
    public static bool SignatureMatches(string payload, string? signature, string secret)
    {
        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
        return signature is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }

    private static async Task<IResult> CreateAsync(
        CheckoutRequest request, AppDbContext db, ITenantContext tenant, BillingService billing, IHttpClientFactory http, IOptions<BillingOptions> options, CancellationToken ct)
    {
        var o = options.Value;
        if (!IsOn(o))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Online payment isn't set up yet.");
        }

        if (request.PlanCode is null || request.Term is null or PlanTerm.Monthly)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Term"] = ["Choose a yearly plan."] });
        }

        var quote = await billing.QuoteAsync(tenant.ShopId, request.PlanCode, request.Term.Value, request.OfferCode, ct);
        if (quote.Error is { } error)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["OfferCode"] = [error] });
        }

        var paise = (int)decimal.Round(quote.Amount * 100);
        var client = http.CreateClient("place");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.RazorpayKeyId}:{o.RazorpayKeySecret}")));
        using var response = await client.PostAsJsonAsync("https://api.razorpay.com/v1/orders", new { amount = paise, currency = "INR", receipt = Guid.CreateVersion7().ToString("N")[..32] }, ct);
        if (!response.IsSuccessStatusCode)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Online payment isn't available right now. Try again or pay by UPI.");
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var orderId = json.RootElement.GetProperty("id").GetString()!;
        db.GatewayOrders.Add(new GatewayOrder
        {
            OrderId = orderId, ShopId = tenant.ShopId, PlanCode = quote.PlanCode, Term = quote.Term, Amount = quote.Amount, OfferCode = quote.OfferCode,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new CheckoutDto(orderId, o.RazorpayKeyId, paise));
    }

    private static async Task<IResult> VerifyAsync(
        CheckoutVerifyRequest request, AppDbContext db, ITenantContext tenant, BillingService billing, IOptions<BillingOptions> options, CancellationToken ct)
    {
        var order = await db.GatewayOrders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == request.OrderId && x.ShopId == tenant.ShopId, ct);
        if (order is null || !IsOn(options.Value) || !SignatureMatches($"{request.OrderId}|{request.PaymentId}", request.Signature, options.Value.RazorpayKeySecret))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This payment couldn't be confirmed.");
        }

        await OpenPlanAsync(order, request.PaymentId!, db, billing, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> WebhookAsync(HttpRequest http, AppDbContext db, BillingService billing, IOptions<BillingOptions> options, CancellationToken ct)
    {
        using var reader = new StreamReader(http.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        var secret = options.Value.RazorpayWebhookSecret;
        if (string.IsNullOrWhiteSpace(secret) || !SignatureMatches(body, http.Headers["X-Razorpay-Signature"], secret))
        {
            return TypedResults.Unauthorized();
        }

        using var json = JsonDocument.Parse(body);
        var kind = json.RootElement.GetProperty("event").GetString();
        if (kind is not ("payment.captured" or "refund.processed"))
        {
            return TypedResults.Ok();
        }

        var payment = json.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
        if (kind == "refund.processed")
        {
            // A full refund in Razorpay takes the paid time back. A part refund is for a person to decide.
            var paymentId = payment.GetProperty("id").GetString();
            if (payment.GetProperty("amount_refunded").GetInt64() >= payment.GetProperty("amount").GetInt64()
                && await db.SubscriptionPayments.AsNoTracking().Where(p => p.Reference == paymentId && p.Method == PaymentMethod.Gateway).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct) is { } refunded)
            {
                await billing.RefundAsync(refunded, ct, fromGateway: true);
            }

            return TypedResults.Ok();
        }

        var orderId = payment.GetProperty("order_id").GetString();
        var order = await db.GatewayOrders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (order is not null && payment.GetProperty("amount").GetInt32() == (int)decimal.Round(order.Amount * 100))
        {
            await OpenPlanAsync(order, payment.GetProperty("id").GetString()!, db, billing, ct);
        }

        return TypedResults.Ok();
    }

    /// <summary>One update decides who records it, so the browser and the webhook can't both add a year.</summary>
    private static async Task OpenPlanAsync(GatewayOrder order, string paymentId, AppDbContext db, BillingService billing, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var claimed = await db.GatewayOrders.Where(x => x.Id == order.Id && x.Status == GatewayOrderStatus.Created)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, GatewayOrderStatus.Paid).SetProperty(x => x.PaymentId, paymentId), ct);
        if (claimed == 1)
        {
            var quote = new Quote(order.PlanCode, order.Term, order.Amount, order.OfferCode, null);
            await billing.RecordAsync(order.ShopId, quote, PaymentMethod.Gateway, paymentId, null, ct);
        }

        await transaction.CommitAsync(ct);
    }
}
