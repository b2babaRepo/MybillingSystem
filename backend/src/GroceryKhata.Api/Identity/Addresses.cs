using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Identity;

/// <summary>A delivery address a person keeps ("Home", "Shop"). Only ever read by its owner; an order copies the text.</summary>
public sealed class SavedAddress : Entity
{
    public Guid UserId { get; set; }

    public required string Label { get; set; }

    public required string Text { get; set; }

    public string? Landmark { get; set; }

    public string? Pincode { get; set; }

    /// <summary>The one checkouts pick first.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Where it is on the map, from the phone when the person chose "use my location": finds shops near this address.</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

public sealed record SavedAddressRequest(Guid Id, string? Label, string? Text, string? Landmark, string? Pincode, double? Latitude = null, double? Longitude = null);

public sealed record SavedAddressDto(Guid Id, string Label, string Text, string? Landmark, string? Pincode, bool IsDefault = false, double? Latitude = null, double? Longitude = null);

public static class Addresses
{
    public const int MaxPerPerson = 10;

    public static IEndpointRouteBuilder MapAddresses(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/addresses").WithTags("Sign-in").RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/default", DefaultAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return app;
    }

    /// <summary>The words an order carries: the address, then the landmark and pincode.</summary>
    /// <summary>Both or neither, and on the globe.</summary>
    private static bool PointOk(SavedAddressRequest r) =>
        r.Latitude is null == r.Longitude is null && r.Latitude is null or (>= -90 and <= 90) && r.Longitude is null or (>= -180 and <= 180);

    private static readonly IResult BadPoint = TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Latitude"] = ["The location isn't valid."] });

    public static string AsText(SavedAddressDto a) => string.Join(", ", new[] { a.Text, a.Landmark, a.Pincode }.Where(x => !string.IsNullOrEmpty(x)));

    private static async Task<IReadOnlyList<SavedAddressDto>> ListAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await db.SavedAddresses.AsNoTracking().Where(a => a.UserId == user.UserId()).OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt)
            .Select(a => new SavedAddressDto(a.Id, a.Label, a.Text, a.Landmark, a.Pincode, a.IsDefault, a.Latitude, a.Longitude)).ToListAsync(ct);

    private static async Task<IResult> UpdateAsync(Guid id, SavedAddressRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var (label, text, landmark, pincode) = (Clean(request.Label, 30), Clean(request.Text, 300), Clean(request.Landmark, 120), Clean(request.Pincode, 6));
        if (label is null || text is not { Length: >= 5 } || (pincode is not null && (pincode.Length != 6 || !pincode.All(char.IsAsciiDigit))))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Text"] = ["Enter a name for it and the full address (a pincode has 6 digits)."] });
        }

        var address = await db.SavedAddresses.SingleOrDefaultAsync(a => a.Id == id && a.UserId == user.UserId(), ct);
        if (address is null)
        {
            return TypedResults.NotFound();
        }

        if (!PointOk(request)) return BadPoint;
        (address.Label, address.Text, address.Landmark, address.Pincode, address.Latitude, address.Longitude) = (label, text, landmark, pincode, request.Latitude, request.Longitude);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DefaultAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        if (!await db.SavedAddresses.AnyAsync(a => a.Id == id && a.UserId == userId, ct))
        {
            return TypedResults.NotFound();
        }

        await db.SavedAddresses.Where(a => a.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, a => a.Id == id), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> AddAsync(SavedAddressRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var (label, text, landmark, pincode) = (Clean(request.Label, 30), Clean(request.Text, 300), Clean(request.Landmark, 120), Clean(request.Pincode, 6));
        if (request.Id == Guid.Empty || label is null || text is not { Length: >= 5 } || (pincode is not null && (pincode.Length != 6 || !pincode.All(char.IsAsciiDigit))))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Text"] = ["Enter a name for it and the full address (a pincode has 6 digits)."] });
        }

        if (await db.SavedAddresses.AnyAsync(a => a.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        if (await db.SavedAddresses.CountAsync(a => a.UserId == userId, ct) >= MaxPerPerson)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Text"] = [$"Keep up to {MaxPerPerson} addresses."] });
        }

        // The first address is the default until another is chosen.
        var first = !await db.SavedAddresses.AnyAsync(a => a.UserId == userId, ct);
        if (!PointOk(request)) return BadPoint;
        db.SavedAddresses.Add(new SavedAddress { Id = request.Id, UserId = userId, Label = label, Text = text, Landmark = landmark, Pincode = pincode, IsDefault = first, Latitude = request.Latitude, Longitude = request.Longitude });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await db.SavedAddresses.Where(a => a.Id == id && a.UserId == user.UserId()).ExecuteDeleteAsync(ct) == 0 ? TypedResults.NotFound() : TypedResults.NoContent();

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
