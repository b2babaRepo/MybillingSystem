using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Catalogue;

public sealed class Product : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>The category's name, copied from <see cref="CategoryId"/> so screens can group by it; kept in step on rename and merge.</summary>
    public string? Category { get; set; }

    /// <summary>The shop's category this item is in (see <see cref="Catalogue.Category"/>).</summary>
    public Guid? CategoryId { get; set; }

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

    /// <summary>
    /// Bought and sold in bags (bori, katta) of about this much of the item's unit (50 for a 50 kg bag). Stock stays
    /// in the unit; the bags are counted beside it on every movement, so the shop knows both "how many kg" and
    /// "how many sealed bags". Null: not kept in bags.
    /// </summary>
    public decimal? BagSize { get; set; }

    /// <summary>What <see cref="BagSize"/> is a count of when buying: a carton of 24, a strip of 10. Null: a bag.</summary>
    public Unit? PurchaseUnit { get; set; }

    /// <summary>Kilograms in one unit (a piece, a packet, a litre): for the lorry's load and sharing freight by weight. Null: not known.</summary>
    public decimal? WeightKg { get; set; }

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

    /// <summary>
    /// The shop doesn't count this item: it is never "out" or "low", it stays orderable online, and it adds nothing to
    /// stock value. Movements are still recorded, so counting can start again from a stocktake.
    /// </summary>
    public bool Uncounted { get; set; }

    /// <summary>The day the shop marked this item sold out: not on sale online for that day, then back by itself.</summary>
    public DateOnly? OutOn { get; set; }

    /// <summary>Whether the online store offers it today.</summary>
    public bool OnSale(DateOnly today) => OutOn != today && (Uncounted || Stock - Reserved > 0);

    /// <summary>Hidden from billing and lists, kept for history.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>A service (haircut, facial), not goods: selling it moves no stock and it never shows as low.</summary>
    public bool IsService { get; set; }

    /// <summary>How long the service takes, in minutes: used to book appointments.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>Medicines: the drug schedule. H, H1 and X sell only on a prescription, with the doctor and patient on the bill. Null: over the counter.</summary>
    public DrugSchedule? Schedule { get; set; }

    /// <summary>Medicines: tablets in a strip (10, 15). Stock stays in strips; a loose sale takes that fraction of one. Null: sold whole only.</summary>
    public int? UnitsPerPack { get; set; }

    /// <summary>Sales &amp; service: each unit has its own serial number (a pump, an inverter), kept from stock-in to sale for warranty claims.</summary>
    public bool TracksSerials { get; set; }

    /// <summary>The company's warranty on one unit, in months; printed on the bill from the day of sale.</summary>
    public int? WarrantyMonths { get; set; }

    /// <summary>The printed MRP, when the shop sells below it: the store shows it struck through with the % off.</summary>
    public decimal? Mrp { get; set; }

    /// <summary>
    /// The price per <see cref="Unit"/> when a bill takes at least <see cref="WholesaleMinQty"/> (a carton of soap, 10 bags
    /// of cement): the bill switches to it by itself. Null: one price for any quantity.
    /// </summary>
    public decimal? WholesalePrice { get; set; }

    public decimal? WholesaleMinQty { get; set; }

    /// <summary>
    /// The name printed on the customer's bill and receipt instead of <see cref="Name"/> ("Innerwear" for a bra or
    /// bridal nightwear), so a private purchase isn't spelled out. Null: the real name, or the discreet default for
    /// innerwear and nightwear (<see cref="BillNames"/>).
    /// </summary>
    public string? BillName { get; set; }

    /// <summary>
    /// Clothes and similar: items that are one design in several sizes or colours share a <see cref="StyleName"/>
    /// ("पुरुष जींस"), and each is one <see cref="Size"/> ("32") and <see cref="Colour"/> ("Blue"). Each stays its own
    /// item with its own stock and price; the style only groups them on screen. Null for ordinary items.
    /// </summary>
    public string? StyleName { get; set; }

    public string? Size { get; set; }

    public string? Colour { get; set; }

    /// <summary>The pack's barcode (EAN-13 on Indian packs), when the shop has given it.</summary>
    public string? Barcode { get; set; }

    /// <summary>Who the photo is from when it isn't the shop's own ("Open Food Facts (CC BY-SA)"): shown beside it.</summary>
    public string? ImageCredit { get; set; }

    /// <summary>How many times customers said the photo is wrong since the shop last looked, and the last thing they said.</summary>
    public int PhotoReports { get; set; }

    public string? PhotoReportNote { get; set; }

    /// <summary>The master-list item it was picked from: its photo shows here until the shop adds its own.</summary>
    public int? MasterItemId { get; set; }

    public MasterItem? MasterItem { get; set; }

    public CatalogueItem? CatalogueItem { get; set; }
}

public enum DrugSchedule
{
    /// <summary>Sold under a pharmacist; label warning only.</summary>
    G,
    /// <summary>Prescription only.</summary>
    H,
    /// <summary>Prescription only, and every sale goes in the H1 register (kept 3 years): antibiotics like cefixime, some habit-forming drugs.</summary>
    H1,
    /// <summary>Prescription in duplicate (one copy kept 2 years), stock under lock.</summary>
    X,
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
    /// <summary>100 kg.</summary>
    Quintal,
    Carton,
    Bottle,
    Set,
    /// <summary>A strip of tablets or capsules.</summary>
    Strip,
}

public static class Units
{
    /// <summary>Units that only come whole: half a carton or 2.5 pieces is a typing slip.</summary>
    public static bool IsWhole(Unit unit) => unit is Unit.Piece or Unit.Packet or Unit.Bag or Unit.Pair or Unit.Box or Unit.Jar
        or Unit.Carton or Unit.Bottle or Unit.Set or Unit.Strip;

    /// <summary>
    /// Kilograms in a quantity of the item: exact for weight units, from <paramref name="weightKg"/> (kg in one unit) for
    /// the rest, and null when the weight isn't known, so a total never quietly counts pieces as kilograms.
    /// </summary>
    public static decimal? Kg(Unit unit, decimal? weightKg, decimal quantity) => unit switch
    {
        Unit.Kg => quantity,
        Unit.G => quantity / 1000,
        Unit.Quintal => quantity * 100,
        Unit.Tonne => quantity * 1000,
        _ => weightKg is > 0 ? quantity * weightKg : null,
    };
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

    /// <summary>Sealed bags in (+) or out (−) with this movement, beside the weight; null when not counted in bags.</summary>
    public decimal? Bags { get; set; }

    /// <summary>The document behind it, besides a bill: "purchase", "receipt", "challan", "supplier_return", "credit_note", "job", "karigar", "stocktake".</summary>
    public string? RefType { get; set; }

    public Guid? RefId { get; set; }

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

    /// <summary>Used up doing a salon service (colour, developer, wax), by the service's list of products.</summary>
    UsedInService,

    /// <summary>Sent to an outside karigar to work on (a karigar challan).</summary>
    SentToKarigar,

    /// <summary>Back from the karigar.</summary>
    BackFromKarigar,

    /// <summary>A sealed bag opened to sell loose: the bags go down, the weight stays.</summary>
    OpenBag,
}

public enum StockReason
{
    Damage,
    Wastage,
    CountCorrection,
    Return,
    Other,

    // The losses GST rule 56 asks to show in the stock account.
    Lost,
    Stolen,
    Destroyed,
    WrittenOff,
    Gift,
    FreeSample,

    /// <summary>Weight lost in storage or handling (moisture, spillage): stock found lighter than the books.</summary>
    WeightLoss,
}
