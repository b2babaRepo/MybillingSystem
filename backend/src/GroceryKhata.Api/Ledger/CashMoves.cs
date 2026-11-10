using System.Globalization;
using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

public enum CashMoveKind
{
    /// <summary>Cash put into the drawer that is not a sale or a payment (the owner's own money, change brought in).</summary>
    Added,

    /// <summary>Cash taken out that is not kharcha (kept aside, a shortfall found).</summary>
    Removed,

    /// <summary>Cash taken from the drawer and paid into the bank: out of the cash book, into the bank book.</summary>
    ToBank,

    /// <summary>Cash drawn from the bank into the drawer.</summary>
    FromBank,
}

/// <summary>Cash moved in or out of the drawer by hand, or between the drawer and a bank account. Struck, never deleted.</summary>
public sealed class CashMove : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public CashMoveKind Kind { get; set; }

    /// <summary>Always above zero; the kind says which way it went.</summary>
    public decimal Amount { get; set; }

    public string? Note { get; set; }

    /// <summary>The bank account for a move to or from the bank (see <see cref="BankSetup"/>); null: the one cash deposits use.</summary>
    public string? BankAccount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset? StruckAt { get; set; }

    public Guid? StruckByUserId { get; set; }

    /// <summary>What it did to the drawer: + in, − out.</summary>
    public decimal CashEffect => Kind is CashMoveKind.Added or CashMoveKind.FromBank ? Amount : -Amount;
}

public sealed record CashMoveRequest(Guid Id, CashMoveKind? Kind, decimal Amount, string? Note, string? BankAccount = null, DateTimeOffset? OccurredAt = null);

public sealed record CashMoveDto(Guid Id, CashMoveKind Kind, decimal Amount, string? Note, string? BankAccount, DateTimeOffset OccurredAt, DateTimeOffset? StruckAt)
{
    public static CashMoveDto From(CashMove m) => new(m.Id, m.Kind, m.Amount, m.Note, m.BankAccount, m.OccurredAt, m.StruckAt);
}

/// <summary>One line of the cash ledger, with cash in hand after it.</summary>
public sealed record CashLedgerLine(DateTimeOffset At, string Source, string Who, decimal Amount, decimal Balance, Guid? MoveId = null, Guid? BillId = null);

/// <summary>One day's close in the cash ledger: what the drawer should have held, and what was counted, if it was.</summary>
public sealed record CashLedgerDay(DateOnly Date, decimal In, decimal Out, decimal Closing, decimal? Counted, decimal? Difference);

/// <summary>
/// Cash in hand over a stretch of days: the opening (the last galla count before it, carried forward), every line in and
/// out with the balance after it, and each day's close. A day the drawer was counted closes at the counted cash, and the
/// difference shows as its own line, so the ledger always agrees with the drawer.
/// </summary>
public sealed record CashLedgerDto(DateOnly From, DateOnly To, decimal Opening, DateOnly? OpeningCountedOn, decimal In, decimal Out, decimal Closing,
    IReadOnlyList<CashLedgerLine> Lines, IReadOnlyList<CashLedgerDay> Days);

