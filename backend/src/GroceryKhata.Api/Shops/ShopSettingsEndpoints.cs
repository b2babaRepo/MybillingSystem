using System.Text.RegularExpressions;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

public sealed record ShopLocationDto(double? Latitude, double? Longitude);

public sealed record ShopSettingsDto(string Name, string Slug, string? Area, string? Address, string? UpiId, string? ReminderTemplate, ShopType Type = ShopType.Kirana, string? Gstin = null, bool GstComposition = false, IReadOnlyList<ShopType>? ExtraTypes = null, decimal StaffMaxDiscountPercent = 10m, bool? DeliversGoods = null, decimal StaffMaxWriteOff = 1000m, int ExchangeSlipDays = 90, decimal ExchangeSlipHelperMax = 5000m, string? BillPrefix = null, IReadOnlyList<string>? BillFields = null,
    decimal CashReceiptLimit = 200_000m, decimal PanFrom = 200_000m, decimal CashPayoutLimit = 10_000m,
    string? GirviState = null, string? GirviLicence = null, decimal? GirviMaxYearlyRate = null, int GirviNoticeDays = 15,
    decimal ContractorDiscountPercent = 0, decimal DealerDiscountPercent = 0, string? ScaleLabel = null, decimal? LoyaltyEarnPer = null, decimal LoyaltyPointValue = 1m);

/// <summary><see cref="Type"/>: null keeps the shop's trade.</summary>
public sealed record ShopSettingsUpdate(string? Name, string? Area, string? Address, string? UpiId, string? ReminderTemplate, ShopType? Type = null, string? Gstin = null, bool? GstComposition = null, IReadOnlyList<ShopType>? ExtraTypes = null, decimal? StaffMaxDiscountPercent = null, bool? DeliversGoods = null, decimal? StaffMaxWriteOff = null, int? ExchangeSlipDays = null, decimal? ExchangeSlipHelperMax = null,
    // Null keeps them (older apps); "" / an empty list clears them.
    string? BillPrefix = null, IReadOnlyList<string>? BillFields = null,
    decimal? CashReceiptLimit = null, decimal? PanFrom = null, decimal? CashPayoutLimit = null,
    // Girvi state rules: "" clears the state, licence or cap; null keeps them.
    string? GirviState = null, string? GirviLicence = null, decimal? GirviMaxYearlyRate = null, int? GirviNoticeDays = null,
    decimal? ContractorDiscountPercent = null, decimal? DealerDiscountPercent = null, string? ScaleLabel = null, decimal? LoyaltyEarnPer = null, decimal? LoyaltyPointValue = null);

