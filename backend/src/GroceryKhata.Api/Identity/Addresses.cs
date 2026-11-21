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
}

public sealed record SavedAddressRequest(Guid Id, string? Label, string? Text, string? Landmark, string? Pincode);

public sealed record SavedAddressDto(Guid Id, string Label, string Text, string? Landmark, string? Pincode, bool IsDefault = false);

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
    public static string AsText(SavedAddressDto a) => string.Join(", ", new[] { a.Text, a.Landmark, a.Pincode }.Where(x => !string.IsNullOrEmpty(x)));

    private static async Task<IReadOnlyList<SavedAddressDto>> ListAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await db.SavedAddresses.AsNoTracking().Where(a => a.UserId == user.UserId()).OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt)
            .Select(a => new SavedAddressDto(a.Id, a.Label, a.Text, a.Landmark, a.Pincode, a.IsDefault)).ToListAsync(ct);

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

        (address.Label, address.Text, address.Landmark, address.Pincode) = (label, text, landmark, pincode);
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
        db.SavedAddresses.Add(new SavedAddress { Id = request.Id, UserId = userId, Label = label, Text = text, Landmark = landmark, Pincode = pincode, IsDefault = first });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await db.SavedAddresses.Where(a => a.Id == id && a.UserId == user.UserId()).ExecuteDeleteAsync(ct) == 0 ? TypedResults.NotFound() : TypedResults.NoContent();

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
