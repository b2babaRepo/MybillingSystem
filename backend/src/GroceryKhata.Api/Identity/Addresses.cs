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
}

public sealed record SavedAddressRequest(Guid Id, string? Label, string? Text, string? Landmark, string? Pincode);

public sealed record SavedAddressDto(Guid Id, string Label, string Text, string? Landmark, string? Pincode);

public static class Addresses
{
    public const int MaxPerPerson = 10;

    public static IEndpointRouteBuilder MapAddresses(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/addresses").WithTags("Sign-in").RequireAuthorization();
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return app;
    }

    /// <summary>The words an order carries: the address, then the landmark and pincode.</summary>
    public static string AsText(SavedAddressDto a) => string.Join(", ", new[] { a.Text, a.Landmark, a.Pincode }.Where(x => !string.IsNullOrEmpty(x)));

    private static async Task<IReadOnlyList<SavedAddressDto>> ListAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await db.SavedAddresses.AsNoTracking().Where(a => a.UserId == user.UserId()).OrderBy(a => a.CreatedAt)
            .Select(a => new SavedAddressDto(a.Id, a.Label, a.Text, a.Landmark, a.Pincode)).ToListAsync(ct);

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

        db.SavedAddresses.Add(new SavedAddress { Id = request.Id, UserId = userId, Label = label, Text = text, Landmark = landmark, Pincode = pincode });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await db.SavedAddresses.Where(a => a.Id == id && a.UserId == user.UserId()).ExecuteDeleteAsync(ct) == 0 ? TypedResults.NotFound() : TypedResults.NoContent();

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
