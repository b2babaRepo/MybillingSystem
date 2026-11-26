using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A cashier's turn at one counter: the cash put in the drawer to start (the float), the bills made on it, and the
/// count at the end. The cashier counts without being told what should be there (a blind count); the server then works
/// out what was expected and how much the drawer is over or short. One open shift per counter and per cashier.
/// </summary>
public sealed class CashierShift : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The counter's name as set on the device ("Counter 2").</summary>
    public required string Counter { get; set; }

    public Guid CashierUserId { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public decimal OpeningFloat { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public Guid? ClosedByUserId { get; set; }

    /// <summary>At close: the cash the shift's bills took (bills cancelled by then left out), less cash refunds for returns.</summary>
    public decimal? CashSales { get; set; }

    /// <summary>At close: float + cash sales. Kept as it was, so a later change can't move a closed shift's result.</summary>
    public decimal? Expected { get; set; }

    public decimal? Counted { get; set; }

    /// <summary>Counted − expected: below 0 short, above 0 over.</summary>
    public decimal? Difference { get; set; }

    public string? Note { get; set; }
}

public sealed record OpenCashierShiftRequest(Guid Id, string? Counter, decimal OpeningFloat);

public sealed record CloseCashierShiftRequest(decimal Counted, string? Note);

/// <summary>
/// A shift as the screen shows it. Expected and the bills' cash stay hidden from a cashier until the shift is closed
/// (the blind count); the owner sees them all along.
/// </summary>
public sealed record CashierShiftDto(
    Guid Id, string Counter, Guid CashierUserId, string? Cashier, DateTimeOffset OpenedAt, decimal OpeningFloat,
    DateTimeOffset? ClosedAt, int Bills, decimal Sales, decimal? CashSales, decimal? Expected, decimal? Counted, decimal? Difference, string? Note);

public static class CashierShiftEndpoints
{
    public static IEndpointRouteBuilder MapCashierShifts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shifts").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapGet("/mine", MineAsync);
        group.MapPost("", OpenAsync);
        group.MapPost("/{id:guid}/close", CloseAsync);
        return app;
    }

    private static async Task<IResult> OpenAsync(OpenCashierShiftRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var counter = request.Counter?.Trim();
        if (string.IsNullOrEmpty(counter) || counter.Length > 30)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Counter"] = ["Name this counter first (like Counter 1)."] });
        }

        if (request.OpeningFloat != 0 && !Money.IsValidAmount(request.OpeningFloat))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["OpeningFloat"] = ["Enter the cash put in the drawer to start."] });
        }

        var userId = user.UserId()!.Value;
        // Sent again (a retry): the same shift.
        if (await db.CashierShifts.AsNoTracking().SingleOrDefaultAsync(s => s.Id == request.Id, ct) is { } same)
        {
            return same.CashierUserId == userId ? TypedResults.Ok(await DtoAsync(db, same, LedgerEndpoints.IsOwner(user), ct)) : TypedResults.Conflict();
        }

        if (await db.CashierShifts.AnyAsync(s => s.ClosedAt == null && (s.Counter == counter || s.CashierUserId == userId), ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A shift is already open on this counter or for you. Close it first.");
        }

        var shift = new CashierShift { Id = request.Id, Counter = counter, CashierUserId = userId, OpenedAt = clock.GetUtcNow(), OpeningFloat = request.OpeningFloat };
        db.CashierShifts.Add(shift);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A shift is already open on this counter or for you. Close it first.");
        }

        return TypedResults.Ok(await DtoAsync(db, shift, LedgerEndpoints.IsOwner(user), ct));
    }

    private static async Task<IResult> CloseAsync(Guid id, CloseCashierShiftRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var shift = await db.CashierShifts.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (shift is null)
        {
            return TypedResults.NotFound();
        }

        var userId = user.UserId()!.Value;
        var owner = LedgerEndpoints.IsOwner(user);
        if (shift.CashierUserId != userId && !owner)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the cashier or the owner can close this shift.");
        }

        if (shift.ClosedAt is not null)
        {
            return TypedResults.Ok(await DtoAsync(db, shift, true, ct));
        }

        if ((request.Counted != 0 && !Money.IsValidAmount(request.Counted)) || request.Note?.Length > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Counted"] = ["Enter the cash you counted."] });
        }

        var (_, _, cash) = await TotalsAsync(db, shift.Id, ct);
        shift.ClosedAt = clock.GetUtcNow();
        shift.ClosedByUserId = userId;
        shift.CashSales = cash;
        shift.Expected = shift.OpeningFloat + cash;
        shift.Counted = request.Counted;
        shift.Difference = request.Counted - shift.Expected;
        shift.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DtoAsync(db, shift, true, ct));
    }

    /// <summary>This cashier's open shift, if any (to carry on billing on it after a reload or on another device).</summary>
    private static async Task<IResult> MineAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var shift = await db.CashierShifts.AsNoTracking().SingleOrDefaultAsync(s => s.CashierUserId == userId && s.ClosedAt == null, ct);
        return shift is null ? TypedResults.NoContent() : TypedResults.Ok(await DtoAsync(db, shift, LedgerEndpoints.IsOwner(user), ct));
    }

    /// <summary>Shifts opened on a day (India time), newest first: every cashier's for the owner, a helper's own otherwise.</summary>
    private static async Task<IResult> ListAsync(string? date, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var day = IndiaTime.Today(clock);
        if (!string.IsNullOrEmpty(date) && !DateOnly.TryParseExact(date, "yyyy-MM-dd", out day))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use a date like 2026-10-27."] });
        }

        var (from, to) = IndiaTime.Day(day);
        var owner = LedgerEndpoints.IsOwner(user);
        var userId = user.UserId()!.Value;
        var shifts = await db.CashierShifts.AsNoTracking()
            .Where(s => s.OpenedAt >= from && s.OpenedAt < to && (owner || s.CashierUserId == userId))
            .OrderByDescending(s => s.OpenedAt).ToListAsync(ct);
        var list = new List<CashierShiftDto>();
        foreach (var s in shifts)
        {
            list.Add(await DtoAsync(db, s, owner, ct));
        }

        return TypedResults.Ok(list);
    }

    /// <summary>The shift's bills (cancelled ones left out): how many, their total, and the cash they took.</summary>
    private static async Task<(int Bills, decimal Sales, decimal Cash)> TotalsAsync(AppDbContext db, Guid shiftId, CancellationToken ct)
    {
        var bills = await db.Bills.AsNoTracking().Where(b => b.ShiftId == shiftId && b.CancelledAt == null)
            .Select(b => new { b.Total, b.PaymentMode, b.PaidAmount, b.Payments }).ToListAsync(ct);
        var cash = bills.Sum(b => BillPayments.Parts(b.PaymentMode, b.PaidAmount, b.Payments).Where(p => p.Mode == PaymentMode.Cash).Sum(p => p.Amount));
        // Cash handed back for goods returned during the shift came out of the same drawer.
        var refunds = await db.CreditNotes.AsNoTracking().Where(n => n.ShiftId == shiftId && n.RefundMode == PaymentMode.Cash).SumAsync(n => n.Total, ct);
        return (bills.Count, bills.Sum(b => b.Total), cash - refunds);
    }

    private static async Task<CashierShiftDto> DtoAsync(AppDbContext db, CashierShift s, bool seesExpected, CancellationToken ct)
    {
        var (bills, sales, cash) = await TotalsAsync(db, s.Id, ct);
        var name = await db.Users.AsNoTracking().Where(u => u.Id == s.CashierUserId).Select(u => u.FullName).SingleOrDefaultAsync(ct);
        var closed = s.ClosedAt is not null;
        // Open: the cashier sees bills and sales but not the cash expected in the drawer; closed: everyone sees the result.
        var show = closed || seesExpected;
        return new CashierShiftDto(s.Id, s.Counter, s.CashierUserId, name, s.OpenedAt, s.OpeningFloat, s.ClosedAt, bills, sales,
            show ? (closed ? s.CashSales : cash) : null, show ? (closed ? s.Expected : s.OpeningFloat + cash) : null, s.Counted, s.Difference, s.Note);
    }
}
