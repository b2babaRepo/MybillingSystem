using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Catalogue;

public sealed class Product : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public Unit Unit { get; set; }

    /// <summary>Price per <see cref="Unit"/>.</summary>
    public decimal SellingPrice { get; set; }

    public decimal? CostPrice { get; set; }
}

public enum Unit
{
    Piece,
    Kg,
    G,
    Litre,
    Ml,
    Packet,
}
