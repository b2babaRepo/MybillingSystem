using System.Text.Json;

namespace GroceryKhata.Api.Ledger;

/// <summary>One part of what was paid on a bill: so much in cash, so much by UPI.</summary>
public sealed record BillPaymentPart(PaymentMode Mode, decimal Amount, string? Reference = null);

/// <summary>A part of the payment, as the bill screen sends it.</summary>
public sealed record BillPaymentRequest(PaymentMode Mode, decimal Amount, string? Reference = null);

/// <summary>
/// How a bill was paid, part by part. Most bills are paid one way: then the bill's own mode and paid amount say it all
/// and nothing more is stored. A bill paid two ways (₹500 cash and ₹300 UPI) keeps its parts as JSON on the bill;
/// every report that splits money by mode reads them through <see cref="Parts"/>, so each rupee is counted under the
/// mode it really came by.
/// </summary>
public static class BillPayments
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<BillPaymentPart> Parts(PaymentMode? mode, decimal paid, string? payments) =>
        !string.IsNullOrEmpty(payments) ? JsonSerializer.Deserialize<List<BillPaymentPart>>(payments, Json) ?? []
        : paid > 0 ? [new BillPaymentPart(mode ?? PaymentMode.Cash, paid)]
        : [];

    public static IReadOnlyList<BillPaymentPart> Parts(this Bill bill) => Parts(bill.PaymentMode, bill.PaidAmount, bill.Payments);

    /// <summary>What came in by this mode on the bill.</summary>
    public static decimal PaidBy(PaymentMode? mode, decimal paid, string? payments, PaymentMode by) =>
        Parts(mode, paid, payments).Where(p => p.Mode == by).Sum(p => p.Amount);

    public static decimal PaidBy(this Bill bill, PaymentMode by) => PaidBy(bill.PaymentMode, bill.PaidAmount, bill.Payments, by);

    public static string? ToJson(IReadOnlyList<BillPaymentPart> parts) => parts.Count > 1 || parts.Any(p => p.Reference is not null) ? JsonSerializer.Serialize(parts, Json) : null;
}
