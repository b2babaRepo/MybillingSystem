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
    /// <summary>The household side: shopping lists, orders, carts, the online store and WhatsApp.</summary>
    private void ConfigurePlanner(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Planner.CustomerCart>(b =>
        {
            b.ToTable("customer_carts");
            b.Property(c => c.Lines).HasColumnType("jsonb");
            b.HasIndex(c => new { c.UserId, c.ShopId }).IsUnique();
            b.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Planner.CustomerPushDevice>(b =>
        {
            b.ToTable("customer_push_devices");
            b.Property(d => d.Endpoint).HasMaxLength(1000);
            b.Property(d => d.P256dh).HasMaxLength(200);
            b.Property(d => d.Auth).HasMaxLength(100);
            b.Property(d => d.Language).HasMaxLength(5);
            b.HasIndex(d => d.Endpoint).IsUnique();
            b.HasIndex(d => d.UserId);
            b.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Planner.SavedItem>(b =>
        {
            b.ToTable("saved_items");
            b.HasIndex(x => new { x.UserId, x.ShopId, x.ProductId }).IsUnique();
            b.HasIndex(x => x.WasOut).HasFilter("was_out");
            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Planner.DeliveryPlan>(b =>
        {
            b.ToTable("delivery_plans");
            b.Property(x => x.Name).HasMaxLength(80);
            b.Property(x => x.Phone).HasMaxLength(20);
            b.Property(x => x.Address).HasMaxLength(300);
            b.Property(x => x.ProductName).HasMaxLength(200);
            b.HasIndex(x => new { x.ShopId, x.UserId });
            b.HasIndex(x => new { x.ShopId, x.StoppedAt });
        });

        modelBuilder.Entity<Planner.UsageEvent>(b =>
        {
            b.ToTable("usage_events");
            b.Property(x => x.Detail).HasMaxLength(10);
            b.HasIndex(x => new { x.CreatedAt, x.Kind });
            b.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<Planner.SavedShop>(b =>
        {
            b.ToTable("saved_shops");
            b.HasIndex(x => new { x.UserId, x.ShopId }).IsUnique();
            b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
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
            b.Property(l => l.RequestedName).HasMaxLength(160);
            // Lines from before options existed: the shop chose, nothing waits.
            b.Property(l => l.Preference).HasDefaultValue(ReplacePreference.ShopChooses);
            b.Property(l => l.Approval).HasDefaultValue(LineApproval.None);
            b.Property(l => l.ShopNote).HasMaxLength(200);
            b.Property(l => l.ReservedQuantity).HasPrecision(12, 3);
            b.HasTenantForeignKey<OrderLine, Product>(nameof(OrderLine.ProductId));
            b.HasIndex(l => new { l.ShopId, l.OrderId, l.LineNo }).IsUnique();
        });

        modelBuilder.Entity<GroceryKhata.Api.Planner.ShopWhatsApp>(b =>
        {
            b.ToTable("shop_whatsapp");
            b.HasTenantKey();
            b.Property(w => w.WabaId).HasMaxLength(40);
            b.Property(w => w.PhoneNumberId).HasMaxLength(40);
            b.Property(w => w.DisplayNumber).HasMaxLength(30);
            b.Property(w => w.VerifiedName).HasMaxLength(120);
            b.HasIndex(w => w.ShopId).IsUnique();
        });

        modelBuilder.Entity<GroceryKhata.Api.Planner.ShopWhatsAppMessage>(b =>
        {
            b.ToTable("shop_whatsapp_messages");
            b.HasTenantKey();
            b.Property(m => m.Phone).HasMaxLength(20);
            b.Property(m => m.Kind).HasMaxLength(20);
            b.Property(m => m.Body).HasMaxLength(4000);
            b.Property(m => m.MetaMessageId).HasMaxLength(120);
            b.Property(m => m.Status).HasMaxLength(20);
            b.Property(m => m.Error).HasMaxLength(300);
            b.Property(m => m.Direction).HasConversion<string>().HasMaxLength(5);
            b.HasIndex(m => new { m.ShopId, m.Phone, m.At });
            b.HasIndex(m => m.MetaMessageId);
        });

        modelBuilder.Entity<GroceryKhata.Api.Planner.WhatsAppNumber>(b =>
        {
            b.ToTable("whatsapp_numbers");
            b.HasKey(n => n.PhoneNumberId);
            b.Property(n => n.PhoneNumberId).HasMaxLength(40);
            b.HasIndex(n => n.ShopId);
        });

        // A household's own record (not a shop's): what the reader proposed for one input.
        modelBuilder.Entity<AiProposal>(b =>
        {
            b.ToTable("ai_proposals");
            b.Property(p => p.InputText).HasMaxLength(2000);
            b.Property(p => p.Reader).HasMaxLength(40);
            b.Property(p => p.Lines).HasColumnType("jsonb");
            b.Property(p => p.Source).HasDefaultValue(ProposalSource.App);
            b.HasIndex(p => new { p.UserId, p.CreatedAt });
            b.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
