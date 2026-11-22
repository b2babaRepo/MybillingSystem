using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Ledger;

public sealed class Customer : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Udhaar to someone who gave no name yet: a placeholder ("Name later 3") the owner names, once known. Kept apart on
    /// Home and the day book so it gets followed up, and refused on bills of ₹50,000 or more (GST needs the buyer then).
    /// </summary>
    public bool NameLater { get; set; }

    /// <summary>E.164, optional: many khata customers are known by name only.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// What the customer owes now. A cache of the sum of their ledger entries, changed only in the same
    /// transaction as the entry and checked by reconciliation. Never edited directly.
    /// </summary>
    /// <summary>The most this customer may owe; a bill that would cross it warns the shop (it doesn't block). Null: none.</summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>The shop lets this customer put app orders on their khata (up to <see cref="CreditLimit"/>).</summary>
    public bool OnlineKhata { get; set; }

    /// <summary>Days the customer has to pay for what they take on credit: each udhaar falls due that many days later. Null: no terms.</summary>
    public int? CreditDays { get; set; }

    /// <summary>The day the customer was last reminded in the app that their udhaar falls due (one a day at most).</summary>
    public DateOnly? LastDueReminderOn { get; set; }

    /// <summary>The state an unregistered customer lives in (GST code, "24"): a bill to them is offered there.</summary>
    public string? State { get; set; }

    public decimal Balance { get; set; }

    /// <summary>When the shop last sent this customer a payment reminder (shown on the dues list).</summary>
    public DateTimeOffset? LastRemindedAt { get; set; }

    /// <summary>
    /// The secret in the customer's read-only khata link (/k/{shop}/{customer}?t=...). Kept so the shop can share
    /// the same link again; replacing it cuts off the old link. Null until the shop first shares one.
    /// </summary>
    public string? KhataToken { get; set; }

    /// <summary>When <see cref="KhataToken"/> was made: a link works for <c>KhataViewEndpoints.LinkLife</c> after it.</summary>
    public DateTimeOffset? KhataTokenAt { get; set; }

    /// <summary>
    /// When the person with this phone proved the khata is theirs, with a code the shop gave them. Until then a typed
    /// number proves nothing: one wrong digit would show this khata to a stranger who signs in with that number.
    /// </summary>
    public DateTimeOffset? PhoneConfirmedAt { get; set; }

    /// <summary>The shop's current code for that step, hashed. Null when none is waiting.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(64)]
    public string? ConfirmCodeHash { get; set; }

    public DateTimeOffset? ConfirmCodeAt { get; set; }

    /// <summary>Wrong tries at the current code. Too many and the code stops.</summary>
    public int ConfirmCodeTries { get; set; }

    /// <summary>A business customer's GSTIN, printed on their tax invoices; its first two digits are their state.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(15)]
    public string? Gstin { get; set; }

    /// <summary>When the shop recorded that this customer agreed to reminders, on this number. Null: no reminders. History: CustomerConsent.</summary>
    public DateTimeOffset? ReminderConsentAt { get; set; }

    /// <summary>The customer's address, printed on their bills: a registered buyer's tax invoice must show it.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Address { get; set; }
}

/// <summary>
/// One line in a customer's khata. Append-only: never updated or deleted; mistakes are fixed with a reversal.
/// <see cref="Amount"/> is signed: positive means the customer owes more, negative means they paid.
/// </summary>
public sealed class LedgerEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public LedgerEntryKind Kind { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Set for payments: keeps the cash book honest.</summary>
    public PaymentMode? PaymentMode { get; set; }

    public Guid? BillId { get; set; }

    /// <summary>For a reversal, the entry it cancels. Unique: an entry can be reversed once.</summary>
    public Guid? ReversesEntryId { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// The bank's reference (UTR) for a UPI payment. The bank holds the proof, so a payment with one can never be
    /// removed, and the same reference can't be recorded twice.
    /// </summary>
    public string? UpiRef { get; set; }

    /// <summary>A cheque payment's number, bank and date (a post-dated one carries a later date).</summary>
    public string? ChequeNo { get; set; }

    public string? ChequeBank { get; set; }

    public DateOnly? ChequeDate { get; set; }

    /// <summary>Pending until the owner marks it cleared or bounced. A reversal carries its cheque's status.</summary>
    public ChequeStatus? ChequeStatus { get; set; }

    public DateTimeOffset? ChequeSettledAt { get; set; }

    /// <summary>When it happened in the shop (the phone's clock), which may be before an offline sync.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>A payment collected by a technician on a field trip: the cash reaches the drawer when they hand it in.</summary>
    public Guid? TripId { get; set; }
}

public enum LedgerEntryKind
{
    /// <summary>Goods given on khata (udhaar).</summary>
    Credit,

    /// <summary>Money received (jama).</summary>
    Payment,

    /// <summary>Cancels an earlier entry.</summary>
    Reversal,

    /// <summary>
    /// Goods taken off a bill after the fact (a customer never got them): lowers what they owe. Not money
    /// received, so it has no payment mode and stays out of the cash book.
    /// </summary>
    Return,

    /// <summary>
    /// What a customer already owed when the shop moved to the app (below zero: an advance). Part of the balance, but
    /// not new udhaar or a sale on any day. Its reversal keeps this kind, so it stays out of those figures too.
    /// </summary>
    Opening,
}

public enum PaymentMode
{
    Cash,
    Upi,

    /// <summary>A bank transfer: paying suppliers only, and never out of the drawer.</summary>
    Bank,

    /// <summary>A debit or credit card on the shop's machine: money that reaches the bank, not the drawer.</summary>
    Card,

