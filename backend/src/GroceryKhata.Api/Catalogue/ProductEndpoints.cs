using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary><see cref="Id"/> is made by the app, so a product added offline syncs once.</summary>
public sealed record ProductRequest(
    Guid Id,
    string? Name,
    Unit? Unit,
    decimal SellingPrice,
    decimal? CostPrice,
    string? Category = null,
    decimal? ReorderLevel = null,
    decimal? OpeningStock = null,
    decimal? GstRate = null,
    string? HsnCode = null);

/// <summary><see cref="GstRate"/> and <see cref="HsnCode"/>: null keeps what the item has (older apps don't send them), unless <see cref="ReplaceTax"/> says null means clear it.</summary>
public sealed record ProductUpdate(
    string? Name, Unit? Unit, decimal SellingPrice, decimal? CostPrice, string? Category, decimal? ReorderLevel, bool? IsActive,
    decimal? GstRate = null, string? HsnCode = null, bool ReplaceTax = false);

/// <summary><see cref="IsLow"/>: out of stock, or at/below the reorder level.</summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Category,
    Unit Unit,
    decimal SellingPrice,
    decimal? CostPrice,
    decimal Stock,
    decimal? ReorderLevel,
    bool IsLow,
    decimal Reserved = 0,
    int? CatalogueItemId = null,
    decimal? PackSize = null,
    Unit? PackUnit = null,
    bool IsDefaultOption = false,
    DateTimeOffset? ImageAt = null,
    decimal? GstRate = null,
    string? HsnCode = null);

/// <summary>A purchase (positive) or an adjustment (signed, with a reason).</summary>
public sealed record StockMovementRequest(Guid Id, Guid ProductId, StockMovementKind? Kind, decimal Quantity, StockReason? Reason, decimal? CostPrice, string? Note);

public sealed record StockMovementDto(
    Guid Id,
    Guid ProductId,
    StockMovementKind Kind,
    decimal Quantity,
    StockReason? Reason,
    Guid? BillId,
    decimal? CostPrice,
    string? Note,
    DateTimeOffset OccurredAt);

public sealed record StocktakeCount(Guid ProductId, decimal Counted);

public sealed record StocktakeRequest(Guid Id, IReadOnlyList<StocktakeCount>? Counts);

public sealed record StocktakeResult(int Adjusted, int Unchanged);

public sealed record ImportRow(
    string? Name, Unit? Unit, decimal? SellingPrice, decimal? CostPrice, string? Category, decimal? OpeningStock, decimal? ReorderLevel,
    int? CatalogueItemId = null, decimal? PackSize = null, Unit? PackUnit = null, decimal? GstRate = null, string? HsnCode = null);

/// <summary>One item's new rate on the "today's rates" screen.</summary>
public sealed record RateChange(Guid ProductId, decimal SellingPrice);

public sealed record RatesRequest(IReadOnlyList<RateChange>? Rates);

public sealed record ImportRequest(IReadOnlyList<ImportRow>? Rows);

public sealed record ImportResult(int Created, int Skipped);

/// <summary>
/// One common-list item the shop starts selling, at its own price. With a <see cref="Name"/> it is one more
/// option of that item ("Aashirvaad atta 5 kg", sold per packet with <see cref="PackSize"/> 5 kg).
/// </summary>
public sealed record FromCatalogueRow(
    int CatalogueItemId, decimal SellingPrice, decimal? OpeningStock, string? Name = null, Unit? Unit = null, decimal? PackSize = null, Unit? PackUnit = null);

