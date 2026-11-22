namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// Discreet names for the customer's bill: a shop's own <see cref="Product.BillName"/>, else "Innerwear" or
/// "Nightwear" for items in those groups of the clothes list (bras, panties, briefs, honeymoon and bridal nightwear),
/// else none (the bill shows the item's name). The shop's screens always show the real name.
/// </summary>
public static class BillNames
{
    public static string? For(Product? product)
    {
        if (product is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(product.BillName))
        {
            return product.BillName;
        }

        var category = product.Category ?? "";
        return category.Contains("Innerwear", StringComparison.OrdinalIgnoreCase) ? "Innerwear"
            : category.Contains("Nightwear", StringComparison.OrdinalIgnoreCase) ? "Nightwear"
            : null;
    }

    public static string? Clean(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim()[..Math.Min(60, name.Trim().Length)];
}
