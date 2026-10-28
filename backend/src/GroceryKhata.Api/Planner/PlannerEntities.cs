using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// A household's standing list: one monthly and one weekly per person. Belongs to the user, not to a shop,
/// so it travels with them to whichever shop they send it. Items stay after sending: next month starts from it.
/// </summary>
public sealed class ShoppingList : Entity
{
    public Guid UserId { get; set; }

    public ListKind Kind { get; set; }

    public List<ShoppingListItem> Items { get; set; } = [];
}

public enum ListKind
{
    Monthly,
    Weekly,
}

/// <summary>One line on a list. Its ID comes from the phone, so edits made offline sync once.</summary>
public sealed class ShoppingListItem : Entity
{
    public Guid ListId { get; set; }

    public required string Name { get; set; }

    /// <summary>Picked from the common catalogue (null: typed in by hand).</summary>
    public int? CatalogueItemId { get; set; }

    public decimal Quantity { get; set; }

    public Unit Unit { get; set; }

    /// <summary>Ticked: confirmed for this round. Cleared after the list is sent.</summary>
    public bool Checked { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// A list sent to a shop. Owned by the shop (it is the shop's work), and also visible to the person who
/// placed it: the query filter and row-level security both allow "my shop OR placed by me".
/// </summary>
public interface IPlacedByUser : ITenantOwned
{
    /// <summary>The household account that placed it; null for a guest order from the online store.</summary>
    Guid? PlacedByUserId { get; set; }
}

public sealed class Order : Entity, IPlacedByUser
{
    public Guid ShopId { get; set; }

    public Guid? PlacedByUserId { get; set; }

    /// <summary>A household's list, or a guest's cart from the shop's online store.</summary>
    public OrderSource Source { get; set; }

    /// <summary>Copied at sending, so the shop sees who it is without reading the users table.</summary>
    public required string CustomerName { get; set; }

    public required string CustomerPhone { get; set; }

    public ListKind ListKind { get; set; }

    public Fulfilment Fulfilment { get; set; }

    /// <summary>Where to deliver (store orders ask for it).</summary>
    public string? Address { get; set; }

    /// <summary>
    /// Hash of the secret in the guest's order-status link. Guests have no account, so the link itself is the
    /// key; only its hash is stored, and order IDs alone never reveal an order.
    /// </summary>
    public string? StatusTokenHash { get; set; }

    /// <summary>When they want it, in their words ("1 Nov, evening").</summary>
    public string? Slot { get; set; }

    public OrderPayment Payment { get; set; }

    public string? Note { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Placed;

    /// <summary>Set at packing, after loose items are weighed and prices filled in.</summary>
    public decimal? FinalAmount { get; set; }

    public DateTimeOffset? PackedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>The bill made at delivery; it posts any unpaid amount to the customer's khata.</summary>
    public Guid? BillId { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}

public sealed class OrderLine : Entity, IPlacedByUser
{
    public Guid ShopId { get; set; }

    public Guid? PlacedByUserId { get; set; }

    public Guid OrderId { get; set; }

    /// <summary>The catalogue item, for store orders: its price is known and its stock is reserved.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Stock this line holds back on the product (its quantity when placed). Released, back to 0, when the order
    /// is delivered (the bill then takes the real stock) or cancelled.
    /// </summary>
    public decimal ReservedQuantity { get; set; }

    public int LineNo { get; set; }

    public required string Name { get; set; }

    public decimal Quantity { get; set; }

    public Unit Unit { get; set; }

    public string? Note { get; set; }

    /// <summary>What the shop actually packed (1.9 kg for 2 kg asked). Null until packed.</summary>
    public decimal? PackedQuantity { get; set; }

    public decimal? UnitPrice { get; set; }

    /// <summary>Out of stock or not wanted: left out of the bill.</summary>
    public bool Removed { get; set; }

    /// <summary>What the shop changed, for the customer to see ("Pillsbury instead of Aashirvaad").</summary>
    public string? ShopNote { get; set; }
}

public enum OrderSource
{
    /// <summary>A household planner list sent to the shop.</summary>
    List,

    /// <summary>A guest's cart from the shop's online store (/s/{slug}).</summary>
    Store,
}

public enum Fulfilment
{
    Delivery,
    Pickup,
}

public enum OrderPayment
{
    Cash,
    Upi,
    Khata,
}

public enum OrderStatus
{
    Placed,
    Packed,
    Delivered,
    Cancelled,
}
