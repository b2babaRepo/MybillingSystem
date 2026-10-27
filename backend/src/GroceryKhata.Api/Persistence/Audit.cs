using System.Text.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GroceryKhata.Api.Persistence;

/// <summary>
/// Who changed what, and when, for records that are edited in place: products (prices!), customers and orders.
/// Khata entries and stock movements need no entry here: they are append-only and carry who and when themselves.
/// </summary>
public sealed class AuditEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>"products", "customers" or "orders".</summary>
    public required string Table { get; set; }

    public Guid RecordId { get; set; }

    /// <summary>"update" or "delete".</summary>
    public required string Action { get; set; }

    /// <summary>JSON: {"selling_price": [44, 46], ...}, old value then new.</summary>
    public required string Changes { get; set; }
}

public sealed record AuditEntryDto(Guid Id, DateTimeOffset At, Guid UserId, string? UserName, string Table, Guid RecordId, string Action, JsonElement Changes);

/// <summary>Writes an <see cref="AuditEntry"/> in the same save as every edit or delete of an audited record.</summary>
public sealed class AuditInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    private static readonly Dictionary<Type, string> Audited = new()
    {
        [typeof(Product)] = "products",
        [typeof(Customer)] = "customers",
        [typeof(Order)] = "orders",
    };

    // Bookkeeping columns, not decisions anyone made.
    private static readonly HashSet<string> Ignored = [nameof(Entity.UpdatedAt), nameof(Entity.CreatedAt), nameof(Customer.LastRemindedAt)];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    private void Record(DbContext context)
    {
        if (tenant.ShopId == Guid.Empty)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted && Audited.ContainsKey(e.Entity.GetType()))
            .ToList();
        foreach (var entry in entries)
        {
            var changes = entry.State == EntityState.Deleted
                ? entry.Properties.ToDictionary(p => SnakeCaseNaming.ToSnakeCase(p.Metadata.Name), p => new[] { p.OriginalValue, null })
                : entry.Properties.Where(Changed).ToDictionary(p => SnakeCaseNaming.ToSnakeCase(p.Metadata.Name), p => new[] { p.OriginalValue, p.CurrentValue });
            if (changes.Count == 0)
            {
                continue;
            }

            context.Add(new AuditEntry
            {
                ShopId = tenant.ShopId,
                UserId = tenant.UserId,
                Table = Audited[entry.Entity.GetType()],
                RecordId = (Guid)entry.Property(nameof(Entity.Id)).CurrentValue!,
                Action = entry.State == EntityState.Deleted ? "delete" : "update",
                Changes = JsonSerializer.Serialize(changes),
            });
        }
    }

    private static bool Changed(PropertyEntry property) =>
        property.IsModified && !Ignored.Contains(property.Metadata.Name) && !Equals(property.OriginalValue, property.CurrentValue);
}

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAudit(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/audit", ListAsync).WithTags("Audit").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>Newest first; optionally for one record (an item's price history, say).</summary>
    private static async Task<IReadOnlyList<AuditEntryDto>> ListAsync(Guid? recordId, AppDbContext db, CancellationToken ct)
    {
        var query = db.AuditEntries.AsNoTracking();
        if (recordId is { } id)
        {
            query = query.Where(a => a.RecordId == id);
        }

        var rows = await (from a in query
                          join u in db.Users.AsNoTracking() on a.UserId equals u.Id into users
                          from u in users.DefaultIfEmpty()
                          orderby a.CreatedAt descending
                          select new { a, Name = u == null ? null : u.FullName })
            .Take(200).ToListAsync(ct);
        return rows.Select(r => new AuditEntryDto(r.a.Id, r.a.CreatedAt, r.a.UserId, r.Name, r.a.Table, r.a.RecordId, r.a.Action,
            JsonDocument.Parse(r.a.Changes).RootElement.Clone())).ToList();
    }
}
