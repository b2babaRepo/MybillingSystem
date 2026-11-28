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
public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
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

    public DbSet<Planner.CustomerCart> CustomerCarts => Set<Planner.CustomerCart>();

    public DbSet<Planner.UsageEvent> UsageEvents => Set<Planner.UsageEvent>();

    public DbSet<Planner.DeliveryPlan> DeliveryPlans => Set<Planner.DeliveryPlan>();

    public DbSet<Ledger.BalanceConfirmation> BalanceConfirmations => Set<Ledger.BalanceConfirmation>();

    public DbSet<Planner.SavedShop> SavedShops => Set<Planner.SavedShop>();

    public DbSet<Planner.CustomerPushDevice> CustomerPushDevices => Set<Planner.CustomerPushDevice>();

    public DbSet<Planner.SavedItem> SavedItems => Set<Planner.SavedItem>();

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

    public DbSet<EmailCode> EmailCodes => Set<EmailCode>();

    public DbSet<Shops.PlatformRole> PlatformRoles => Set<Shops.PlatformRole>();

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

    public DbSet<ShopPicture> ShopPictures => Set<ShopPicture>();

    public DbSet<MenuNode> MenuNodes => Set<MenuNode>();

    public DbSet<ProductExtraPhoto> ProductExtraPhotos => Set<ProductExtraPhoto>();

    public DbSet<AiProposal> AiProposals => Set<AiProposal>();

    public DbSet<BalanceCheck> BalanceChecks => Set<BalanceCheck>();

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<MetalRate> MetalRates => Set<MetalRate>();
    public DbSet<JewelleryPiece> JewelleryPieces => Set<JewelleryPiece>();
    public DbSet<JewelleryBill> JewelleryBills => Set<JewelleryBill>();
    public DbSet<JewelleryBillLine> JewelleryBillLines => Set<JewelleryBillLine>();
    public DbSet<OldMetalPurchase> OldMetalPurchases => Set<OldMetalPurchase>();

    public DbSet<OldMetalVoucher> OldMetalVouchers => Set<OldMetalVoucher>();
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

    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public DbSet<HeldBill> HeldBills => Set<HeldBill>();

    public DbSet<LoyaltyEntry> LoyaltyEntries => Set<LoyaltyEntry>();

    public DbSet<Shops.Terminal> Terminals => Set<Shops.Terminal>();

    public DbSet<MallFeed> MallFeeds => Set<MallFeed>();

    public DbSet<GiftVoucher> GiftVouchers => Set<GiftVoucher>();

    public DbSet<Ledger.CashMove> CashMoves => Set<Ledger.CashMove>();
    public DbSet<Catalogue.Stocktake> Stocktakes => Set<Catalogue.Stocktake>();
    public DbSet<Ledger.PushDevice> PushDevices => Set<Ledger.PushDevice>();
    public DbSet<Ledger.PushKey> PushKeys => Set<Ledger.PushKey>();

    public DbSet<Challan> Challans => Set<Challan>();

    public DbSet<Painter> Painters => Set<Painter>();

    public DbSet<PainterPayout> PainterPayouts => Set<PainterPayout>();

    public DbSet<BrandScheme> BrandSchemes => Set<BrandScheme>();

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
        // Each part of the app configures its own tables (AppDbContext.<Part>.cs).
        ConfigureAccounts(modelBuilder);
        ConfigureLedger(modelBuilder);
        ConfigurePlanner(modelBuilder);
        ConfigureCatalogue(modelBuilder);
        ConfigurePurchasing(modelBuilder);
        ConfigureServices(modelBuilder);
        ConfigureJewellery(modelBuilder);

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
