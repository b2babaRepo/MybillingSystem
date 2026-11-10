namespace GroceryKhata.Api.Common;

/// <summary>The shop the current request or job works in.</summary>
public interface ITenantContext
{
    /// <summary><see cref="Guid.Empty"/> when no shop is selected; shop-owned queries then return nothing.</summary>
    Guid ShopId { get; }

    bool HasShop => ShopId != Guid.Empty;

    /// <summary>The signed-in person, shop token or not. Lets a household see the orders it placed at any shop.</summary>
    Guid UserId { get; }

    /// <summary>What the owner has stopped this helper doing (Identity.StaffLimits); empty for owners.</summary>
    IReadOnlyCollection<string> Blocked => [];

    bool IsBlocked(string limit) => Blocked.Contains(limit);
}

/// <summary>
/// Scoped holder for the current shop. Set once per request by the tenant middleware
/// (from the token's shop_id claim) or once per job before it touches data.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid ShopId { get; private set; }

    public void SetShop(Guid shopId)
    {
        if (shopId == Guid.Empty)
        {
            throw new ArgumentException("Shop ID must not be empty.", nameof(shopId));
        }

        if (ShopId != Guid.Empty && ShopId != shopId)
        {
            throw new InvalidOperationException("The shop for this scope is already set.");
        }

        ShopId = shopId;
    }

    public Guid UserId { get; private set; }

    public void SetUser(Guid userId) => UserId = userId;

    public IReadOnlyCollection<string> Blocked { get; private set; } = [];

    public void SetBlocked(IReadOnlyCollection<string> blocked) => Blocked = blocked;
}
