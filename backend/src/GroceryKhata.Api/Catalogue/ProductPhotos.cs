using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// More photos of an item beyond its main one (<see cref="ProductImage"/>): the back of a pack, a kurta's side and
/// fabric, a necklace on a stand. Shown as a gallery to customers. Shrunk on the phone like the main photo.
/// </summary>
public sealed class ProductExtraPhoto : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid ShopId { get; set; }

    public Guid ProductId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public string? ThumbType { get; set; }

    public byte[]? ThumbData { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record ExtraPhotoRequest(Guid Id, string? DataUrl, string? ThumbDataUrl = null);

public static class ProductPhotoEndpoints
{
    /// <summary>Photos beyond the main one: enough for every side of an item, small enough to keep the shop's data light.</summary>
    public const int MaxExtra = 5;

    private const int MaxBytes = 150_000;
    private const int MaxThumbBytes = 40_000;

    public static IEndpointRouteBuilder MapProductPhotos(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1/products/{id:guid}/photos").WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        shop.MapPost("", AddAsync);
        shop.MapDelete("/{photoId:guid}", DeleteAsync);
        // Public like the main photo: the online store shows them. The photo id never changes, so it can be cached for good.
        app.MapGet("/api/v1/images/{shopId:guid}/{productId:guid}/{photoId:guid}", GetAsync).WithTags("Items").AllowAnonymous().DisableRateLimiting();
        return app;
    }

    /// <summary>The extra photos of these items, oldest first: what the item lists and the store send to show a gallery.</summary>
    internal static async Task<Dictionary<Guid, List<Guid>>> IdsAsync(AppDbContext db, IReadOnlyCollection<Guid>? productIds, CancellationToken ct)
    {
        var query = db.ProductExtraPhotos.AsNoTracking();
        if (productIds is not null)
        {
            query = query.Where(p => productIds.Contains(p.ProductId));
        }

        return (await query.OrderBy(p => p.CreatedAt).Select(p => new { p.ProductId, p.Id }).ToListAsync(ct))
            .GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());
    }

    private static async Task<IResult> AddAsync(Guid id, ExtraPhotoRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.ProductExtraPhotos.AsNoTracking().AnyAsync(p => p.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        if (request.Id == Guid.Empty || Common.Photos.Decode(request.DataUrl, MaxBytes) is not { } photo)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["DataUrl"] = ["Send a WebP, JPEG or PNG photo under 150 KB."] });
        }

        if (!await db.Products.AnyAsync(p => p.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        if (await db.ProductExtraPhotos.CountAsync(p => p.ProductId == id, ct) >= MaxExtra)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["DataUrl"] = [$"An item can have {MaxExtra} more photos besides its main one. Remove one first."] });
        }

        var thumb = Common.Photos.Decode(request.ThumbDataUrl, MaxThumbBytes);
        db.ProductExtraPhotos.Add(new ProductExtraPhoto
        {
            Id = request.Id, ProductId = id, ContentType = photo.ContentType, Data = photo.Data, ThumbType = thumb?.ContentType, ThumbData = thumb?.Data, CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, Guid photoId, AppDbContext db, CancellationToken ct)
    {
        await db.ProductExtraPhotos.Where(p => p.Id == photoId && p.ProductId == id).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(Guid shopId, Guid productId, Guid photoId, string? size, HttpContext http, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (!await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        tenant.SetShop(shopId);
        if (await db.ProductExtraPhotos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == photoId && p.ProductId == productId, ct) is not { } photo)
        {
            return TypedResults.NotFound();
        }

        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return size == "thumb" && photo.ThumbData is { } small
            ? TypedResults.File(small, photo.ThumbType ?? photo.ContentType)
            : TypedResults.File(photo.Data, photo.ContentType);
    }
}