/// <summary>The shop's own details: name, where it is, UPI ID and reminder wording. Everyone reads; the owner changes.</summary>
public static partial class ShopSettingsEndpoints
{
    public static IEndpointRouteBuilder MapShopSettings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shop").WithTags("Shop");
        group.MapGet("", GetAsync).RequireAuthorization(AppPolicies.ShopMember);
        group.MapPut("", UpdateAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/location", async (AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
            TypedResults.Ok(await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new ShopLocationDto(s.Latitude, s.Longitude)).SingleAsync(ct))).RequireAuthorization(AppPolicies.ShopMember);
        group.MapPut("/location", SetLocationAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>The shop's place on the map (the owner stands in the shop and taps "use my location"): customers nearby find it. Both null clears it.</summary>
    private static async Task<IResult> SetLocationAsync(ShopLocationDto request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (request.Latitude is null != request.Longitude is null || request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Latitude"] = ["The location isn't valid."] });
        }

        await db.Shops.Where(s => s.Id == tenant.ShopId).ExecuteUpdateAsync(x => x.SetProperty(s => s.Latitude, request.Latitude).SetProperty(s => s.Longitude, request.Longitude), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId)
            .SingleOrDefaultAsync(ct) is { } s
            ? TypedResults.Ok(new ShopSettingsDto(s.Name, s.Slug, s.Area, s.Address, s.UpiId, s.ReminderTemplate, s.Type, s.Gstin, s.GstComposition, ShopTypes.Parse(s.ExtraTypes), s.StaffMaxDiscountPercent, s.DeliversGoods, s.StaffMaxWriteOff, s.ExchangeSlipDays, s.ExchangeSlipHelperMax, s.BillPrefix, Ledger.BillFields.Labels(s.BillFields), s.CashReceiptLimit, s.PanFrom, s.CashPayoutLimit, s.GirviState, s.GirviLicence, s.GirviMaxYearlyRate, s.GirviNoticeDays, s.ContractorDiscountPercent, s.DealerDiscountPercent, s.ScaleLabel, s.LoyaltyEarnPer, s.LoyaltyPointValue))
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
        if (gstin is not null && !Ledger.GstExport.IsValidGstin(gstin))
        {
            errors[nameof(request.Gstin)] = ["This GSTIN isn't valid: check each letter and digit (the last one is a check digit)."];
        }

        if (request.CashReceiptLimit is < 1_000 or > 10_000_000 || request.PanFrom is < 1_000 or > 10_000_000 || request.CashPayoutLimit is < 0 or > 10_000_000)
        {
            errors[nameof(request.CashReceiptLimit)] = ["Set the cash and PAN limits between ₹1,000 and ₹1 crore."];
        }

        if (request.GirviState is { Length: > 0 } st && !Ledger.GirviRules.States.ContainsKey(st))
        {
            errors[nameof(request.GirviState)] = ["Choose a state from the list."];
        }

        if (request.GirviLicence is { Length: > 60 } || request.GirviMaxYearlyRate is < 0 or > 100 || request.GirviNoticeDays is < 0 or > 365)
        {
            errors[nameof(request.GirviLicence)] = ["Licence up to 60 letters, a yearly interest cap from 0 to 100% and notice from 0 to 365 days."];
        }

        if (request.ExchangeSlipHelperMax is < 0 or > 10_000_000)
        {
            errors[nameof(request.ExchangeSlipHelperMax)] = ["Set the helper exchange slip limit between ₹0 and ₹1 crore."];
        }

        if (request.ExchangeSlipDays is < 1 or > 730)
        {
            errors[nameof(request.ExchangeSlipDays)] = ["Set exchange slips to last between 1 and 730 days."];
        }

        if (request.StaffMaxWriteOff is < 0 or > 10_000_000)
        {
            errors[nameof(request.StaffMaxWriteOff)] = ["Set the helper write-off limit between ₹0 and ₹1 crore."];
        }

        if (request.StaffMaxDiscountPercent is < 0 or > 100)
        {
            errors[nameof(request.StaffMaxDiscountPercent)] = ["Set the staff discount limit between 0 and 100%."];
        }

        var prefix = request.BillPrefix is null ? null : Ledger.BillFields.CleanPrefix(request.BillPrefix);
        if (request.BillPrefix is not null && prefix is null)
        {
            errors[nameof(request.BillPrefix)] = ["A bill-number prefix is 1 to 3 letters or digits, like MB."];
        }

        var fieldLabels = request.BillFields is null ? null : Ledger.BillFields.CleanLabels(request.BillFields);
        if (request.BillFields is not null && fieldLabels is null)
        {
            errors[nameof(request.BillFields)] = [$"Up to {Ledger.BillFields.MaxFields} bill fields, each under {Ledger.BillFields.MaxLabel} letters."];
        }

        var scale = request.ScaleLabel?.Trim();
        if (!string.IsNullOrEmpty(scale) && !(scale.Length == 13 && ScaleLabelPattern().IsMatch(scale)))
        {
            errors[nameof(request.ScaleLabel)] = ["A scale label is 13 characters starting with 2, like 21PPPPPWWWWWC: 4-6 P for the item code, then 4-6 W (grams) or R (paise), then C."];
        }

        // 0 turns points off; otherwise from ₹1 to ₹1 lakh a point, each point worth up to ₹100.
        if (request.LoyaltyEarnPer is < 0 or > 100_000 || request.LoyaltyPointValue is <= 0 or > 100
            || (request.LoyaltyEarnPer is { } per && decimal.Round(per, 2) != per) || (request.LoyaltyPointValue is { } worth && decimal.Round(worth, 2) != worth))
        {
            errors[nameof(request.LoyaltyEarnPer)] = ["Points: give rupees per point (or 0 for off) and what a point is worth, up to ₹100."];
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
        shop.DeliversGoods = request.DeliversGoods ?? shop.DeliversGoods;
        shop.StaffMaxDiscountPercent = request.StaffMaxDiscountPercent ?? shop.StaffMaxDiscountPercent;
        shop.StaffMaxWriteOff = request.StaffMaxWriteOff ?? shop.StaffMaxWriteOff;
        shop.ExchangeSlipDays = request.ExchangeSlipDays ?? shop.ExchangeSlipDays;
        shop.ExchangeSlipHelperMax = request.ExchangeSlipHelperMax ?? shop.ExchangeSlipHelperMax;
        shop.CashReceiptLimit = request.CashReceiptLimit ?? shop.CashReceiptLimit;
        shop.PanFrom = request.PanFrom ?? shop.PanFrom;
        shop.CashPayoutLimit = request.CashPayoutLimit ?? shop.CashPayoutLimit;
        if (request.GirviState is not null) shop.GirviState = request.GirviState.Length == 0 ? null : request.GirviState;
        if (request.GirviLicence is not null) shop.GirviLicence = string.IsNullOrWhiteSpace(request.GirviLicence) ? null : request.GirviLicence.Trim();
        if (request.GirviMaxYearlyRate is not null) shop.GirviMaxYearlyRate = request.GirviMaxYearlyRate == 0 ? null : request.GirviMaxYearlyRate;
        shop.GirviNoticeDays = request.GirviNoticeDays ?? shop.GirviNoticeDays;
        shop.ContractorDiscountPercent = request.ContractorDiscountPercent is { } cp ? decimal.Round(Math.Clamp(cp, 0, 50), 2) : shop.ContractorDiscountPercent;
        shop.DealerDiscountPercent = request.DealerDiscountPercent is { } dp ? decimal.Round(Math.Clamp(dp, 0, 50), 2) : shop.DealerDiscountPercent;
        // New bills take the prefix; bills already made keep theirs.
        if (prefix is not null) shop.BillPrefix = prefix.Length == 0 ? null : prefix;
        if (fieldLabels is not null) shop.BillFields = Ledger.BillFields.LabelsJson(fieldLabels);
        if (scale is not null) shop.ScaleLabel = scale.Length == 0 ? null : scale;
        if (request.LoyaltyEarnPer is { } earnPer) shop.LoyaltyEarnPer = earnPer == 0 ? null : earnPer;
        if (request.LoyaltyPointValue is { } pointValue) shop.LoyaltyPointValue = pointValue;
        if (request.ExtraTypes is { } extra)
        {
            shop.ExtraTypes = extra.Where(x => x != shop.Type).Distinct().Select(ShopTypes.Name).ToArray();
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new ShopSettingsDto(shop.Name, shop.Slug, shop.Area, shop.Address, shop.UpiId, shop.ReminderTemplate, shop.Type, shop.Gstin, shop.GstComposition, ShopTypes.Parse(shop.ExtraTypes), shop.StaffMaxDiscountPercent, shop.DeliversGoods, shop.StaffMaxWriteOff, shop.ExchangeSlipDays, shop.ExchangeSlipHelperMax, shop.BillPrefix, Ledger.BillFields.Labels(shop.BillFields), shop.CashReceiptLimit, shop.PanFrom, shop.CashPayoutLimit, shop.GirviState, shop.GirviLicence, shop.GirviMaxYearlyRate, shop.GirviNoticeDays, shop.ContractorDiscountPercent, shop.DealerDiscountPercent, shop.ScaleLabel, shop.LoyaltyEarnPer, shop.LoyaltyPointValue));
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [GeneratedRegex(@"^[a-z0-9.\-_]{2,256}@[a-z][a-z0-9]{1,63}$")]
    private static partial Regex UpiPattern();

    [GeneratedRegex(@"^2[0-9x]*P{4,6}(W{4,6}|R{4,6})C$")]
    private static partial Regex ScaleLabelPattern();

}
