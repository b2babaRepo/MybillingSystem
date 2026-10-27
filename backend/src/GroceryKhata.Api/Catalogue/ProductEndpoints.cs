using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary><see cref="Id"/> is made by the app, so a product added offline syncs once.</summary>
public sealed record ProductRequest(Guid Id, string? Name, Unit? Unit, decimal SellingPrice, decimal? CostPrice);

public sealed record ProductDto(Guid Id, string Name, Unit Unit, decimal SellingPrice, decimal? CostPrice);

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapCatalogue(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products").WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);

        group.MapGet("", async (AppDbContext db, CancellationToken ct) =>
            await db.Products.AsNoTracking().OrderBy(p => p.Name)
                .Select(p => new ProductDto(p.Id, p.Name, p.Unit, p.SellingPrice, p.CostPrice))
                .ToListAsync(ct));

        group.MapPost("", CreateAsync);

        return app;
    }

    private static async Task<IResult> CreateAsync(ProductRequest request, AppDbContext db, CancellationToken ct)
    {
        if (await Find(db, request.Id).SingleOrDefaultAsync(ct) is { } existing)
        {
            return TypedResults.Ok(existing);
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty)
        {
            errors[nameof(request.Id)] = ["The app must send an ID for every new record."];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        {
            errors[nameof(request.Name)] = ["Enter the product's name."];
        }

        if (request.Unit is null)
        {
            errors[nameof(request.Unit)] = ["Choose a unit."];
        }

        if (!Money.IsValidAmount(request.SellingPrice))
        {
            errors[nameof(request.SellingPrice)] = ["Enter a selling price above ₹0."];
        }

        if (request.CostPrice is { } cost && !Money.IsValidAmount(cost))
        {
            errors[nameof(request.CostPrice)] = ["Enter a valid cost price, or leave it empty."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        db.Products.Add(new Product
        {
            Id = request.Id,
            Name = request.Name!.Trim(),
            Unit = request.Unit!.Value,
            SellingPrice = request.SellingPrice,
            CostPrice = request.CostPrice,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            db.ChangeTracker.Clear();
            return await Find(db, request.Id).SingleOrDefaultAsync(ct) is { } saved
                ? TypedResults.Ok(saved)
                : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This ID is already in use.");
        }

        return TypedResults.Created($"/api/v1/products/{request.Id}", await Find(db, request.Id).SingleAsync(ct));
    }

    private static IQueryable<ProductDto> Find(AppDbContext db, Guid id) =>
        db.Products.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Unit, p.SellingPrice, p.CostPrice));
}
