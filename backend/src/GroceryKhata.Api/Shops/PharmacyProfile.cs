using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>
/// A medical shop's licences and pharmacist. Retail drug licences (Form 20 and 21) no longer expire, but a retention fee
/// is due every five years from issue (Rule 63, from 2017); the app reminds the shop 60 days before. Shown on bills later.
/// </summary>
public sealed class PharmacyProfile : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>Form 20: allopathic drugs other than those in Schedules C, C1 and X.</summary>
    public string? LicenceForm20 { get; set; }

    /// <summary>Form 21: drugs in Schedules C and C1.</summary>
    public string? LicenceForm21 { get; set; }

    public DateOnly? LicenceIssuedOn { get; set; }

    public string? PharmacistName { get; set; }

    /// <summary>The pharmacist's registration number with the State Pharmacy Council.</summary>
    public string? PharmacistRegNo { get; set; }

    public string? Gstin { get; set; }

    public string? FssaiNo { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record PharmacyProfileRequest(
    string? LicenceForm20, string? LicenceForm21, DateOnly? LicenceIssuedOn, string? PharmacistName, string? PharmacistRegNo, string? Gstin, string? FssaiNo);

/// <summary><see cref="RetentionDueOn"/>: the next five-year retention fee date; <see cref="RetentionSoon"/> when it is 60 days or less away (or past).</summary>
public sealed record PharmacyProfileDto(
    string? LicenceForm20, string? LicenceForm21, DateOnly? LicenceIssuedOn, string? PharmacistName, string? PharmacistRegNo, string? Gstin, string? FssaiNo,
    DateOnly? RetentionDueOn, bool RetentionSoon, bool Complete);

public static class PharmacyProfiles
{
    public static IEndpointRouteBuilder MapPharmacyProfile(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pharmacy/profile").WithTags("Shops").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", GetAsync);
        group.MapPut("", SetAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();

    /// <summary>The first five-year mark from issue that is still ahead (today counts), or null without an issue date.</summary>
    public static DateOnly? RetentionDue(DateOnly? issuedOn, DateOnly today)
    {
        if (issuedOn is not { } issued)
        {
            return null;
        }

        var due = issued.AddYears(5);
        while (due < today)
        {
            due = due.AddYears(5);
        }

        return due;
    }

    private static async Task<PharmacyProfileDto> GetAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var p = await db.PharmacyProfiles.AsNoTracking().SingleOrDefaultAsync(ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        var due = RetentionDue(p?.LicenceIssuedOn, today);
        return new PharmacyProfileDto(p?.LicenceForm20, p?.LicenceForm21, p?.LicenceIssuedOn, p?.PharmacistName, p?.PharmacistRegNo, p?.Gstin, p?.FssaiNo,
            due, due is { } d && d <= today.AddDays(60), p is { LicenceForm20: not null, PharmacistName: not null, PharmacistRegNo: not null });
    }

    private static async Task<IResult> SetAsync(PharmacyProfileRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        var errors = new Dictionary<string, string[]>();
        if (new[] { request.LicenceForm20, request.LicenceForm21, request.PharmacistRegNo, request.FssaiNo }.Any(s => s?.Trim().Length > 40)) errors["LicenceForm20"] = ["Licence and registration numbers are at most 40 characters."];
        if (request.PharmacistName?.Trim().Length > 80) errors["PharmacistName"] = ["Keep the name under 80 characters."];
        if (Clean(request.Gstin) is { } gstin && !System.Text.RegularExpressions.Regex.IsMatch(gstin, "^[0-9]{2}[A-Z0-9]{13}$")) errors["Gstin"] = ["A GSTIN is 15 letters and digits, starting with the state code."];
        if (request.LicenceIssuedOn > today) errors["LicenceIssuedOn"] = ["The issue date can't be in the future."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var profile = await db.PharmacyProfiles.SingleOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = new PharmacyProfile();
            db.PharmacyProfiles.Add(profile);
        }

        (profile.LicenceForm20, profile.LicenceForm21, profile.LicenceIssuedOn, profile.PharmacistRegNo, profile.Gstin, profile.FssaiNo, profile.UpdatedAt) =
            (Clean(request.LicenceForm20), Clean(request.LicenceForm21), request.LicenceIssuedOn, Clean(request.PharmacistRegNo), Clean(request.Gstin), Clean(request.FssaiNo), clock.GetUtcNow());
        profile.PharmacistName = string.IsNullOrWhiteSpace(request.PharmacistName) ? null : request.PharmacistName.Trim();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await GetAsync(db, clock, ct));
    }
}
