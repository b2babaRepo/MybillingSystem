using System.Globalization;
using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// The evening count of the cash box (galla), like the last line of the rokad bahi: what was in it at opening, what the
/// day's cash in and out says should be there, what was really counted, and the difference (short or extra).
/// One per shop day; counting again replaces it. The next day opens with what was counted.
/// </summary>
public sealed class CashCount : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public DateOnly Day { get; set; }

    public decimal Opening { get; set; }

    public decimal Expected { get; set; }

    public decimal Counted { get; set; }

    public decimal Difference { get; set; }

    public string? Note { get; set; }

    public Guid CountedByUserId { get; set; }

    public DateTimeOffset CountedAt { get; set; }
}

/// <summary>
/// The day's drawer sum. <see cref="Opening"/> comes from the previous count unless the shop typed it
/// (<see cref="OpeningFromCount"/> false when no earlier count exists, so the screen asks for it).
/// </summary>
public sealed record CashCountDto(
    DateOnly Date, decimal Opening, bool OpeningFromCount, decimal CashIn, decimal CashOut, decimal Expected,
    decimal? Counted, decimal? Difference, string? Note, string? CountedBy, DateTimeOffset? CountedAt);

public sealed record CashCountRequest(string? Date, decimal? Opening, decimal Counted, string? Note);

public static class CashCountEndpoints
{
    public static IEndpointRouteBuilder MapCashCounts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cash-count").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", GetAsync);
        group.MapPost("", SaveAsync);
        return app;
    }

    private static async Task<IResult> GetAsync(string? date, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        ParseDay(date, clock) is { } day ? TypedResults.Ok(await SummaryAsync(db, day, null, ct)) : BadDate();

    private static async Task<IResult> SaveAsync(CashCountRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ParseDay(request.Date, clock) is not { } day)
        {
            return BadDate();
        }

        if (day > IndiaTime.Today(clock))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Date"] = ["You can't count a day that hasn't come yet."] });
        }

        if (request.Counted < 0 || request.Counted > Money.MaxAmount || request.Opening is < 0 || request.Note?.Length > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Counted"] = ["Enter the cash you counted."] });
        }

        var summary = await SummaryAsync(db, day, request.Opening, ct);
        var count = await db.CashCounts.SingleOrDefaultAsync(c => c.Day == day, ct);
        if (count is null)
        {
            count = new CashCount { Day = day };
            db.CashCounts.Add(count);
        }

        count.Opening = summary.Opening;
        count.Expected = summary.Expected;
        count.Counted = request.Counted;
        count.Difference = request.Counted - summary.Expected;
        count.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        count.CountedByUserId = user.UserId()!.Value;
        count.CountedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await SummaryAsync(db, day, null, ct));
    }

    /// <summary>
    /// The day's sum. Opening: the typed one, else this day's saved count's, else the last earlier count's counted
    /// cash, else 0 (and the screen asks).
    /// </summary>
    private static async Task<CashCountDto> SummaryAsync(AppDbContext db, DateOnly day, decimal? typedOpening, CancellationToken ct)
    {
        var saved = await (from c in db.CashCounts.AsNoTracking()
                           where c.Day == day
                           join u in db.Users.AsNoTracking() on c.CountedByUserId equals u.Id into users
                           from u in users.DefaultIfEmpty()
                           select new { c, Name = u == null ? null : u.FullName })
            .SingleOrDefaultAsync(ct);
        var previous = await db.CashCounts.AsNoTracking().Where(c => c.Day < day).OrderByDescending(c => c.Day)
            .Select(c => (decimal?)c.Counted).FirstOrDefaultAsync(ct);
        var opening = typedOpening ?? saved?.c.Opening ?? previous ?? 0m;
        var book = await CashBookEndpoints.DayAsync(db, day, ct);
        var expected = opening + book.Cash - book.SpentCash;
        return new CashCountDto(
            day, opening, typedOpening is null && (saved is not null || previous is not null), book.Cash, book.SpentCash, expected,
            saved?.c.Counted, saved is null ? null : saved.c.Counted - expected, saved?.c.Note, saved?.Name, saved?.c.CountedAt);
    }

    private static DateOnly? ParseDay(string? date, TimeProvider clock) =>
        string.IsNullOrEmpty(date) ? IndiaTime.Today(clock)
        : DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static IResult BadDate() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use a date like 2026-10-27."] });
}
