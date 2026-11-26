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
    /// <summary>Jewellery shops: rates, tagged pieces, girvi, schemes, orders, the karigar workshop and old-metal vouchers.</summary>
    private void ConfigureJewellery(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<MetalRate>(b =>
        {
            b.ToTable("metal_rates");
            b.HasTenantKey();
            b.Property(r => r.Metal).HasMaxLength(10);
            b.Property(r => r.SellPerGram).HasPrecision(12, 2);
            b.Property(r => r.BuyPerGram).HasPrecision(12, 2);
            b.HasIndex(r => new { r.ShopId, r.Metal, r.Purity, r.CreatedAt });
        });

        modelBuilder.Entity<JewelleryPiece>(b =>
        {
            b.ToTable("jewellery_pieces");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(80);
            b.Property(p => p.Category).HasMaxLength(60);
            b.Property(p => p.Tray).HasMaxLength(30);
            b.Property(p => p.Metal).HasMaxLength(10);
            b.Property(p => p.MakingType).HasMaxLength(10);
            b.Property(p => p.Huid).HasMaxLength(6);
            b.Property(p => p.Gross).HasPrecision(10, 3);
            b.Property(p => p.Stone).HasPrecision(10, 3);
            b.Property(p => p.WastagePercent).HasPrecision(5, 2);
            b.Property(p => p.MakingValue).HasPrecision(14, 2);
            b.Property(p => p.StoneValue).HasPrecision(14, 2);
            b.Property(p => p.HallmarkCharge).HasPrecision(10, 2);
            b.Property(p => p.FixedPrice).HasPrecision(14, 2);
            b.HasIndex(p => new { p.ShopId, p.TagNo }).IsUnique();
            b.HasIndex(p => new { p.ShopId, p.Huid });
            b.HasIndex(p => new { p.ShopId, p.SoldBillId });
        });

        modelBuilder.Entity<JewelleryBill>(b =>
        {
            b.ToTable("jewellery_bills");
            b.HasTenantKey();
            b.Property(x => x.Pan).HasMaxLength(10);
            b.Property(x => x.MoneyPaid).HasPrecision(14, 2);
            b.Property(x => x.OldMetalValue).HasPrecision(14, 2);
            b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(x => new { x.ShopId, x.BillId }).IsUnique();
            b.HasIndex(x => new { x.ShopId, x.CustomerId, x.CreatedAt });
        });

        modelBuilder.Entity<JewelleryBillLine>(b =>
        {
            b.ToTable("jewellery_bill_lines");
            b.HasTenantKey();
            b.Property(l => l.Name).HasMaxLength(80);
            b.Property(l => l.Metal).HasMaxLength(10);
            b.Property(l => l.MakingType).HasMaxLength(10);
            b.Property(l => l.Huid).HasMaxLength(6);
            foreach (var w in new[] { nameof(JewelleryBillLine.Gross), nameof(JewelleryBillLine.Stone), nameof(JewelleryBillLine.Net) }) b.Property(w).HasPrecision(10, 3);
            foreach (var m in new[] { nameof(JewelleryBillLine.RatePerGram), nameof(JewelleryBillLine.MetalValue), nameof(JewelleryBillLine.WastagePercent), nameof(JewelleryBillLine.WastageValue),
                nameof(JewelleryBillLine.MakingValue), nameof(JewelleryBillLine.MakingAmount), nameof(JewelleryBillLine.StoneValue), nameof(JewelleryBillLine.HallmarkCharge),
                nameof(JewelleryBillLine.Taxable), nameof(JewelleryBillLine.Gst) }) b.Property(m).HasPrecision(14, 2);
            b.HasIndex(l => new { l.ShopId, l.BillId });
        });

        modelBuilder.Entity<MetalParty>(b =>
        {
            b.ToTable("metal_parties");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(80);
            b.Property(p => p.Phone).HasMaxLength(20);
            b.Property(p => p.Kind).HasMaxLength(10);
            b.Property(p => p.AllowancePercent).HasPrecision(5, 2);
            b.Property(p => p.LabourPerGram).HasPrecision(12, 2);
        });

        modelBuilder.Entity<MetalMove>(b =>
        {
            b.ToTable("metal_moves");
            b.HasTenantKey();
            b.Property(m => m.Kind).HasMaxLength(12);
            b.Property(m => m.Metal).HasMaxLength(10);
            b.Property(m => m.Note).HasMaxLength(200);
            b.Property(m => m.Gross).HasPrecision(12, 3);
            b.Property(m => m.Reading).HasPrecision(5, 2);
            b.Property(m => m.Fine).HasPrecision(12, 3);
            b.Property(m => m.Allowance).HasPrecision(12, 3);
            b.Property(m => m.BalanceEffect).HasPrecision(12, 3);
            b.Property(m => m.LooseEffect).HasPrecision(12, 3);
            b.Property(m => m.Labour).HasPrecision(14, 2);
            b.Property(m => m.RatePerGram).HasPrecision(12, 2);
            b.Property(m => m.CashEffect).HasPrecision(14, 2);
            b.Property(m => m.Mode).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(m => new { m.ShopId, m.Number }).IsUnique();
            b.HasIndex(m => new { m.ShopId, m.PartyId });
            b.HasIndex(m => new { m.ShopId, m.At });
        });

        modelBuilder.Entity<PieceStone>(b =>
        {
            b.ToTable("piece_stones");
            b.HasTenantKey();
            b.Property(s => s.Kind).HasMaxLength(40);
            b.Property(s => s.Shape).HasMaxLength(30);
            b.Property(s => s.Colour).HasMaxLength(20);
            b.Property(s => s.Clarity).HasMaxLength(20);
            b.Property(s => s.CertLab).HasMaxLength(20);
            b.Property(s => s.CertNo).HasMaxLength(40);
            b.Property(s => s.Carat).HasPrecision(10, 3);
            b.Property(s => s.RatePerCarat).HasPrecision(14, 2);
            b.Property(s => s.Value).HasPrecision(14, 2);
            b.HasIndex(s => new { s.ShopId, s.PieceId });
        });

        modelBuilder.Entity<TrayCount>(b =>
        {
            b.ToTable("tray_counts");
            b.HasTenantKey();
            b.Property(c => c.Tray).HasMaxLength(30);
            b.Property(c => c.Kind).HasMaxLength(10);
            b.Property(c => c.ExpectedGross).HasPrecision(12, 3);
            b.Property(c => c.CountedGross).HasPrecision(12, 3);
            b.HasIndex(c => new { c.ShopId, c.Tray, c.CreatedAt });
        });

        modelBuilder.Entity<WeighingScale>(b =>
        {
            b.ToTable("weighing_scales");
            b.HasTenantKey();
            b.Property(s => s.Name).HasMaxLength(60);
            b.Property(s => s.ReadsTo).HasPrecision(6, 3);
        });

        modelBuilder.Entity<GirviLoan>(b =>
        {
            b.ToTable("girvi_loans");
            b.HasTenantKey();
            b.Property(l => l.Items).HasMaxLength(200);
            b.Property(l => l.Metal).HasMaxLength(10);
            b.Property(l => l.Kyc).HasMaxLength(60);
            b.Property(l => l.Note).HasMaxLength(200);
            b.Property(l => l.Gross).HasPrecision(10, 3);
            b.Property(l => l.Fine).HasPrecision(10, 3);
            b.Property(l => l.Reading).HasPrecision(5, 2);
            b.Property(l => l.RatePerMonth).HasPrecision(5, 2);
            b.Property(l => l.PledgeValue).HasPrecision(14, 2);
            b.Property(l => l.Principal).HasPrecision(14, 2);
            b.Property(l => l.Mode).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(l => new { l.ShopId, l.Number }).IsUnique();
            b.HasIndex(l => new { l.ShopId, l.CreatedAt });
        });

        modelBuilder.Entity<GirviPayment>(b =>
        {
            b.ToTable("girvi_payments");
            b.HasTenantKey();
            b.Property(p => p.Amount).HasPrecision(14, 2);
            b.Property(p => p.InterestPart).HasPrecision(14, 2);
            b.Property(p => p.PrincipalPart).HasPrecision(14, 2);
            b.Property(p => p.Mode).HasConversion<string>().HasMaxLength(10);
            b.HasIndex(p => new { p.ShopId, p.LoanId });
            b.HasIndex(p => new { p.ShopId, p.At });
        });

        modelBuilder.Entity<SchemePlan>(b =>
        {
            b.ToTable("scheme_plans");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(60);
            b.Property(p => p.Instalment).HasPrecision(14, 2);
            b.Property(p => p.Bonus).HasPrecision(14, 2);
            b.Property(p => p.Kind).HasMaxLength(10).HasDefaultValue("amount");
        });

        modelBuilder.Entity<SchemeMember>(b =>
        {
            b.ToTable("scheme_members");
            b.HasTenantKey();
            b.HasIndex(m => new { m.ShopId, m.Number }).IsUnique();
            b.HasIndex(m => new { m.ShopId, m.CustomerId });
        });

        modelBuilder.Entity<SchemeInstalment>(b =>
        {
            b.ToTable("scheme_instalments");
            b.HasTenantKey();
            b.Property(i => i.Amount).HasPrecision(14, 2);
            b.Property(i => i.Grams).HasPrecision(10, 3);
            b.Property(i => i.RatePerGram).HasPrecision(12, 2);
            b.HasIndex(i => new { i.ShopId, i.MemberId });
            b.HasIndex(i => new { i.ShopId, i.EntryId }).IsUnique();
        });

        modelBuilder.Entity<JewelleryOrder>(b =>
        {
            b.ToTable("jewellery_orders");
            b.HasTenantKey();
            b.Property(o => o.Kind).HasMaxLength(10);
            b.Property(o => o.Status).HasMaxLength(12);
            b.Property(o => o.Description).HasMaxLength(200);
            b.Property(o => o.Note).HasMaxLength(200);
            b.Property(o => o.Metal).HasMaxLength(10);
            b.Property(o => o.Weight).HasPrecision(10, 3);
            b.Property(o => o.WeightOut).HasPrecision(10, 3);
            b.Property(o => o.FixedRate).HasPrecision(12, 2);
            b.Property(o => o.Charge).HasPrecision(14, 2);
            b.Property(o => o.Advance).HasPrecision(14, 2);
            b.HasIndex(o => new { o.ShopId, o.Number }).IsUnique();
            b.HasIndex(o => new { o.ShopId, o.Status, o.DueOn });
        });

        modelBuilder.Entity<ApprovalMemo>(b =>
        {
            b.ToTable("approval_memos");
            b.HasTenantKey();
            b.Property(m => m.Party).HasMaxLength(80);
            b.HasIndex(m => new { m.ShopId, m.Number }).IsUnique();
        });

        modelBuilder.Entity<ApprovalItem>(b =>
        {
            b.ToTable("approval_items");
            b.HasTenantKey();
            b.Property(i => i.GrossOut).HasPrecision(10, 3);
            b.Property(i => i.GrossBack).HasPrecision(10, 3);
            b.HasIndex(i => new { i.ShopId, i.MemoId });
            b.HasIndex(i => new { i.ShopId, i.PieceId });
        });

        modelBuilder.Entity<OldMetalPurchase>(b =>
        {
            b.ToTable("old_metal_purchases");
            b.HasTenantKey();
            b.Property(o => o.Metal).HasMaxLength(10);
            b.Property(o => o.Description).HasMaxLength(120);
            b.Property(o => o.Gross).HasPrecision(10, 3);
            b.Property(o => o.Fine).HasPrecision(10, 3);
            b.Property(o => o.Reading).HasPrecision(5, 2);
            b.Property(o => o.DeductionPercent).HasPrecision(5, 2);
            b.Property(o => o.RatePerGram).HasPrecision(12, 2);
            b.Property(o => o.Value).HasPrecision(14, 2);
            b.HasIndex(o => new { o.ShopId, o.BillId });
            b.HasIndex(o => new { o.ShopId, o.VoucherId });
        });

        modelBuilder.Entity<OldMetalVoucher>(b =>
        {
            b.ToTable("old_metal_vouchers");
            b.HasTenantKey();
            b.Property(v => v.SellerName).HasMaxLength(80);
            b.Property(v => v.SellerPhone).HasMaxLength(20);
            b.Property(v => v.SellerAddress).HasMaxLength(200);
            b.Property(v => v.Pan).HasMaxLength(10);
            b.Property(v => v.Mode).HasConversion<string>().HasMaxLength(10);
            b.Property(v => v.Total).HasPrecision(14, 2);
            b.HasIndex(v => new { v.ShopId, v.Number }).IsUnique();
        });
    }
}
