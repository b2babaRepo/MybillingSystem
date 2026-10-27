using System.Reflection;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
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
    public static readonly string[] ShopOwnedTables = ["products", "customers", "ledger_entries", "bills", "bill_lines"];

    private Guid CurrentShopId => tenant.ShopId;

    public DbSet<Shop> Shops => Set<Shop>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Product> Products => Set<Product>();

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
            b.HasIndex(p => new { p.ShopId, p.Name });
            b.HasOne<Shop>().WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
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
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            applyFilter.MakeGenericMethod(type.ClrType).Invoke(this, [modelBuilder]);
        }

        SnakeCaseNaming.Apply(modelBuilder);
    }

    private void ApplyShopFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantOwned
        => modelBuilder.Entity<T>().HasQueryFilter(e => e.ShopId == CurrentShopId);
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
