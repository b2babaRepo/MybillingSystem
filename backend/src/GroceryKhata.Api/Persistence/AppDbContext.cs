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
    public static readonly string[] ShopOwnedTables = ["products", "customers", "ledger_entries", "bills", "bill_lines", "orders", "order_lines", "stock_movements", "audit_log", "expenses", "balance_checks", "cash_counts", "challans", "painters", "suppliers", "purchases", "purchase_lines", "supplier_entries", "credit_notes", "credit_note_lines"];

    private Guid CurrentShopId => tenant.ShopId;

    /// <summary>The shop this request works in (Shops itself is not filtered by shop).</summary>
    public Guid ShopIdNow => tenant.ShopId;

    private Guid CurrentUserId => tenant.UserId;

    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();

    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Shop> Shops => Set<Shop>();

    public DbSet<Common.AiUsageDay> AiUsageDays => Set<Common.AiUsageDay>();

    public DbSet<Ledger.IncomingReceipt> IncomingReceipts => Set<Ledger.IncomingReceipt>();

    public DbSet<Ledger.CustomerConsent> CustomerConsents => Set<Ledger.CustomerConsent>();

    public DbSet<Ledger.ReminderLog> ReminderLogs => Set<Ledger.ReminderLog>();

    public DbSet<Ledger.ReceiptAllocation> ReceiptAllocations => Set<Ledger.ReceiptAllocation>();

    public DbSet<Market> Markets => Set<Market>();

    public DbSet<MasterItem> MasterItems => Set<MasterItem>();

    public DbSet<MasterItemImage> MasterItemImages => Set<MasterItemImage>();

    public DbSet<MasterSuggestion> MasterSuggestions => Set<MasterSuggestion>();

    public DbSet<SavedAddress> SavedAddresses => Set<SavedAddress>();

    public DbSet<Common.JobRun> JobRuns => Set<Common.JobRun>();

    public DbSet<Common.SeenMessage> SeenMessages => Set<Common.SeenMessage>();

    public DbSet<DairyFarmerPhone> DairyFarmerPhones => Set<DairyFarmerPhone>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();

    public DbSet<CreditNoteLine> CreditNoteLines => Set<CreditNoteLine>();

    public DbSet<Purchase> Purchases => Set<Purchase>();

    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();

    public DbSet<SupplierReturn> SupplierReturns => Set<SupplierReturn>();

    public DbSet<SupplierReturnLine> SupplierReturnLines => Set<SupplierReturnLine>();

    public DbSet<WarrantyClaim> WarrantyClaims => Set<WarrantyClaim>();

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Amc> Amcs => Set<Amc>();

    public DbSet<TripEntry> TripEntries => Set<TripEntry>();

    public DbSet<TripPhoto> TripPhotos => Set<TripPhoto>();

    public DbSet<SupplierEntry> SupplierEntries => Set<SupplierEntry>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<SaleOffer> SaleOffers => Set<SaleOffer>();

    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<ArrivalLine> ArrivalLines => Set<ArrivalLine>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<SupplierItem> SupplierItems => Set<SupplierItem>();

    public DbSet<StockHold> StockHolds => Set<StockHold>();

    public DbSet<Godown> Godowns => Set<Godown>();

    public DbSet<Ledger.BalanceItem> BalanceItems => Set<Ledger.BalanceItem>();

    public DbSet<BagType> BagTypes => Set<BagType>();

    public DbSet<PurchaseCharge> PurchaseCharges => Set<PurchaseCharge>();

    public DbSet<FestivalSetting> FestivalSettings => Set<FestivalSetting>();

    public DbSet<BagMove> BagMoves => Set<BagMove>();

    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();

    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();

    public DbSet<OrderShipment> OrderShipments => Set<OrderShipment>();

    public DbSet<OrderFollowUp> OrderFollowUps => Set<OrderFollowUp>();

    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<SaleOrder> SaleOrders => Set<SaleOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    public DbSet<Billing.GatewayOrder> GatewayOrders => Set<Billing.GatewayOrder>();

    public DbSet<Billing.InvoiceCounter> InvoiceCounters => Set<Billing.InvoiceCounter>();

    public DbSet<ShopPhoto> ShopPhotos => Set<ShopPhoto>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();

    public DbSet<Shops.PlatformAdmin> PlatformAdmins => Set<Shops.PlatformAdmin>();

    public DbSet<Shops.PlatformActivity> PlatformActivities => Set<Shops.PlatformActivity>();

    public DbSet<Agents.FieldAgent> FieldAgents => Set<Agents.FieldAgent>();

    public DbSet<Agents.ShopRegistration> ShopRegistrations => Set<Agents.ShopRegistration>();

    public DbSet<Agents.RegistrationPhoto> RegistrationPhotos => Set<Agents.RegistrationPhoto>();

    public DbSet<Agents.ConsentCode> ConsentCodes => Set<Agents.ConsentCode>();

    public DbSet<Agents.AgentPayout> AgentPayouts => Set<Agents.AgentPayout>();

    public DbSet<Teams.Team> Teams => Set<Teams.Team>();

    public DbSet<Teams.TeamMember> TeamMembers => Set<Teams.TeamMember>();

    public DbSet<Teams.TeamExpense> TeamExpenses => Set<Teams.TeamExpense>();

    public DbSet<Teams.TeamAdvance> TeamAdvances => Set<Teams.TeamAdvance>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<AiProposal> AiProposals => Set<AiProposal>();

    public DbSet<BalanceCheck> BalanceChecks => Set<BalanceCheck>();

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<MetalRate> MetalRates => Set<MetalRate>();
    public DbSet<JewelleryPiece> JewelleryPieces => Set<JewelleryPiece>();
    public DbSet<JewelleryBill> JewelleryBills => Set<JewelleryBill>();
    public DbSet<JewelleryBillLine> JewelleryBillLines => Set<JewelleryBillLine>();
    public DbSet<OldMetalPurchase> OldMetalPurchases => Set<OldMetalPurchase>();
    public DbSet<GirviLoan> GirviLoans => Set<GirviLoan>();
    public DbSet<MetalParty> MetalParties => Set<MetalParty>();
    public DbSet<MetalMove> MetalMoves => Set<MetalMove>();
    public DbSet<PieceStone> PieceStones => Set<PieceStone>();
    public DbSet<TrayCount> TrayCounts => Set<TrayCount>();
    public DbSet<WeighingScale> WeighingScales => Set<WeighingScale>();
    public DbSet<GirviPayment> GirviPayments => Set<GirviPayment>();
    public DbSet<SchemePlan> SchemePlans => Set<SchemePlan>();
    public DbSet<SchemeMember> SchemeMembers => Set<SchemeMember>();
    public DbSet<SchemeInstalment> SchemeInstalments => Set<SchemeInstalment>();
    public DbSet<JewelleryOrder> JewelleryOrders => Set<JewelleryOrder>();
    public DbSet<ApprovalMemo> ApprovalMemos => Set<ApprovalMemo>();
    public DbSet<ApprovalItem> ApprovalItems => Set<ApprovalItem>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobEntry> JobEntries => Set<JobEntry>();

    public DbSet<Machine> Machines => Set<Machine>();

    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<TableBooking> TableBookings => Set<TableBooking>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Stay> Stays => Set<Stay>();

    public DbSet<StayCharge> StayCharges => Set<StayCharge>();

    public DbSet<RatePeriod> RatePeriods => Set<RatePeriod>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<BridalBooking> BridalBookings => Set<BridalBooking>();

    public DbSet<BridalEvent> BridalEvents => Set<BridalEvent>();

    public DbSet<ServiceProduct> ServiceProducts => Set<ServiceProduct>();

    public DbSet<ClientCard> ClientCards => Set<ClientCard>();

    public DbSet<PharmacyProfile> PharmacyProfiles => Set<PharmacyProfile>();

    public DbSet<PrintLook> PrintLooks => Set<PrintLook>();

    public DbSet<GroceryKhata.Api.Ledger.PurchaseUpload> PurchaseUploads => Set<GroceryKhata.Api.Ledger.PurchaseUpload>();

    public DbSet<GroceryKhata.Api.Ledger.Loan> Loans => Set<GroceryKhata.Api.Ledger.Loan>();

    public DbSet<GroceryKhata.Api.Planner.ShopWhatsApp> ShopWhatsApps => Set<GroceryKhata.Api.Planner.ShopWhatsApp>();

    public DbSet<GroceryKhata.Api.Planner.WhatsAppNumber> WhatsAppNumbers => Set<GroceryKhata.Api.Planner.WhatsAppNumber>();

    public DbSet<GroceryKhata.Api.Planner.ShopWhatsAppMessage> ShopWhatsAppMessages => Set<GroceryKhata.Api.Planner.ShopWhatsAppMessage>();

    public DbSet<GroceryKhata.Api.Ledger.LoanPayment> LoanPayments => Set<GroceryKhata.Api.Ledger.LoanPayment>();

    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();

    public DbSet<ProductSerial> ProductSerials => Set<ProductSerial>();

    public DbSet<BatchSale> BatchSales => Set<BatchSale>();

    public DbSet<BillPrescription> BillPrescriptions => Set<BillPrescription>();

    public DbSet<PrescriptionPhoto> PrescriptionPhotos => Set<PrescriptionPhoto>();
    public DbSet<DairyFarmer> DairyFarmers => Set<DairyFarmer>();

    public DbSet<DairyRatePlan> DairyRatePlans => Set<DairyRatePlan>();

    public DbSet<DairyRate> DairyRates => Set<DairyRate>();

    public DbSet<DairyGridCell> DairyGridCells => Set<DairyGridCell>();

    public DbSet<MilkCollection> MilkCollections => Set<MilkCollection>();

    public DbSet<DairyFarmerEntry> DairyFarmerEntries => Set<DairyFarmerEntry>();

    public DbSet<MilkDispatch> MilkDispatches => Set<MilkDispatch>();

    public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();

    public DbSet<StylistRate> StylistRates => Set<StylistRate>();

    public DbSet<StylistEarning> StylistEarnings => Set<StylistEarning>();

    public DbSet<PrepaidPlan> PrepaidPlans => Set<PrepaidPlan>();

    public DbSet<CustomerPlan> CustomerPlans => Set<CustomerPlan>();

    public DbSet<KarigarChallan> KarigarChallans => Set<KarigarChallan>();

    public DbSet<CashCount> CashCounts => Set<CashCount>();

    public DbSet<Ledger.CashMove> CashMoves => Set<Ledger.CashMove>();
    public DbSet<Catalogue.Stocktake> Stocktakes => Set<Catalogue.Stocktake>();
    public DbSet<Ledger.PushDevice> PushDevices => Set<Ledger.PushDevice>();
    public DbSet<Ledger.PushKey> PushKeys => Set<Ledger.PushKey>();

    public DbSet<Challan> Challans => Set<Challan>();

    public DbSet<Painter> Painters => Set<Painter>();

    public DbSet<Billing.Plan> Plans => Set<Billing.Plan>();

    public DbSet<Billing.Subscription> Subscriptions => Set<Billing.Subscription>();

    public DbSet<Billing.SubscriptionPayment> SubscriptionPayments => Set<Billing.SubscriptionPayment>();

    public DbSet<Billing.PaymentClaim> PaymentClaims => Set<Billing.PaymentClaim>();

    public DbSet<Billing.Offer> Offers => Set<Billing.Offer>();

    public DbSet<Billing.AgentProposal> AgentProposals => Set<Billing.AgentProposal>();

    public DbSet<Dispute> Disputes => Set<Dispute>();

    public DbSet<DisputeItem> DisputeItems => Set<DisputeItem>();

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

        // No shop filter: the WhatsApp bot looks a sender up here before it knows the centre. Only phone → farmer links.
        modelBuilder.Entity<DairyFarmerPhone>(b =>
        {
            b.ToTable("dairy_farmer_phones");
            b.HasKey(p => new { p.Phone, p.FarmerId });
            b.Property(p => p.Phone).HasMaxLength(20);
            b.HasIndex(p => p.FarmerId);
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
            b.Property(i => i.Aisle).HasMaxLength(20);
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
            b.Property(l => l.RequestedName).HasMaxLength(160);
            // Lines from before options existed: the shop chose, nothing waits.
            b.Property(l => l.Preference).HasDefaultValue(ReplacePreference.ShopChooses);
            b.Property(l => l.Approval).HasDefaultValue(LineApproval.None);
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

        modelBuilder.Entity<Shops.PlatformAdmin>(b =>
        {
            b.ToTable("platform_admins");
            b.HasIndex(a => a.UserId).IsUnique();
            b.Property(a => a.Role).HasDefaultValue(GroceryKhata.Api.Shops.AdminRole.Super);
            b.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

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
            b.HasOne(p => p.CatalogueItem).WithMany().HasForeignKey(p => p.CatalogueItemId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<Shop>().WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<MeterReading>(b =>
        {
            b.ToTable("meter_readings");
            b.HasTenantKey();
            b.Property(r => r.Note).HasMaxLength(200);
            b.HasIndex(r => new { r.ShopId, r.Day });
        });

        modelBuilder.Entity<CashCount>(b =>
        {
            b.ToTable("cash_counts");
            b.HasTenantKey();
            b.Property(c => c.Note).HasMaxLength(200);
            b.HasIndex(c => new { c.ShopId, c.Day });
        });

        modelBuilder.Entity<CreditNote>(b =>
        {
            b.ToTable("credit_notes");
            b.HasTenantKey();
            b.Ignore(n => n.NoteNo);
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

        modelBuilder.Entity<Amc>(b =>
        {
            b.ToTable("amcs");
            b.HasTenantKey();
            b.Property(x => x.Title).HasMaxLength(120);
            b.Property(x => x.SerialNo).HasMaxLength(40);
            b.Property(x => x.Fee).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<Trip>(b =>
        {
            b.ToTable("trips");
            b.HasTenantKey();
            b.Property(x => x.Technician).HasMaxLength(60);
            b.Property(x => x.Place).HasMaxLength(80);
            b.Property(x => x.Returned).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<TripPhoto>(b =>
        {
            b.ToTable("trip_photos");
            b.HasKey(x => x.EntryId);
            b.HasIndex(x => x.ShopId);
            b.Property(x => x.ContentType).HasMaxLength(20);
        });

        modelBuilder.Entity<TripEntry>(b =>
        {
            b.ToTable("trip_entries");
            b.HasTenantKey();
            b.Property(x => x.Description).HasMaxLength(80);
            b.Property(x => x.Amount).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.TripId });
        });

        modelBuilder.Entity<WarrantyClaim>(b =>
        {
            b.ToTable("warranty_claims");
            b.HasTenantKey();
            b.Property(c => c.Parts).HasPrecision(12, 2);
            b.Property(c => c.Labour).HasPrecision(12, 2);
            b.Property(c => c.CreditAmount).HasPrecision(12, 2);
            b.Property(c => c.Description).HasMaxLength(200);
            b.Property(c => c.RejectReason).HasMaxLength(200);
            b.Property(c => c.SerialNo).HasMaxLength(40);
            b.Property(c => c.DispatchNo).HasMaxLength(40);
            b.Property(c => c.CreditNoteNo).HasMaxLength(40);
            b.HasIndex(c => new { c.ShopId, c.Number }).IsUnique();
        });

        modelBuilder.Entity<SupplierReturnLine>(b =>
        {
            b.ToTable("supplier_return_lines");
            b.HasTenantKey();
            b.Property(l => l.Quantity).HasPrecision(14, 3);
            b.Property(l => l.UnitCost).HasPrecision(12, 2);
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

        modelBuilder.Entity<FestivalSetting>(b =>
        {
            b.ToTable("festival_settings");
            b.HasTenantKey();
            b.Property(s => s.Key).HasMaxLength(30);
            b.HasIndex(s => new { s.ShopId, s.Key, s.Year }).IsUnique();
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

        modelBuilder.Entity<Ledger.BalanceItem>(b =>
        {
            b.ToTable("balance_items");
            b.HasTenantKey();
            b.Property(i => i.Name).HasMaxLength(80);
            b.Property(i => i.Amount).HasPrecision(14, 2);
            b.Property(i => i.Kind).HasConversion<string>().HasMaxLength(20);
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
            b.HasIndex(s => new { s.ShopId, s.ProductId, s.SupplierId }).IsUnique();
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

        modelBuilder.Entity<Job>(b =>
        {
            b.ToTable("jobs");
            b.Property(x => x.SerialNo).HasMaxLength(40);
            b.Property(x => x.Complaint).HasMaxLength(300);
            b.HasIndex(x => new { x.ShopId, x.SerialNo });
            b.HasTenantKey();
            b.Property(j => j.Title).HasMaxLength(120);
            b.Property(j => j.Note).HasMaxLength(300);
            b.Property(j => j.Price).HasPrecision(12, 2);
            b.Property(j => j.EstimatedCost).HasPrecision(12, 2);
            b.HasIndex(j => new { j.ShopId, j.Number }).IsUnique();
        });

        modelBuilder.Entity<Venue>(b =>
        {
            b.ToTable("venues");
            b.HasTenantKey();
            b.Property(v => v.Name).HasMaxLength(60);
        });

        modelBuilder.Entity<Booking>(b =>
        {
            b.ToTable("bookings");
            b.HasTenantKey();
            b.Property(x => x.PlateRate).HasPrecision(12, 2);
            b.Property(x => x.HallRent).HasPrecision(12, 2);
            b.Property(x => x.Deposit).HasPrecision(12, 2);
            b.Property(x => x.DepositTaken).HasPrecision(12, 2);
            b.Property(x => x.DepositReturned).HasPrecision(12, 2);
            b.Property(x => x.Note).HasMaxLength(300);
            b.Property(x => x.CancelReason).HasMaxLength(200);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.ShopId, x.VenueId, x.Date });
        });

        modelBuilder.Entity<TableBooking>(b =>
        {
            b.ToTable("table_bookings");
            b.HasTenantKey();
            b.Property(x => x.Name).HasMaxLength(80);
            b.Property(x => x.Phone).HasMaxLength(20);
            b.Property(x => x.Cake).HasMaxLength(120);
            b.Property(x => x.Table).HasMaxLength(30);
            b.Property(x => x.Note).HasMaxLength(200);
            b.Property(x => x.Deposit).HasPrecision(12, 2);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.ShopId, x.Date });
        });

        modelBuilder.Entity<Room>(b =>
        {
            b.ToTable("rooms");
            b.HasTenantKey();
            b.Property(r => r.Name).HasMaxLength(30);
            b.Property(r => r.Kind).HasMaxLength(40);
            b.Property(r => r.Rate).HasPrecision(12, 2);
            b.Property(r => r.IcalImportUrl).HasMaxLength(500);
            b.Property(r => r.IcalToken).HasMaxLength(40);
            b.HasIndex(r => r.IcalToken).IsUnique();
        });

        modelBuilder.Entity<RatePeriod>(b =>
        {
            b.ToTable("rate_periods");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(60);
            b.Property(p => p.Rate).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Stay>(b =>
        {
            b.ToTable("stays");
            b.HasTenantKey();
            b.Property(s => s.GuestName).HasMaxLength(80);
            b.Property(s => s.Phone).HasMaxLength(20);
            b.Property(s => s.IdType).HasMaxLength(30);
            b.Property(s => s.IdLast4).HasMaxLength(4);
            b.Property(s => s.SourceRef).HasMaxLength(40);
            b.Property(s => s.Note).HasMaxLength(200);
            b.Property(s => s.Rate).HasPrecision(12, 2);
            b.Property(s => s.Commission).HasPrecision(12, 2);
            b.Property(s => s.RoomCharge).HasPrecision(12, 2);
            b.HasIndex(s => s.BookingId);
            b.HasMany(s => s.Charges).WithOne().HasForeignKey(c => c.StayId);
            b.HasIndex(s => new { s.ShopId, s.Number }).IsUnique();
            b.HasIndex(s => new { s.ShopId, s.RoomId, s.CheckIn });
        });

        modelBuilder.Entity<StayCharge>(b =>
        {
            b.ToTable("stay_charges");
            b.HasTenantKey();
            b.Property(c => c.Name).HasMaxLength(60);
            b.Property(c => c.Amount).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Appointment>(b =>
        {
            b.ToTable("appointments");
            b.HasTenantKey();
            b.Property(a => a.Name).HasMaxLength(80);
            b.Property(a => a.Phone).HasMaxLength(20);
            b.Property(a => a.Note).HasMaxLength(200);
            b.HasMany(a => a.Services).WithOne().HasForeignKey(s => s.AppointmentId);
            b.HasIndex(a => new { a.ShopId, a.Number }).IsUnique();
            b.HasIndex(a => new { a.ShopId, a.Date, a.StylistId });
        });

        modelBuilder.Entity<BridalBooking>(b =>
        {
            b.ToTable("bridal_bookings");
            b.HasTenantKey();
            b.Property(x => x.Title).HasMaxLength(120);
            b.Property(x => x.Address).HasMaxLength(200);
            b.Property(x => x.Note).HasMaxLength(300);
            b.Property(x => x.TravelCharge).HasPrecision(12, 2);
            b.HasMany(x => x.Events).WithOne().HasForeignKey(e => e.BookingId);
            b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<ServiceProduct>(b =>
        {
            b.ToTable("service_products");
            b.HasTenantKey();
            b.Property(s => s.Quantity).HasPrecision(12, 3);
            b.HasIndex(s => new { s.ShopId, s.ServiceId, s.ProductId }).IsUnique();
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

        modelBuilder.Entity<DairyFarmer>(b =>
        {
            b.ToTable("dairy_farmers");
            b.HasTenantKey();
            b.Property(f => f.Name).HasMaxLength(80);
            b.Property(f => f.Phone).HasMaxLength(20);
            b.Property(f => f.Village).HasMaxLength(60);
            b.HasIndex(f => new { f.ShopId, f.Code }).IsUnique();
        });

        modelBuilder.Entity<DairyRatePlan>(b =>
        {
            b.ToTable("dairy_rate_plans");
            b.HasTenantKey();
            b.HasMany(p => p.Rates).WithOne().HasForeignKey(r => r.PlanId);
            b.HasMany(p => p.Grid).WithOne().HasForeignKey(c => c.PlanId);
            b.HasIndex(p => new { p.ShopId, p.For, p.From });
        });

        modelBuilder.Entity<DairyRate>(b =>
        {
            b.ToTable("dairy_rates");
            b.HasTenantKey();
            foreach (var p in new[] { nameof(DairyRate.FatRate), nameof(DairyRate.SnfRate), nameof(DairyRate.FlatRate) }) b.Property(p).HasPrecision(10, 2);
            b.Property(r => r.MinFat).HasPrecision(5, 2);
            b.Property(r => r.MinSnf).HasPrecision(5, 2);
        });

        modelBuilder.Entity<DairyGridCell>(b =>
        {
            b.ToTable("dairy_grid_cells");
            b.HasTenantKey();
            b.Property(c => c.Fat).HasPrecision(4, 1);
            b.Property(c => c.Snf).HasPrecision(4, 1);
            b.Property(c => c.RatePerLitre).HasPrecision(8, 2);
            b.HasIndex(c => new { c.ShopId, c.PlanId, c.Kind, c.Fat, c.Snf }).IsUnique();
        });

        modelBuilder.Entity<MilkCollection>(b =>
        {
            b.ToTable("milk_collections");
            b.HasTenantKey();
            b.Property(c => c.Litres).HasPrecision(8, 2);
            b.Property(c => c.Fat).HasPrecision(5, 2);
            b.Property(c => c.Snf).HasPrecision(5, 2);
            b.Property(c => c.Clr).HasPrecision(5, 1);
            b.Property(c => c.Rate).HasPrecision(10, 2);
            b.Property(c => c.Amount).HasPrecision(12, 2);
            b.Property(c => c.EditLog).HasMaxLength(2000);
            b.HasIndex(c => new { c.ShopId, c.Date, c.Shift });
            b.HasIndex(c => new { c.ShopId, c.FarmerId, c.Date });
        });

        modelBuilder.Entity<DairyFarmerEntry>(b =>
        {
            b.ToTable("dairy_farmer_entries");
            b.HasTenantKey();
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.Property(e => e.Note).HasMaxLength(120);
            b.HasIndex(e => new { e.ShopId, e.FarmerId, e.Date });
        });

        modelBuilder.Entity<MilkDispatch>(b =>
        {
            b.ToTable("milk_dispatches");
            b.HasTenantKey();
            b.Property(d => d.Agency).HasMaxLength(60);
            foreach (var p in new[] { nameof(MilkDispatch.Litres), nameof(MilkDispatch.AcceptedLitres) }) b.Property(p).HasPrecision(10, 2);
            foreach (var p in new[] { nameof(MilkDispatch.Fat), nameof(MilkDispatch.Snf), nameof(MilkDispatch.AcceptedFat), nameof(MilkDispatch.AcceptedSnf) }) b.Property(p).HasPrecision(5, 2);
            b.Property(d => d.Amount).HasPrecision(12, 2);
            b.HasIndex(d => new { d.ShopId, d.Date });
        });

        modelBuilder.Entity<PrescriptionPhoto>(b =>
        {
            b.ToTable("prescription_photos");
            b.HasKey(p => p.BillId);
            b.Property(p => p.ContentType).HasMaxLength(20);
            b.HasIndex(p => p.ShopId);
        });

        modelBuilder.Entity<BillPrescription>(b =>
        {
            b.ToTable("bill_prescriptions");
            b.HasTenantKey();
            b.Property(x => x.DoctorName).HasMaxLength(80);
            b.Property(x => x.DoctorAddress).HasMaxLength(200);
            b.Property(x => x.DoctorRegNo).HasMaxLength(40);
            b.Property(x => x.PatientName).HasMaxLength(80);
            b.Property(x => x.PatientAddress).HasMaxLength(200);
            b.HasIndex(x => new { x.ShopId, x.BillId }).IsUnique();
        });

        modelBuilder.Entity<BatchSale>(b =>
        {
            b.ToTable("batch_sales");
            b.HasTenantKey();
            b.Property(x => x.Quantity).HasPrecision(14, 3);
            b.HasIndex(x => new { x.ShopId, x.BillId });
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

        modelBuilder.Entity<GroceryKhata.Api.Ledger.PurchaseUpload>(b =>
        {
            b.ToTable("purchase_uploads");
            b.HasTenantKey();
            b.Property(u => u.Source).HasMaxLength(10);
            b.HasIndex(u => new { u.ShopId, u.UsedAt });
        });

        modelBuilder.Entity<PrintLook>(b =>
        {
            b.ToTable("print_looks");
            b.HasTenantKey();
            b.Property(p => p.Theme).HasMaxLength(20);
            b.Property(p => p.Terms).HasMaxLength(GroceryKhata.Api.Shops.PrintLooks.MaxTerms);
            b.HasIndex(p => p.ShopId).IsUnique();
        });

        modelBuilder.Entity<ClientCard>(b =>
        {
            b.ToTable("client_cards");
            b.HasTenantKey();
            b.Property(c => c.HairType).HasMaxLength(60);
            b.Property(c => c.SkinType).HasMaxLength(60);
            b.Property(c => c.Allergies).HasMaxLength(500);
            b.Property(c => c.ColourFormula).HasMaxLength(500);
            b.Property(c => c.Notes).HasMaxLength(500);
            b.HasIndex(c => new { c.ShopId, c.CustomerId }).IsUnique();
        });

        modelBuilder.Entity<BridalEvent>(b =>
        {
            b.ToTable("bridal_events");
            b.HasTenantKey();
            b.Property(e => e.Name).HasMaxLength(40);
            b.Property(e => e.Price).HasPrecision(12, 2);
            b.HasIndex(e => new { e.ShopId, e.Date, e.StylistId });
        });

        modelBuilder.Entity<AppointmentService>(b =>
        {
            b.ToTable("appointment_services");
            b.HasTenantKey();
            b.Property(s => s.Name).HasMaxLength(120);
            b.Property(s => s.Price).HasPrecision(12, 2);
        });

        modelBuilder.Entity<StylistRate>(b =>
        {
            b.ToTable("stylist_rates");
            b.HasTenantKey();
            b.Property(r => r.Percent).HasPrecision(5, 2);
            b.HasIndex(r => new { r.ShopId, r.MembershipId }).IsUnique();
        });

        modelBuilder.Entity<StylistEarning>(b =>
        {
            b.ToTable("stylist_earnings");
            b.HasTenantKey();
            b.Property(e => e.Base).HasPrecision(12, 2);
            b.Property(e => e.Percent).HasPrecision(5, 2);
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.HasIndex(e => new { e.ShopId, e.MembershipId, e.PaidAt });
        });

        modelBuilder.Entity<PrepaidPlan>(b =>
        {
            b.ToTable("prepaid_plans");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(60);
            b.Property(p => p.Price).HasPrecision(12, 2);
            b.Property(p => p.DiscountPercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<CustomerPlan>(b =>
        {
            b.ToTable("customer_plans");
            b.HasTenantKey();
            b.Property(p => p.Name).HasMaxLength(60);
            b.Property(p => p.Price).HasPrecision(12, 2);
            b.Property(p => p.SessionValue).HasPrecision(12, 2);
            b.Property(p => p.DiscountPercent).HasPrecision(5, 2);
            b.HasIndex(p => new { p.ShopId, p.CustomerId });
            b.HasIndex(p => new { p.ShopId, p.ExpiresOn });
        });

        modelBuilder.Entity<Machine>(b =>
        {
            b.ToTable("machines");
            b.HasTenantKey();
            b.Property(m => m.Name).HasMaxLength(80);
            b.Property(m => m.Cost).HasPrecision(12, 2);
            b.Property(m => m.RatePercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<KarigarChallan>(b =>
        {
            b.ToTable("karigar_challans");
            b.HasTenantKey();
            b.Property(c => c.Karigar).HasMaxLength(80);
            b.Property(c => c.KarigarGstin).HasMaxLength(15);
            b.Property(c => c.Note).HasMaxLength(200);
            b.Property(c => c.Quantity).HasPrecision(12, 3);
            b.Property(c => c.ReceivedQuantity).HasPrecision(12, 3);
            b.Property(c => c.UnitCost).HasPrecision(12, 2);
            b.HasIndex(c => new { c.ShopId, c.Number }).IsUnique();
        });

        modelBuilder.Entity<JobEntry>(b =>
        {
            b.ToTable("job_entries");
            b.HasTenantKey();
            b.Property(e => e.Description).HasMaxLength(120);
            b.Property(e => e.Amount).HasPrecision(12, 2);
            b.Property(e => e.Quantity).HasPrecision(12, 3);
            b.HasIndex(e => new { e.ShopId, e.JobId });
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

        modelBuilder.Entity<Catalogue.Stocktake>(b =>
        {
            b.ToTable("stocktakes");
            b.HasTenantKey();
            b.Property(t => t.Lines).HasColumnType("jsonb");
            b.Property(t => t.ValueDifference).HasPrecision(14, 2);
            b.HasIndex(t => new { t.ShopId, t.Number }).IsUnique();
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

        modelBuilder.Entity<ProductImage>(b =>
        {
            b.Property(i => i.ThumbType).HasMaxLength(20);
            b.ToTable("product_images");
            b.HasKey(i => i.ProductId);
            b.HasIndex(i => i.ShopId);
            b.Property(i => i.ContentType).HasMaxLength(20);
            b.HasOne<Product>().WithOne().HasForeignKey<ProductImage>(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
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
