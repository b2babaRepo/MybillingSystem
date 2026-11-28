using System.Text.RegularExpressions;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// The platform's changes to a trade's category menu (the departments and category tiles every shop of that trade
/// browses on the bill screen, in orders and in its online store). The app carries a built-in tree; a row here
/// renames, reorders, hides, moves or pictures one of its nodes, or adds a new category that items join by the
/// words in their names. Platform-owned: one menu for every shop of the trade, edited by platform admins.
/// </summary>
public sealed partial class MenuNode
{
    public int Id { get; set; }

    /// <summary>The trade's menu: "kirana" today.</summary>
    public required string Trade { get; set; }

    /// <summary>The node's key in the tree ("staples", "dal"), or a new one the admin made.</summary>
    public required string Key { get; set; }

    /// <summary>"dept" for a department on the rail, "cat" for a category tile.</summary>
    public required string Kind { get; set; }

    /// <summary>A category: the department it sits under (null: where the built-in tree puts it).</summary>
    public string? ParentKey { get; set; }

    public string? NameHi { get; set; }
    public string? NameEn { get; set; }
    public string? NameGu { get; set; }

    /// <summary>Position among its siblings, lowest first; null: the built-in order.</summary>
    public int? SortOrder { get; set; }

    public bool IsHidden { get; set; }

    /// <summary>A new category: comma-separated words in item names that put an item in it ("rusk, toast").</summary>
    public string? Words { get; set; }

    /// <summary>A drawn icon's name from the app's set, used until there is a picture.</summary>
    public string? Icon { get; set; }

    public string? PictureType { get; set; }
    public byte[]? Picture { get; set; }
    public DateTimeOffset? PictureAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,39}$")]
    internal static partial Regex KeyShape();
}

public sealed record MenuNodeDto(string Key, string Kind, string? ParentKey, string? NameHi, string? NameEn, string? NameGu, int? SortOrder, bool IsHidden, string? Words, string? Icon, DateTimeOffset? PictureAt);

public sealed record MenuNodeRequest(string Kind, string? ParentKey, string? NameHi, string? NameEn, string? NameGu, int? SortOrder, bool IsHidden, string? Words, string? Icon);

public sealed record MenuPictureRequest(string? DataUrl, string Kind);

public static class MenuNodeEndpoints
{
    /// <summary>The admin's original may be a large studio shot; the server keeps a 320 px square of it.</summary>
    private const int MaxUpload = 4_000_000;

