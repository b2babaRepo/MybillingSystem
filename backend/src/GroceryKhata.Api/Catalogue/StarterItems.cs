using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// The usual items of each trade, so a new cement or paint shop doesn't start from an empty list. Prices start at 0
/// and are filled in on the "today's rates" screen. GST rates are those in force from 22 Sep 2025 (cement and paint
/// 18%, sand and aggregate 5%); bricks are left for the shop to set, as sources disagree on their current rate.
/// </summary>
public static class StarterItems
{
    public static IReadOnlyList<ImportRow> For(ShopType type) => type switch
    {
        ShopType.BuildingMaterials => BuildingMaterials,
        ShopType.Paint => Paint,
        ShopType.WaterSupply => Water,
        ShopType.Beauty => Beauty,
        ShopType.Sweets => Sweets,
        ShopType.Toys => Toys,
        ShopType.Clothes => Clothes,
        _ => [],
    };

    private static ImportRow Item(string name, string category, Unit unit, decimal? gst, string? hsn) =>
        new(name, unit, 0, null, category, null, null, GstRate: gst, HsnCode: hsn);

    private static readonly ImportRow[] BuildingMaterials =
    [
        Item("Cement OPC 53 (50 kg)", "Cement", Unit.Bag, 18, "2523"),
        Item("Cement PPC (50 kg)", "Cement", Unit.Bag, 18, "2523"),
        Item("White cement", "Cement", Unit.Kg, 18, "2523"),
        Item("River sand", "Sand & aggregate", Unit.Brass, 5, "2505"),
        Item("M-sand", "Sand & aggregate", Unit.Brass, 5, "2505"),
        Item("Aggregate 20 mm (gitti)", "Sand & aggregate", Unit.Brass, 5, "2517"),
        Item("Aggregate 10 mm (gitti)", "Sand & aggregate", Unit.Brass, 5, "2517"),
        Item("Red bricks", "Bricks", Unit.Piece, null, "6901"),
        Item("Fly ash bricks", "Bricks", Unit.Piece, null, "6810"),
        Item("TMT bar 8 mm", "Steel", Unit.Kg, 18, "7214"),
        Item("TMT bar 10 mm", "Steel", Unit.Kg, 18, "7214"),
        Item("TMT bar 12 mm", "Steel", Unit.Kg, 18, "7214"),
        Item("TMT bar 16 mm", "Steel", Unit.Kg, 18, "7214"),
        Item("Binding wire", "Steel", Unit.Kg, 18, "7217"),
    ];

    // Rates from the September 2025 change where sources agreed; null where they didn't (the shop sets it). A CA should check them.
    private static readonly ImportRow[] Kirana =
    [
        Item("Atta 5 kg", "Atta & rice", Unit.Packet, null, "1101"),
        Item("Rice (loose)", "Atta & rice", Unit.Kg, null, "1006"),
        Item("Toor dal", "Dal", Unit.Kg, null, "0713"),
        Item("Sugar", "Basics", Unit.Kg, null, "1701"),
        Item("Salt 1 kg", "Basics", Unit.Packet, null, "2501"),
        Item("Mustard oil 1 L", "Oil & ghee", Unit.Piece, null, "1514"),
        Item("Tea 250 g", "Tea & coffee", Unit.Packet, null, "0902"),
        Item("Biscuits", "Snacks", Unit.Packet, null, "1905"),
        Item("Soap", "Personal care", Unit.Piece, null, "3401"),
        Item("Detergent 1 kg", "Cleaning", Unit.Packet, null, "3402"),
    ];

    private static readonly ImportRow[] Water =
    [
        Item("Water jar 20 L (refill)", "Water", Unit.Jar, 5, "2201"),
        Item("Water jar 20 L (new jar)", "Jars", Unit.Jar, null, "3923"),
        Item("Jar deposit", "Deposit", Unit.Jar, null, null),
        Item("Water bottle 1 L", "Bottles", Unit.Piece, 5, "2201"),
        Item("Water bottle 500 ml", "Bottles", Unit.Piece, 5, "2201"),
        Item("Dispenser pump", "Accessories", Unit.Piece, null, null),
    ];

