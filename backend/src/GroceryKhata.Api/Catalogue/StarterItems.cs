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
        ShopType.Banquet => Banquet,
        ShopType.Restaurant => Restaurant,
        ShopType.Lodge => Lodge,
        ShopType.Medical => Medical,
        _ => [],
    };

    // Most medicines (3003/3004) and devices are 5% from 22 Sep 2025; a few life-saving drugs are nil and some toiletries
    // differ, so the shop checks each rate with its CA. Sold by the strip or bottle; prices are the MRP, filled in later.
    private static readonly ImportRow[] Medical =
    [
        Medicine("Paracetamol 500 mg (strip of 10)", "Fever & pain", null),
        Medicine("Paracetamol 650 mg (strip of 15)", "Fever & pain", null),
        Medicine("Ibuprofen 400 mg (strip of 10)", "Fever & pain", DrugSchedule.H),
        Medicine("Cetirizine 10 mg (strip of 10)", "Cold & allergy", null),
        Item("Cough syrup 100 ml", "Cold & allergy", Unit.Piece, 5, "3004"),
        Item("Antacid gel 170 ml", "Stomach", Unit.Piece, 5, "3004"),
        Medicine("Pantoprazole 40 mg (strip of 15)", "Stomach", DrugSchedule.H),
        Item("ORS sachet", "Stomach", Unit.Packet, 5, "3004"),
        Medicine("Amoxicillin 500 mg (strip of 10)", "Antibiotics", DrugSchedule.H),
        Medicine("Azithromycin 500 mg (strip of 3)", "Antibiotics", DrugSchedule.H),
        Medicine("Metformin 500 mg (strip of 10)", "Diabetes & BP", DrugSchedule.H),
        Medicine("Amlodipine 5 mg (strip of 10)", "Diabetes & BP", DrugSchedule.H),
        Medicine("Cefixime 200 mg (strip of 10)", "Antibiotics", DrugSchedule.H1),
        Medicine("Vitamin D3 60K (strip of 4)", "Vitamins", null),
        Medicine("Multivitamin (strip of 15)", "Vitamins", null),
        Item("Glucometer strips (50)", "Devices", Unit.Packet, 5, "3822"),
        Item("Digital thermometer", "Devices", Unit.Piece, 5, "9025"),
        Item("Bandage roll", "First aid", Unit.Piece, 5, "3005"),
        Item("Antiseptic liquid 100 ml", "First aid", Unit.Piece, 5, "3004"),
        Item("Surgical gloves (pair)", "First aid", Unit.Piece, 5, "4015"),
        Item("Baby diapers (pack)", "Baby care", Unit.Packet, 5, "9619"),
    ];

    private static ImportRow Item(string name, string category, Unit unit, decimal? gst, string? hsn) =>
        new(name, unit, 0, null, category, null, null, GstRate: gst, HsnCode: hsn);

    private static ImportRow Medicine(string name, string category, DrugSchedule? schedule) =>
        new(name, Unit.Piece, 0, null, category, null, null, GstRate: 5, HsnCode: "3004", Schedule: schedule, UnitsPerPack: PerStrip(name));

    /// <summary>"Paracetamol 650 mg (strip of 15)" → 15, so a strip can be sold loose.</summary>
    private static int? PerStrip(string name) =>
        System.Text.RegularExpressions.Regex.Match(name, @"strip of (\d+)") is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;

    private static ImportRow Service(string name, string category, int minutes, string sac) =>
        new(name, Unit.Piece, 0, null, category, null, null, GstRate: 5, HsnCode: sac, IsService: true, DurationMinutes: minutes);

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
        // Services move no stock; minutes are a usual time to book. Services are 5% (no input credit) from 22 Sep 2025.
        Service("Haircut (men)", "Hair", 30, "999721"),
        Service("Haircut (women)", "Hair", 45, "999721"),
        Service("Shave / beard trim", "Hair", 20, "999721"),
        Service("Hair colour", "Hair", 90, "999721"),
        Service("Hair spa", "Hair", 60, "999721"),
        Service("Threading (eyebrows)", "Face", 15, "999722"),
        Service("Facial", "Face", 60, "999722"),
        Service("Clean-up", "Face", 40, "999722"),
        Service("Waxing (full arms)", "Body", 30, "999729"),
        Service("Manicure", "Hands & feet", 40, "999722"),
        Service("Pedicure", "Hands & feet", 45, "999722"),
        Service("Party makeup", "Makeup", 60, "999722"),
        Service("Bridal makeup", "Makeup", 180, "999722"),
        Service("Mehndi (both hands)", "Makeup", 60, "999729"),
        // Products sold over the counter keep their own goods rate: shampoo and hair oil 5%, creams and make-up 18%.
        Item("Shampoo", "Products", Unit.Piece, 5, "3305"),
        Item("Hair oil", "Products", Unit.Piece, 5, "3305"),
        Item("Face cream", "Products", Unit.Piece, 18, "3304"),
        Item("Hair serum", "Products", Unit.Piece, 18, "3305"),
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

    // Extras a hall adds to a function's final bill. GST on hall packages is unsettled (5% or 18%): left for the shop and its CA to set.
    private static readonly ImportRow[] Banquet =
    [
        Item("Decoration package", "Decoration", Unit.Piece, null, null),
        Item("Stage and flower decoration", "Decoration", Unit.Piece, null, null),
        Item("DJ and sound", "Music", Unit.Piece, null, null),
        Item("Generator (per hour)", "Power", Unit.Piece, null, null),
        Item("Room for baraat (per night)", "Rooms", Unit.Piece, null, null),
        Item("Extra plate", "Food", Unit.Piece, null, null),
        Item("Birthday cake (per kg)", "Food", Unit.Kg, null, null),
        Item("Balloon decoration", "Decoration", Unit.Piece, null, null),
        Item("Valet parking", "Services", Unit.Piece, null, null),
        Item("Mineral water 1 L", "Food", Unit.Piece, null, null),
    ];

    // A starting menu. Restaurant GST (usually 5% without input credit) is set by the shop with its CA, so it is left empty here.
    private static readonly ImportRow[] Restaurant =
    [
        Item("Paneer butter masala", "Main course", Unit.Piece, null, null),
        Item("Dal makhani", "Main course", Unit.Piece, null, null),
        Item("Veg biryani", "Rice", Unit.Piece, null, null),
        Item("Jeera rice", "Rice", Unit.Piece, null, null),
        Item("Tandoori roti", "Breads", Unit.Piece, null, null),
        Item("Butter naan", "Breads", Unit.Piece, null, null),
        Item("Masala dosa", "South Indian", Unit.Piece, null, null),
        Item("Veg thali", "Thali", Unit.Piece, null, null),
        Item("Masala chai", "Drinks", Unit.Piece, null, null),
        Item("Cold drink", "Drinks", Unit.Piece, null, null),
        Item("Gulab jamun (2 pc)", "Sweets", Unit.Piece, null, null),
        Item("Birthday cake (per kg)", "Party", Unit.Kg, null, null),
        Item("Party decoration", "Party", Unit.Piece, null, null),
    ];

    // Extras a lodge adds to a room bill. Room GST depends on the night's rate (a CA sets it), so rates are left empty.
    private static readonly ImportRow[] Lodge =
    [
        Item("Extra bed (per night)", "Room extras", Unit.Piece, null, null),
        Item("Early check-in", "Room extras", Unit.Piece, null, null),
        Item("Late check-out", "Room extras", Unit.Piece, null, null),
        Item("Laundry (per piece)", "Services", Unit.Piece, null, null),
        Item("Breakfast", "Food", Unit.Piece, null, null),
        Item("Lunch / dinner thali", "Food", Unit.Piece, null, null),
        Item("Tea / coffee", "Food", Unit.Piece, null, null),
        Item("Mineral water 1 L", "Food", Unit.Piece, null, null),
        Item("Airport / station pickup", "Services", Unit.Piece, null, null),
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