    public static IEndpointRouteBuilder MapMenuNodes(this IEndpointRouteBuilder app)
    {
        // Every shop's app (and its online store) reads the menu; nothing in it is private.
        app.MapGet("/api/v1/menu/{trade}", ListAsync).WithTags("Catalogue").AllowAnonymous();
        app.MapGet("/api/v1/menu-pictures/{trade}/{key}", PictureAsync).WithTags("Catalogue").AllowAnonymous().DisableRateLimiting();

        var admin = app.MapGroup("/api/v1/platform/menu/{trade}/{key}").WithTags("Platform")
            .RequireAuthorization(AppPolicies.PlatformAdmin).RequireAuthorization(AppPolicies.PlatformOpenShop).RecordsActivity();
        admin.MapPut("", PutAsync);
        admin.MapDelete("", ResetAsync);
        admin.MapPut("/picture", PutPictureAsync).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(5_600_000));
        admin.MapDelete("/picture", DeletePictureAsync);
        return app;
    }

    private static MenuNodeDto ToDto(MenuNode n) => new(n.Key, n.Kind, n.ParentKey, n.NameHi, n.NameEn, n.NameGu, n.SortOrder, n.IsHidden, n.Words, n.Icon, n.PictureAt);

    private static async Task<List<MenuNodeDto>> ListAsync(string trade, AppDbContext db, CancellationToken ct) =>
        (await db.MenuNodes.AsNoTracking().Where(n => n.Trade == trade).OrderBy(n => n.Id).ToListAsync(ct)).Select(ToDto).ToList();

    private static Dictionary<string, string[]> Check(string trade, string key, string kind)
    {
        var errors = new Dictionary<string, string[]>();
        if (!MenuNode.KeyShape().IsMatch(trade)) errors["Trade"] = ["Not a trade."];
        if (!MenuNode.KeyShape().IsMatch(key)) errors["Key"] = ["Use lower-case letters, digits, - or _ (up to 40)."];
        if (kind is not ("dept" or "cat")) errors["Kind"] = ["A department (dept) or a category (cat)."];
        return errors;
    }

    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(max, value.Trim().Length)];

    private static async Task<IResult> PutAsync(string trade, string key, MenuNodeRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var errors = Check(trade, key, request.Kind);
        if (request.ParentKey is { } parent && (request.Kind != "cat" || !MenuNode.KeyShape().IsMatch(parent))) errors["ParentKey"] = ["Only a category sits under a department."];
        if (request.Words?.Length > 300) errors["Words"] = ["Keep the words under 300 letters."];
        if (request.SortOrder is < 0 or > 999) errors["SortOrder"] = ["Between 0 and 999."];
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var node = await db.MenuNodes.SingleOrDefaultAsync(n => n.Trade == trade && n.Key == key, ct);
        if (node is null) db.MenuNodes.Add(node = new MenuNode { Trade = trade, Key = key, Kind = request.Kind });
        node.Kind = request.Kind;
        node.ParentKey = request.ParentKey;
        (node.NameHi, node.NameEn, node.NameGu) = (Clean(request.NameHi, 60), Clean(request.NameEn, 60), Clean(request.NameGu, 60));
        (node.SortOrder, node.IsHidden, node.Words, node.Icon) = (request.SortOrder, request.IsHidden, Clean(request.Words, 300), Clean(request.Icon, 20));
        node.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(node));
    }

    /// <summary>Back to the built-in tree for this node (a node the admin added is removed), picture and all.</summary>
    private static async Task<IResult> ResetAsync(string trade, string key, AppDbContext db, CancellationToken ct)
    {
        await db.MenuNodes.Where(n => n.Trade == trade && n.Key == key).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> PutPictureAsync(string trade, string key, MenuPictureRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var errors = Check(trade, key, request.Kind);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
        if (Common.Photos.Decode(request.DataUrl, MaxUpload) is not { } photo)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["DataUrl"] = ["Send a WebP, JPEG or PNG photo under 4 MB."] });
        }

        (string ContentType, byte[] Data) square;
        try
        {
            square = PhotoResize.Square(photo.Data, PhotoResize.Thumb, 80);
        }
        catch (Exception exception) when (exception is SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["DataUrl"] = ["That file isn't a picture we can read."] });
        }

        var now = clock.GetUtcNow();
        var node = await db.MenuNodes.SingleOrDefaultAsync(n => n.Trade == trade && n.Key == key, ct);
        if (node is null) db.MenuNodes.Add(node = new MenuNode { Trade = trade, Key = key, Kind = request.Kind });
        (node.PictureType, node.Picture, node.PictureAt, node.UpdatedAt) = (square.ContentType, square.Data, now, now);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(node));
    }

    private static async Task<IResult> DeletePictureAsync(string trade, string key, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        await db.MenuNodes.Where(n => n.Trade == trade && n.Key == key).ExecuteUpdateAsync(s => s
            .SetProperty(n => n.Picture, (byte[]?)null).SetProperty(n => n.PictureType, (string?)null)
            .SetProperty(n => n.PictureAt, (DateTimeOffset?)null).SetProperty(n => n.UpdatedAt, clock.GetUtcNow()), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> PictureAsync(string trade, string key, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var picture = await db.MenuNodes.AsNoTracking().Where(n => n.Trade == trade && n.Key == key && n.Picture != null)
            .Select(n => new { n.Picture, n.PictureType, n.PictureAt }).SingleOrDefaultAsync(ct);
        if (picture is null) return TypedResults.NotFound();
        // The URL carries the picture's time (?v=), so it can be cached for good.
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return TypedResults.File(picture.Picture!, picture.PictureType ?? "image/webp", lastModified: picture.PictureAt);
    }
}
