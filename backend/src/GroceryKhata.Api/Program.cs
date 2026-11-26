using GroceryKhata.Api.Teams;
using GroceryKhata.Api.Agents;
using System.Text.Json;
using System.Text.Json.Serialization;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Planner;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddHardening();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GroceryKhata.Api.Ledger.BooksClosedHandler>();
builder.Services.AddExceptionHandler<GroceryKhata.Api.Ledger.BadRequestHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
builder.Services.AddPersistence();
builder.Services.AddAppAuthentication();
// Place lookups use IPv4: a network with broken IPv6 would otherwise hang until the timeout.
builder.Services.AddHttpClient("place").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    ConnectCallback = async (context, ct) =>
    {
        var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, ct);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    },
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddOrderReading(builder.Configuration);
builder.Services.AddAiPrivacy(builder.Configuration);
builder.Services.AddWhatsAppOrders(builder.Configuration);
builder.Services.AddWhatsAppSignup(builder.Configuration);
builder.Services.AddBalanceChecks();
builder.Services.AddStockAlerts();
builder.Services.AddBackInStock();
builder.Services.AddKhataDueReminders();
builder.Services.AddBilling();
builder.Services.AddFieldAgents(builder.Configuration);
builder.Services.AddAgentPerformance(builder.Configuration);
builder.Services.AddBillPhotoReading(builder.Configuration);
builder.Services.AddServiceReading(builder.Configuration);
builder.Services.AddSingleton<GroceryKhata.Api.Catalogue.IBarcodePhotos, GroceryKhata.Api.Catalogue.OpenFoodFactsPhotos>();
builder.Services.AddInvoiceReading(builder.Configuration);
builder.Services.AddDairyPhotoReading(builder.Configuration);
builder.Services.AddDairyHelper(builder.Configuration);
builder.Services.AddOtpSignIn(builder.Configuration, builder.Environment);

var app = builder.Build();
GroceryKhata.Api.Common.AiBudget.Scopes = app.Services.GetRequiredService<IServiceScopeFactory>();

// Before anything touches the database: refuse unsafe production settings.
await app.CheckProductionSafetyAsync();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    // Development and tests only. Production runs migrations at deploy, after a backup.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// The master item lists start from the lists the app was built with (once; admins own them after).
await MasterItems.SeedAsync(app.Services);
await MasterItems.SeedListsAsync(app.Services);

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:SeedDemo"))
{
    await DemoSeeder.RunAsync(app.Services, app.Environment.ContentRootPath, app.Logger);
}

// Any request slower than 2 s is logged with its path and time, so a stuck screen can be traced to the step that waited.
app.Use(async (http, next) =>
{
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    await next(http);
    var took = System.Diagnostics.Stopwatch.GetElapsedTime(started);
    // Waiting for new orders holds the request on purpose.
    if (took.TotalSeconds >= 2 && !http.Request.Path.StartsWithSegments("/api/v1/orders/wait"))
    {
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SlowRequest")
            .LogWarning("SLOW {Method} {Path} took {Seconds:0.0}s (status {Status})", http.Request.Method, http.Request.Path, took.TotalSeconds, http.Response.StatusCode);
    }
});
app.UseHardening();
app.UseExceptionHandler();
app.UseStatusCodePages();
// Limits first (by IP, after the proxy's forwarded address is read): a request over the limit is turned away before
// its token is checked or the tenant step opens a database connection for it.
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();
app.UseMiddleware<ReadOnlyAccess>();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().DisableRateLimiting();
app.MapReadiness();
app.MapAuth();
app.MapCatalogue();
app.MapLedger();
app.MapSaleOffers();
app.MapDashboard();
app.MapCashBook();
app.MapBankBook();
app.MapCashLimits();
app.MapCustomerMove();
app.MapStockLedger();
app.MapLastStockIn();
app.MapBillFinder();
app.MapExchangeSlips();
app.MapStockHealth();
app.MapArrivals();
app.MapCategories();
app.MapSupplierItems();
app.MapStockHolds();
app.MapGodowns();
app.MapBardana();
app.MapFestivals();
app.MapBooks();
app.MapGoodsTracking();
app.MapStockAlerts();
app.MapCashMoves();
app.MapAudit();
app.MapPlanner();
app.MapCommonCatalogue();
app.MapOrders();
app.MapStore();
app.MapReports();
app.MapBalanceSheet();
app.MapYearReports();
app.MapDataChecks();
app.MapLoans();
app.MapShopSettings();
app.MapShopData();
app.MapStaff();
app.MapBalanceChecks();
app.MapExpenses();
app.MapBillPhotos();
app.MapInvoicePhotos();
app.MapPurchaseUploads();
GroceryKhata.Api.Ledger.PartyImport.MapPartyImport(app);
GroceryKhata.Api.Ledger.Receipts.MapReceipts(app);
GroceryKhata.Api.Ledger.Reminders.MapReminders(app);
GroceryKhata.Api.Ledger.BillLinks.MapBillLinks(app);
app.MapMarkets();
app.MapProfile();
app.MapPhoneChange();
app.MapBalanceConfirmations();
app.MapAddresses();
app.MapCustomerAccount();
app.MapOrderUpdates();
app.MapCustomerExtras();
app.MapUsageEvents();
app.MapMyRefills();
app.MapMySalon();
app.MapDeliveryPlans();
app.MapPassbook();
app.MapKhataDues();
app.MapGstExport();
app.MapCreditNotes();
app.MapMasterItems();
app.MapRazorpay();
app.MapSuppliers();
app.MapJobs();
app.MapWorkshop();
app.MapBookings();
app.MapTableBookings();
app.MapStays();
app.MapAppointments();
app.MapBridal();
app.MapSalonCards();
app.MapPharmacyProfile();
app.MapPrintLook();
app.MapBillPasscode();
app.MapLastPrices();
app.MapBatches();
app.MapSerials();
app.MapPrescriptions();
app.MapSupplierReturns();
app.MapPurchaseOrders();
app.MapWarrantyClaims();
app.MapTrips();
app.MapAmcs();
app.MapServiceAssistant();
app.MapPhotoSources();
app.MapMasterPhotos();
app.MapPharmacyMonth();
app.MapPharmacyAssist();
app.MapDairy();
app.MapDairyChecks();
app.MapDairyAssistant();
app.MapStylistPay();
app.MapPrepaid();
app.MapCashCounts();
app.MapCashierShifts();
app.MapHeldBills();
app.MapMeterReadings();
app.MapGoodsReceipts();
app.MapEstimates();
app.MapSaleOrders();
app.MapSalesInsights();
app.MapDayReport();
app.MapCheques();
app.MapTrades();
app.MapBrandSchemes();
app.MapBilling();
app.MapAgent();
app.MapPlatform();
app.MapFieldAgents();
app.MapShopRegistrations();
app.MapAgentPerformance();
app.MapInvoices();
app.MapTeamKharcha();
app.MapOtpSignIn();
app.MapKhataView();
app.MapDisputes();
app.MapProductImages();
app.MapProductPhotos();
app.MapOrderDrafts();
app.MapAiPrivacy();
app.MapWhatsAppOrders();
app.MapWhatsAppSignup();
app.MapWhatsAppShopMessages();
app.MapJewellery();
app.MapJewelleryBook();
app.MapJewelleryWorkshop();
app.MapJewelleryReports();

app.Run();

/// <summary>Visible to the integration tests' WebApplicationFactory.</summary>
public partial class Program;