    /// <summary>
    /// A customer's cheque against their khata. It lowers what they owe when handed over, but counts as money received
    /// only once it clears; a bounce puts the amount back on the khata.
    /// </summary>
    Cheque,
}

public enum ChequeStatus
{
    Pending,
    Cleared,
    Bounced,
}

/// <summary>A cash memo. Paid in full, or partly or wholly on the customer's khata.</summary>
public sealed class Bill : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>Null for a walk-in cash sale.</summary>
    public Guid? CustomerId { get; set; }

    public decimal Total { get; set; }

    /// <summary>
    /// The bill's serial number in its financial year (April to March), 1, 2, 3… with no gaps: a missing number would
    /// show a bill was removed. Given by the server when the bill is saved, in the order bills reach it.
    /// </summary>
    public int Number { get; set; }

    /// <summary>"2026-27".</summary>
    public string FinancialYear { get; set; } = "";

    /// <summary>
    /// The shop's bill-number prefix when this bill was made ("MB"), kept on the bill so a later change of prefix never
    /// renumbers bills already given. Null: none. The numbering (1, 2, 3… in the year) is the same with or without it.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>"2026-27/0457" or "MB/2026-27/0457", as printed.</summary>
    public string BillNo => No(Prefix, FinancialYear, Number);

    /// <summary>A bill number as printed: at most 16 characters, as GST invoices must be (a prefix is at most 3).</summary>
    public static string No(string? prefix, string year, int number) => prefix is null ? $"{year}/{number:D4}" : $"{prefix}/{year}/{number:D4}";

    /// <summary>The shop's own extra bill fields as filled on this bill (PO no., doctor…): JSON [{label, value}]. Null: none.</summary>
    public string? Fields { get; set; }

    public decimal PaidAmount { get; set; }

    /// <summary>An exchange: the credit note whose returned goods pay part of this bill, and how much of it this bill used.</summary>
    public Guid? ExchangeNoteId { get; set; }

    public decimal ExchangeCredit { get; set; }

    /// <summary>A bill paid more than one way: its parts as JSON (see <see cref="BillPayments"/>). Null: one way, by <see cref="PaymentMode"/>.</summary>
    public string? Payments { get; set; }

    public PaymentMode? PaymentMode { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public List<BillLine> Lines { get; set; } = [];

    /// <summary>The contractor's site this went to ("Mansarovar plot 12"): a customer's khata adds up per site.</summary>
    public string? Site { get; set; }

    /// <summary>The truck or tempo that carried it; goods over ₹50,000 moved by road need an e-way bill.</summary>
    public string? VehicleNumber { get; set; }

    /// <summary>The counter (device) that made the bill, as the shop named it on that device ("Counter 2"); null: unnamed.</summary>
    public string? Counter { get; set; }

    /// <summary>The painter who brought the customer (paint shops): their commission is counted from these bills.</summary>
    public Guid? PainterId { get; set; }

    /// <summary>The shop's GSTIN when billed (kept, so a later change doesn't rewrite old bills). Null: not registered then.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(15)]
    public string? Gstin { get; set; }

    /// <summary>Set when the bill is cancelled. It keeps its number (no gaps) but no longer counts as a sale.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? CancelReason { get; set; }

    public Guid? CancelledByUserId { get; set; }

    /// <summary>The buyer's GSTIN when billed (B2B). Null: an ordinary customer.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(15)]
    public string? BuyerGstin { get; set; }

    /// <summary>A composition shop's bill: a bill of supply, no tax on it.</summary>
    public bool BillOfSupply { get; set; }

    /// <summary>Seconds from the first item to saving, as the counter timed it. Null: not timed.</summary>
    public int? EntrySeconds { get; set; }

    /// <summary>Place of supply as a two-digit GST state code: the buyer's GSTIN state, else the state delivered to, else the shop's.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(2)]
    public string? PlaceOfSupply { get; set; }
}

public sealed class BillLine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BillId { get; set; }

    /// <summary>1, 2, 3… in the order the items were added: lines saved together share a timestamp.</summary>
    public int LineNo { get; set; }

    /// <summary>Null for a loose item typed on the spot.</summary>
    public Guid? ProductId { get; set; }

    public required string Name { get; set; }

    public decimal Quantity { get; set; }

    public Unit Unit { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>The item's regular price when this line was sold below it (a discount); null at the regular price or above.</summary>
    public decimal? ListPrice { get; set; }

    /// <summary>The planned offer this line was sold under, and its name as it was, for the bill and the offer's report.</summary>
    public Guid? OfferId { get; set; }

    public string? OfferName { get; set; }

    /// <summary>What the customer's bill shows instead of <see cref="Name"/> ("Innerwear"); null shows the name.</summary>
    public string? PrintName { get; set; }

    /// <summary>Who sold this line (a person with access to the shop), for staff sales and commission.</summary>
    public Guid? SalespersonId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The item's GST % and HSN code when sold (kept, so a later change doesn't rewrite old bills).</summary>
    public decimal? GstRate { get; set; }

    public string? HsnCode { get; set; }

    /// <summary>The item's cost when sold, so a later cost change doesn't rewrite past profit. Null: not known.</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Sealed bags sold on this line (a 50 kg bag sold whole), beside the weight.</summary>
    public decimal? Bags { get; set; }

    /// <summary>The batches it came from, for medicines: "B123 exp 05/27". Printed on the bill, as the drug rules ask.</summary>
    public string? Batch { get; set; }

    /// <summary>The tax worked out when billed (prices include GST): value without tax, and the tax by head. Null: no tax on this line.</summary>
    public decimal? TaxableValue { get; set; }

    public decimal? Cgst { get; set; }

    public decimal? Sgst { get; set; }

    public decimal? Igst { get; set; }
}
