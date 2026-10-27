using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Ledger;

public sealed class Customer : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>E.164, optional: many khata customers are known by name only.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// What the customer owes now. A cache of the sum of their ledger entries, changed only in the same
    /// transaction as the entry and checked by reconciliation. Never edited directly.
    /// </summary>
    public decimal Balance { get; set; }
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

    /// <summary>When it happened in the shop (the phone's clock), which may be before an offline sync.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public enum LedgerEntryKind
{
    /// <summary>Goods given on khata (udhaar).</summary>
    Credit,

    /// <summary>Money received (jama).</summary>
    Payment,

    /// <summary>Cancels an earlier entry.</summary>
    Reversal,
}

public enum PaymentMode
{
    Cash,
    Upi,
}

/// <summary>A cash memo. Paid in full, or partly or wholly on the customer's khata.</summary>
public sealed class Bill : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>Null for a walk-in cash sale.</summary>
    public Guid? CustomerId { get; set; }

    public decimal Total { get; set; }

    public decimal PaidAmount { get; set; }

    public PaymentMode? PaymentMode { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public List<BillLine> Lines { get; set; } = [];
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

    public decimal Amount { get; set; }
}
