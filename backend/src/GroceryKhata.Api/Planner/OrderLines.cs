using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// Options on order lines. A household either picks the exact option at the chosen shop ("Aashirvaad 5 kg") or
/// leaves it to the shop ("atta 5 kg, any brand"), and says what the shop may do: choose, ask first, or not replace.
/// The shop packs any of its options; the bill takes the packed option's price. A line waits for the household's
/// OK when it isn't what they asked for (and they said "ask me"), or when its price moved more than
/// <see cref="PriceTolerance"/> from what they saw when sending.
/// </summary>
public static class OrderLines
{
    /// <summary>A price change the household accepts without being asked: 10% of the line, or ₹20, whichever is more.</summary>
    public static decimal PriceTolerance(decimal expected) => Math.Max(expected * 0.10m, 20m);

    internal static OrderLine FromListItem(
        ShoppingListItem item, LineChoice? choice, IReadOnlyList<Product> products, IReadOnlyDictionary<int, Product> defaults, Guid userId, int lineNo)
    {
        var itemId = item.CatalogueItemId ?? CommonCatalogue.Match(item.Name);
        var picked = choice?.ProductId is { } wanted
            ? products.SingleOrDefault(p => p.Id == wanted && (p.CatalogueItemId ?? CommonCatalogue.Match(p.Name)) == itemId)
            : null;
        var offered = picked ?? (itemId is { } id ? defaults.GetValueOrDefault(id) : null);
        var quantity = offered is null ? null : QuantityIn(item.Quantity, item.Unit, offered);
        if (picked is not null && quantity is null)
        {
            quantity = 1; // a pack size the list's unit can't be converted to: one pack, the shop can change it
        }

        var line = new OrderLine
        {
            PlacedByUserId = userId,
            LineNo = lineNo,
            Name = item.Name,
            Quantity = item.Quantity,
            Unit = item.Unit,
            Note = item.Note,
            AiConfidence = item.AiConfidence,
            RequestedName = $"{item.Name} {item.Quantity:0.###} {item.Unit.ToString().ToLowerInvariant()}",
            Preference = picked is null && choice?.Preference == ReplacePreference.DontReplace
                ? ReplacePreference.ShopChooses // "only this" needs a "this": without a pick it means the shop chooses
                : choice?.Preference ?? ReplacePreference.ShopChooses,
            RequestedProductId = picked?.Id,
        };
        if (offered is not null && quantity is { } q)
        {
            line.ProductId = offered.Id;
            line.Name = offered.Name;
            line.Unit = offered.Unit;
            line.Quantity = q;
            line.UnitPrice = offered.SellingPrice;
            line.ExpectedAmount = Money.LineAmount(q, offered.SellingPrice);
        }

        return line;
    }

    /// <summary>
    /// How much of <paramref name="option"/> covers what was asked: the same unit as is, or whole packs (5 kg asked,
    /// 1 kg packs: 5 packets). Null when the units don't convert (litres asked, sold by the kg).
    /// </summary>
    public static decimal? QuantityIn(decimal quantity, Unit unit, Product option)
    {
        if (option.Unit == unit)
        {
            return quantity;
        }

        if (option.PackSize is { } size && option.PackUnit is { } packUnit && Base(quantity, unit) is { } wanted && Base(size, packUnit) is { } pack
            && Family(unit) == Family(packUnit))
        {
            return Math.Max(1, Math.Ceiling(wanted / pack));
        }

        return Base(quantity, unit) is { } amount && Base(1, option.Unit) is { } per && Family(unit) == Family(option.Unit)
            ? decimal.Round(amount / per, 3)
            : null;
    }

    private static decimal? Base(decimal quantity, Unit unit) => unit switch
    {
        Unit.Kg or Unit.Litre => quantity * 1000,
        Unit.G or Unit.Ml or Unit.Piece or Unit.Packet => quantity,
        _ => null,
    };

    private static int Family(Unit unit) => unit switch
    {
        Unit.Kg or Unit.G => 1,
        Unit.Litre or Unit.Ml => 2,
        _ => 3,
    };

    /// <summary>
    /// After packing a line: is it something the household must OK first? Null when fine; an error when the shop
    /// packed something else although the household said "don't replace".
    /// </summary>
    internal static (bool NeedsApproval, string? Error) Check(OrderLine line)
    {
        if (line.Removed)
        {
            return (false, null);
        }

        var replaced = line.RequestedProductId is { } asked && line.ProductId != asked;
        if (replaced && line.Preference == ReplacePreference.DontReplace)
        {
            return (false, $"The customer asked for exactly this \"{line.RequestedName}\": pack it, or mark it not available.");
        }

        var askFirst = line.Preference == ReplacePreference.AskMe && (replaced || line.RequestedProductId is null);
        var priceMoved = line.ExpectedAmount is { } expected && line.UnitPrice is { } price
            && Math.Abs(Money.LineAmount(line.PackedQuantity ?? line.Quantity, price) - expected) > PriceTolerance(expected);
        return (askFirst || priceMoved, null);
    }
}
