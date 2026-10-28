namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One item in the common catalogue every shop and household shares (table catalogue_items, not owned by any
/// shop). Households pick from it to build lists; a shop's own product is matched to it, with the shop's own price,
/// so a list sent to any shop arrives already matched to that shop's items and prices. A product is matched by
/// its name (<see cref="CommonCatalogue.Match"/>), so shops need no extra setup and renaming re-matches.
/// </summary>
public sealed class CatalogueItem
{
    /// <summary>Fixed numbers, kept forever: lists and products in every shop point at them.</summary>
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string NameEn { get; set; }

    public Unit Unit { get; set; }

    public required string Category { get; set; }

    /// <summary>A typical price per unit, a guide only. Each shop sets its own price on its product.</summary>
    public decimal TypicalPrice { get; set; }
}

public static class CommonCatalogue
{
    public static readonly IReadOnlyList<CatalogueItem> Items = Build();

    private static readonly Dictionary<string, CatalogueItem> ByName = Items
        .SelectMany(i => new[] { (Key: i.Name, Item: i), (Key: i.NameEn, Item: i) })
        .GroupBy(p => p.Key.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Item);

    /// <summary>
    /// The catalogue item a shop's product is: the same name (Hindi or English), or a name followed by a brand
    /// ("आटा (Aashirvaad)", "Atta - Pillsbury"). Longest match wins, so "बासमती चावल" is not "चावल".
    /// </summary>
    public static int? Match(string? productName)
    {
        var name = productName?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (ByName.TryGetValue(name, out var exact))
        {
            return exact.Id;
        }

        return ByName.Where(p => name.StartsWith(p.Key + " ", StringComparison.Ordinal) || name.StartsWith(p.Key + "(", StringComparison.Ordinal))
            .OrderByDescending(p => p.Key.Length).Select(p => (int?)p.Value.Id).FirstOrDefault();
    }

    private static List<CatalogueItem> Build()
    {
        const string grains = "आटा, चावल, दाल", sweet = "चीनी, नमक, गुड़", oil = "तेल, घी", spice = "मसाले",
            dairy = "चाय, कॉफ़ी, दूध", snacks = "बिस्कुट, नमकीन", clean = "साफ़-सफ़ाई", home = "घर का सामान";
        // Ids are the row positions: only ever add rows at the end, never reorder or remove.
        (string Hi, string En, Unit Unit, string Category, decimal Price)[] rows =
        [
            ("आटा", "Atta", Unit.Kg, grains, 45), ("चावल", "Rice", Unit.Kg, grains, 50), ("बासमती चावल", "Basmati rice", Unit.Kg, grains, 120),
            ("मैदा", "Maida", Unit.Kg, grains, 40), ("सूजी", "Suji", Unit.Kg, grains, 50), ("बेसन", "Besan", Unit.Kg, grains, 95),
            ("तूर दाल", "Toor dal", Unit.Kg, grains, 160), ("मूंग दाल", "Moong dal", Unit.Kg, grains, 130), ("चना दाल", "Chana dal", Unit.Kg, grains, 95),
            ("उड़द दाल", "Urad dal", Unit.Kg, grains, 140), ("मसूर दाल", "Masoor dal", Unit.Kg, grains, 110), ("राजमा", "Rajma", Unit.Kg, grains, 150),
            ("काबुली चना", "Kabuli chana", Unit.Kg, grains, 140), ("पोहा", "Poha", Unit.Kg, grains, 60),
            ("चीनी", "Sugar", Unit.Kg, sweet, 46), ("गुड़", "Jaggery", Unit.Kg, sweet, 70), ("नमक", "Salt", Unit.Packet, sweet, 28),
            ("सरसों तेल", "Mustard oil", Unit.Litre, oil, 175), ("रिफ़ाइंड तेल", "Refined oil", Unit.Litre, oil, 150),
            ("मूंगफली तेल", "Groundnut oil", Unit.Litre, oil, 210), ("देसी घी", "Desi ghee", Unit.Litre, oil, 620), ("वनस्पति", "Vanaspati", Unit.Kg, oil, 140),
            ("हल्दी पाउडर", "Turmeric powder", Unit.Packet, spice, 35), ("लाल मिर्च पाउडर", "Red chilli powder", Unit.Packet, spice, 45),
            ("धनिया पाउडर", "Coriander powder", Unit.Packet, spice, 35), ("जीरा", "Jeera", Unit.G, spice, 0.6m), ("राई", "Mustard seeds", Unit.G, spice, 0.2m),
            ("गरम मसाला", "Garam masala", Unit.Packet, spice, 60), ("हींग", "Hing", Unit.Packet, spice, 50), ("काली मिर्च", "Black pepper", Unit.G, spice, 1m),
            ("इलायची", "Elaichi", Unit.G, spice, 3.5m), ("मेथी दाना", "Methi seeds", Unit.G, spice, 0.2m),
            ("चायपत्ती", "Tea", Unit.Packet, dairy, 140), ("कॉफ़ी", "Coffee", Unit.Packet, dairy, 180), ("दूध", "Milk", Unit.Litre, dairy, 66),
            ("दही", "Curd", Unit.Kg, dairy, 80), ("पनीर", "Paneer", Unit.G, dairy, 0.45m), ("मक्खन", "Butter", Unit.Packet, dairy, 60),
            ("ब्रेड", "Bread", Unit.Packet, snacks, 45), ("बिस्कुट", "Biscuits", Unit.Packet, snacks, 10), ("नमकीन", "Namkeen", Unit.Packet, snacks, 50),
            ("मैगी", "Maggi", Unit.Packet, snacks, 14), ("अंडे", "Eggs", Unit.Piece, snacks, 7),
            ("नहाने का साबुन", "Bath soap", Unit.Piece, clean, 40), ("कपड़े धोने का साबुन", "Washing soap", Unit.Piece, clean, 30),
            ("सर्फ़ / डिटर्जेंट", "Detergent", Unit.Kg, clean, 120), ("बर्तन धोने का साबुन", "Dishwash bar", Unit.Piece, clean, 20),
            ("फ़िनाइल", "Phenyl", Unit.Litre, clean, 90), ("टूथपेस्ट", "Toothpaste", Unit.Piece, clean, 95), ("शैम्पू", "Shampoo", Unit.Packet, clean, 2),
            ("माचिस", "Matchbox", Unit.Packet, home, 10), ("अगरबत्ती", "Agarbatti", Unit.Packet, home, 50), ("मोमबत्ती", "Candles", Unit.Packet, home, 40),
        ];
        return rows.Select((r, i) => new CatalogueItem { Id = i + 1, Name = r.Hi, NameEn = r.En, Unit = r.Unit, Category = r.Category, TypicalPrice = r.Price }).ToList();
    }
}