    private static readonly ImportRow[] Beauty =
    [
        Item("Haircut", "Services", Unit.Piece, 5, "999721"),
        Item("Hair colour", "Services", Unit.Piece, 5, "999721"),
        Item("Threading", "Services", Unit.Piece, 5, "999722"),
        Item("Facial", "Services", Unit.Piece, 5, "999722"),
        Item("Manicure", "Services", Unit.Piece, 5, "999722"),
        Item("Pedicure", "Services", Unit.Piece, 5, "999722"),
        Item("Bridal makeup", "Services", Unit.Piece, 5, "999729"),
        Item("Shampoo", "Products", Unit.Piece, null, "3305"),
        Item("Face cream", "Products", Unit.Piece, null, "3304"),
    ];

    private static readonly ImportRow[] Sweets =
    [
        Item("Kaju katli", "Mithai", Unit.Kg, 5, "2106"),
        Item("Rasgulla", "Mithai", Unit.Kg, 5, "2106"),
        Item("Gulab jamun", "Mithai", Unit.Kg, 5, "2106"),
        Item("Laddu", "Mithai", Unit.Kg, 5, "2106"),
        Item("Barfi", "Mithai", Unit.Kg, 5, "2106"),
        Item("Namkeen (bhujia)", "Namkeen", Unit.Kg, 5, "2106"),
        Item("Mixture", "Namkeen", Unit.Kg, 5, "2106"),
        Item("Samosa", "Snacks", Unit.Piece, null, null),
        Item("Sweet box (1 kg)", "Packing", Unit.Box, null, null),
    ];

    private static readonly ImportRow[] Toys =
    [
        Item("Soft toy", "Soft toys", Unit.Piece, 5, "9503"),
        Item("Wooden puzzle", "Puzzles", Unit.Piece, 5, "9503"),
        Item("Toy car", "Vehicles", Unit.Piece, null, "9503"),
        Item("Doll", "Dolls", Unit.Piece, null, "9503"),
        Item("Building blocks", "Blocks", Unit.Box, null, "9503"),
        Item("Board game", "Games", Unit.Box, null, "9504"),
        Item("Ball", "Sports", Unit.Piece, null, "9506"),
    ];

    // Garments: 5% when sold at up to ₹2,500 a piece, 18% above (set per item by its price).
    private static readonly ImportRow[] Clothes =
    [
        Item("Men's shirt", "Men", Unit.Piece, 5, "6205"),
        Item("Men's trousers", "Men", Unit.Piece, 5, "6203"),
        Item("T-shirt", "Men", Unit.Piece, 5, "6109"),
        Item("Kurti", "Women", Unit.Piece, 5, "6204"),
        Item("Saree", "Women", Unit.Piece, 5, "5407"),
        Item("Kids' frock", "Kids", Unit.Piece, 5, "6209"),
        Item("Socks", "Accessories", Unit.Pair, 5, "6115"),
        Item("Handkerchief", "Accessories", Unit.Dozen, 5, "6213"),
    ];

    private static readonly ImportRow[] Paint =
    [
        Item("Interior emulsion 1 L", "Interior", Unit.Piece, 18, "3209"),
        Item("Interior emulsion 4 L", "Interior", Unit.Piece, 18, "3209"),
        Item("Interior emulsion 10 L", "Interior", Unit.Piece, 18, "3209"),
        Item("Interior emulsion 20 L", "Interior", Unit.Piece, 18, "3209"),
        Item("Exterior emulsion 4 L", "Exterior", Unit.Piece, 18, "3209"),
        Item("Exterior emulsion 20 L", "Exterior", Unit.Piece, 18, "3209"),
        Item("Primer 4 L", "Primer & putty", Unit.Piece, 18, "3209"),
        Item("Wall putty (40 kg)", "Primer & putty", Unit.Bag, 18, "3214"),
        Item("Enamel 1 L", "Enamel", Unit.Piece, 18, "3208"),
        Item("Distemper 10 kg", "Interior", Unit.Piece, 18, "3210"),
    ];
}
