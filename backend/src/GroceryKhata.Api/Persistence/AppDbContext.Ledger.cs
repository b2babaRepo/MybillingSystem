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
    /// <summary>The khata and bills: customers, entries, bills, credit notes, receipts, disputes, cash and bank, loans.</summary>
    private void ConfigureLedger(ModelBuilder modelBuilder)
    {

        // No shop filter: the WhatsApp bot looks a sender up here before it knows the centre. Only phone → farmer links.
        modelBuilder.Entity<DairyFarmerPhone>(b =>
        {
            b.ToTable("dairy_farmer_phones");
            b.HasKey(p => new { p.Phone, p.FarmerId });
            b.Property(p => p.Phone).HasMaxLength(20);
            b.HasIndex(p => p.FarmerId);
        });

        modelBuilder.Entity<Challan>(b =>
        {
            b.ToTable("challans");
            b.HasTenantKey();
            b.Property(c => c.Lines).HasColumnType("jsonb");
            b.Property(c => c.Site).HasMaxLength(80);
            b.Property(c => c.VehicleNumber).HasMaxLength(15);
            b.Property(c => c.PlaceOfSupply).HasMaxLength(2);
            b.HasIndex(c => new { c.ShopId, c.CustomerId, c.InvoiceId });
            b.HasTenantForeignKey<Challan, Customer>(nameof(Challan.CustomerId));
        });

        modelBuilder.Entity<Painter>(b =>
        {
            b.ToTable("painters");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(120);
            b.Property(p => p.Phone).HasMaxLength(16);
            b.Property(p => p.CommissionPercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<PainterPayout>(b =>
        {
            b.ToTable("painter_payouts");
            b.HasTenantKey();
            b.Property(p => p.Amount).HasPrecision(14, 2);
            b.Property(p => p.Mode).HasConversion<string>().HasMaxLength(10);
            b.Property(p => p.Note).HasMaxLength(120);
            b.HasIndex(p => new { p.ShopId, p.PainterId });
        });

        modelBuilder.Entity<BrandScheme>(b =>
        {
            b.ToTable("brand_schemes");
            b.HasTenantKey();
            b.Property(x => x.Name).HasMaxLength(80);
            b.Property(x => x.Brand).HasMaxLength(40);
            b.Property(x => x.Basis).HasMaxLength(10);
            b.Property(x => x.Target).HasPrecision(14, 2);
            b.Property(x => x.RebatePercent).HasPrecision(5, 2);
            b.Property(x => x.Received).HasPrecision(14, 2);
            b.Property(x => x.Note).HasMaxLength(120);
        });

        modelBuilder.Entity<SaleOrder>(b =>
        {
            b.ToTable("sale_orders");
            b.HasTenantKey();
            b.Property(o => o.Lines).HasColumnType("jsonb");
            b.Property(o => o.BillIds).HasColumnType("jsonb");
            b.Property(o => o.PartyName).HasMaxLength(80);
            b.Property(o => o.Note).HasMaxLength(300);
            b.Property(o => o.Total).HasPrecision(14, 2);
            b.HasIndex(o => new { o.ShopId, o.Number }).IsUnique();
        });

        modelBuilder.Entity<Estimate>(b =>
        {
            b.ToTable("estimates");
            b.HasTenantKey();
            b.Property(e => e.Lines).HasColumnType("jsonb");
            b.Property(e => e.PartyName).HasMaxLength(80);
            b.Property(e => e.Note).HasMaxLength(300);
            b.HasIndex(e => new { e.ShopId, e.Number }).IsUnique();
        });

        modelBuilder.Entity<CashCount>(b =>
        {
            b.ToTable("cash_counts");
            b.HasTenantKey();
            b.Property(c => c.Note).HasMaxLength(200);
            b.HasIndex(c => new { c.ShopId, c.Day });
        });

        modelBuilder.Entity<CashierShift>(b =>
        {
            b.ToTable("cashier_shifts");
            b.HasTenantKey();
            b.Property(s => s.Counter).HasMaxLength(30);
            b.Property(s => s.Note).HasMaxLength(200);
            // One open shift per counter and per cashier.
            b.HasIndex(s => new { s.ShopId, s.Counter }).IsUnique().HasFilter("closed_at IS NULL");
            b.HasIndex(s => new { s.ShopId, s.CashierUserId }).IsUnique().HasFilter("closed_at IS NULL");
            b.HasIndex(s => new { s.ShopId, s.OpenedAt });
        });

        modelBuilder.Entity<GiftVoucher>(b =>
        {
            b.ToTable("gift_vouchers");
            b.HasTenantKey();
            b.Property(v => v.Code).HasMaxLength(12);
            b.Property(v => v.BuyerName).HasMaxLength(80);
            b.HasIndex(v => new { v.ShopId, v.Code }).IsUnique();
            b.HasIndex(v => new { v.ShopId, v.SoldAt });
        });

        // Not shop-scoped on purpose: a mall's key is looked up before any shop is chosen (see MallFeeds).
        modelBuilder.Entity<MallFeed>(b =>
        {
            b.ToTable("mall_feeds");
            b.Property(f => f.Name).HasMaxLength(80);
            b.Property(f => f.KeyPrefix).HasMaxLength(12);
            b.Property(f => f.KeyHash).HasMaxLength(64);
            b.Property(f => f.PushUrl).HasMaxLength(300);
            b.Property(f => f.LastPushError).HasMaxLength(300);
            b.HasIndex(f => f.KeyHash).IsUnique();
            b.HasIndex(f => f.ShopId);
        });

        modelBuilder.Entity<Shops.Terminal>(b =>
        {
            b.ToTable("terminals");
            b.HasTenantKey();
            b.Property(t => t.Code).HasMaxLength(4);
            b.Property(t => t.Name).HasMaxLength(30);
            b.Property(t => t.MallMachineId).HasMaxLength(40);
            b.HasIndex(t => new { t.ShopId, t.Code }).IsUnique();
        });

        modelBuilder.Entity<LoyaltyEntry>(b =>
        {
            b.ToTable("loyalty_entries");
            b.HasTenantKey();
            b.HasIndex(e => new { e.ShopId, e.CustomerId });
            b.HasIndex(e => e.BillId).HasFilter("bill_id IS NOT NULL");
        });

        modelBuilder.Entity<HeldBill>(b =>
        {
            b.ToTable("held_bills");
            b.HasTenantKey();
            b.Property(h => h.Counter).HasMaxLength(30);
            b.Property(h => h.Label).HasMaxLength(120);
            b.Property(h => h.Payload).HasColumnType("jsonb");
            b.HasIndex(h => new { h.ShopId, h.HeldAt });
        });

        modelBuilder.Entity<CreditNote>(b =>
        {
            b.ToTable("credit_notes");
            b.HasTenantKey();
            b.Ignore(n => n.NoteNo);
            b.HasIndex(n => n.ShiftId).HasFilter("shift_id IS NOT NULL");
            b.Property(n => n.FinancialYear).HasMaxLength(7);
            b.Property(n => n.Reason).HasMaxLength(200);
            b.Property(n => n.Total).HasPrecision(12, 2);
            b.HasIndex(n => new { n.ShopId, n.FinancialYear, n.Number }).IsUnique();
            b.HasIndex(n => new { n.ShopId, n.BillId });
            b.HasMany(n => n.Lines).WithOne().HasForeignKey(l => l.CreditNoteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditNoteLine>(b =>
        {
            b.ToTable("credit_note_lines");
            b.HasTenantKey();
            b.Property(l => l.Name).HasMaxLength(120);
            b.Property(l => l.HsnCode).HasMaxLength(8);
            b.Property(l => l.Quantity).HasPrecision(12, 3);
            foreach (var money in new[] { nameof(CreditNoteLine.Amount), nameof(CreditNoteLine.TaxableValue), nameof(CreditNoteLine.Cgst), nameof(CreditNoteLine.Sgst), nameof(CreditNoteLine.Igst) })
            {
                b.Property(money).HasPrecision(12, 2);
            }

            b.Property(l => l.GstRate).HasPrecision(5, 2);
        });

        modelBuilder.Entity<SaleOffer>(b =>
        {
            b.ToTable("sale_offers");
            b.HasTenantKey();
            b.Property(o => o.Name).HasMaxLength(60);
            b.Property(o => o.Category).HasMaxLength(60);
            b.Property(o => o.Value).HasPrecision(12, 2);
            b.HasIndex(o => new { o.ShopId, o.EndsAt });
        });

        modelBuilder.Entity<Ledger.BalanceItem>(b =>
        {
            b.ToTable("balance_items");
            b.HasTenantKey();
            b.Property(i => i.Name).HasMaxLength(80);
            b.Property(i => i.Amount).HasPrecision(14, 2);
            b.Property(i => i.Kind).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<GroceryKhata.Api.Ledger.Loan>(b =>
        {
            b.ToTable("loans");
            b.HasTenantKey();
            b.Property(l => l.Lender).HasMaxLength(80);
            b.Property(l => l.Note).HasMaxLength(200);
            b.Property(l => l.Amount).HasPrecision(14, 2);
            b.Property(l => l.RatePercent).HasPrecision(6, 3);
            b.Property(l => l.Emi).HasPrecision(14, 2);
            b.Property(l => l.ReceivedInto).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<GroceryKhata.Api.Ledger.LoanPayment>(b =>
        {
            b.ToTable("loan_payments");
            b.HasTenantKey();
            b.Property(p => p.Principal).HasPrecision(14, 2);
            b.Property(p => p.Interest).HasPrecision(14, 2);
            b.HasIndex(p => new { p.ShopId, p.LoanId });
        });

        modelBuilder.Entity<Ledger.CashMove>(b =>
        {
            b.ToTable("cash_moves");
            b.HasTenantKey();
            b.Property(m => m.Amount).HasPrecision(12, 2);
            b.Property(m => m.Note).HasMaxLength(120);
            b.Property(m => m.BankAccount).HasMaxLength(40);
            b.Ignore(m => m.CashEffect);
            b.HasIndex(m => new { m.ShopId, m.OccurredAt });
        });

        modelBuilder.Entity<Expense>(b =>
        {
            b.ToTable("expenses");
            b.HasTenantKey();
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.Property(e => e.Note).HasMaxLength(120);
            b.HasIndex(e => new { e.ShopId, e.OccurredAt });
        });

        modelBuilder.Entity<BalanceCheck>(b =>
        {
            b.ToTable("balance_checks");
            b.HasTenantKey();
            b.Property(c => c.Details).HasColumnType("jsonb");
            b.HasIndex(c => new { c.ShopId, c.CheckedAt });
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("customers");
            b.HasTenantKey();
            b.Property(c => c.PriceGroup).HasMaxLength(12);
            b.Property(c => c.Name).HasMaxLength(120);
            b.Property(c => c.State).HasMaxLength(2);
            b.Property(c => c.Phone).HasMaxLength(16);
            b.HasIndex(c => new { c.ShopId, c.Name });
            b.HasIndex(c => new { c.ShopId, c.Phone });
            // A household finds its own khata in every shop by phone (see KhataViewEndpoints).
            b.HasIndex(c => c.Phone);
            b.Property(c => c.KhataToken).HasMaxLength(64);
            b.HasOne<Shop>().WithMany().HasForeignKey(c => c.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bill>(b =>
        {
            b.ToTable("bills");
            b.Property(x => x.Payments).HasColumnType("jsonb");
            b.Property(x => x.Prefix).HasMaxLength(3);
            b.Property(x => x.Fields).HasColumnType("jsonb");
            b.HasTenantKey();
            b.HasIndex(x => x.ShiftId).HasFilter("shift_id IS NOT NULL");
            b.HasIndex(x => new { x.ShopId, x.OccurredAt });
            b.HasTenantForeignKey<Bill, Customer>(nameof(Bill.CustomerId));
            b.Property(x => x.Site).HasMaxLength(80);
            b.Property(x => x.VehicleNumber).HasMaxLength(15);
            b.HasIndex(x => new { x.ShopId, x.PainterId, x.OccurredAt });
            b.Property(x => x.FinancialYear).HasMaxLength(7);
            b.Ignore(x => x.BillNo);
            b.HasIndex(x => new { x.ShopId, x.FinancialYear, x.Number }).IsUnique();
            b.HasMany(x => x.Lines).WithOne()
                .HasForeignKey(l => new { l.ShopId, l.BillId })
                .HasPrincipalKey(x => new { x.ShopId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillLine>(b =>
        {
            b.ToTable("bill_lines");
            b.Property(l => l.Name).HasMaxLength(120);
            b.Property(l => l.OfferName).HasMaxLength(60);
            b.HasIndex(l => l.OfferId).HasFilter("offer_id IS NOT NULL");
            b.Property(l => l.Quantity).HasPrecision(12, 3);
            b.Property(l => l.GstRate).HasPrecision(5, 2);
            b.Property(l => l.HsnCode).HasMaxLength(8);
            b.Property(l => l.Batch).HasMaxLength(200);
            b.Property(l => l.UnitCost).HasPrecision(12, 2);
            b.Property(l => l.TaxableValue).HasPrecision(12, 2);
            b.Property(l => l.Cgst).HasPrecision(12, 2);
            b.Property(l => l.Sgst).HasPrecision(12, 2);
            b.Property(l => l.Igst).HasPrecision(12, 2);
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
            b.Property(e => e.UpiRef).HasMaxLength(30);
            b.Property(e => e.ChequeNo).HasMaxLength(12);
            b.Property(e => e.ChequeBank).HasMaxLength(40);
            b.HasIndex(e => new { e.ShopId, e.ChequeStatus }).HasFilter("cheque_status IS NOT NULL");
            // Not unique: a corrected or split payment shows the same reference. One UTR, one receipt is kept on the receipt.
            b.HasIndex(e => new { e.ShopId, e.UpiRef }).HasFilter("upi_ref IS NOT NULL");
        });

        modelBuilder.Entity<Ledger.BalanceConfirmation>(b =>
        {
            b.ToTable("balance_confirmations");
            b.HasTenantKey();
            b.HasIndex(c => new { c.ShopId, c.CustomerId, c.CreatedAt });
            b.HasTenantForeignKey<Ledger.BalanceConfirmation, Customer>(nameof(Ledger.BalanceConfirmation.CustomerId));
        });

        modelBuilder.Entity<Ledger.CustomerConsent>(b =>
        {
            b.ToTable("customer_consents");
            b.HasTenantKey();
            b.HasIndex(c => new { c.ShopId, c.CustomerId, c.RecordedAt });
            b.HasTenantForeignKey<Ledger.CustomerConsent, Customer>(nameof(Ledger.CustomerConsent.CustomerId));
        });

        modelBuilder.Entity<Ledger.ReminderLog>(b =>
        {
            b.ToTable("reminder_logs");
            b.HasTenantKey();
            b.HasIndex(r => new { r.ShopId, r.CustomerId, r.SentAt });
            b.HasTenantForeignKey<Ledger.ReminderLog, Customer>(nameof(Ledger.ReminderLog.CustomerId));
        });

        modelBuilder.Entity<Ledger.IncomingReceipt>(b =>
        {
            b.ToTable("incoming_receipts");
            b.HasTenantKey();
            b.Property(r => r.Amount).HasPrecision(14, 2);
            b.HasIndex(r => new { r.ShopId, r.Utr }).IsUnique().HasFilter("utr IS NOT NULL");
            b.HasIndex(r => new { r.ShopId, r.Fingerprint }).IsUnique().HasFilter("fingerprint IS NOT NULL");
            b.HasIndex(r => new { r.ShopId, r.ReceivedAt });
            b.HasTenantForeignKey<Ledger.IncomingReceipt, Customer>(nameof(Ledger.IncomingReceipt.ClaimedByCustomerId));
            b.HasOne<Shop>().WithMany().HasForeignKey(r => r.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Ledger.ReceiptAllocation>(b =>
        {
            b.ToTable("receipt_allocations");
            b.HasTenantKey();
            b.Property(a => a.Amount).HasPrecision(14, 2);
            b.HasIndex(a => new { a.ShopId, a.ReceiptId });
            b.HasIndex(a => new { a.ShopId, a.LedgerEntryId }).IsUnique();
            b.HasTenantForeignKey<Ledger.ReceiptAllocation, Ledger.IncomingReceipt>(nameof(Ledger.ReceiptAllocation.ReceiptId));
            b.HasTenantForeignKey<Ledger.ReceiptAllocation, Customer>(nameof(Ledger.ReceiptAllocation.CustomerId));
            b.HasTenantForeignKey<Ledger.ReceiptAllocation, LedgerEntry>(nameof(Ledger.ReceiptAllocation.LedgerEntryId));
        });

        modelBuilder.Entity<Dispute>(b =>
        {
            b.ToTable("disputes");
            b.HasTenantKey();
            b.Property(d => d.Message).HasMaxLength(300);
            b.Property(d => d.Resolution).HasMaxLength(300);
            // One open dispute per entry: asking again while it's open adds nothing.
            b.HasIndex(d => d.EntryId).IsUnique().HasFilter("resolved_at IS NULL");
            b.HasIndex(d => new { d.ShopId, d.ResolvedAt });
            b.HasTenantForeignKey<Dispute, Customer>(nameof(Dispute.CustomerId));
            b.HasTenantForeignKey<Dispute, LedgerEntry>(nameof(Dispute.EntryId));
            b.HasMany(d => d.Items).WithOne().HasForeignKey(i => i.DisputeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DisputeItem>(b =>
        {
            b.ToTable("dispute_items");
            b.HasTenantKey();
            b.Property(i => i.ReceivedQuantity).HasPrecision(12, 3);
            b.Property(i => i.OutcomeNote).HasMaxLength(300);
            b.HasIndex(i => new { i.DisputeId, i.BillLineId }).IsUnique();
            b.HasTenantForeignKey<DisputeItem, BillLine>(nameof(DisputeItem.BillLineId));
        });
    }
}
