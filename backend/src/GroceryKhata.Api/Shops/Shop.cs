using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Shops;

/// <summary>One kirana shop: the tenant. Not tenant-owned itself.</summary>
public sealed class Shop : Entity
{
    public required string Name { get; set; }

    /// <summary>
    /// The shop's registration number, 1, 2, 3… in the order shops open, never reused. Given by the database
    /// (shop_number_seq), so two shops opening at once can't share one.
    /// </summary>
    public long Number { get; set; }

    /// <summary>"GKS-000042": the shop ID quoted on bills, calls and agent sign-ups.</summary>
    public string Code => FormatCode(Number);

    public static string FormatCode(long number) => $"GKS-{number:D6}";

    /// <summary>"GKS-42", "gks000042" or "42" → 42; anything else → null.</summary>
    public static long? ParseCode(string? text)
    {
        var digits = new string((text ?? "").Trim().ToUpperInvariant().Replace("GKS", "").Where(char.IsAsciiDigit).ToArray());
        var rest = (text ?? "").Trim().ToUpperInvariant().Replace("GKS", "").Replace("-", "").Trim();
        return digits.Length is > 0 and <= 12 && digits.Length == rest.Length ? long.Parse(digits) : null;
    }

    /// <summary>Unique, lower case. The storefront address later: /s/{slug}.</summary>
    public required string Slug { get; set; }

    /// <summary>The market or locality households search by ("Raja Park bazaar", "Shastri Nagar").</summary>
    public string? Area { get; set; }

    public string? Address { get; set; }

    /// <summary>The shop's UPI ID (name@bank), put in reminders so customers can pay straight away.</summary>
    public string? UpiId { get; set; }

    /// <summary>
    /// The hash of the shop's bill passcode: when set, cancelling a bill, taking goods back on a credit note, cancelling an
    /// exchange slip or a supplier bill asks for it, whoever is signed in. Null: no passcode.
    /// </summary>
    public string? BillPasscodeHash { get; set; }

    /// <summary>Put before every new bill number ("MB" → MB/2026-27/0001): 1 to 3 capital letters or digits. Null: none.</summary>
    public string? BillPrefix { get; set; }

    /// <summary>The shop's own extra fields on bills ("PO no.", "Doctor", "Delivery date"): a JSON list of up to 4 labels. Null: none.</summary>
    public string? BillFields { get; set; }

    /// <summary>
    /// How the shop's weighing scale prints its label barcodes (13 characters, like 21PPPPPWWWWWC): fixed digits, x any
    /// digit, P the item's code, W the weight in grams or R the price in paise, C the check digit. Null: no scale labels.
    /// </summary>
    public string? ScaleLabel { get; set; }

    /// <summary>
    /// The shop's own WhatsApp reminder wording, with {name}, {amount}, {shop} and {upi} filled in per
    /// customer. Null: the app's default Hinglish text.
    /// </summary>
    public string? ReminderTemplate { get; set; }

    /// <summary>The most a helper (not the owner) may take off an item's regular price on a bill, in percent.</summary>
    public decimal StaffMaxDiscountPercent { get; set; } = 10m;

    /// <summary>The most a helper may write off (damage, loss, a count) at cost in one go; more needs the owner.</summary>
    public decimal StaffMaxWriteOff { get; set; } = 1000m;

    /// <summary>How many days an exchange slip can be used (big chains: Max 90, Westside 180).</summary>
    public int ExchangeSlipDays { get; set; } = 90;

    /// <summary>
    /// Jewellery: the most staff may take off a tagged piece's making charge or wastage on a bill, in percent of it
    /// (10 = making ₹500/g can go down to ₹450/g). Null: no limit. The owner is never limited.
    /// </summary>
    public decimal? JewelleryMaxOffPercent { get; set; }

    /// <summary>Jewellery: show tagged pieces in stock, priced at today's rate, on the shop's online page.</summary>
    public bool JewelleryShowOnline { get; set; }

    /// <summary>A helper can use an exchange slip up to this much; more needs the owner.</summary>
    public decimal ExchangeSlipHelperMax { get; set; } = 5000m;

    /// <summary>False when the shop is closed; its members can no longer sign in to it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>What the shop sells: switches on that trade's units, starter items and bill buttons.</summary>
    public ShopType Type { get; set; } = ShopType.Kirana;

    /// <summary>The number on the shop itself in its market ("Shop 12"), for finding it. Not the GKS shop ID.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string? ShopNo { get; set; }

    public string? State { get; set; }

