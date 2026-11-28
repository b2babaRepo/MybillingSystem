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
    /// <summary>People, shops, memberships and sign-in; the platform's plans, billing and field agents; team kharcha.</summary>
    private void ConfigureAccounts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Billing.InvoiceCounter>(b =>
        {
            b.ToTable("invoice_counters");
            b.HasKey(c => c.Prefix);
            b.Property(c => c.Prefix).HasMaxLength(20);
        });

        modelBuilder.Entity<Billing.GatewayOrder>(b =>
        {
            b.ToTable("gateway_orders");
            b.Property(o => o.OrderId).HasMaxLength(40);
            b.Property(o => o.PlanCode).HasMaxLength(20);
            b.Property(o => o.OfferCode).HasMaxLength(40);
            b.Property(o => o.PaymentId).HasMaxLength(40);
            b.HasIndex(o => o.OrderId).IsUnique();
        });

        modelBuilder.Entity<Common.JobRun>(b =>
        {
            b.ToTable("job_runs");
            b.HasKey(r => r.Job);
            b.Property(r => r.Job).HasMaxLength(60);
        });

        modelBuilder.Entity<Common.SeenMessage>(b =>
        {
            b.ToTable("seen_messages");
            b.HasKey(m => m.MessageId);
            b.Property(m => m.MessageId).HasMaxLength(200);
        });

        modelBuilder.Entity<SavedAddress>(b =>
        {
            b.ToTable("saved_addresses");
            b.Property(a => a.Label).HasMaxLength(30);
            b.Property(a => a.Text).HasMaxLength(300);
            b.Property(a => a.Landmark).HasMaxLength(120);
            b.Property(a => a.Pincode).HasMaxLength(6);
            b.HasIndex(a => a.UserId);
            b.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Market>(b =>
        {
            b.ToTable("markets");
            b.Property(m => m.Name).HasMaxLength(120);
            b.Property(m => m.City).HasMaxLength(80);
            b.Property(m => m.State).HasMaxLength(80);
            b.Property(m => m.District).HasMaxLength(80);
            b.Property(m => m.Village).HasMaxLength(80);
            b.HasIndex(m => m.Name);
        });

        modelBuilder.Entity<ShopPhoto>(b =>
        {
            b.ToTable("shop_photos");
            b.Property(p => p.ContentType).HasMaxLength(40);
            b.HasIndex(p => p.ShopId).IsUnique();
            b.HasOne<Shop>().WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Common.AiUsageDay>(b =>
        {
            b.ToTable("ai_usage_days");
            b.HasKey(d => d.Day);
        });

        modelBuilder.Entity<Shop>(b =>
        {
            b.ToTable("shops");
            b.Property(s => s.JewelleryMaxOffPercent).HasPrecision(5, 2);
            b.Property(s => s.ContractorDiscountPercent).HasPrecision(5, 2);
            b.Property(s => s.DealerDiscountPercent).HasPrecision(5, 2);
            b.Property(s => s.State).HasMaxLength(80);
            b.Property(s => s.BillPrefix).HasMaxLength(3);
            b.Property(s => s.BillFields).HasColumnType("jsonb");
            b.Property(s => s.BankAccounts).HasColumnType("jsonb");
            b.Property(s => s.District).HasMaxLength(80);
            b.Property(s => s.City).HasMaxLength(80);
            b.Property(s => s.Village).HasMaxLength(80);
            b.HasOne<Market>().WithMany().HasForeignKey(s => s.MarketId).OnDelete(DeleteBehavior.SetNull);
            b.Property(s => s.Name).HasMaxLength(120);
            b.Property(s => s.Number).HasDefaultValueSql("nextval('shop_number_seq')").ValueGeneratedOnAdd();
            b.HasIndex(s => s.Number).IsUnique();
            b.Ignore(s => s.Code);
            b.Property(s => s.Slug).HasMaxLength(80);
            b.HasIndex(s => s.Slug).IsUnique();
            b.Property(s => s.Type).HasDefaultValue(ShopType.Kirana);
            b.Property(s => s.Area).HasMaxLength(120);
            b.Property(s => s.Address).HasMaxLength(200);
            b.Property(s => s.UpiId).HasMaxLength(60);
            b.Property(s => s.ReminderTemplate).HasMaxLength(500);
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

        modelBuilder.Entity<Shops.PlatformAdmin>(b =>
        {
            b.ToTable("platform_admins");
            b.HasIndex(a => a.UserId).IsUnique();
            b.Property(a => a.Role).HasDefaultValue(GroceryKhata.Api.Shops.AdminRole.Super);
            b.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shops.PlatformRole>(b =>
        {
            b.ToTable("platform_roles");
            b.Property(r => r.Name).HasMaxLength(40);
            b.HasIndex(r => r.Name).IsUnique();
        });

        modelBuilder.Entity<Shops.PlatformAdmin>().HasOne<Shops.PlatformRole>().WithMany().HasForeignKey(a => a.CustomRoleId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmailCode>(b =>
        {
            b.ToTable("email_codes");
            b.Property(c => c.Email).HasMaxLength(160);
            b.Property(c => c.CodeHash).HasMaxLength(64);
            b.HasIndex(c => new { c.UserId, c.Purpose, c.CreatedAt });
            b.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // One account per verified email: it is a way in.
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique().HasFilter("email_verified_at IS NOT NULL").HasDatabaseName("ix_users_verified_email");

        modelBuilder.Entity<OtpCode>(b =>
        {
            b.ToTable("otp_codes");
            b.Property(c => c.Phone).HasMaxLength(16);
            b.Property(c => c.CodeHash).HasMaxLength(64);
            b.HasIndex(c => new { c.Phone, c.CreatedAt });
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.Property(t => t.TokenHash).HasMaxLength(64);
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => t.UserId);
            b.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Platform billing: not shop-owned (admins see every shop's); a shop reads only its own, by its id from the token.
        modelBuilder.Entity<Billing.Plan>(b =>
        {
            b.ToTable("plans");
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Code).HasMaxLength(20);
            b.Property(x => x.Name).HasMaxLength(40);
        });
        modelBuilder.Entity<Billing.Subscription>(b =>
        {
            b.ToTable("subscriptions");
            b.HasIndex(x => x.ShopId).IsUnique();
            b.Property(x => x.PlanCode).HasMaxLength(20);
            b.Property(x => x.CompPlanCode).HasMaxLength(20);
            b.Property(x => x.CompReason).HasMaxLength(200);
            b.Property(x => x.HoldReason).HasMaxLength(200);
            b.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Billing.SubscriptionPayment>(b =>
        {
            b.ToTable("subscription_payments");
            b.HasIndex(x => new { x.ShopId, x.PaidAt });
            b.HasIndex(x => x.InvoiceNo).IsUnique();
            b.Property(x => x.PlanCode).HasMaxLength(20);
            b.Property(x => x.Reference).HasMaxLength(60);
            b.Property(x => x.OfferCode).HasMaxLength(20);
            b.Property(x => x.InvoiceNo).HasMaxLength(30);
            // Payments are tax records: a shop being deleted must not take them with it.
        });
        modelBuilder.Entity<Billing.PaymentClaim>(b =>
        {
            b.ToTable("payment_claims");
            b.HasIndex(x => new { x.Status, x.CreatedAt });
            b.HasIndex(x => x.UpiRef);
            b.Property(x => x.UpiRef).HasMaxLength(30);
            b.Property(x => x.PlanCode).HasMaxLength(20);
            b.Property(x => x.OfferCode).HasMaxLength(20);
            b.Property(x => x.Note).HasMaxLength(200);
            b.Property(x => x.Status).HasDefaultValue(Billing.ClaimStatus.Pending);
        });
        modelBuilder.Entity<Billing.Offer>(b =>
        {
            b.ToTable("offers");
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Code).HasMaxLength(20);
            b.Property(x => x.Title).HasMaxLength(80);
            b.Property(x => x.PlanCode).HasMaxLength(20);
        });
        modelBuilder.Entity<Billing.AgentProposal>(b =>
        {
            b.ToTable("agent_proposals");
            b.HasIndex(x => new { x.Status, x.CreatedAt });
            b.HasIndex(x => new { x.ShopId, x.Kind, x.CreatedAt });
            b.Property(x => x.Reason).HasMaxLength(300);
            b.Property(x => x.Status).HasDefaultValue(Billing.ProposalStatus.Pending);
        });

        modelBuilder.Entity<PharmacyProfile>(b =>
        {
            b.ToTable("pharmacy_profiles");
            b.HasTenantKey();
            b.Property(p => p.LicenceForm20).HasMaxLength(40);
            b.Property(p => p.LicenceForm21).HasMaxLength(40);
            b.Property(p => p.PharmacistName).HasMaxLength(80);
            b.Property(p => p.PharmacistRegNo).HasMaxLength(40);
            b.Property(p => p.Gstin).HasMaxLength(15);
            b.Property(p => p.FssaiNo).HasMaxLength(40);
            b.HasIndex(p => p.ShopId).IsUnique();
        });

        modelBuilder.Entity<PrintLook>(b =>
        {
            b.ToTable("print_looks");
            b.HasTenantKey();
            b.Property(p => p.Theme).HasMaxLength(20);
            b.Property(p => p.Terms).HasMaxLength(GroceryKhata.Api.Shops.PrintLooks.MaxTerms);
            b.HasIndex(p => p.ShopId).IsUnique();
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

        modelBuilder.Entity<Agents.ShopRegistration>(b =>
        {
            b.ToTable("shop_registrations");
            b.HasIndex(x => new { x.AgentId, x.CreatedAt });
            b.HasIndex(x => new { x.Status, x.CreatedAt });
            b.HasIndex(x => x.OwnerPhone);
            b.Property(x => x.ShopName).HasMaxLength(120);
            b.Property(x => x.Address).HasMaxLength(300);
            b.Property(x => x.Market).HasMaxLength(120);
            b.Property(x => x.OwnerName).HasMaxLength(120);
            b.Property(x => x.OwnerPhone).HasMaxLength(20);
            b.Property(x => x.AltPhone).HasMaxLength(20);
            b.Property(x => x.Gstin).HasMaxLength(15);
            b.Property(x => x.PlanCode).HasMaxLength(40);
            b.Property(x => x.PaymentRef).HasMaxLength(30);
            b.Property(x => x.RejectReason).HasMaxLength(200);
            b.HasOne<Agents.FieldAgent>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Photos).WithOne().HasForeignKey(p => p.RegistrationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Agents.RegistrationPhoto>(b =>
        {
            b.ToTable("registration_photos");
            b.Property(x => x.Kind).HasMaxLength(10);
            b.Property(x => x.ContentType).HasMaxLength(20);
        });

        modelBuilder.Entity<Teams.Team>(b =>
        {
            b.ToTable("teams");
            b.Property(x => x.Name).HasMaxLength(60);
            b.Property(x => x.InviteCode).HasMaxLength(8);
            b.HasIndex(x => x.InviteCode).IsUnique();
        });

        modelBuilder.Entity<Teams.TeamMember>(b =>
        {
            b.ToTable("team_members");
            b.Property(x => x.DisplayName).HasMaxLength(40);
            b.Property(x => x.ConsentVersion).HasMaxLength(30);
            b.HasIndex(x => new { x.TeamId, x.UserId });
            b.HasIndex(x => x.UserId);
            b.HasOne<Teams.Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Teams.TeamAdvance>(b =>
        {
            b.ToTable("team_advances");
            b.Property(x => x.Amount).HasPrecision(12, 2);
            b.Property(x => x.Note).HasMaxLength(120);
            b.HasIndex(x => new { x.TeamId, x.MemberId });
            b.HasOne<Teams.Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Teams.TeamMember>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Teams.TeamExpense>(b =>
        {
            b.ToTable("team_expenses");
            b.Property(x => x.Amount).HasPrecision(12, 2);
            b.Property(x => x.Note).HasMaxLength(120);
            b.Property(x => x.StruckReason).HasMaxLength(120);
            b.HasIndex(x => new { x.TeamId, x.OccurredAt });
            b.HasOne<Teams.Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Teams.TeamMember>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Agents.AgentPayout>(b =>
        {
            b.ToTable("agent_payouts");
            b.HasIndex(x => new { x.AgentId, x.CreatedAt });
            b.Property(x => x.Reference).HasMaxLength(60);
            b.HasOne<Agents.FieldAgent>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Agents.ConsentCode>(b =>
        {
            b.ToTable("consent_codes");
            b.HasIndex(x => new { x.Phone, x.CreatedAt });
            b.Property(x => x.Phone).HasMaxLength(20);
            b.Property(x => x.CodeHash).HasMaxLength(64);
        });

        modelBuilder.Entity<Agents.FieldAgent>(b =>
        {
            b.ToTable("field_agents");
            b.HasIndex(x => x.UserId).IsUnique();
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.AadhaarKey);
            b.HasIndex(x => new { x.Status, x.CreatedAt });
            b.Property(x => x.Code).HasMaxLength(12);
            b.Property(x => x.FullName).HasMaxLength(120);
            b.Property(x => x.Address).HasMaxLength(300);
            b.Property(x => x.Market).HasMaxLength(120);
            b.Property(x => x.AadhaarLast4).HasMaxLength(4);
            b.Property(x => x.AadhaarKey).HasMaxLength(64);
            b.Property(x => x.UpiId).HasMaxLength(60);
            b.Property(x => x.StatusReason).HasMaxLength(200);
            b.HasOne<Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Shops.PlatformActivity>(b =>
        {
            b.ToTable("platform_activities");
            b.Property(x => x.Action).HasMaxLength(120);
            b.Property(x => x.Detail).HasColumnType("jsonb");
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => new { x.ShopId, x.CreatedAt });
            b.HasIndex(x => new { x.AdminUserId, x.CreatedAt });
        });
    }
}
