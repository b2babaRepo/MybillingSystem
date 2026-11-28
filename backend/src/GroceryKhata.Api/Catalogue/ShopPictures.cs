using System.Text.RegularExpressions;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// A shop's own picture for something on its menu that is not an item: a department on the bill screen's rail
/// ("staples", "masala"…) or a category tile ("cat:dal"). One per key. Like item photos, the phone shrinks it to a
/// small WebP first, and it is kept in the database with the shop's other data.
/// </summary>
public sealed partial class ShopPicture : ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>What it is the picture of: a department or category key from the app's category tree.</summary>
    public required string Key { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [GeneratedRegex("^[a-z0-9][a-z0-9:_-]{0,39}$")]
    internal static partial Regex KeyShape();
}

public sealed record ShopPictureDto(string Key, DateTimeOffset UpdatedAt);

public static class ShopPictureEndpoints
{
    /// <summary>A 512 px WebP is well under this.</summary>
    private const int MaxBytes = 150_000;

    public static IEndpointRouteBuilder MapShopPictures(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1/shop/pictures").WithTags("Items");
        shop.MapGet("", ListAsync).RequireAuthorization(AppPolicies.ShopMember);
        shop.MapPut("/{key}", PutAsync).RequireAuthorization(AppPolicies.ShopOwner);
        shop.MapDelete("/{key}", DeleteAsync).RequireAuthorization(AppPolicies.ShopOwner);

        // Public like item photos; the URL carries the picture's time (?v=), so it can be cached for good.
        app.MapGet("/api/v1/pictures/{shopId:guid}/{key}", GetAsync).WithTags("Items").AllowAnonymous().DisableRateLimiting();
        return app;
    }

    private static async Task<List<ShopPictureDto>> ListAsync(AppDbContext db, CancellationToken ct) =>
        await db.ShopPictures.AsNoTracking().OrderBy(p => p.Key).Select(p => new ShopPictureDto(p.Key, p.UpdatedAt)).ToListAsync(ct);

    private static async Task<IResult> PutAsync(string key, ProductImageRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!ShopPicture.KeyShape().IsMatch(key))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Key"] = ["Not a menu key."] });
        }

        if (Photos.Decode(request.DataUrl, MaxBytes) is not { } photo)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["DataUrl"] = ["Send a WebP, JPEG or PNG photo under 150 KB."] });
        }

        var now = clock.GetUtcNow();
        var picture = await db.ShopPictures.SingleOrDefaultAsync(p => p.Key == key, ct);
        if (picture is null)
        {
            db.ShopPictures.Add(picture = new ShopPicture { Key = key, ContentType = photo.ContentType, Data = photo.Data });
        }

        (picture.ContentType, picture.Data, picture.UpdatedAt) = (photo.ContentType, photo.Data, now);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new ShopPictureDto(key, now));
    }

    private static async Task<IResult> DeleteAsync(string key, AppDbContext db, CancellationToken ct)
    {
        await db.ShopPictures.Where(p => p.Key == key).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(Guid shopId, string key, HttpContext http, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (!await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        tenant.SetShop(shopId);
        if (await db.ShopPictures.AsNoTracking().SingleOrDefaultAsync(p => p.Key == key, ct) is not { } picture)
        {
            return TypedResults.NotFound();
        }

        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return TypedResults.File(picture.Data, picture.ContentType, lastModified: picture.UpdatedAt);
    }
}
