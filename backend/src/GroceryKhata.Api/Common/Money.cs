namespace GroceryKhata.Api.Common;

/// <summary>Rules for rupee amounts and quantities (numeric(18,2) and numeric(12,3) in the database).</summary>
public static class Money
{
    public const decimal MaxAmount = 10_000_000m;

    public const decimal MaxQuantity = 100_000m;

    /// <summary>A positive amount in rupees with at most two decimals (paise).</summary>
    public static bool IsValidAmount(decimal amount) =>
        amount > 0 && amount <= MaxAmount && decimal.Round(amount, 2) == amount;

    public static bool IsValidQuantity(decimal quantity) =>
        quantity > 0 && quantity <= MaxQuantity && decimal.Round(quantity, 3) == quantity;

    /// <summary>Line amount, rounded half away from zero to the paisa, as shopkeepers round.</summary>
    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
}
