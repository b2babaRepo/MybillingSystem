using System.Text.Json;
using System.Text.Json.Serialization;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Planner;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
builder.Services.AddPersistence();
builder.Services.AddAppAuthentication();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<StockService>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    // Development and tests only. Production runs migrations at deploy, after a backup.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:SeedDemo"))
{
    await DemoSeeder.RunAsync(app.Services, app.Environment.ContentRootPath, app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuth();
app.MapCatalogue();
app.MapLedger();
app.MapDashboard();
app.MapCashBook();
app.MapAudit();
app.MapPlanner();
app.MapOrders();
app.MapStore();

app.Run();

/// <summary>Visible to the integration tests' WebApplicationFactory.</summary>
public partial class Program;
