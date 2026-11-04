using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// A shop's photo of one of its items. The phone shrinks it to a small WebP square before upload (no image
/// library on the server, nothing extra in the app's first load), so a row is ~10–40 KB. Kept in the database
/// next to the shop's other data: backed up, exported and deleted with it.
/// </summary>
public sealed class ProductImage : ITenantOwned
{
    /// <summary>The product's id: one photo per item.</summary>
    public Guid ProductId { get; set; }

    public Guid ShopId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>A small copy (about 320 px) for card grids: a quarter of the memory on the phone. Null for older photos.</summary>
    public byte[]? ThumbData { get; set; }

    public string? ThumbType { get; set; }
}

/// <summary><see cref="ThumbDataUrl"/>: the small copy the phone made alongside, optional.</summary>
public sealed record ProductImageRequest(string? DataUrl, string? ThumbDataUrl = null);

public static class ProductImageEndpoints
{
    /// <summary>Largest photo accepted after the phone has shrunk it: generous for a 512 px WebP.</summary>
    private const int MaxBytes = 150_000;

    /// <summary>The card-grid copy: a 320 px WebP of a pack shot is about 15–20 KB.</summary>
    private const int MaxThumbBytes = 40_000;


    public static IEndpointRouteBuilder MapProductImages(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1/products/{id:guid}/image").WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        shop.MapPut("", PutAsync);
        shop.MapDelete("", DeleteAsync);

        // Public, so the online store and households' screens can show them. The URL carries the photo's time
        // (?v=), so a new photo is a new URL and an old one can be cached for good, on the phone too.
        app.MapGet("/api/v1/images/{shopId:guid}/{productId:guid}", GetAsync).WithTags("Items").AllowAnonymous().DisableRateLimiting();
        return app;
    }

    private static async Task<IResult> PutAsync(Guid id, ProductImageRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (Common.Photos.Decode(request.DataUrl, MaxBytes) is not { } photo)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["DataUrl"] = ["Send a WebP, JPEG or PNG photo under 150 KB."] });
        }

        var (type, data) = photo;
        var thumb = Common.Photos.Decode(request.ThumbDataUrl, MaxThumbBytes);
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow();
        var image = await db.ProductImages.SingleOrDefaultAsync(i => i.ProductId == id, ct);
        if (image is null)
        {
            db.ProductImages.Add(new ProductImage { ProductId = id, ContentType = type, Data = data, UpdatedAt = now, ThumbType = thumb?.ContentType, ThumbData = thumb?.Data });
        }
        else
        {
            image.ContentType = type;
            image.Data = data;
            image.UpdatedAt = now;
            (image.ThumbType, image.ThumbData) = (thumb?.ContentType, thumb?.Data);
        }

        product.ImageAt = now;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new { imageAt = now });
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        await db.ProductImages.Where(i => i.ProductId == id).ExecuteDeleteAsync(ct);
        await db.Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.ImageAt, (DateTimeOffset?)null), ct);
        return TypedResults.NoContent();
    }

    /// <summary><paramref name="size"/> "thumb": the small copy for card grids, or the full photo when there is none yet.</summary>
    private static async Task<IResult> GetAsync(Guid shopId, Guid productId, string? size, HttpContext http, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (!await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        tenant.SetShop(shopId);
        var image = await db.ProductImages.AsNoTracking().SingleOrDefaultAsync(i => i.ProductId == productId, ct);
        if (image is null)
        {
            return TypedResults.NotFound();
        }

        // Overrides the API-wide no-store: a product photo is not private, and its URL changes with it.
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return size == "thumb" && image.ThumbData is { } small
            ? TypedResults.File(small, image.ThumbType ?? image.ContentType, lastModified: image.UpdatedAt)
            : TypedResults.File(image.Data, image.ContentType, lastModified: image.UpdatedAt);
    }
}
