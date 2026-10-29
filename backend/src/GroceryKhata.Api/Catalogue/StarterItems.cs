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
