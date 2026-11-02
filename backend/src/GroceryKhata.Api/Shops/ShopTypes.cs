namespace GroceryKhata.Api.Shops;

/// <summary>Shop types as the app writes them ("water_supply"), for the shop's extra trades kept as text.</summary>
public static class ShopTypes
{
    public static string Name(ShopType type) => string.Concat(type.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    public static IReadOnlyList<ShopType> Parse(IEnumerable<string> names) =>
        names.Select(n => Enum.TryParse<ShopType>(n.Replace("_", ""), ignoreCase: true, out var t) ? (ShopType?)t : null).OfType<ShopType>().ToList();
}
