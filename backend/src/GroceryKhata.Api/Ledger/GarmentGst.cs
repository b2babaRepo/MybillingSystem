namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Clothes and footwear GST depends on the price of each piece (from 22 Sep 2025): 5% when a piece sells at up to
/// ₹2,500 before GST, 18% above. Prices in the app include GST, so the test is the piece price with 5% taken out.
/// Applies to garments (HSN chapters 61 and 62) and footwear (64) whose item rate is one of the garment slabs; any
/// other rate (an exempt item, a rate the shop set on purpose such as 0%) is left as it is. A CA should confirm the
/// rule for packs and sets sold as one line.
/// </summary>
public static class GarmentGst
{
    public const decimal Threshold = 2500m;

    public static bool IsGarment(string? hsnCode) =>
        hsnCode is { Length: >= 2 } h && (h.StartsWith("61", StringComparison.Ordinal) || h.StartsWith("62", StringComparison.Ordinal) || h.StartsWith("64", StringComparison.Ordinal));

    /// <summary>The rate for one bill line: the piece rule for garments, else the item's own rate.</summary>
    public static decimal? RateFor(string? hsnCode, decimal? itemRate, decimal unitPrice)
    {
        if (!IsGarment(hsnCode) || itemRate is not (5m or 12m or 18m))
        {
            return itemRate;
        }

        return unitPrice / 1.05m <= Threshold ? 5m : 18m;
    }
}
