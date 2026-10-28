using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

public sealed record CatalogueItemDto(int Id, string Name, string NameEn, Unit Unit, string Category, decimal TypicalPrice);

/// <summary>One shop's price for a catalogue item: what a household sees before sending its list there.</summary>
public sealed record ShopPriceDto(int CatalogueItemId, Guid ProductId, string Name, Unit Unit, decimal Price, bool InStock);

/// <summary>One of a shop's options for a common-list item (brand, pack size, loose), with its price.</summary>
public sealed record ShopOptionDto(
    int CatalogueItemId, Guid ProductId, string Name, Unit Unit, decimal? PackSize, Unit? PackUnit, decimal Price, bool InStock, bool IsDefault);

/// <summary>The common catalogue households pick from, and each shop's own prices for it.</summary>
public static class CatalogueEndpoints
{
    public static IEndpointRouteBuilder MapCommonCatalogue(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Catalogue").RequireAuthorization();
        group.MapGet("/catalogue", GetCatalogueAsync);
        group.MapGet("/shops/{shopId:guid}/prices", GetShopPricesAsync);
        group.MapGet("/shops/{shopId:guid}/options", GetShopOptionsAsync);
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

    /// <summary>Every option the shop has for each common-list item, default first, then cheapest.</summary>
    private static async Task<IResult> GetShopOptionsAsync(Guid shopId, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (tenant.ShopId != Guid.Empty && tenant.ShopId != shopId)
        {
            return TypedResults.NotFound();
        }

        tenant.SetShop(shopId);
        if (!await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        var defaults = MatchToCatalogue(await ShopProductsAsync(db, ct)).Select(m => m.Product.Id).ToHashSet();
        return TypedResults.Ok((await ShopProductsAsync(db, ct))
            .Select(p => (Id: p.CatalogueItemId ?? CommonCatalogue.Match(p.Name), Product: p))
            .Where(m => m.Id is not null)
            .OrderBy(m => m.Id).ThenByDescending(m => defaults.Contains(m.Product.Id)).ThenBy(m => m.Product.SellingPrice)
            .Select(m => new ShopOptionDto(m.Id!.Value, m.Product.Id, m.Product.Name, m.Product.Unit, m.Product.PackSize, m.Product.PackUnit,
                m.Product.SellingPrice, m.Product.Stock - m.Product.Reserved > 0, defaults.Contains(m.Product.Id)))
            .ToList());
    }

    internal static Task<List<Product>> ShopProductsAsync(AppDbContext db, CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => p.IsActive && p.SellingPrice > 0).ToListAsync(ct);

    /// <summary>
    /// The shop's product for each catalogue item. When a shop has several options (two brands of atta), its chosen
    /// default stands in, else the one with most stock: it's what the shop would pack first.
    /// </summary>
    internal static IEnumerable<(int CatalogueItemId, Product Product)> MatchToCatalogue(IEnumerable<Product> products) =>
        products.Select(p => (Id: p.CatalogueItemId ?? CommonCatalogue.Match(p.Name), Product: p))
            .Where(m => m.Id is not null)
            .GroupBy(m => m.Id!.Value)
            .Select(g => (g.Key, g.OrderByDescending(m => m.Product.IsDefaultOption).ThenByDescending(m => m.Product.Stock).First().Product));
}
