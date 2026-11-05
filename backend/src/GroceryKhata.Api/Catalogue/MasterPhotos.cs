using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One photo on a master item, chosen by the platform team: every shop item picked from that master item shows it
/// until the shop adds its own. Not owned by any shop, so it lives once and is served from here to every shop.
/// </summary>
public sealed class MasterItemImage
{
    public int MasterItemId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public required string ThumbType { get; set; }

    public required byte[] ThumbData { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary><see cref="Credit"/>: where the photo is from when it needs a credit line ("Unsplash", "Wikimedia: name, CC BY").</summary>
public sealed record MasterPhotoRequest(string? DataUrl, string? Credit);

public static class MasterPhotos
{
    /// <summary>What a master item's photo says under it in shops, unless it carries a credit of its own.</summary>
    public const string Representative = "Representative image";

    /// <summary>The admin's original may be large (a studio shot); the server makes the 512 and 320 px copies from it.</summary>
    private const int MaxUpload = 4_000_000;

    public static IEndpointRouteBuilder MapMasterPhotos(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/v1/platform/master-items/{id:int}/image").WithTags("Platform")
            .RequireAuthorization(AppPolicies.PlatformAdmin).RequireAuthorization(AppPolicies.PlatformOpenShop).RecordsActivity();
        admin.MapPut("", PutAsync).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(5_600_000));
        admin.MapDelete("", DeleteAsync);
        // Public like shop photos: the pick screen and the online store show them. The URL carries the photo's time.
        app.MapGet("/api/v1/master-images/{id:int}", GetAsync).WithTags("Catalogue").AllowAnonymous().DisableRateLimiting();
        return app;
    }

    private static async Task<IResult> PutAsync(int id, MasterPhotoRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.MasterItems.SingleOrDefaultAsync(i => i.Id == id, ct) is not { } item) return TypedResults.NotFound();
        if (Common.Photos.Decode(request.DataUrl, MaxUpload) is not { } photo) return Invalid("DataUrl", "Send a WebP, JPEG or PNG photo under 4 MB.");

        (string ContentType, byte[] Data) full, thumb;
        try
        {
            (full, thumb) = (PhotoResize.Square(photo.Data, PhotoResize.Full), PhotoResize.Square(photo.Data, PhotoResize.Thumb, 70));
        }
        catch (Exception exception) when (exception is SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
        {
            return Invalid("DataUrl", "This photo couldn't be read.");
        }

        var now = clock.GetUtcNow();
        var image = await db.MasterItemImages.SingleOrDefaultAsync(i => i.MasterItemId == id, ct);
        if (image is null)
        {
            db.MasterItemImages.Add(image = new MasterItemImage { MasterItemId = id, ContentType = full.ContentType, Data = full.Data, ThumbType = thumb.ContentType, ThumbData = thumb.Data });
        }

        (image.ContentType, image.Data, image.ThumbType, image.ThumbData, image.UpdatedAt) = (full.ContentType, full.Data, thumb.ContentType, thumb.Data, now);
        var credit = request.Credit?.Trim();
        (item.ImageAt, item.ImageCredit, item.UpdatedAt) = (now, string.IsNullOrEmpty(credit) ? null : credit[..Math.Min(120, credit.Length)], now);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new { imageAt = now });
    }

    private static async Task<IResult> DeleteAsync(int id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        await db.MasterItemImages.Where(i => i.MasterItemId == id).ExecuteDeleteAsync(ct);
        await db.MasterItems.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.ImageAt, (DateTimeOffset?)null).SetProperty(i => i.ImageCredit, (string?)null), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(int id, string? size, HttpContext http, AppDbContext db, CancellationToken ct) =>
        await db.MasterItemImages.AsNoTracking().SingleOrDefaultAsync(i => i.MasterItemId == id, ct) is { } image
            ? Serve(http, image, size)
            : TypedResults.NotFound();

    /// <summary>The master photo as a shop item's own (when the shop has none): the full photo, or the card-grid copy.</summary>
    internal static IResult Serve(HttpContext http, MasterItemImage image, string? size)
    {
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return size == "thumb"
            ? TypedResults.File(image.ThumbData, image.ThumbType, lastModified: image.UpdatedAt)
            : TypedResults.File(image.Data, image.ContentType, lastModified: image.UpdatedAt);
    }
}
