using System.Text.RegularExpressions;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

public sealed record ShopSettingsDto(string Name, string Slug, string? Area, string? Address, string? UpiId, string? ReminderTemplate, ShopType Type = ShopType.Kirana, string? Gstin = null, bool GstComposition = false, IReadOnlyList<ShopType>? ExtraTypes = null, decimal StaffMaxDiscountPercent = 10m);

/// <summary><see cref="Type"/>: null keeps the shop's trade.</summary>
public sealed record ShopSettingsUpdate(string? Name, string? Area, string? Address, string? UpiId, string? ReminderTemplate, ShopType? Type = null, string? Gstin = null, bool? GstComposition = null, IReadOnlyList<ShopType>? ExtraTypes = null, decimal? StaffMaxDiscountPercent = null);

/// <summary>The shop's own details: name, where it is, UPI ID and reminder wording. Everyone reads; the owner changes.</summary>
public static partial class ShopSettingsEndpoints
{
    public static IEndpointRouteBuilder MapShopSettings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shop").WithTags("Shop");
        group.MapGet("", GetAsync).RequireAuthorization(AppPolicies.ShopMember);
        group.MapPut("", UpdateAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> GetAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId)
            .SingleOrDefaultAsync(ct) is { } s
            ? TypedResults.Ok(new ShopSettingsDto(s.Name, s.Slug, s.Area, s.Address, s.UpiId, s.ReminderTemplate, s.Type, s.Gstin, s.GstComposition, ShopTypes.Parse(s.ExtraTypes), s.StaffMaxDiscountPercent))
            : TypedResults.NotFound();

    private static async Task<IResult> UpdateAsync(ShopSettingsUpdate request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        {
            errors[nameof(request.Name)] = ["Enter the shop's name."];
        }

        var upi = string.IsNullOrWhiteSpace(request.UpiId) ? null : request.UpiId.Trim().ToLowerInvariant();
        if (upi is not null && !UpiPattern().IsMatch(upi))
        {
            errors[nameof(request.UpiId)] = ["A UPI ID looks like name@bank, e.g. sharmastore@okaxis."];
        }

        if (request.ReminderTemplate?.Length > 500 || request.Area?.Length > 120 || request.Address?.Length > 200)
        {
            errors[nameof(request.ReminderTemplate)] = ["Keep the reminder under 500 characters and the address short."];
        }
        else if (request.ReminderTemplate is { } template && !string.IsNullOrWhiteSpace(template) && !template.Contains("{amount}", StringComparison.Ordinal))
        {
            errors[nameof(request.ReminderTemplate)] = ["The reminder must include {amount}, so the customer knows how much is due."];
        }

        var gstin = string.IsNullOrWhiteSpace(request.Gstin) ? null : request.Gstin.Trim().ToUpperInvariant();
        if (gstin is not null && !GstinPattern().IsMatch(gstin))
        {
            errors[nameof(request.Gstin)] = ["A GSTIN has 15 letters and digits, like 08ABCDE1234F1Z5."];
        }

        if (request.StaffMaxDiscountPercent is < 0 or > 100)
        {
            errors[nameof(request.StaffMaxDiscountPercent)] = ["Set the staff discount limit between 0 and 100%."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var shop = await db.Shops.SingleAsync(s => s.Id == tenant.ShopId, ct);
        shop.Name = request.Name!.Trim();
        shop.Area = Clean(request.Area);
        shop.Address = Clean(request.Address);
        shop.UpiId = upi;
        shop.ReminderTemplate = Clean(request.ReminderTemplate);
        shop.Type = request.Type ?? shop.Type;
        shop.Gstin = gstin;
        shop.GstComposition = request.GstComposition ?? shop.GstComposition;
        shop.StaffMaxDiscountPercent = request.StaffMaxDiscountPercent ?? shop.StaffMaxDiscountPercent;
        if (request.ExtraTypes is { } extra)
        {
            shop.ExtraTypes = extra.Where(x => x != shop.Type).Distinct().Select(ShopTypes.Name).ToArray();
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new ShopSettingsDto(shop.Name, shop.Slug, shop.Area, shop.Address, shop.UpiId, shop.ReminderTemplate, shop.Type, shop.Gstin, shop.GstComposition, ShopTypes.Parse(shop.ExtraTypes), shop.StaffMaxDiscountPercent));
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [GeneratedRegex(@"^[a-z0-9.\-_]{2,256}@[a-z][a-z0-9]{1,63}$")]
    private static partial Regex UpiPattern();

    [GeneratedRegex(@"^[0-9]{2}[A-Z0-9]{13}$")]
    private static partial Regex GstinPattern();
}
