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
            customers = await db.Customers.AsNoTracking().ToListAsync(ct),
            ledgerEntries = await db.LedgerEntries.AsNoTracking().OrderBy(e => e.OccurredAt).ToListAsync(ct),
            bills = await db.Bills.AsNoTracking().Include(b => b.Lines).OrderBy(b => b.OccurredAt).ToListAsync(ct),
            orders = await db.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.ShopId == tenant.ShopId).OrderBy(o => o.CreatedAt)
                .Select(o => new { o.Id, o.Source, o.CustomerName, o.CustomerPhone, o.Address, o.Status, o.FinalAmount, o.CreatedAt, o.DeliveredAt, o.Lines })
                .ToListAsync(ct),
            auditLog = await db.AuditEntries.AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync(ct),
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
    /// Deletes the shop and all its rows, children before parents, in one transaction: all or nothing. People's
    /// accounts stay (they may own other shops or shop as households); only their access to this shop goes.
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

        var shopId = tenant.ShopId;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.AuditEntries.ExecuteDeleteAsync(ct);
        await db.Expenses.ExecuteDeleteAsync(ct);
        await db.CashCounts.ExecuteDeleteAsync(ct);
        await db.Challans.ExecuteDeleteAsync(ct);
        await db.BalanceChecks.ExecuteDeleteAsync(ct);
        await db.StockMovements.ExecuteDeleteAsync(ct);
        await db.OrderLines.Where(l => l.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.Orders.Where(o => o.ShopId == shopId).ExecuteDeleteAsync(ct);
        // Reversals point at the entries they cancel: delete those first.
        await db.LedgerEntries.Where(e => e.ReversesEntryId != null).ExecuteDeleteAsync(ct);
        await db.LedgerEntries.ExecuteDeleteAsync(ct);
        await db.BillLines.ExecuteDeleteAsync(ct);
        await db.Bills.ExecuteDeleteAsync(ct);
        await db.Painters.ExecuteDeleteAsync(ct);
        await db.Customers.ExecuteDeleteAsync(ct);
        await db.Products.ExecuteDeleteAsync(ct);
        var membershipIds = await db.Memberships.Where(m => m.ShopId == shopId).Select(m => m.Id).ToListAsync(ct);
        await db.RefreshTokens.Where(t => t.MembershipId != null && membershipIds.Contains(t.MembershipId.Value)).ExecuteDeleteAsync(ct);
        await db.Memberships.Where(m => m.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.Shops.Where(s => s.Id == shopId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogWarning("Shop {ShopId} deleted by its owner {UserId}", shopId, user.UserId());
        return TypedResults.NoContent();
    }
}
