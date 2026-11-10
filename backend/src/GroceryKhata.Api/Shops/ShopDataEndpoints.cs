using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

public sealed record DeleteShopRequest(string? ConfirmName);

/// <summary>
/// The shop's data belongs to the shop. The owner can download all of it as one JSON file, and can delete the
/// shop with everything in it (khata, bills, stock, customers, orders): typing the shop's name confirms it.
/// </summary>
public static class ShopDataEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static IEndpointRouteBuilder MapShopData(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shop").WithTags("Shop").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/export", ExportAsync);
        group.MapPost("/delete", DeleteAsync);
        return app;
    }

    private static async Task<IResult> ExportAsync(AppDbContext db, ITenantContext tenant, TimeProvider clock, ILogger<AppDbContext> logger, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().SingleAsync(s => s.Id == tenant.ShopId, ct);
        // Every query below is limited to this shop by the tenant filter (and row-level security).
        var export = new
        {
            exportedAt = clock.GetUtcNow(),
            shop = new { shop.Name, shop.Slug, shop.Area, shop.Address, shop.UpiId, shop.ReminderTemplate, shop.CreatedAt },
            products = await db.Products.AsNoTracking().ToListAsync(ct),
            stockMovements = await db.StockMovements.AsNoTracking().OrderBy(m => m.OccurredAt).ToListAsync(ct),
            // Named fields, not whole rows: khata links and code hashes are keys, and this file gets passed around.
            customers = await db.Customers.AsNoTracking().Select(c => new { c.Id, c.Name, c.Phone, c.Balance, c.CreditLimit, c.Gstin, c.Address, c.State, c.LastRemindedAt, c.ReminderConsentAt, c.CreatedAt }).ToListAsync(ct),
            ledgerEntries = await db.LedgerEntries.AsNoTracking().OrderBy(e => e.OccurredAt).ToListAsync(ct),
            bills = await db.Bills.AsNoTracking().Include(b => b.Lines).OrderBy(b => b.OccurredAt).ToListAsync(ct),
            orders = await db.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.ShopId == tenant.ShopId).OrderBy(o => o.CreatedAt)
                .Select(o => new { o.Id, o.Source, o.CustomerName, o.CustomerPhone, o.Address, o.Status, o.FinalAmount, o.CreatedAt, o.DeliveredAt, o.Lines })
                .ToListAsync(ct),
            // Rows from before these were kept out of the log.
            auditLog = (await db.AuditEntries.AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync(ct)).Where(a => !a.Changes.Contains("khata_token") && !a.Changes.Contains("confirm_code_hash")).ToList(),
            expenses = await db.Expenses.AsNoTracking().OrderBy(e => e.OccurredAt).ToListAsync(ct),
            cashCounts = await db.CashCounts.AsNoTracking().OrderBy(c => c.Day).ToListAsync(ct),
            challans = await db.Challans.AsNoTracking().OrderBy(c => c.OccurredAt).ToListAsync(ct),
            painters = await db.Painters.AsNoTracking().ToListAsync(ct),
        };
        logger.LogInformation("Shop {ShopId} exported its data", tenant.ShopId);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(export, Json));
        return TypedResults.File(bytes, "application/json", $"{shop.Slug}-{IndiaTime.Today(clock):yyyy-MM-dd}.json");
    }

    /// <summary>
    /// Closes the shop: nobody can open it and its store goes. Its records stay, because GST invoices must be kept for
    /// years. People's accounts stay too. Support can reopen it.
    /// </summary>
    private static async Task<IResult> DeleteAsync(
        DeleteShopRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, ILogger<AppDbContext> logger, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().SingleAsync(s => s.Id == tenant.ShopId, ct);
        if (!string.Equals(request.ConfirmName?.Trim(), shop.Name, StringComparison.Ordinal))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ConfirmName)] = ["Type the shop's name exactly to confirm. This cannot be undone."],
            });
        }

        // Closed, not erased: bills are tax records that must be kept, and a mistake can be undone by support.
        var closing = await db.Shops.SingleAsync(s => s.Id == tenant.ShopId, ct);
        (closing.IsActive, closing.BlockedReason) = (false, "Closed by its owner.");
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Shop {ShopId} was closed by user {UserId}", tenant.ShopId, user.UserId());
        return TypedResults.NoContent();
    }
}
