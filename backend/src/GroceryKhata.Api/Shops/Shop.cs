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
    /// The shop's own WhatsApp reminder wording, with {name}, {amount}, {shop} and {upi} filled in per
    /// customer. Null: the app's default Hinglish text.
    /// </summary>
    public string? ReminderTemplate { get; set; }

    /// <summary>False when the shop is closed; its members can no longer sign in to it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>What the shop sells: switches on that trade's units, starter items and bill buttons.</summary>
    public ShopType Type { get; set; } = ShopType.Kirana;

    /// <summary>The number on the shop itself in its market ("Shop 12"), for finding it. Not the GKS shop ID.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(30)]
    public string? ShopNo { get; set; }

    public string? State { get; set; }

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
}
