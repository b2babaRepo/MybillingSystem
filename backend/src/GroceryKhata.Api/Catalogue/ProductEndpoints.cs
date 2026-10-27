using System.Security.Claims;
using GroceryKhata.Api.Identity;

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
    decimal? OpeningStock = null);

public sealed record ProductUpdate(string? Name, Unit? Unit, decimal SellingPrice, decimal? CostPrice, string? Category, decimal? ReorderLevel, bool? IsActive);

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
    decimal Reserved = 0);

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

public sealed record ImportRow(string? Name, Unit? Unit, decimal? SellingPrice, decimal? CostPrice, string? Category, decimal? OpeningStock, decimal? ReorderLevel);

public sealed record ImportRequest(IReadOnlyList<ImportRow>? Rows);

public sealed record ImportResult(int Created, int Skipped);

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

        products.MapPost("/import", async (ImportRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.ImportAsync(request, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Result: { } result } => Results.Ok(result),
                _ => Results.BadRequest(),
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
