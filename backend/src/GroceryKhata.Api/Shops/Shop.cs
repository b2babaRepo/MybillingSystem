using GroceryKhata.Api.Common;

namespace GroceryKhata.Api.Shops;

/// <summary>One kirana shop: the tenant. Not tenant-owned itself.</summary>
public sealed class Shop : Entity
{
    public required string Name { get; set; }

    /// <summary>Unique, lower case. The storefront address later: /s/{slug}.</summary>
    public required string Slug { get; set; }

    /// <summary>The market or locality households search by ("Raja Park bazaar", "Shastri Nagar").</summary>
    public string? Area { get; set; }

    public string? Address { get; set; }

    /// <summary>False when the shop is closed; its members can no longer sign in to it.</summary>
    public bool IsActive { get; set; } = true;
}
