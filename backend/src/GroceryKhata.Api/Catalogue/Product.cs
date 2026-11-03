using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Catalogue;

public sealed class Product : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>Free text the shop chooses ("Atta & rice", "Oil & ghee"); used to group and filter.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// The common catalogue item this is: set when the shop adds it from the common list, or matched from its name
    /// when created or renamed. Null for the shop's own items (local brands, loose goods).
    /// </summary>
    public int? CatalogueItemId { get; set; }

    /// <summary>
    /// For a packed option sold by the pack: what one pack holds (5 with <see cref="PackUnit"/> kg = a 5 kg bag).
    /// Null for loose goods sold by weight, and for things sold one by one.
    /// </summary>
    public decimal? PackSize { get; set; }

    public Unit? PackUnit { get; set; }

    /// <summary>
    /// The option the shop gives when a household leaves the choice to the shop ("atta, any brand"). One per
    /// common-list item; without one, the option with most stock stands in.
    /// </summary>
    public bool IsDefaultOption { get; set; }

    /// <summary>When the shop last added its photo (<see cref="ProductImage"/>); null: no photo, the app draws one.</summary>
    public DateTimeOffset? ImageAt { get; set; }

    public Unit Unit { get; set; }

    /// <summary>Price per <see cref="Unit"/>. 0 means "not set yet" (items from the starter catalogue).</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>GST % (prices are GST-inclusive, as shops quote them). Null: not set; kirana staples are mostly nil.</summary>
    public decimal? GstRate { get; set; }

    /// <summary>The HSN code for GST bills (cement 2523, sand 2505, TMT bars 7214).</summary>
    public string? HsnCode { get; set; }

    public decimal? CostPrice { get; set; }

    /// <summary>
    /// How much is on the shelf, in <see cref="Unit"/>. A cache of the sum of this product's stock movements,
    /// changed only in the same transaction as the movement. May go negative: the shelf is the truth and a
    /// sale is never blocked; negative stock shows as a warning.
    /// </summary>
    public decimal Stock { get; set; }

    /// <summary>
    /// Held back for online orders not yet delivered. The shelf still has it, but the store shows
    /// <see cref="Stock"/> − <see cref="Reserved"/> as available, so two customers don't buy the last packet.
    /// </summary>
    public decimal Reserved { get; set; }

    /// <summary>At or below this, the item is on the low-stock list. Null: never warn.</summary>
    public decimal? ReorderLevel { get; set; }

    /// <summary>Hidden from billing and lists, kept for history.</summary>
    public bool IsActive { get; set; } = true;
}

public enum Unit
{
    Piece,
    Kg,
    G,
    Litre,
    Ml,
    Packet,
    /// <summary>A sack (cement: 50 kg).</summary>
    Bag,
    Tonne,
    /// <summary>100 cubic feet: how sand and aggregate are sold.</summary>
    Brass,
    Cft,
    Metre,
    Pair,
    Dozen,
    Box,
    /// <summary>A 20 L water jar or can.</summary>
    Jar,
    /// <summary>Square feet: laminates, veneer and sheets quoted by area.</summary>
    SqFt,
    /// <summary>Running feet: beading, patti and edge band.</summary>
    RunningFt,
}

/// <summary>
/// One change to a product's stock. Append-only, like the khata ledger: a wrong entry is fixed by another
/// adjustment, never by editing. <see cref="Quantity"/> is signed: + into the shop, − out of it.
/// </summary>
public sealed class StockMovement : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid ProductId { get; set; }

    public StockMovementKind Kind { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Why an adjustment happened. Required for adjustments, empty otherwise.</summary>
    public StockReason? Reason { get; set; }

    /// <summary>For sales: the bill that sold it.</summary>
    public Guid? BillId { get; set; }

    /// <summary>For purchases: what the shop paid per unit this time.</summary>
    public decimal? CostPrice { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public enum StockMovementKind
{
    /// <summary>What was on the shelf when the item was added.</summary>
    Opening,

    /// <summary>Bought from a supplier.</summary>
    Purchase,

    /// <summary>Sold on a bill (made automatically).</summary>
    Sale,

    /// <summary>Damage, wastage, a stocktake correction, a customer return.</summary>
    Adjustment,

    /// <summary>Taken off the shelf to make something to order (a job): its cost counts on the job's bill.</summary>
    UsedOnJob,

    /// <summary>Sent to an outside karigar to work on (a karigar challan).</summary>
    SentToKarigar,

    /// <summary>Back from the karigar.</summary>
    BackFromKarigar,
}

public enum StockReason
{
    Damage,
    Wastage,
    CountCorrection,
    Return,
    Other,
}
