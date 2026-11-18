using GroceryKhata.Api.Catalogue;

namespace GroceryKhata.Api.Ledger;

/// <summary><see cref="Id"/> is made by the app, so a retried or synced-again request finds the same customer.</summary>
public sealed record CustomerRequest(Guid Id, string? Name, string? Phone, bool NameLater = false);

/// <summary>Puts a paid walk-in bill on a customer's khata after the fact; older than a week needs the owner and a reason.</summary>
public sealed record AttachCustomerRequest(Guid CustomerId, string? Reason);

public sealed record CustomerDto(Guid Id, string Name, string? Phone, decimal Balance, decimal? CreditLimit = null, bool NameLater = false, int? CreditDays = null);

/// <summary>
/// The fast path: "Ramesh, ₹340, udhaar" with no items. <see cref="Amount"/> is always positive;
/// <see cref="Type"/> says which way it goes.
/// </summary>
public sealed record KhataEntryRequest(
    Guid Id,
    Guid CustomerId,
    KhataEntryType? Type,
    decimal Amount,
    PaymentMode? PaymentMode,
    string? Note,
    DateTimeOffset? OccurredAt,
    string? UpiRef = null,
    string? ChequeNo = null,
    string? ChequeBank = null,
    DateOnly? ChequeDate = null);

public enum KhataEntryType
{
    /// <summary>Udhaar: the customer took goods and owes more.</summary>
    Credit,

    /// <summary>Jama: the customer paid.</summary>
    Payment,
}

public sealed record ReverseRequest(Guid Id, string? Note);

/// <summary><see cref="OfferId"/>: the planned offer the counter applied to this line, if any.</summary>
public sealed record BillLineRequest(Guid? ProductId, string? Name, decimal Quantity, Unit? Unit, decimal UnitPrice, Guid? SalespersonId = null, Guid? OfferId = null, decimal? Bags = null);

public sealed record BillRequest(
    Guid Id,
    Guid? CustomerId,
    IReadOnlyList<BillLineRequest>? Lines,
    decimal PaidAmount,
    PaymentMode? PaymentMode,
    DateTimeOffset? OccurredAt,
    string? Site = null,
    string? VehicleNumber = null,
    Guid? PainterId = null,
    PrescriptionRequest? Prescription = null,
    IReadOnlyList<Catalogue.BillSerialRequest>? Serials = null,
    Guid? ExchangeNoteId = null,
    string? PlaceOfSupply = null,
    int? EntrySeconds = null,
    // The shop chose a bill without GST (goods with no GST, or a sale the shop doesn't bill under its GSTIN): no tax
    // invoice, no GST worked out, and it stays out of the GST return.
    bool WithoutGst = false,
    // Paid more than one way: each part's mode and amount. They add up to PaidAmount; none: one way, by PaymentMode.
    IReadOnlyList<BillPaymentRequest>? Payments = null,
    // The counter (device) it was made on, as named on that device.
    string? Counter = null,
    // Handed out from this godown rather than the shop shelf: the goods move out of the godown with the bill.
    Guid? GodownId = null,
    // The shop's own extra bill fields as filled in (PO no., doctor…).
    IReadOnlyList<BillFieldValue>? Fields = null);

/// <summary>For Schedule H, H1 and X medicines: who prescribed them and for whom, as the drug rules ask.</summary>
public sealed record PrescriptionRequest(string? DoctorName, string? DoctorAddress, string? DoctorRegNo, string? PatientName, string? PatientAddress, DateOnly? PrescribedOn);

public sealed record LedgerEntryDto(
    Guid Id,
    Guid CustomerId,
    LedgerEntryKind Kind,
    decimal Amount,
    PaymentMode? PaymentMode,
    Guid? BillId,
    Guid? ReversesEntryId,
    string? Note,
    DateTimeOffset OccurredAt)
{
    public static LedgerEntryDto From(LedgerEntry e) =>
        new(e.Id, e.CustomerId, e.Kind, e.Amount, e.PaymentMode, e.BillId, e.ReversesEntryId, e.Note, e.OccurredAt);
}

/// <summary>The entry and the customer's balance after it.</summary>
public sealed record KhataEntryResponse(LedgerEntryDto Entry, decimal CustomerBalance);

public sealed record BillLineDto(Guid? ProductId, string Name, decimal Quantity, Unit Unit, decimal UnitPrice, decimal Amount, decimal? GstRate = null, string? HsnCode = null,
    decimal? TaxableValue = null, decimal? Cgst = null, decimal? Sgst = null, decimal? Igst = null, string? Batch = null, decimal? ListPrice = null, string? PrintName = null, decimal? Bags = null);

