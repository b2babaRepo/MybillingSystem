namespace GroceryKhata.Api.Common;

/// <summary>
/// Base for all entities: a version 7 GUID key plus UTC created and updated times.
/// Rows written from the app (bills, khata entries, customers) take the client's ID, which doubles as the idempotency key.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A row that belongs to one shop. EF Core filters these by the signed-in shop and stamps
/// <see cref="ShopId"/> on insert; PostgreSQL row-level security checks it again. Never set it from request data.
/// </summary>
public interface ITenantOwned
{
    Guid ShopId { get; set; }
}
