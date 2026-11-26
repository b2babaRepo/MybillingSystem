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
    /// <summary>Buying: suppliers, purchases, purchase orders, arrivals, goods receipts, returns and bags.</summary>
    private void ConfigurePurchasing(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<GoodsReceipt>(b =>
        {
            b.ToTable("goods_receipts");
            b.HasTenantKey();
            b.Property(r => r.Note).HasMaxLength(200);
            b.HasIndex(r => new { r.ShopId, r.SupplierId, r.PurchaseId });
            b.HasIndex(r => new { r.ShopId, r.Number }).IsUnique();
            b.Property(r => r.DebitNoteNo).HasMaxLength(16);
            b.HasIndex(r => new { r.ShopId, r.DebitNoteNo }).IsUnique();
        });

        modelBuilder.Entity<Supplier>(b =>
        {
            b.ToTable("suppliers");
            b.HasTenantKey();
            b.Property(s => s.Name).HasMaxLength(120);
            b.Property(s => s.Phone).HasMaxLength(20);
            b.Property(s => s.Gstin).HasMaxLength(15);
            b.Property(s => s.Address).HasMaxLength(200);
        });

        modelBuilder.Entity<Purchase>(b =>
        {
            b.ToTable("purchases");
            b.HasTenantKey();
            b.Property(p => p.BillNo).HasMaxLength(40);
            b.Property(p => p.Total).HasPrecision(12, 2);
            b.Property(p => p.ExtraCharges).HasPrecision(12, 2);
            b.HasMany(p => p.Lines).WithOne().HasForeignKey(l => l.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Charges).WithOne().HasForeignKey(c => c.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            b.Property(p => p.CostSplit).HasMaxLength(20);
        });

        modelBuilder.Entity<PurchaseLine>(b =>
        {
            b.ToTable("purchase_lines");
            b.HasTenantKey();
            b.Property(l => l.Quantity).HasPrecision(12, 3);
            b.Property(l => l.UnitCost).HasPrecision(12, 2);
            b.Property(l => l.FreeQuantity).HasPrecision(12, 3);
            b.Property(l => l.LandedUnitCost).HasPrecision(12, 2);
            b.Property(l => l.DiscountPercent).HasPrecision(5, 2);
            b.Property(l => l.GstRate).HasPrecision(5, 2);
            b.Property(l => l.Taxable).HasPrecision(12, 2);
            b.Property(l => l.Gst).HasPrecision(12, 2);
        });

        modelBuilder.Entity<SupplierReturn>(b =>
        {
            b.ToTable("supplier_returns");
            b.HasTenantKey();
            b.Property(r => r.Value).HasPrecision(12, 2);
            b.Property(r => r.CreditAmount).HasPrecision(12, 2);
            b.Property(r => r.CreditNoteNo).HasMaxLength(40);
            b.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.ReturnId);
            b.HasIndex(r => new { r.ShopId, r.Number }).IsUnique();
        });

        modelBuilder.Entity<SupplierReturnLine>(b =>
        {
            b.ToTable("supplier_return_lines");
            b.HasTenantKey();
            b.Property(l => l.Quantity).HasPrecision(14, 3);
            b.Property(l => l.UnitCost).HasPrecision(12, 2);
        });

        modelBuilder.Entity<PurchaseOrder>(b =>
        {
            b.ToTable("purchase_orders");
            b.HasTenantKey();
            b.Property(o => o.Note).HasMaxLength(200);
            b.HasIndex(o => new { o.ShopId, o.Number }).IsUnique();
            b.HasIndex(o => new { o.ShopId, o.SupplierId, o.Status });
            b.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrderLine>(b =>
        {
            b.ToTable("purchase_order_lines");
            b.HasTenantKey();
            b.Property(l => l.Quantity).HasPrecision(12, 3);
            b.Property(l => l.Received).HasPrecision(12, 3);
            b.Property(l => l.ShortClosed).HasPrecision(12, 3);
            b.Property(l => l.Rate).HasPrecision(12, 2);
        });

        modelBuilder.Entity<OrderShipment>(b =>
        {
            b.ToTable("order_shipments");
            b.HasTenantKey();
            b.Property(s => s.Transporter).HasMaxLength(80);
            b.Property(s => s.LrNo).HasMaxLength(40);
            b.Property(s => s.VehicleNo).HasMaxLength(20);
            b.Property(s => s.EwayBillNo).HasMaxLength(12);
            b.Property(s => s.Freight).HasPrecision(12, 2);
            b.HasIndex(s => new { s.ShopId, s.OrderId });
        });

        modelBuilder.Entity<OrderFollowUp>(b =>
        {
            b.ToTable("order_follow_ups");
            b.HasTenantKey();
            b.Property(f => f.Note).HasMaxLength(300);
            b.HasIndex(f => new { f.ShopId, f.OrderId, f.At });
        });

        modelBuilder.Entity<PurchaseCharge>(b =>
        {
            b.ToTable("purchase_charges");
            b.HasTenantKey();
            b.Property(c => c.Label).HasMaxLength(60);
            b.Property(c => c.Amount).HasPrecision(14, 2);
            b.HasIndex(c => new { c.ShopId, c.PurchaseId });
        });

        modelBuilder.Entity<BagType>(b =>
        {
            b.ToTable("bag_types");
            b.HasTenantKey();
            b.Property(t => t.Name).HasMaxLength(60);
            b.Property(t => t.SaleRate).HasPrecision(12, 2);
        });

        modelBuilder.Entity<BagMove>(b =>
        {
            b.ToTable("bag_moves");
            b.HasTenantKey();
            b.Property(m => m.Quantity).HasPrecision(12, 3);
            b.Property(m => m.Rate).HasPrecision(12, 2);
            b.Property(m => m.Amount).HasPrecision(14, 2);
            b.Property(m => m.Party).HasMaxLength(80);
            b.Property(m => m.Note).HasMaxLength(200);
            b.HasIndex(m => new { m.ShopId, m.BagTypeId });
            b.HasIndex(m => new { m.ShopId, m.OccurredAt });
        });

        modelBuilder.Entity<StockHold>(b =>
        {
            b.ToTable("stock_holds");
            b.HasTenantKey();
            b.Property(h => h.Quantity).HasPrecision(12, 3);
            b.Property(h => h.PartyName).HasMaxLength(80);
            b.Property(h => h.Note).HasMaxLength(200);
            b.HasIndex(h => new { h.ShopId, h.Status, h.ProductId });
        });

        modelBuilder.Entity<SupplierItem>(b =>
        {
            b.ToTable("supplier_items");
            b.HasTenantKey();
            b.Property(s => s.SupplierCode).HasMaxLength(40);
            b.Property(s => s.CaseQty).HasPrecision(12, 3);
            b.Property(s => s.MinQty).HasPrecision(12, 3);
            b.HasIndex(s => new { s.ShopId, s.ProductId, s.SupplierId }).IsUnique();
        });

        modelBuilder.Entity<ArrivalLine>(b =>
        {
            b.ToTable("goods_receipt_lines");
            b.HasTenantKey();
            b.Property(l => l.Received).HasPrecision(12, 3);
            b.Property(l => l.Damaged).HasPrecision(12, 3);
            b.Property(l => l.Bags).HasPrecision(12, 3);
            b.Property(l => l.Rate).HasPrecision(12, 2);
            b.HasIndex(l => new { l.ShopId, l.ProductId, l.OccurredAt });
            b.HasIndex(l => new { l.ShopId, l.SupplierId, l.OccurredAt });
            b.HasIndex(l => new { l.ShopId, l.OrderId });
            b.HasIndex(l => new { l.ShopId, l.ReceiptId });
            b.HasIndex(l => new { l.ShopId, l.PurchaseId });
        });

        modelBuilder.Entity<SupplierEntry>(b =>
        {
            b.ToTable("supplier_entries");
            b.HasTenantKey();
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.Property(e => e.Note).HasMaxLength(40);
            b.Property(e => e.Reference).HasMaxLength(40);
            b.HasIndex(e => new { e.ShopId, e.SupplierId });
        });

        modelBuilder.Entity<GroceryKhata.Api.Ledger.PurchaseUpload>(b =>
        {
            b.ToTable("purchase_uploads");
            b.HasTenantKey();
            b.Property(u => u.Source).HasMaxLength(10);
            b.HasIndex(u => new { u.ShopId, u.UsedAt });
        });

        modelBuilder.Entity<Ledger.PushDevice>(b =>
        {
            b.ToTable("push_devices");
            b.HasTenantKey();
            b.Property(d => d.Endpoint).HasMaxLength(1000);
            b.Property(d => d.P256dh).HasMaxLength(200);
            b.Property(d => d.Auth).HasMaxLength(100);
            b.HasIndex(d => d.Endpoint).IsUnique();
        });

        modelBuilder.Entity<Ledger.PushKey>(b =>
        {
            b.ToTable("push_keys");
            b.Property(k => k.Id).ValueGeneratedNever();
        });
    }
}