public sealed record BillDto(
    Guid Id,
    Guid? CustomerId,
    decimal Total,
    decimal PaidAmount,
    decimal OnKhata,
    PaymentMode? PaymentMode,
    DateTimeOffset OccurredAt,
    IReadOnlyList<BillLineDto> Lines,
    decimal? CustomerBalance,
    string? Site = null,
    string? VehicleNumber = null,
    Guid? PainterId = null,
    string? BillNo = null,
    string? Gstin = null,
    DateTimeOffset? CancelledAt = null,
    string? CancelReason = null,
    string? BuyerGstin = null,
    bool BillOfSupply = false,
    PrescriptionRequest? Prescription = null,
    decimal ExchangeCredit = 0,
    string? ExchangeNoteNo = null,
    string? PlaceOfSupply = null,
    string? ShopAddress = null,
    string? BuyerAddress = null,
    IReadOnlyList<BillPaymentPart>? Payments = null,
    string? Counter = null,
    Shops.BillPrintDto? Print = null,
    // The khata part of the bill is due on this day (the customer's credit days after the bill). Null: no terms.
    DateOnly? DueOn = null,
    // The shop's own extra fields as filled on this bill.
    IReadOnlyList<BillFieldValue>? Fields = null);

public sealed record CancelBillRequest(string? Reason);

public sealed record StatementLine(
    Guid Id,
    LedgerEntryKind Kind,
    decimal Amount,
    PaymentMode? PaymentMode,
    Guid? BillId,
    Guid? ReversesEntryId,
    string? Note,
    DateTimeOffset OccurredAt,
    decimal RunningBalance,
    bool IsReversed,
    BillSummary? Bill = null,
    string? RecordedBy = null,
    string? UpiRef = null,
    DisputeView? Dispute = null,
    bool EnteredLate = false,
    DateTimeOffset? RecordedAt = null,
    string? ChequeNo = null,
    ChequeStatus? ChequeStatus = null);

/// <summary>The latest dispute on an entry, as both sides see it.</summary>
public sealed record DisputeView(
    Guid Id, string Message, DateTimeOffset RaisedAt, string? Resolution, DateTimeOffset? ResolvedAt, IReadOnlyList<DisputeItemView> Items);

/// <summary>One bill item the customer questioned, and what the shop did about it.</summary>
public sealed record DisputeItemView(
    Guid Id,
    Guid BillLineId,
    string Name,
    decimal Quantity,
    Catalogue.Unit Unit,
    decimal UnitPrice,
    ItemProblem Problem,
    decimal? ReceivedQuantity,
    decimal DisputedAmount,
    ItemOutcome? Outcome,
    string? OutcomeNote,
    DateTimeOffset? ResolvedAt);

/// <summary>What was bought on a bill, so the khata shows the items behind each udhaar, not just an amount.</summary>
public sealed record BillSummary(decimal Total, decimal Paid, IReadOnlyList<BillItem> Items, string? Site = null);

public sealed record BillItem(Guid Id, string Name, decimal Quantity, Catalogue.Unit Unit, decimal UnitPrice, decimal Amount);

/// <summary>
/// <see cref="OpeningBalance"/>: owed before the period (previous dues carried forward); 0 for the whole khata.
/// <see cref="ClosingBalance"/>: owed at its end. <see cref="Month"/>: "2026-10", or null for everything.
/// </summary>
public sealed record StatementDto(CustomerDto Customer, IReadOnlyList<StatementLine> Entries, decimal OpeningBalance, decimal ClosingBalance, string? Month);

/// <summary>A bill as printed or shared: its lines, and the customer it was for.</summary>
public sealed record BillView(BillDto Bill, CustomerDto? Customer);

public sealed record BalanceMismatch(Guid CustomerId, string Name, decimal StoredBalance, decimal SumOfEntries);

/// <summary>
/// The outcome of a write. <see cref="Replayed"/> means the ID was already saved, so nothing new was
/// written: the normal result of an offline queue sending the same request twice.
/// </summary>
/// <summary>An item whose stock, stock ledger and batches disagree.</summary>
public sealed record StockMismatch(Guid ProductId, string Name, decimal Stock, decimal Ledger, decimal? Batches);

public sealed record LedgerResult<T>(T? Value, bool Replayed, Dictionary<string, string[]>? Errors, bool NotFound, string? Conflict)
    where T : class
{
    public static LedgerResult<T> Created(T value) => new(value, false, null, false, null);

    public static LedgerResult<T> Replay(T value) => new(value, true, null, false, null);

    public static LedgerResult<T> Invalid(string field, string message) => new(null, false, new() { [field] = [message] }, false, null);

    public static LedgerResult<T> Invalid(Dictionary<string, string[]> errors) => new(null, false, errors, false, null);

    public static LedgerResult<T> Missing() => new(null, false, null, true, null);

    public static LedgerResult<T> Conflicted(string message) => new(null, false, null, false, message);

    public IResult ToHttp(string location) => this switch
    {
        { Value: { } value, Replayed: true } => TypedResults.Ok(value),
        { Value: { } value } => TypedResults.Created(location, value),
        { Errors: { } errors } => TypedResults.ValidationProblem(errors),
        { NotFound: true } => TypedResults.NotFound(),
        _ => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: Conflict),
    };
}
