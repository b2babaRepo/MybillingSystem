using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>A market shops belong to ("Raja Park bazaar"). The platform's own list, not a shop's data.</summary>
public sealed class Market : Entity
{
    public required string Name { get; set; }

    public string? State { get; set; }

    public string? District { get; set; }

    public string? City { get; set; }

    /// <summary>For a village market: City is then usually empty.</summary>
    public string? Village { get; set; }

    /// <summary>False: kept on the shops that have it, but no longer offered for new ones.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>The shop-front photo. Its own table, so loading shops doesn't load pictures.</summary>
public sealed class ShopPhoto : Entity
{
    public Guid ShopId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }
}

public sealed record MarketDto(Guid Id, string Name, string? City, bool IsActive, int Shops, string? State = null, string? District = null, string? Village = null);

public sealed record PlaceDto(string? State, string? District, string? City, string? Village);

public sealed record MarketRequest(string? Name, string? City, bool? IsActive = null, string? State = null, string? District = null, string? Village = null);

public static class Markets
{
    private const int MaxPhotoBytes = 150 * 1024;
    private static readonly string[] ImageTypes = ["image/jpeg", "image/webp"];

    public static IEndpointRouteBuilder MapMarkets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/platform/markets").WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin).RecordsActivity();
        group.MapGet("", ListAsync);
        group.MapGet("/place", PlaceAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        group.MapPost("", AddAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        group.MapPut("/{id:guid}", ChangeAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        return app;
    }

    /// <summary>A photo from a data URL, or null if it isn't a small JPEG or WebP.</summary>
    public static ShopPhoto? ParsePhoto(Guid shopId, string? dataUrl)
    {
        var parts = dataUrl?.Split(',', 2);
        var type = parts is [var head, _] && head.StartsWith("data:", StringComparison.Ordinal) && head.EndsWith(";base64", StringComparison.Ordinal) ? head[5..^7] : null;
        if (type is null || !ImageTypes.Contains(type))
        {
            return null;
        }

        try
        {
            var data = Convert.FromBase64String(parts![1]);
            return data.Length is > 0 and <= MaxPhotoBytes ? new ShopPhoto { ShopId = shopId, ContentType = type, Data = data } : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// State, district, city and village at a GPS point, to fill the form. Only the point is sent, by the server, to
    /// Geocoding:ReverseUrl (default: OpenStreetMap's Nominatim, for low admin use). Empty on any failure: the admin types them.
    /// </summary>
    private static async Task<PlaceDto> PlaceAsync(double lat, double lng, IHttpClientFactory http, IConfiguration config, ILogger<Market> logger, CancellationToken ct)
    {
        var empty = new PlaceDto(null, null, null, null);
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            return empty;
        }

        try
        {
            var client = http.CreateClient("place");
            client.Timeout = TimeSpan.FromSeconds(6);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GroceryKhata/1.0");
            var baseUrl = config["Geocoding:ReverseUrl"] ?? "https://nominatim.openstreetmap.org/reverse";
            var url = FormattableString.Invariant($"{baseUrl}?format=jsonv2&zoom=14&accept-language=en&lat={lat:F5}&lon={lng:F5}");
            using var json = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(url, ct));
            if (!json.RootElement.TryGetProperty("address", out var a))
            {
                return empty;
            }

            string? Get(params string[] keys) => keys.Select(k => a.TryGetProperty(k, out var v) ? Clean(v.GetString()) : null).FirstOrDefault(v => v is not null);
            return new PlaceDto(Get("state"), Get("state_district", "county"), Get("city", "town"), Get("village", "hamlet"));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "Place lookup failed");
            return empty;
        }
    }

    private static async Task<IReadOnlyList<MarketDto>> ListAsync(AppDbContext db, CancellationToken ct) =>
        await db.Markets.AsNoTracking().OrderBy(m => m.Name)
            .Select(m => new MarketDto(m.Id, m.Name, m.City, m.IsActive, db.Shops.Count(s => s.MarketId == m.Id), m.State, m.District, m.Village))
            .ToListAsync(ct);

    private static async Task<IResult> AddAsync(MarketRequest request, AppDbContext db, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (name is not { Length: >= 2 and <= 120 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Enter the market's name."] });
        }

        // "Main Bazaar" exists in many towns: the same name is a duplicate only in the same place.
        var (state, district, city, village) = (Clean(request.State), Clean(request.District), Clean(request.City), Clean(request.Village));
        if (await db.Markets.AnyAsync(m => m.Name.ToLower() == name.ToLower() && m.State == state && m.District == district && m.City == city && m.Village == village, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This market is already on the list.");
        }

        var market = new Market { Name = name, State = state, District = district, City = city, Village = village };
        db.Markets.Add(market);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new MarketDto(market.Id, market.Name, market.City, market.IsActive, 0, state, district, village));
    }

    private static async Task<IResult> ChangeAsync(Guid id, MarketRequest request, AppDbContext db, CancellationToken ct)
    {
        var market = await db.Markets.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (market is null)
        {
            return TypedResults.NotFound();
        }

        var name = request.Name?.Trim();
        if (name is not { Length: >= 2 and <= 120 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Enter the market's name."] });
        }

        var (state, district, city, village) = (Clean(request.State), Clean(request.District), Clean(request.City), Clean(request.Village));
        if (await db.Markets.AnyAsync(m => m.Id != id && m.Name.ToLower() == name.ToLower() && m.State == state && m.District == district && m.City == city && m.Village == village, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This market is already on the list.");
        }

        (market.Name, market.State, market.District, market.City, market.Village, market.IsActive) = (name, state, district, city, village, request.IsActive ?? market.IsActive);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    public static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 80)];
    }
}
