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

public sealed partial class AppDbContext
{
    /// <summary>Items, stock movements, categories, godowns, batches, serials and the master lists.</summary>
    private void ConfigureCatalogue(ModelBuilder modelBuilder)
    {

        // The platform's master item lists: shared by every shop, owned by none.
        modelBuilder.Entity<MasterItemImage>(b =>
        {
            b.ToTable("master_item_images");
            b.HasKey(i => i.MasterItemId);
            b.Property(i => i.ContentType).HasMaxLength(20);
            b.Property(i => i.ThumbType).HasMaxLength(20);
            b.HasOne<MasterItem>().WithOne().HasForeignKey<MasterItemImage>(i => i.MasterItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MasterItem>(b =>
        {
            b.ToTable("master_items");
            b.Property(i => i.ImageCredit).HasMaxLength(120);
            b.Property(i => i.Trade).HasMaxLength(30);
            b.Property(i => i.Category).HasMaxLength(60);
            b.Property(i => i.Name).HasMaxLength(120);
            b.Property(i => i.NameHi).HasMaxLength(120);
            b.Property(i => i.GstRate).HasPrecision(5, 2);
            b.Property(i => i.HsnCode).HasMaxLength(8);
            b.HasIndex(i => new { i.Trade, i.Category });
            b.HasOne<CatalogueItem>().WithMany().HasForeignKey(i => i.CatalogueItemId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MasterSuggestion>(b =>
        {
            b.ToTable("master_suggestions");
            b.Property(s => s.ShopName).HasMaxLength(120);
            b.Property(s => s.Trade).HasMaxLength(30);
            b.Property(s => s.Category).HasMaxLength(60);
            b.Property(s => s.Name).HasMaxLength(120);
            b.Property(s => s.Note).HasMaxLength(200);
            b.HasIndex(s => new { s.Status, s.CreatedAt });
            b.HasIndex(s => s.ShopId);
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
            b.Property(i => i.Aisle).HasMaxLength(20);
            b.HasData(CommonCatalogue.Items);
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.HasTenantKey();
            b.HasOne(p => p.MasterItem).WithMany().HasForeignKey(p => p.MasterItemId).OnDelete(DeleteBehavior.SetNull);
            b.Property(p => p.Name).HasMaxLength(120);
            b.Property(p => p.PackSize).HasPrecision(12, 3);
            b.Property(p => p.GstRate).HasPrecision(5, 2);
            b.Property(p => p.HsnCode).HasMaxLength(8);
            b.Property(p => p.Category).HasMaxLength(60);
            b.Property(p => p.IsActive).HasDefaultValue(true);
            b.Property(p => p.Stock).HasPrecision(12, 3);
            b.Property(p => p.ReorderLevel).HasPrecision(12, 3);
            b.Property(p => p.WholesalePrice).HasPrecision(12, 2);
            b.Property(p => p.WholesaleMinQty).HasPrecision(12, 3);
            b.Property(p => p.Reserved).HasPrecision(12, 3);
            b.HasIndex(p => new { p.ShopId, p.Name });
            b.HasIndex(p => new { p.ShopId, p.Category });
            b.HasIndex(p => new { p.ShopId, p.CatalogueItemId });
            // One active item per barcode in a shop: scanning it must find exactly one item.
            b.HasIndex(p => new { p.ShopId, p.Barcode }).IsUnique().HasFilter("barcode IS NOT NULL AND is_active");
            b.HasIndex(p => p.ExtraBarcodes).HasMethod("gin");
            b.HasOne(p => p.CatalogueItem).WithMany().HasForeignKey(p => p.CatalogueItemId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Shop>().WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FestivalSetting>(b =>
        {
            b.ToTable("festival_settings");
            b.HasTenantKey();
            b.Property(s => s.Key).HasMaxLength(30);
            b.HasIndex(s => new { s.ShopId, s.Key, s.Year }).IsUnique();
        });

        modelBuilder.Entity<Godown>(b =>
        {
            b.ToTable("godowns");
            b.HasTenantKey();
            b.Property(g => g.Name).HasMaxLength(60);
            b.Property(g => g.Address).HasMaxLength(200);
        });

        modelBuilder.Entity<StockTransfer>(b =>
        {
            b.ToTable("stock_transfers");
            b.HasTenantKey();
            b.Property(t => t.Note).HasMaxLength(200);
            b.HasIndex(t => new { t.ShopId, t.Number }).IsUnique();
            b.HasMany(t => t.Lines).WithOne().HasForeignKey(l => l.TransferId);
        });

        modelBuilder.Entity<StockTransferLine>(b =>
        {
            b.ToTable("stock_transfer_lines");
            b.HasTenantKey();
            b.Property(l => l.Quantity).HasPrecision(12, 3);
            b.Property(l => l.Bags).HasPrecision(12, 3);
            b.HasIndex(l => new { l.ShopId, l.ProductId });
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("categories");
            b.HasTenantKey();
            b.Property(c => c.Name).HasMaxLength(60);
            b.Property(c => c.NormalisedName).HasMaxLength(60);
            b.Property(c => c.DefaultHsn).HasMaxLength(8);
            b.Property(c => c.DefaultGstRate).HasPrecision(5, 2);
            b.HasIndex(c => new { c.ShopId, c.NormalisedName }).IsUnique().HasFilter("merged_into_id IS NULL");
        });

        modelBuilder.Entity<ProductSerial>(b =>
        {
            b.ToTable("product_serials");
            b.HasTenantKey();
            b.Property(s => s.SerialNo).HasMaxLength(40);
            b.HasIndex(s => new { s.ShopId, s.ProductId, s.SerialNo }).IsUnique();
            b.HasIndex(s => new { s.ShopId, s.SerialNo });
            b.HasIndex(s => new { s.ShopId, s.BillId });
        });

        modelBuilder.Entity<ProductBatch>(b =>
        {
            b.ToTable("product_batches");
            b.HasTenantKey();
            b.Property(x => x.BatchNo).HasMaxLength(30);
            b.Property(x => x.Mrp).HasPrecision(12, 2);
            b.Property(x => x.CostPrice).HasPrecision(12, 2);
            b.Property(x => x.Quantity).HasPrecision(14, 3);
            b.HasIndex(x => new { x.ShopId, x.ProductId, x.BatchNo, x.Expiry }).IsUnique();
            b.HasIndex(x => new { x.ShopId, x.Expiry });
        });

        modelBuilder.Entity<BatchSale>(b =>
        {
            b.ToTable("batch_sales");
            b.HasTenantKey();
            b.Property(x => x.Quantity).HasPrecision(14, 3);
            b.HasIndex(x => new { x.ShopId, x.BillId });
        });

        modelBuilder.Entity<Catalogue.Stocktake>(b =>
        {
            b.ToTable("stocktakes");
            b.HasTenantKey();
            b.Property(t => t.Lines).HasColumnType("jsonb");
            b.Property(t => t.ValueDifference).HasPrecision(14, 2);
            b.HasIndex(t => new { t.ShopId, t.Number }).IsUnique();
        });

        modelBuilder.Entity<ProductExtraPhoto>(b =>
        {
            b.ToTable("product_extra_photos");
            b.HasKey(p => p.Id);
            b.HasIndex(p => new { p.ShopId, p.ProductId });
            b.Property(p => p.ContentType).HasMaxLength(20);
            b.Property(p => p.ThumbType).HasMaxLength(20);
            b.HasOne<Product>().WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductImage>(b =>
        {
            b.Property(i => i.ThumbType).HasMaxLength(20);
            b.ToTable("product_images");
            b.HasKey(i => i.ProductId);
            b.HasIndex(i => i.ShopId);
            b.Property(i => i.ContentType).HasMaxLength(20);
            b.HasOne<Product>().WithOne().HasForeignKey<ProductImage>(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
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
    }
}
