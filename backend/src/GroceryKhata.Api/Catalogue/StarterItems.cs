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
        ShopType.Hardware => Hardware,
        ShopType.Electrical => Electrical,
        ShopType.Woodwork => Woodwork,
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

    // Most hardware and electrical goods are 18%; HSN codes are the usual ones. A CA should check both.
    private static readonly ImportRow[] Hardware =
    [
        Item("Door hinge 4 inch", "Hinges", Unit.Pair, 18, "8302"),
        Item("Tower bolt 6 inch", "Bolts", Unit.Piece, 18, "8302"),
        Item("Padlock", "Locks", Unit.Piece, 18, "8301"),
        Item("Door handle", "Fittings", Unit.Piece, 18, "8302"),
        Item("Wood screw 1 inch", "Screws & nails", Unit.Box, 18, "7318"),
        Item("Wire nails 2 inch", "Screws & nails", Unit.Kg, 18, "7317"),
        Item("PVC pipe 1 inch", "Plumbing", Unit.Metre, 18, "3917"),
        Item("CPVC elbow 1 inch", "Plumbing", Unit.Piece, 18, "3917"),
        Item("Ball valve 1/2 inch", "Plumbing", Unit.Piece, 18, "8481"),
        Item("Water tap", "Plumbing", Unit.Piece, 18, "8481"),
        Item("Hacksaw blade", "Tools", Unit.Piece, 18, "8202"),
        Item("Hammer", "Tools", Unit.Piece, 18, "8205"),
        Item("Fevicol 1 kg", "Adhesives", Unit.Piece, 18, "3506"),
        Item("M-seal", "Adhesives", Unit.Piece, 18, "3214"),
    ];

    // Plywood, boards and timber are 18% since 22 Sep 2025; HSN codes are the usual ones. A CA should check both.
    private static readonly ImportRow[] Woodwork =
    [
        Item("Plywood 6 mm 8x4", "Plywood & boards", Unit.Piece, 18, "4412"),
        Item("Plywood 12 mm 8x4", "Plywood & boards", Unit.Piece, 18, "4412"),
        Item("Plywood 18 mm 8x4", "Plywood & boards", Unit.Piece, 18, "4412"),
        Item("Block board 19 mm 8x4", "Plywood & boards", Unit.Piece, 18, "4412"),
        Item("MDF 12 mm 8x4", "Plywood & boards", Unit.Piece, 18, "4411"),
        Item("Particle board 18 mm 8x4", "Plywood & boards", Unit.Piece, 18, "4410"),
        Item("Laminate 1 mm 8x4", "Laminates & veneer", Unit.Piece, 18, "4823"),
        Item("Veneer", "Laminates & veneer", Unit.SqFt, 18, "4408"),
        Item("Teak (sagwan) wood", "Timber", Unit.Cft, 18, "4407"),
        Item("Sal wood", "Timber", Unit.Cft, 18, "4407"),
        Item("Pine wood", "Timber", Unit.Cft, 18, "4407"),
        Item("Sheesham wood", "Timber", Unit.Cft, 18, "4407"),
        Item("Beading (patti)", "Beading & edging", Unit.RunningFt, 18, "4409"),
        Item("Edge band tape", "Beading & edging", Unit.RunningFt, 18, "3920"),
        Item("Fevicol SH 1 kg", "Adhesives & polish", Unit.Piece, 18, "3506"),
        Item("Wood polish 1 L", "Adhesives & polish", Unit.Piece, 18, "3208"),
        Item("Sofa foam 4 inch", "Upholstery", Unit.Piece, 18, "3921"),
        Item("Sofa fabric", "Upholstery", Unit.Metre, 5, "5407"),
        Item("Hinge 4 inch", "Fittings", Unit.Pair, 18, "8302"),
        Item("Drawer channel 18 inch", "Fittings", Unit.Pair, 18, "8302"),
        Item("Cabinet handle", "Fittings", Unit.Piece, 18, "8302"),
        Item("Wood screw 1 inch (box)", "Fittings", Unit.Box, 18, "7318"),
    ];

    private static readonly ImportRow[] Electrical =
    [
        Item("Wire 1.0 sq mm (90 m coil)", "Wires", Unit.Box, 18, "8544"),
        Item("Wire 1.5 sq mm (90 m coil)", "Wires", Unit.Box, 18, "8544"),
        Item("Wire 2.5 sq mm (90 m coil)", "Wires", Unit.Box, 18, "8544"),
        Item("Wire 1.5 sq mm (per metre)", "Wires", Unit.Metre, 18, "8544"),
        Item("Switch 6 A", "Switches", Unit.Piece, 18, "8536"),
        Item("Socket 6 A", "Switches", Unit.Piece, 18, "8536"),
        Item("Switch board 4 module", "Switches", Unit.Piece, 18, "8538"),
        Item("MCB 16 A", "Protection", Unit.Piece, 18, "8536"),
        Item("LED bulb 9 W", "Lights", Unit.Piece, 18, "8539"),
        Item("LED tube light 20 W", "Lights", Unit.Piece, 18, "8539"),
        Item("Ceiling fan", "Fans", Unit.Piece, 18, "8414"),
        Item("Exhaust fan", "Fans", Unit.Piece, 18, "8414"),
        Item("PVC conduit pipe 20 mm", "Fittings", Unit.Metre, 18, "3917"),
        Item("Insulation tape", "Fittings", Unit.Piece, 18, "3919"),
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
