using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>
/// How the shop's bills look on paper: its logo, the owner's signature over "Authorised signatory", a print style and a
/// few lines of terms ("Goods once sold…", bank details). The pictures are small (the phone shrinks them before upload)
/// and kept apart from the shop row, so only bill printing ever loads them.
/// </summary>
public sealed class PrintLook : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>A data URL (WebP, JPEG or PNG), at most <see cref="PrintLooks.MaxImageBytes"/>. Null: no logo.</summary>
    public string? Logo { get; set; }

    public string? Signature { get; set; }

    /// <summary>One of <see cref="PrintLooks.Themes"/>. Null: classic.</summary>
    public string? Theme { get; set; }

    /// <summary>Printed at the foot of every bill. Null: nothing.</summary>
    public string? Terms { get; set; }
}

/// <summary>On a bill: what to print it with.</summary>
public sealed record BillPrintDto(string? Logo, string? Signature, string Theme, string? Terms);

/// <summary>A null picture removes it; a missing one (<see cref="KeepLogo"/>, <see cref="KeepSignature"/>) leaves it as it is.</summary>
public sealed record PrintLookRequest(string? Logo, string? Signature, string? Theme, string? Terms, bool KeepLogo = false, bool KeepSignature = false);

public static class PrintLooks
{
    public const int MaxImageBytes = 60_000;
    public const int MaxTerms = 400;
    public static readonly string[] Themes = ["classic", "modern", "compact"];

    public static IEndpointRouteBuilder MapPrintLook(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shop/print").WithTags("Shop").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", async (AppDbContext db, CancellationToken ct) => TypedResults.Ok(await ForBillAsync(db, ct)));
        group.MapPut("", SetAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>The current shop's print look (the tenant filter picks the shop).</summary>
    public static async Task<BillPrintDto> ForBillAsync(AppDbContext db, CancellationToken ct)
    {
        var look = await db.PrintLooks.AsNoTracking().SingleOrDefaultAsync(ct);
        return new BillPrintDto(look?.Logo, look?.Signature, look?.Theme ?? "classic", look?.Terms);
    }

    private static async Task<IResult> SetAsync(PrintLookRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (field, picture, keep) in new[] { ("Logo", request.Logo, request.KeepLogo), ("Signature", request.Signature, request.KeepSignature) })
        {
            if (!keep && picture is not null && Photos.Decode(picture, MaxImageBytes) is null)
            {
                errors[field] = ["Send a WebP, JPEG or PNG picture under 60 KB."];
            }
        }

        var theme = string.IsNullOrWhiteSpace(request.Theme) ? "classic" : request.Theme.Trim().ToLowerInvariant();
        if (!Themes.Contains(theme)) errors["Theme"] = [$"Choose one of: {string.Join(", ", Themes)}."];
        var terms = string.IsNullOrWhiteSpace(request.Terms) ? null : request.Terms.Trim();
        if (terms?.Length > MaxTerms) errors["Terms"] = [$"Keep the terms under {MaxTerms} characters."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var look = await db.PrintLooks.SingleOrDefaultAsync(ct);
        if (look is null)
        {
            db.PrintLooks.Add(look = new PrintLook());
        }

        if (!request.KeepLogo) look.Logo = request.Logo;
        if (!request.KeepSignature) look.Signature = request.Signature;
        (look.Theme, look.Terms) = (theme, terms);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ForBillAsync(db, ct));
    }
}
