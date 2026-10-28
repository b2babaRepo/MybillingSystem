using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

public sealed record CatalogueItemDto(int Id, string Name, string NameEn, Unit Unit, string Category, decimal TypicalPrice);

/// <summary>One shop's price for a catalogue item: what a household sees before sending its list there.</summary>
public sealed record ShopPriceDto(int CatalogueItemId, Guid ProductId, string Name, Unit Unit, decimal Price, bool InStock);

/// <summary>The common catalogue households pick from, and each shop's own prices for it.</summary>
public static class CatalogueEndpoints
{
    public static IEndpointRouteBuilder MapCommonCatalogue(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Catalogue").RequireAuthorization();
        group.MapGet("/catalogue", GetCatalogueAsync);
        group.MapGet("/shops/{shopId:guid}/prices", GetShopPricesAsync);
        return app;
    }

    private static async Task<IReadOnlyList<CatalogueItemDto>> GetCatalogueAsync(AppDbContext db, CancellationToken ct) =>
        await db.CatalogueItems.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new CatalogueItemDto(i.Id, i.Name, i.NameEn, i.Unit, i.Category, i.TypicalPrice))
            .ToListAsync(ct);

    private static async Task<IResult> GetShopPricesAsync(Guid shopId, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (tenant.ShopId != Guid.Empty && tenant.ShopId != shopId)
        {
            return TypedResults.NotFound();
        }

        // Read as that shop (its products are behind row-level security); priced, active items only.
        tenant.SetShop(shopId);
        if (!await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MatchToCatalogue(await ShopProductsAsync(db, ct)).Select(m =>
            new ShopPriceDto(m.CatalogueItemId, m.Product.Id, m.Product.Name, m.Product.Unit, m.Product.SellingPrice, m.Product.Stock - m.Product.Reserved > 0)).ToList());
    }

    internal static Task<List<Product>> ShopProductsAsync(AppDbContext db, CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => p.IsActive && p.SellingPrice > 0).ToListAsync(ct);

    /// <summary>
    /// The shop's product for each catalogue item. When a shop has several (two brands of atta), the one with most
    /// stock stands in: it's what the shop would pack first.
    /// </summary>
    internal static IEnumerable<(int CatalogueItemId, Product Product)> MatchToCatalogue(IEnumerable<Product> products) =>
        products.Select(p => (Id: CommonCatalogue.Match(p.Name), Product: p))
            .Where(m => m.Id is not null)
            .GroupBy(m => m.Id!.Value)
            .Select(g => (g.Key, g.OrderByDescending(m => m.Product.Stock).First().Product));
}
