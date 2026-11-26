using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Catalogue;

/// <summary>Square item photos made on the server (ImageSharp, Six Labors Split License): white behind, centred, WebP.</summary>
public static class PhotoResize
{
    /// <summary>The 512 px item-page photo and the 320 px card-grid copy, the same sizes the phone makes.</summary>
    public const int Full = 512, Thumb = 320;

    public static (string ContentType, byte[] Data) Square(byte[] input, int size, int quality = 75)
    {
        using var image = Image.Load<Rgba32>(input);
        var side = Math.Max(image.Width, image.Height);
        // Pad to a square on white (the whole pack stays in view), then shrink; never enlarge a small photo.
        image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(side, side), Mode = ResizeMode.Pad, PadColor = Color.White })
            .BackgroundColor(Color.White)
            .Resize(Math.Min(size, side), Math.Min(size, side)));
        using var output = new MemoryStream();
        image.SaveAsWebp(output, new WebpEncoder { Quality = quality });
        return ("image/webp", output.ToArray());
    }
}

/// <summary>A pack photo found by barcode: the product's name and the front photo's bytes.</summary>
public sealed record BarcodePhoto(string? Name, byte[] Image);

/// <summary>A packaged product's details found by barcode, to fill in a new item: <see cref="Name"/> is brand, product and pack together.</summary>
public sealed record BarcodeDetails(string Name, string? Brand, string? Product, string? Pack);

/// <summary>Looks up a packaged product's front photo (and its details) by its barcode.</summary>
public interface IBarcodePhotos
{
    Task<BarcodePhoto?> FindAsync(string barcode, CancellationToken ct);

    /// <summary>The product's brand, name and pack size. Null: not known (or not looked up).</summary>
    Task<BarcodeDetails?> DetailsAsync(string barcode, CancellationToken ct) => Task.FromResult<BarcodeDetails?>(null);
}

/// <summary>
/// Open Food Facts: free product photos under CC BY-SA, used with a credit line wherever they show. Only its own
/// image hosts are fetched, and a photo is size-capped before it is read.
/// </summary>
public sealed class OpenFoodFactsPhotos(IHttpClientFactory http, ILogger<OpenFoodFactsPhotos> logger) : IBarcodePhotos
{
    private const int MaxDownload = 4_000_000;

    public async Task<BarcodePhoto?> FindAsync(string barcode, CancellationToken ct)
    {
        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25); // its API is slow at times
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GroceryKhata/1.0");
        using var response = await client.GetAsync($"https://world.openfoodfacts.org/api/v2/product/{barcode}.json?fields=product_name,image_front_url", ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("product", out var product)) return null;
        var url = product.TryGetProperty("image_front_url", out var u) ? u.GetString() : null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.Host.EndsWith(".openfoodfacts.org", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var image = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!image.IsSuccessStatusCode || image.Content.Headers.ContentLength > MaxDownload) return null;
        var bytes = await image.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > MaxDownload) return null;
        logger.LogInformation("Open Food Facts photo found for {Barcode}", barcode);
        return new BarcodePhoto(product.TryGetProperty("product_name", out var n) ? n.GetString() : null, bytes);
    }

    public async Task<BarcodeDetails?> DetailsAsync(string barcode, CancellationToken ct)
    {
        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GroceryKhata/1.0");
        using var response = await client.GetAsync($"https://world.openfoodfacts.org/api/v2/product/{barcode}.json?fields=product_name,product_name_en,brands,quantity", ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("product", out var p)) return null;
        string? Text(string field) => p.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()?.Trim() is { Length: > 0 } x ? x : null;
        return PhotoSources.Details(Text("product_name") ?? Text("product_name_en"), Text("brands"), Text("quantity"));
    }
}

public sealed record BarcodePhotoRequest(string? Barcode);

public sealed record PhotoReportRequest(string? Reason);

/// <summary>Item photos from other places than the shop's camera, and customers' reports of a wrong photo.</summary>
public static class PhotoSources
{
    public const string OpenFoodFactsCredit = "Open Food Facts (CC BY-SA)";

    public static IEndpointRouteBuilder MapPhotoSources(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1/products/{id:guid}/image").WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        shop.MapPost("/barcode", FromBarcodeAsync);
        app.MapGet("/api/v1/barcodes/{code}", LookupAsync).WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        shop.MapDelete("/reports", KeepAsync);
        // Anyone looking at the online store can say a photo is wrong; the shop decides.
        app.MapPost("/api/v1/images/{shopId:guid}/{productId:guid}/report", ReportAsync).WithTags("Items").AllowAnonymous();
        return app;
    }

