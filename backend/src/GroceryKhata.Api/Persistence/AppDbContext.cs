using System.Reflection;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Planner;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GroceryKhata.Api.Persistence;

/// <summary>
/// The one database context. Every <see cref="ITenantOwned"/> entity gets a query filter on the current
/// shop; <see cref="Interceptors.ShopStampInterceptor"/> stamps and guards writes, and PostgreSQL
/// row-level security (see the Initial migration) is the second guard.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
    /// <summary>Tables protected by row-level security. Keep in step with <see cref="ITenantOwned"/> entities.</summary>
    public static readonly string[] ShopOwnedTables = ["products", "customers", "ledger_entries", "bills", "bill_lines", "orders", "order_lines", "stock_movements", "audit_log"];

    private Guid CurrentShopId => tenant.ShopId;

    private Guid CurrentUserId => tenant.UserId;

    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();

    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Shop> Shops => Set<Shop>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<Bill> Bills => Set<Bill>();

    public DbSet<BillLine> BillLines => Set<BillLine>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shop>(b =>
        {
            b.ToTable("shops");
            b.Property(s => s.Name).HasMaxLength(120);
            b.Property(s => s.Slug).HasMaxLength(80);
            b.HasIndex(s => s.Slug).IsUnique();
            b.Property(s => s.Area).HasMaxLength(120);
            b.Property(s => s.Address).HasMaxLength(200);
            b.Property(s => s.UpiId).HasMaxLength(60);
            b.Property(s => s.ReminderTemplate).HasMaxLength(500);
        });

        modelBuilder.Entity<ShoppingList>(b =>
        {
            b.ToTable("shopping_lists");
            b.HasIndex(l => new { l.UserId, l.Kind }).IsUnique();
            b.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(l => l.Items).WithOne().HasForeignKey(i => i.ListId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingListItem>(b =>
        {
            b.ToTable("shopping_list_items");
            b.Property(i => i.Name).HasMaxLength(120);
            b.Property(i => i.Quantity).HasPrecision(12, 3);
            b.Property(i => i.Note).HasMaxLength(200);
            b.HasOne<CatalogueItem>().WithMany().HasForeignKey(i => i.CatalogueItemId).OnDelete(DeleteBehavior.SetNull);
        });

        // Shared by everyone, owned by no shop: no tenant key, no row-level security. Rows come from code.
        modelBuilder.Entity<CatalogueItem>(b =>
        {
            b.ToTable("catalogue_items");
            b.Property(i => i.Id).ValueGeneratedNever();
            b.Property(i => i.Name).HasMaxLength(120);
            b.Property(i => i.NameEn).HasMaxLength(120);
            b.Property(i => i.Category).HasMaxLength(60);
            b.Property(i => i.TypicalPrice).HasPrecision(12, 2);
            b.HasData(CommonCatalogue.Items);
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasTenantKey();
            b.Property(o => o.CustomerName).HasMaxLength(120);
            b.Property(o => o.CustomerPhone).HasMaxLength(16);
            b.Property(o => o.Slot).HasMaxLength(80);
            b.Property(o => o.Note).HasMaxLength(300);
            b.Property(o => o.Address).HasMaxLength(300);
            // Orders from before the online store were all household lists.
            b.Property(o => o.Source).HasDefaultValue(OrderSource.List);
            b.Property(o => o.StatusTokenHash).HasMaxLength(64);
            b.HasIndex(o => new { o.ShopId, o.Status, o.CreatedAt });
            b.HasIndex(o => new { o.PlacedByUserId, o.CreatedAt });
            b.HasOne<Shop>().WithMany().HasForeignKey(o => o.ShopId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<User>().WithMany().HasForeignKey(o => o.PlacedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasTenantForeignKey<Order, Bill>(nameof(Order.BillId));
            b.HasMany(o => o.Lines).WithOne()
                .HasForeignKey(l => new { l.ShopId, l.OrderId })
                .HasPrincipalKey(o => new { o.ShopId, o.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderLine>(b =>
        {
            b.ToTable("order_lines");
            b.Property(l => l.Name).HasMaxLength(120);
            b.Property(l => l.Quantity).HasPrecision(12, 3);
            b.Property(l => l.PackedQuantity).HasPrecision(12, 3);
            b.Property(l => l.Note).HasMaxLength(200);
            b.Property(l => l.ShopNote).HasMaxLength(200);
            b.Property(l => l.ReservedQuantity).HasPrecision(12, 3);
            b.HasTenantForeignKey<OrderLine, Product>(nameof(OrderLine.ProductId));
            b.HasIndex(l => new { l.ShopId, l.OrderId, l.LineNo }).IsUnique();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.Phone).HasMaxLength(16);
            b.HasIndex(u => u.Phone).IsUnique();
            b.Property(u => u.FullName).HasMaxLength(120);
            b.Property(u => u.PasswordHash).HasMaxLength(200);
            b.Property(u => u.PreferredLanguage).HasMaxLength(5);
        });

        modelBuilder.Entity<Membership>(b =>
        {
            b.ToTable("memberships");
            b.HasIndex(m => new { m.UserId, m.ShopId }).IsUnique();
            b.HasIndex(m => m.ShopId);
            b.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Shop>().WithMany().HasForeignKey(m => m.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.Property(t => t.TokenHash).HasMaxLength(64);
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => t.UserId);
            b.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(120);
            b.Property(p => p.Category).HasMaxLength(60);
            b.Property(p => p.IsActive).HasDefaultValue(true);
            b.Property(p => p.Stock).HasPrecision(12, 3);
            b.Property(p => p.ReorderLevel).HasPrecision(12, 3);
            b.Property(p => p.Reserved).HasPrecision(12, 3);
            b.HasIndex(p => new { p.ShopId, p.Name });
            b.HasIndex(p => new { p.ShopId, p.Category });
            b.HasOne<Shop>().WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("audit_log");
            b.Property(a => a.Table).HasMaxLength(40);
            b.Property(a => a.Action).HasMaxLength(10);
            b.Property(a => a.Changes).HasColumnType("jsonb");
            b.HasIndex(a => new { a.ShopId, a.RecordId, a.CreatedAt });
            b.HasIndex(a => new { a.ShopId, a.CreatedAt });
        });

        modelBuilder.Entity<StockMovement>(b =>
        {
            b.ToTable("stock_movements");
            b.Property(m => m.Quantity).HasPrecision(12, 3);
            b.Property(m => m.Note).HasMaxLength(200);
            b.HasIndex(m => new { m.ShopId, m.ProductId, m.OccurredAt });
            b.HasTenantForeignKey<StockMovement, Product>(nameof(StockMovement.ProductId));
            b.HasTenantForeignKey<StockMovement, Bill>(nameof(StockMovement.BillId));
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("customers");
            b.HasTenantKey();
            b.Property(c => c.Name).HasMaxLength(120);
            b.Property(c => c.Phone).HasMaxLength(16);
            b.HasIndex(c => new { c.ShopId, c.Name });
            b.HasIndex(c => new { c.ShopId, c.Phone });
            b.HasOne<Shop>().WithMany().HasForeignKey(c => c.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bill>(b =>
        {
            b.ToTable("bills");
            b.HasTenantKey();
            b.HasIndex(x => new { x.ShopId, x.OccurredAt });
            b.HasTenantForeignKey<Bill, Customer>(nameof(Bill.CustomerId));
            b.HasMany(x => x.Lines).WithOne()
                .HasForeignKey(l => new { l.ShopId, l.BillId })
                .HasPrincipalKey(x => new { x.ShopId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillLine>(b =>
        {
            b.ToTable("bill_lines");
            b.Property(l => l.Name).HasMaxLength(120);
            b.Property(l => l.Quantity).HasPrecision(12, 3);
            b.HasIndex(l => new { l.ShopId, l.BillId, l.LineNo }).IsUnique();
            b.HasTenantForeignKey<BillLine, Product>(nameof(BillLine.ProductId));
        });

        modelBuilder.Entity<LedgerEntry>(b =>
        {
            b.ToTable("ledger_entries");
            b.HasTenantKey();
            b.Property(e => e.Note).HasMaxLength(200);
            b.HasIndex(e => new { e.ShopId, e.CustomerId, e.OccurredAt });
            b.HasIndex(e => e.ReversesEntryId).IsUnique().HasFilter("reverses_entry_id IS NOT NULL");
            b.HasTenantForeignKey<LedgerEntry, Customer>(nameof(LedgerEntry.CustomerId));
            b.HasTenantForeignKey<LedgerEntry, Bill>(nameof(LedgerEntry.BillId));
            b.HasTenantForeignKey<LedgerEntry, LedgerEntry>(nameof(LedgerEntry.ReversesEntryId));
        });

        var applyFilter = typeof(AppDbContext).GetMethod(nameof(ApplyShopFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var applyPlacedFilter = typeof(AppDbContext).GetMethod(nameof(ApplyShopOrPlacedByFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            var filter = typeof(IPlacedByUser).IsAssignableFrom(type.ClrType) ? applyPlacedFilter : applyFilter;
            filter.MakeGenericMethod(type.ClrType).Invoke(this, [modelBuilder]);
        }

        SnakeCaseNaming.Apply(modelBuilder);
    }

    private void ApplyShopFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantOwned
        => modelBuilder.Entity<T>().HasQueryFilter(e => e.ShopId == CurrentShopId);

    /// <summary>Orders: the shop sees its own; the person who placed one sees it at whichever shop it went to.</summary>
    private void ApplyShopOrPlacedByFilter<T>(ModelBuilder modelBuilder) where T : class, IPlacedByUser
        => modelBuilder.Entity<T>().HasQueryFilter(e => e.ShopId == CurrentShopId || e.PlacedByUserId == CurrentUserId);
}

/// <summary>
/// Links between shop-owned tables include shop_id, so the database itself refuses to point a row of
/// one shop at a row of another (for example a Shop A khata entry at a Shop B customer).
/// </summary>
internal static class TenantKeys
{
    /// <summary>A unique (shop_id, id) key that other shop-owned tables can reference.</summary>
    public static void HasTenantKey<T>(this EntityTypeBuilder<T> builder)
        where T : Entity, ITenantOwned =>
        builder.HasAlternateKey(e => new { e.ShopId, e.Id });

    /// <summary>A foreign key on (shop_id, column) pointing at the parent's (shop_id, id).</summary>
    public static void HasTenantForeignKey<TDependent, TPrincipal>(this EntityTypeBuilder<TDependent> builder, string foreignKeyProperty)
        where TDependent : Entity, ITenantOwned
        where TPrincipal : Entity, ITenantOwned =>
        builder.HasOne<TPrincipal>()
            .WithMany()
            .HasForeignKey(nameof(ITenantOwned.ShopId), foreignKeyProperty)
            .HasPrincipalKey(nameof(ITenantOwned.ShopId), nameof(Entity.Id))
            .OnDelete(DeleteBehavior.Restrict);
}
