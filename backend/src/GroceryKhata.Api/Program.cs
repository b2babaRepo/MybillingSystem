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
builder.Services.AddBalanceChecks();
builder.Services.AddBilling();
builder.Services.AddFieldAgents(builder.Configuration);
builder.Services.AddAgentPerformance(builder.Configuration);
builder.Services.AddBillPhotoReading(builder.Configuration);
builder.Services.AddOtpSignIn(builder.Configuration, builder.Environment);

var app = builder.Build();

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

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:SeedDemo"))
{
    await DemoSeeder.RunAsync(app.Services, app.Environment.ContentRootPath, app.Logger);
}

app.UseHardening();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().DisableRateLimiting();
app.MapReadiness();
app.MapAuth();
app.MapCatalogue();
app.MapLedger();
app.MapDashboard();
app.MapCashBook();
app.MapAudit();
app.MapPlanner();
app.MapCommonCatalogue();
app.MapOrders();
app.MapStore();
app.MapReports();
app.MapShopSettings();
app.MapShopData();
app.MapStaff();
app.MapBalanceChecks();
app.MapExpenses();
app.MapBillPhotos();
app.MapMarkets();
app.MapProfile();
app.MapAddresses();
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
app.MapCashCounts();
app.MapTrades();
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
app.MapOrderDrafts();
app.MapAiPrivacy();
app.MapWhatsAppOrders();

app.Run();

/// <summary>Visible to the integration tests' WebApplicationFactory.</summary>
public partial class Program;