public sealed record FromCatalogueRequest(IReadOnlyList<FromCatalogueRow>? Items);

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapCatalogue(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/v1/products").WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);

        products.MapGet("", (string? search, string? category, bool? low, string? sort, StockService stock, CancellationToken ct) =>
            stock.ListAsync(search, category, low == true, ct, popular: sort == "popular"));

        products.MapGet("/categories", (StockService stock, CancellationToken ct) => stock.CategoriesAsync(ct));

        products.MapGet("/{id:guid}", async (Guid id, StockService stock, CancellationToken ct) =>
            await stock.FindAsync(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());

        products.MapGet("/{id:guid}/movements", (Guid id, StockService stock, CancellationToken ct) => stock.MovementsAsync(id, ct));

        products.MapPost("", async (ProductRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.CreateAsync(request, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Replayed: true, Product: { } product } => Results.Ok(product),
                { Product: { } product } => Results.Created($"/api/v1/products/{product.Id}", product),
                _ => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This ID is already in use."),
            });

        products.MapPut("/{id:guid}", async (Guid id, ProductUpdate request, StockService stock, CancellationToken ct) =>
            await stock.UpdateAsync(id, request, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Product: { } product } => Results.Ok(product),
                _ => Results.NotFound(),
            });

        // Today's rates: building-material prices move daily, so the owner changes many at once. Each change is in
        // the audit log (old and new price), which is the rate history.
        products.MapPut("/rates", async (RatesRequest request, StockService stock, CancellationToken ct) =>
            await stock.SetRatesAsync(request, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Changed: var changed } => Results.Ok(new { changed }),
            }).RequireAuthorization(AppPolicies.ShopOwner);

        // A new shop of another trade starts with that trade's usual items (prices to fill in on the rates screen).
        products.MapPost("/starter", async (ClaimsPrincipal user, StockService stock, AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            var type = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.Type).SingleAsync(ct);
            var rows = StarterItems.For(type);
            if (rows.Count == 0)
            {
                return Results.Ok(new ImportResult(0, 0));
            }

            var (result, errors) = await stock.ImportAsync(new ImportRequest(rows), user.UserId()!.Value, ct);
            return errors is null ? Results.Ok(result) : Results.ValidationProblem(errors);
        }).RequireAuthorization(AppPolicies.ShopOwner);

        products.MapPost("/import", async (ImportRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.ImportAsync(request, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Result: { } result } => Results.Ok(result),
                _ => Results.BadRequest(),
            });

        // The shop picks items from the common list and sets its own price: names, units and categories come from
        // the list, so every shop's atta is the same atta to households. Items the shop already sells are skipped.
        products.MapPost("/from-catalogue", async (FromCatalogueRequest request, ClaimsPrincipal user, StockService stock, AppDbContext db, CancellationToken ct) =>
        {
            var rows = request.Items ?? [];
            var unknown = rows.Where(r => CommonCatalogue.Items.All(i => i.Id != r.CatalogueItemId)).Select(r => r.CatalogueItemId).ToList();
            if (rows.Count == 0 || unknown.Count > 0 || rows.Any(r => r.SellingPrice <= 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Items"] = [unknown.Count > 0 ? "Some items are not on the common list." : "Choose items and enter your price for each."],
                });
            }

            var sold = (await db.Products.AsNoTracking().Select(p => new { p.CatalogueItemId, p.Name }).ToListAsync(ct))
                .Select(p => p.CatalogueItemId ?? CommonCatalogue.Match(p.Name)).OfType<int>().ToHashSet();
            // A plain row starts selling the item (skipped if already sold); a named row adds another option of it.
            var fresh = rows.Where(r => !string.IsNullOrWhiteSpace(r.Name) || sold.Add(r.CatalogueItemId)).ToList();
            if (fresh.Count == 0)
            {
                return Results.Ok(new ImportResult(0, rows.Count));
            }

            var byId = CommonCatalogue.Items.ToDictionary(i => i.Id);
            var import = new ImportRequest(fresh.Select(r => byId[r.CatalogueItemId] is var item
                ? new ImportRow(string.IsNullOrWhiteSpace(r.Name) ? item.Name : r.Name.Trim(), r.Unit ?? item.Unit, r.SellingPrice, null, item.Category,
                    r.OpeningStock, null, item.Id, r.PackSize, r.PackUnit)
                : null!).ToList());
            return await stock.ImportAsync(import, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Result: { } result } => Results.Ok(result with { Skipped = result.Skipped + rows.Count - fresh.Count }),
                _ => Results.BadRequest(),
            };
        });

        // The option the shop gives when a household leaves the choice to it: one per common-list item.
        products.MapPost("/{id:guid}/default", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var all = await db.Products.Where(p => p.IsActive).ToListAsync(ct);
            var chosen = all.SingleOrDefault(p => p.Id == id);
            var itemId = chosen is null ? null : chosen.CatalogueItemId ?? CommonCatalogue.Match(chosen.Name);
            if (itemId is null)
            {
                return Results.NotFound();
            }

            foreach (var option in all.Where(p => (p.CatalogueItemId ?? CommonCatalogue.Match(p.Name)) == itemId))
            {
                option.IsDefaultOption = option.Id == id;
            }

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        var stockGroup = app.MapGroup("/api/v1").WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);

        stockGroup.MapPost("/stock-movements", async (StockMovementRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.AddMovementAsync(request, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { NotFound: true } => Results.ValidationProblem(new Dictionary<string, string[]> { ["ProductId"] = ["Product not found."] }),
                { Movement: { } movement } => Results.Ok(movement),
                _ => Results.BadRequest(),
            });

        stockGroup.MapPost("/stocktakes", async (StocktakeRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.StocktakeAsync(request, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Result: { } result } => Results.Ok(result),
                _ => Results.BadRequest(),
            });

        return app;
    }
}
