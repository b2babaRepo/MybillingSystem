using GroceryKhata.Api.Shops;
using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

public static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedger(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);

        group.MapGet("/customers", (string? search, LedgerService ledger, CancellationToken ct) => ledger.ListCustomersAsync(search, ct));

        group.MapPost("/customers", async (CustomerRequest request, LedgerService ledger, CancellationToken ct) =>
            (await ledger.CreateCustomerAsync(request, ct)).ToHttp($"/api/v1/customers/{request.Id}/statement"))
            .AddEndpointFilter(Billing.PlanGate.CustomerLimitAsync);

        group.MapGet("/customers/{id:guid}/statement", async (Guid id, string? month, LedgerService ledger, CancellationToken ct) =>
        {
            var period = IndiaTime.ParseMonth(month);
            if (month is not null && period is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
            }

            return await ledger.GetStatementAsync(id, period, ct) is { } statement ? Results.Ok(statement) : Results.NotFound();
        });

        group.MapGet("/bills/{id:guid}", async (Guid id, LedgerService ledger, CancellationToken ct) =>
            await ledger.GetBillAsync(id, ct) is { } bill ? Results.Ok(bill) : Results.NotFound());

        group.MapPost("/khata-entries", async (KhataEntryRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.AddKhataEntryAsync(request, user.UserId()!.Value, ct, IsOwner(user))).ToHttp($"/api/v1/customers/{request.CustomerId}/statement"));

        group.MapPost("/ledger-entries/{id:guid}/reverse",
            async (Guid id, ReverseRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
                (await ledger.ReverseAsync(id, request, user.UserId()!.Value, IsOwner(user), ct)).ToHttp($"/api/v1/ledger-entries/{request.Id}"));

        group.MapPost("/bills", async (BillRequest request, ClaimsPrincipal user, LedgerService ledger, AppDbContext db, TimeProvider clock, HttpContext http, CancellationToken ct) =>
        {
            if (request.GodownId is null && await ShelfShortAsync(request, db, ct) is { } shelfProblem)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = [shelfProblem] });
            }

            // A supervisor's PIN lets a cashier sell below their discount limit; it is kept on the bill.
            var result = await ledger.CreateBillAsync(request, user.UserId()!.Value, ct, isOwner: IsOwner(user), approvedBy: IsOwner(user) ? null : await Shops.Supervisor.ApproverAsync(http));
            if (result.Value is not null && request.GodownId is { } godown && await Catalogue.Godowns.IsActiveAsync(db, godown, ct))
            {
                // Sold from a godown: what the bill took leaves the godown (as much as it holds), not the shelf. Same
                // id as the bill, so a retried bill moves nothing twice.
                var sold = await db.StockMovements.AsNoTracking().Where(m => m.BillId == request.Id && m.Kind == Catalogue.StockMovementKind.Sale)
                    .Select(m => new { m.ProductId, m.Quantity }).ToListAsync(ct);
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await Catalogue.Godowns.MoveForAsync(db, request.Id, godown, null, sold.Select(m => (m.ProductId, -m.Quantity)), "Sold from the godown on a bill", user.UserId()!.Value, clock.GetUtcNow(), ct);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }

            return result.ToHttp($"/api/v1/bills/{request.Id}");
        });

        group.MapPost("/bills/{id:guid}/attach-customer", async (Guid id, AttachCustomerRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.AttachCustomerAsync(id, request, user.UserId()!.Value, IsOwner(user), ct)).ToHttp($"/api/v1/bills/{id}"));

        // The owner, or a cashier with a supervisor's PIN (kept on the bill as who approved it).
        group.MapPost("/bills/{id:guid}/cancel", async (Guid id, CancelBillRequest request, ClaimsPrincipal user, LedgerService ledger, HttpContext http, CancellationToken ct) =>
            (await ledger.CancelBillAsync(id, request, user.UserId()!.Value, ct, await Shops.Supervisor.ApproverAsync(http))).ToHttp($"/api/v1/bills/{id}"))
            .RequireAuthorization(AppPolicies.ShopMember).RequireOwnerOrSupervisor().RequireBillPasscode();

        group.MapGet("/ledger/reconcile/stock", ReconcileStockAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/ledger/reconcile", (LedgerService ledger, CancellationToken ct) => ledger.ReconcileAsync(ct))
            .RequireAuthorization(AppPolicies.ShopOwner);

        return app;
    }

    /// <summary>
    /// A bill from the shop shelf for more than the shelf has, while the rest of that item sits in a godown: the shelf
    /// would go below zero. The goods should be brought to the shop first, or the bill made from the godown. A shop
    /// with no godown stock of the item is not stopped here.
    /// </summary>
    private static async Task<string?> ShelfShortAsync(BillRequest request, AppDbContext db, CancellationToken ct)
    {
        if (request.Lines is not { Count: > 0 } lines || !await db.Godowns.AnyAsync(ct) || await db.Bills.AnyAsync(b => b.Id == request.Id, ct))
        {
            return null;
        }

        var held = await Catalogue.Godowns.HeldAsync(db, ct);
        var wanted = lines.Where(l => l.ProductId != null).GroupBy(l => l.ProductId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var ids = wanted.Keys.Where(id => held.Any(h => h.Key.Product == id && h.Value > 0)).ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Stock, p.Unit }).ToListAsync(ct);
        foreach (var p in products)
        {
            // Lines sold in another unit (grams of a kg item) are left to the bill's own stock rules.
            var quantity = wanted[p.Id].Where(l => l.Unit is null || l.Unit == p.Unit).Sum(l => l.Quantity);
            var inGodowns = held.Where(h => h.Key.Product == p.Id).Sum(h => h.Value);
            var shelf = p.Stock - inGodowns;
            if (quantity > shelf && inGodowns > 0)
            {
                return $"Only {Math.Max(0, shelf):0.###} of {p.Name} is on the shop shelf; {inGodowns:0.###} is in the godown. Bring it to the shop first, or bill it from the godown.";
            }
        }

        return null;
    }

    /// <summary>
    /// Items whose stock doesn't add up: the stock figure against the sum of its stock ledger, and for items kept by
    /// batch, against what their batches hold. Empty when everything ties.
    /// </summary>
    private static async Task<IReadOnlyList<StockMismatch>> ReconcileStockAsync(AppDbContext db, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().Where(p => !p.IsService).Select(p => new { p.Id, p.Name, p.Stock }).ToListAsync(ct);
        var ledger = await db.StockMovements.AsNoTracking().GroupBy(m => m.ProductId).Select(g => new { g.Key, Sum = g.Sum(m => m.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var batches = await db.ProductBatches.AsNoTracking().GroupBy(b => b.ProductId).Select(g => new { g.Key, Sum = g.Sum(b => b.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        return products
            .Select(p => new StockMismatch(p.Id, p.Name, p.Stock, ledger.GetValueOrDefault(p.Id), batches.TryGetValue(p.Id, out var b) ? b : null))
            .Where(m => m.Stock != m.Ledger || (m.Batches is { } held && held != Math.Max(0, m.Stock)))
            .ToList();
    }

    internal static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));
}

