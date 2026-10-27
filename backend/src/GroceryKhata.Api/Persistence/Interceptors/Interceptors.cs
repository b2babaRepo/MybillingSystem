using System.Data.Common;
using GroceryKhata.Api.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GroceryKhata.Api.Persistence.Interceptors;

/// <summary>
/// Sets ShopId on every new shop-owned row and blocks any update or delete of another shop's row.
/// The second line of defence in the app, behind the query filter.
/// </summary>
public sealed class ShopStampInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added)
            {
                if (tenant.ShopId == Guid.Empty)
                {
                    throw new InvalidOperationException("No shop selected.");
                }

                entry.Entity.ShopId = tenant.ShopId;
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var originalShopId = entry.Property(e => e.ShopId).OriginalValue;
                if (tenant.ShopId == Guid.Empty || originalShopId != tenant.ShopId || entry.Entity.ShopId != tenant.ShopId)
                {
                    throw new InvalidOperationException("Cross-shop change blocked.");
                }
            }
        }
    }
}

/// <summary>Sets CreatedAt and UpdatedAt in UTC on every save.</summary>
public sealed class TimestampInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext context)
    {
        var now = clock.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}

/// <summary>
/// Tells PostgreSQL which shop this connection works for (the app.shop_id setting), every time a
/// connection opens. Row-level security policies on shop-owned tables compare against it, so even a
/// query that skips the EF filter can't read or write another shop's rows. Empty means no shop: no rows.
/// </summary>
public sealed class ShopSessionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    private const string Sql = "SELECT set_config('app.shop_id', @shop_id, false), set_config('app.user_id', @user_id, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "shop_id";
        parameter.Value = tenant.HasShop ? tenant.ShopId.ToString() : string.Empty;
        command.Parameters.Add(parameter);
        var user = command.CreateParameter();
        user.ParameterName = "user_id";
        user.Value = tenant.UserId == Guid.Empty ? string.Empty : tenant.UserId.ToString();
        command.Parameters.Add(user);
        return command;
    }
}
