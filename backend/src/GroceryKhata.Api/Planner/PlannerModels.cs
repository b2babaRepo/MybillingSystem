using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Planner;

public sealed record ListItemDto(Guid Id, string Name, decimal Quantity, Unit Unit, bool Checked, string? Note);

public sealed record ListDto(ListKind Kind, IReadOnlyList<ListItemDto> Items);

/// <summary>Creates or replaces one item (PUT by the app's own ID, so a retry is harmless).</summary>
public sealed record UpsertItemRequest(string? Name, decimal Quantity, Unit? Unit, bool Checked, string? Note)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 120)
        {
            errors[nameof(Name)] = ["Enter the item's name."];
        }

        if (!Money.IsValidQuantity(Quantity))
        {
            errors[nameof(Quantity)] = ["Quantity must be above 0, with at most three decimals."];
        }

        if (Note?.Length > 200)
        {
            errors[nameof(Note)] = ["Keep the note under 200 characters."];
        }

        return errors;
    }
}

public sealed record ShopDirectoryDto(Guid Id, string Name, string? Area, string? Address);

public sealed record PlaceOrderRequest(
    Guid Id,
    Guid ShopId,
    ListKind? Kind,
    Fulfilment? Fulfilment,
    string? Slot,
    OrderPayment? Payment,
    string? Note);

public sealed record OrderLineDto(
    Guid Id,
    int LineNo,
    string Name,
    decimal Quantity,
    Unit Unit,
    string? Note,
    decimal? PackedQuantity,
    decimal? UnitPrice,
    bool Removed,
    string? ShopNote,
    decimal? Amount);

public sealed record OrderDto(
    Guid Id,
    Guid ShopId,
    string ShopName,
    string CustomerName,
    string CustomerPhone,
    ListKind ListKind,
    Fulfilment Fulfilment,
    string? Slot,
    OrderPayment Payment,
    string? Note,
    OrderStatus Status,
    decimal? FinalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PackedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CancelledAt,
    IReadOnlyList<OrderLineDto> Lines,
    OrderSource Source,
    string? Address)
{
    public static OrderDto From(Order o, string shopName) => new(
        o.Id, o.ShopId, shopName, o.CustomerName, o.CustomerPhone, o.ListKind, o.Fulfilment, o.Slot, o.Payment, o.Note,
        o.Status, o.FinalAmount, o.CreatedAt, o.PackedAt, o.DeliveredAt, o.CancelledAt,
        o.Lines.OrderBy(l => l.LineNo).Select(l => new OrderLineDto(
            l.Id, l.LineNo, l.Name, l.Quantity, l.Unit, l.Note, l.PackedQuantity, l.UnitPrice, l.Removed, l.ShopNote,
            l.Removed || l.UnitPrice is null ? null : Money.LineAmount(l.PackedQuantity ?? l.Quantity, l.UnitPrice.Value))).ToList(),
        o.Source,
        o.Address);
}

public sealed record PackLineRequest(Guid Id, decimal? PackedQuantity, decimal? UnitPrice, bool Removed, string? ShopNote);

public sealed record PackRequest(IReadOnlyList<PackLineRequest>? Lines);

public sealed record DeliverRequest(decimal? PaidAmount, Ledger.PaymentMode? PaymentMode);