    /// <summary>The CA's own Tally account names (JSON: key to name), so the export matches their books. Empty: the defaults.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(2000)]
    public string? TallyLedgers { get; set; }

    public string? District { get; set; }

    public string? City { get; set; }

    public string? Village { get; set; }

    /// <summary>The market from the platform's list. Optional: <see cref="Area"/> stays as the shop's own words.</summary>
    public Guid? MarketId { get; set; }

    /// <summary>Where the shop is (GPS). Null: not captured yet.</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>On the composition scheme: bills are bills of supply, with no tax collected.</summary>
    public bool GstComposition { get; set; }

    /// <summary>The books are closed up to this day: nothing dated on or before it can be added, changed or cancelled.</summary>
    public DateOnly? BooksClosedUntil { get; set; }

    /// <summary>The shop sends goods out, so its bills offer delivery. Null: not chosen yet, its trade decides.</summary>
    public bool? DeliversGoods { get; set; }

    /// <summary>Other trades the shop also works in ("kirana" plus "sweets"). The <see cref="Type"/> is its main one.</summary>
    public string[] ExtraTypes { get; set; } = [];

    /// <summary>The shop's GSTIN, printed on tax bills. Null: not registered (bills show no GST split).</summary>
    public string? Gstin { get; set; }

    /// <summary>When a platform admin checked and approved the shop. Null: new, not looked at yet.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Why a platform admin blocked the shop (IsActive false), shown to its people when they try to sign in.</summary>
    public string? BlockedReason { get; set; }

    /// <summary>The platform admins' private support note about this shop. Never shown to the shop.</summary>
    public string? AdminNote { get; set; }

    /// <summary>The bank balance on the passbook on <see cref="BankOpeningDate"/>, where the bank book starts. Null: from ₹0.</summary>
    public decimal? BankOpeningBalance { get; set; }

    public DateOnly? BankOpeningDate { get; set; }

    /// <summary>The bank accounts and which kind of money uses which, as JSON (see <see cref="Ledger.BankSetup"/>). Null: one account.</summary>
    public string? BankAccounts { get; set; }

    /// <summary>Online orders: the delivery charge (null or 0: free), shown on every cart before checkout.</summary>
    public decimal? StoreDeliveryFee { get; set; }

    /// <summary>Online orders at or above this item total deliver free.</summary>
    public decimal? StoreFreeDeliveryAbove { get; set; }

    /// <summary>The smallest item total the shop takes online.</summary>
    public decimal? StoreMinOrder { get; set; }

    /// <summary>The times customers pick from ("Now", "Evening", "Tomorrow morning"), set by the shop.</summary>
    public string[] StoreSlots { get; set; } = [];

    /// <summary>A festive look the owner turns on for the online store ("diwali", "holi", "eid", "wedding"...); null: the usual one.</summary>
    public string? StoreTheme { get; set; }
}

/// <summary>The trade. One app for all: the type only changes defaults (units, starter items, bill buttons).</summary>
public enum ShopType
{
    Kirana,
    /// <summary>Cement, sand, aggregate, bricks, steel.</summary>
    BuildingMaterials,
    Paint,
    Hardware,
    Other,
    /// <summary>20 L cans and jars delivered on rounds.</summary>
    WaterSupply,
    /// <summary>Salon and beauty parlour: services and products.</summary>
    Beauty,
    Sweets,
    Toys,
    Clothes,
    /// <summary>Wires, switches, lights and fans; electricians bring customers.</summary>
    Electrical,
    /// <summary>Plywood, timber, laminates and fittings; carpenters bring customers. Workshops making furniture too.</summary>
    Woodwork,
    /// <summary>Banquet hall, marriage garden or party hall: books dates for weddings, birthdays and anniversaries.</summary>
    Banquet,
    /// <summary>Restaurant, cafe or dhaba: a menu, table bookings, birthday and anniversary parties.</summary>
    Restaurant,
    /// <summary>Hotel, lodge, guest house or dharamshala: rooms booked by the night.</summary>
    Lodge,
    /// <summary>Medical shop or chemist: medicines by batch and expiry, a pharmacist, a drug licence and the Schedule H1 register.</summary>
    Medical,
    /// <summary>Village milk collection centre: buys milk from farmers by fat and SNF, sells it on to Sudha or another dairy.</summary>
    Dairy,
    /// <summary>Sales and service: sells pumps, motors, inverters, batteries, fans, ACs or solar by serial number, and installs and repairs them.</summary>
    SalesService,
    /// <summary>Jewellery shop (sarafa): gold and silver sold by weight at the day's rate, tagged pieces, old gold taken in.</summary>
    Jewellery,
}