    /// <summary>
    /// A new item's name from what the barcode database knows: the first brand (unless the name already says it), the
    /// product, and the pack ("Tata Salt 1 kg"). Each part is kept short; null when there is no product name.
    /// </summary>
    public static BarcodeDetails? Details(string? product, string? brands, string? pack)
    {
        static string? Short(string? s, int max) => s is null ? null : s.Trim() is { Length: > 0 } t ? t[..Math.Min(t.Length, max)].Trim() : null;
        var name = Short(product, 80);
        if (name is null) return null;
        var brand = Short(brands?.Split(',')[0], 40);
        var size = Short(pack, 20);
        var parts = new List<string>();
        if (brand is not null && !name.Contains(brand, StringComparison.OrdinalIgnoreCase)) parts.Add(brand);
        parts.Add(name);
        // A name that already gives a size ("Masala 70g") keeps it; the pack only fills in one that doesn't.
        if (size is not null && !name.Contains(size, StringComparison.OrdinalIgnoreCase) && !name.Any(char.IsAsciiDigit)) parts.Add(size);
        return new BarcodeDetails(string.Join(' ', parts)[..Math.Min(string.Join(' ', parts).Length, 120)], brand, name, size);
    }

    /// <summary>What a barcode is, from Open Food Facts, to fill in a new item. 204: not known there.</summary>
    private static async Task<IResult> LookupAsync(string code, IBarcodePhotos photos, ILogger<IBarcodePhotos> logger, CancellationToken ct)
    {
        if (CleanBarcode(code) is not { } barcode) return Invalid("Barcode", "Enter the 8 or 13 digits under the barcode.");
        try
        {
            return await photos.DetailsAsync(barcode, ct) is { } details ? TypedResults.Ok(details) : TypedResults.NoContent();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation(exception, "Barcode lookup failed for {Barcode}", barcode);
            return TypedResults.NoContent();
        }
    }

    /// <summary>Barcodes on Indian packs are EAN-13 (890…) or EAN-8; UPC-A from imports is 12 digits.</summary>
    public static string? CleanBarcode(string? barcode) =>
        barcode?.Trim() is { Length: 8 or 12 or 13 or 14 } b && b.All(char.IsAsciiDigit) ? b : null;

    private static async Task<IResult> FromBarcodeAsync(Guid id, BarcodePhotoRequest request, AppDbContext db, IBarcodePhotos photos, TimeProvider clock, ILogger<IBarcodePhotos> logger, CancellationToken ct)
    {
        if (CleanBarcode(request.Barcode) is not { } barcode) return Invalid("Barcode", "Enter the 8 or 13 digits under the barcode.");
        if (await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct) is not { } product) return TypedResults.NotFound();

        BarcodePhoto? found;
        try
        {
            found = await photos.FindAsync(barcode, ct);
        }
        // A timeout talking to Open Food Facts is a cancellation too, but not the shop's: only theirs passes through.
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Barcode photo lookup failed");
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Open Food Facts couldn't be reached just now.", detail: "Try again, or take the photo yourself.");
        }

        if (found is null) return Invalid("Barcode", "No photo for this barcode on Open Food Facts. Take the photo yourself.");
        (string, byte[]) full, thumb;
        try
        {
            (full, thumb) = (PhotoResize.Square(found.Image, PhotoResize.Full), PhotoResize.Square(found.Image, PhotoResize.Thumb, 70));
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            return Invalid("Barcode", "The photo found couldn't be read. Take the photo yourself.");
        }

        var now = clock.GetUtcNow();
        await ProductImageEndpoints.SaveAsync(db, product, full, thumb, now, ct);
        product.ImageCredit = OpenFoodFactsCredit;
        // The code goes on the item too, unless another item scans as it already.
        var code = Barcodes.Normalize(barcode);
        if (product.Barcode is null && !await Barcodes.TakenAsync(db, code, product.Id, ct)) product.Barcode = code;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new { imageAt = now, name = found.Name, credit = OpenFoodFactsCredit });
    }

    /// <summary>The shop looked and keeps the photo: the reports are cleared.</summary>
    private static async Task<IResult> KeepAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        await db.Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PhotoReports, 0).SetProperty(p => p.PhotoReportNote, (string?)null), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ReportAsync(Guid shopId, Guid productId, PhotoReportRequest request, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (!await db.Shops.AsNoTracking().AnyAsync(s => s.Id == shopId && s.IsActive, ct)) return TypedResults.NotFound();
        tenant.SetShop(shopId);
        var note = request.Reason?.Trim() is { Length: > 0 } r ? r[..Math.Min(120, r.Length)] : null;
        var changed = await db.Products.Where(p => p.Id == productId && (p.ImageAt != null || (p.MasterItem != null && p.MasterItem.ImageAt != null)) && p.PhotoReports < 1000)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PhotoReports, p => p.PhotoReports + 1).SetProperty(p => p.PhotoReportNote, p => note ?? p.PhotoReportNote), ct);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