public static class CashMoves
{
    public static IEndpointRouteBuilder MapCashMoves(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Khata").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/cash-moves", AddAsync);
        group.MapPost("/cash-moves/{id:guid}/strike", StrikeAsync);
        group.MapGet("/cash-ledger", LedgerAsync);
        return app;
    }

    private static async Task<IResult> AddAsync(CashMoveRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.CashMoves.AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.Id, ct) is { } replay)
        {
            return TypedResults.Ok(CashMoveDto.From(replay));
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["id"] = ["Send an id."];
        if (request.Kind is null) errors["kind"] = ["Say whether cash was added, removed, put in the bank or drawn from it."];
        if (!Money.IsValidAmount(request.Amount)) errors["amount"] = ["Enter an amount above ₹0 with at most two decimals."];
        if (request.Note?.Trim().Length > 120) errors["note"] = ["Keep the note under 120 characters."];
        if (request.Kind is CashMoveKind.Added or CashMoveKind.Removed && string.IsNullOrWhiteSpace(request.Note))
        {
            errors["note"] = ["Say why, so the cash book explains itself later."];
        }

        var now = clock.GetUtcNow();
        if (request.OccurredAt is { } at && (at > now.AddMinutes(5) || at < now.AddDays(-31)))
        {
            errors["occurredAt"] = ["Pick a time in the last month."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var move = new CashMove
        {
            Id = request.Id, Kind = request.Kind!.Value, Amount = request.Amount, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            BankAccount = request.Kind is CashMoveKind.ToBank or CashMoveKind.FromBank && !string.IsNullOrWhiteSpace(request.BankAccount) ? request.BankAccount.Trim() : null,
            OccurredAt = request.OccurredAt ?? now, CreatedByUserId = user.UserId()!.Value,
        };
        db.CashMoves.Add(move);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/cash-moves/{move.Id}", CashMoveDto.From(move));
    }

    private static async Task<IResult> StrikeAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var move = await db.CashMoves.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (move is null)
        {
            return TypedResults.NotFound();
        }

        if (move.StruckAt is null)
        {
            move.StruckAt = clock.GetUtcNow();
            move.StruckByUserId = user.UserId();
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(CashMoveDto.From(move));
    }

    private static async Task<IResult> LedgerAsync(string? from, string? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        DateOnly start = new(today.Year, today.Month, 1), end = today;
        if ((from is not null && !DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
            || (to is not null && !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["from"] = ["Use dates like 2026-11-01."] });
        }

        if (end > today) end = today;
        if (end < start || end.DayNumber - start.DayNumber > 62)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["to"] = ["Pick up to two months, ending on or after the start."] });
        }

        var counts = await db.CashCounts.AsNoTracking().Where(c => c.Day <= end).Select(c => new { c.Day, c.Counted }).ToListAsync(ct);
        var countOn = counts.ToDictionary(c => c.Day, c => c.Counted);
        // Opening: the last count before the period, then each day's cash between it and the period (at most a year back).
        var anchor = counts.Where(c => c.Day < start).OrderByDescending(c => c.Day).FirstOrDefault();
        var opening = anchor?.Counted ?? 0m;
        var walkFrom = anchor is null ? start : anchor.Day.AddDays(1);
        var firstSale = await db.Bills.AsNoTracking().OrderBy(b => b.OccurredAt).Select(b => (DateTimeOffset?)b.OccurredAt).FirstOrDefaultAsync(ct);
        // No count yet: walk from the first sale (at most a year), when that is before the period.
        if (anchor is null && firstSale is { } first && DateOnly.FromDateTime(first.ToOffset(IndiaTime.Offset).DateTime) is var firstDay && firstDay < start)
        {
            walkFrom = firstDay < start.AddDays(-366) ? start.AddDays(-366) : firstDay;
        }

        for (var d = walkFrom; d < start; d = d.AddDays(1))
        {
            var book = await CashBookEndpoints.DayAsync(db, d, ct);
            opening = countOn.TryGetValue(d, out var c) ? c : opening + book.NetCash;
        }

        var running = opening;
        var lines = new List<CashLedgerLine>();
        var days = new List<CashLedgerDay>();
        decimal moneyIn = 0, moneyOut = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var book = await CashBookEndpoints.DayAsync(db, d, ct);
            decimal dayIn = 0, dayOut = 0;
            foreach (var l in book.Lines.Where(l => l.Mode == PaymentMode.Cash && l.Amount != 0))
            {
                running += l.Amount;
                if (l.Amount > 0) dayIn += l.Amount; else dayOut -= l.Amount;
                lines.Add(new CashLedgerLine(l.OccurredAt, ToSnake(l.Source.ToString()), l.Who, l.Amount, running, l.MoveId, l.BillId));
            }

            decimal? counted = countOn.TryGetValue(d, out var c) ? c : null;
            decimal? difference = counted is null ? null : counted.Value - running;
            if (difference is { } diff && diff != 0)
            {
                running = counted!.Value;
                if (diff > 0) dayIn += diff; else dayOut -= diff;
                lines.Add(new CashLedgerLine(IndiaTime.Day(d).End.AddSeconds(-1), "count_difference", "", diff, running));
            }

            moneyIn += dayIn;
            moneyOut += dayOut;
            days.Add(new CashLedgerDay(d, dayIn, dayOut, running, counted, difference));
        }

        return TypedResults.Ok(new CashLedgerDto(start, end, opening, anchor?.Day, moneyIn, moneyOut, running, lines, days));
    }

    private static string ToSnake(string name) =>
        string.Concat(name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));
}
