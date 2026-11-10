using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// One reading of the shop's electricity meter, written off the meter's dial. A small log on its own, apart from the
/// kharcha: a reading can be taken before the bill comes, and a bill can be paid in a later month. It never works out
/// rupees, since a bill has fixed charges, slabs and duty that units times a rate would get wrong.
/// </summary>
public sealed class MeterReading : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public DateOnly Day { get; set; }

    public decimal Reading { get; set; }

    /// <summary>The bill's multiplying factor: units are the dial's difference times this. 1 for most shops.</summary>
    public decimal Factor { get; set; } = 1;

    /// <summary>Why it is lower than the last one: a new meter, or the dial went round. Null for an ordinary reading.</summary>
    public MeterReset? Reset { get; set; }

    public string? Note { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public enum MeterReset
{
    /// <summary>A new meter: units start again from this reading.</summary>
    NewMeter,
    /// <summary>The dial passed its last number and started from zero.</summary>
    WentRound,
}

/// <summary>A reading with the units since the one before, over how many days. Units are null for the first reading or after a new meter.</summary>
public sealed record MeterReadingDto(Guid Id, DateOnly Day, decimal Reading, decimal Factor, MeterReset? Reset, string? Note, decimal? Units, int? Days, decimal? PerDay, string? By);

public sealed record MeterReadingRequest(Guid Id, DateOnly Day, decimal Reading, decimal? Factor, MeterReset? Reset, string? Note);

public static class MeterEndpoints
{
    public static IEndpointRouteBuilder MapMeterReadings(this IEndpointRouteBuilder app)
    {
        // Owners and helpers both read the meter; nothing here is money.
        var group = app.MapGroup("/api/v1/meter").WithTags("Expenses").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        return app;
    }

    /// <summary>The last 24 readings, newest first, each with the units since the reading before it.</summary>
    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct) => TypedResults.Ok(await HistoryAsync(db, ct));

    internal static async Task<List<MeterReadingDto>> HistoryAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await (from r in db.MeterReadings.AsNoTracking()
                          join u in db.Users.AsNoTracking() on r.CreatedByUserId equals u.Id into users
                          from u in users.DefaultIfEmpty()
                          orderby r.Day descending, r.CreatedAt descending
                          select new { r, By = u == null ? null : u.FullName }).Take(25).ToListAsync(ct);
        var result = new List<MeterReadingDto>();
        for (var i = 0; i < Math.Min(rows.Count, 24); i++)
        {
            var (r, by) = (rows[i].r, rows[i].By);
            var before = i + 1 < rows.Count ? rows[i + 1].r : null;
            decimal? units = null;
            int? days = null;
            if (before is not null && r.Reset != MeterReset.NewMeter)
            {
                // A dial that went round: what was left to its end isn't known, so only the part from zero is counted.
                var dial = r.Reset == MeterReset.WentRound ? r.Reading : r.Reading - before.Reading;
                units = dial < 0 ? null : decimal.Round(dial * r.Factor, 2);
                days = r.Day.DayNumber - before.Day.DayNumber;
            }

            result.Add(new MeterReadingDto(r.Id, r.Day, r.Reading, r.Factor, r.Reset, r.Note, units, days,
                units is { } u && days is > 0 ? decimal.Round(u / days.Value, 1) : null, by));
        }

        return result;
    }

    /// <summary>Saves a reading. Safe to send twice (the phone's ID). Lower than the last one is allowed, with the reason.</summary>
    private static async Task<IResult> AddAsync(MeterReadingRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.MeterReadings.AsNoTracking().AnyAsync(r => r.Id == request.Id, ct))
        {
            return TypedResults.Ok(await HistoryAsync(db, ct));
        }

        var today = IndiaTime.Today(clock);
        if (request.Id == Guid.Empty)
        {
            return Problems.Invalid("id", "Send a new id for the reading.");
        }

        if (request.Reading < 0 || request.Reading > 99_999_999)
        {
            return Problems.Invalid("reading", "Write the number on the meter.");
        }

        if (request.Day > today || request.Day < today.AddYears(-1))
        {
            return Problems.Invalid("day", "The reading's day can't be in the future or more than a year back.");
        }

        if (request.Factor is { } f && (f <= 0 || f > 1000))
        {
            return Problems.Invalid("factor", "The multiplying factor is on the bill; it is 1 for most shops.");
        }

        var last = await db.MeterReadings.AsNoTracking().Where(r => r.Day <= request.Day).OrderByDescending(r => r.Day).ThenByDescending(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        if (last is not null && request.Reading < last.Reading && request.Reset is null)
        {
            return Problems.Invalid("reset", $"This is lower than the last reading ({last.Reading:0.##}). Fix the number, or say if it is a new meter or the dial went round.");
        }

        // A reading for an earlier day can't be above one taken later (unless that later one started a new meter).
        var after = await db.MeterReadings.AsNoTracking().Where(r => r.Day > request.Day).OrderBy(r => r.Day).ThenBy(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        if (after is { Reset: null } && request.Reading > after.Reading)
        {
            return Problems.Invalid("reading", $"This is higher than the reading on {after.Day:d MMM} ({after.Reading:0.##}). Check the number or the day.");
        }

        db.MeterReadings.Add(new MeterReading
        {
            Id = request.Id, Day = request.Day, Reading = request.Reading, Factor = request.Factor ?? last?.Factor ?? 1, Reset = request.Reset,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()[..Math.Min(request.Note.Trim().Length, 200)],
            CreatedByUserId = user.UserId()!.Value,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await HistoryAsync(db, ct));
    }
}
