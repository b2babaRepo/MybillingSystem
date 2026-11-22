using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>A room the lodge lets out: "101", "Deluxe 2". Its rate is the usual per-night price.</summary>
public sealed class Room : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public string? Kind { get; set; }

    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>The booking site's calendar link (iCal) for this room: its bookings are brought in from it.</summary>
    public string? IcalImportUrl { get; set; }

    /// <summary>The secret in this room's own calendar link, which booking sites read to block the nights booked here.</summary>
    public string? IcalToken { get; set; }
}

/// <summary>
/// A season or date price: from one date to another (both nights included) a room, or every room, goes for this rate a
/// night (Diwali, wedding season, a festival weekend). A room's own period wins over one for all rooms.
/// </summary>
public sealed class RatePeriod : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public Guid? RoomId { get; set; }

    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A guest's stay in a room from check-in date to check-out date (the nights in between). No two live stays share a
/// room on the same night. Extras (food, extra bed, laundry) run on the stay and the room bill is made at checkout;
/// an advance is a payment on the guest's khata, so the bill takes it off by itself.
/// </summary>
public sealed class Stay : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid RoomId { get; set; }

    public DateOnly CheckIn { get; set; }

    /// <summary>The morning the guest leaves: not a night stayed.</summary>
    public DateOnly CheckOut { get; set; }

    public StayStatus Status { get; set; }

    public Guid? CustomerId { get; set; }

    public required string GuestName { get; set; }

    public string? Phone { get; set; }

    public int Guests { get; set; }

    /// <summary>The ID seen at check-in ("Aadhaar", "Passport"); only the last four characters are kept.</summary>
    public string? IdType { get; set; }

    public string? IdLast4 { get; set; }

    /// <summary>A foreign national: Form C must be filed on the FRRO portal within 24 hours of arrival.</summary>
    public bool Foreigner { get; set; }

    public DateTimeOffset? FormCFiledAt { get; set; }

    public decimal Rate { get; set; }

    /// <summary>All the nights together, each at its own rate (season prices), fixed when booked.</summary>
    public decimal RoomCharge { get; set; }

    /// <summary>The hall function these rooms are for (the baraat's rooms at a wedding): billed with the function.</summary>
    public Guid? BookingId { get; set; }

    public StaySource Source { get; set; }

    /// <summary>The booking site's reference, so its payout can be matched.</summary>
    public string? SourceRef { get; set; }

    /// <summary>The booking site's commission on this stay: a cost, recorded in kharcha at checkout.</summary>
    public decimal Commission { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }

    public Guid? BillId { get; set; }

    public string? Note { get; set; }

    public List<StayCharge> Charges { get; set; } = [];
}

/// <summary>An extra on a stay: food sent up, an extra bed, laundry.</summary>
public sealed class StayCharge : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid StayId { get; set; }

    public required string Name { get; set; }

    public decimal Amount { get; set; }
}

public enum StayStatus
{
    Booked,
    CheckedIn,
    CheckedOut,
    Cancelled,
}

public enum StaySource
{
    Direct,
    WalkIn,
    MakeMyTrip,
    Goibibo,
    BookingCom,
    Oyo,
    Airbnb,
    Other,
}

public sealed record RoomRequest(Guid Id, string? Name, string? Kind, decimal Rate);

public sealed record RoomDto(Guid Id, string Name, string? Kind, decimal Rate, string? IcalImportUrl = null, string? IcalExportPath = null);

public sealed record RatePeriodRequest(Guid Id, string? Name, DateOnly From, DateOnly To, Guid? RoomId, decimal Rate);

public sealed record RatePeriodDto(Guid Id, string Name, DateOnly From, DateOnly To, Guid? RoomId, decimal Rate);

public sealed record QuoteDto(int Nights, decimal Total, IReadOnlyList<decimal> NightRates);

public sealed record IcalRequest(string? ImportUrl);

public sealed record IcalSyncResult(int Added, int Skipped, IReadOnlyList<string> Problems);

public sealed record StayRequest(
    Guid Id, Guid RoomId, DateOnly CheckIn, DateOnly CheckOut, Guid? CustomerId, string? GuestName, string? Phone, int Guests, decimal? Rate,
    StaySource? Source, string? SourceRef, decimal Commission, bool Foreigner, string? Note, Guid? BookingId = null);

public sealed record CheckInRequest(string? IdType, string? IdNumber);

public sealed record StayChargeRequest(Guid Id, string? Name, decimal Amount);

public sealed record StayAdvanceRequest(decimal Amount, PaymentMode? Mode);

public sealed record CheckoutRequest(Guid BillId, decimal PaidAmount, PaymentMode? Mode);

public sealed record StayChargeDto(Guid Id, string Name, decimal Amount);

public sealed record StayDto(
    Guid Id, int Number, Guid RoomId, string RoomName, DateOnly CheckIn, DateOnly CheckOut, int Nights, StayStatus Status, Guid? CustomerId, string GuestName,
    string? Phone, int Guests, string? IdType, string? IdLast4, bool Foreigner, DateTimeOffset? FormCFiledAt, bool FormCDue, decimal Rate, StaySource Source,
    string? SourceRef, decimal Commission, decimal RoomTotal, IReadOnlyList<StayChargeDto> Charges, decimal Total, Guid? BillId, string? Note, Guid? BookingId = null);

/// <summary>Today at a glance: each room free, booked for today or with a guest in; and who arrives and leaves.</summary>
public sealed record RoomBoardRow(RoomDto Room, string State, StayDto? Stay);

public static class Stays
{
    public static IEndpointRouteBuilder MapStays(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Rooms").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/rooms", async (AppDbContext db, CancellationToken ct) =>
            await db.Rooms.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Name).Select(r => new RoomDto(r.Id, r.Name, r.Kind, r.Rate)).ToListAsync(ct));
        group.MapPost("/rooms", AddRoomAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/rooms/{id:guid}/quote", QuoteAsync);
        group.MapGet("/room-rates", async (AppDbContext db, CancellationToken ct) =>
            await db.RatePeriods.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.From).Select(p => new RatePeriodDto(p.Id, p.Name, p.From, p.To, p.RoomId, p.Rate)).ToListAsync(ct));
        group.MapPost("/room-rates", AddRatePeriodAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/room-rates/{id:guid}/remove", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            await db.RatePeriods.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/rooms/calendars", CalendarsAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPut("/rooms/{id:guid}/ical", SetIcalAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/rooms/{id:guid}/ical/sync", SyncIcalAsync).RequireAuthorization(AppPolicies.ShopOwner);
        app.MapGet("/api/v1/store/{slug}/rooms/{token}.ics", ExportIcalAsync).WithTags("Rooms").AllowAnonymous().RequireRateLimiting(Planner.StoreEndpoints.StoreRateLimit);
        group.MapGet("/rooms/board", BoardAsync);
        group.MapGet("/stays", ListAsync);
        group.MapGet("/stays/{id:guid}", GetAsync);
        group.MapPost("/stays", CreateAsync);
        group.MapPost("/stays/{id:guid}/check-in", CheckInAsync);
        group.MapPost("/stays/{id:guid}/form-c", FormCAsync);
        group.MapPost("/stays/{id:guid}/charges", ChargeAsync);
        group.MapPost("/stays/{id:guid}/advance", AdvanceAsync);
        group.MapPost("/stays/{id:guid}/checkout", CheckoutAsync);
        group.MapPost("/stays/{id:guid}/cancel", CancelAsync);
        return app;
    }

    /// <summary>Form C is due within 24 hours of a foreign guest's arrival.</summary>
    public static readonly TimeSpan FormCWithin = TimeSpan.FromHours(24);

    private static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);

    private static int Nights(Stay s) => s.CheckOut.DayNumber - s.CheckIn.DayNumber;

    private static StayDto ToDto(Stay s, string room)
    {
        var roomTotal = s.RoomCharge > 0 ? s.RoomCharge : Nights(s) * s.Rate;
        var charges = s.Charges.OrderBy(c => c.CreatedAt).Select(c => new StayChargeDto(c.Id, c.Name, c.Amount)).ToList();
        return new StayDto(s.Id, s.Number, s.RoomId, room, s.CheckIn, s.CheckOut, Nights(s), s.Status, s.CustomerId, s.GuestName, s.Phone, s.Guests, s.IdType, s.IdLast4,
            s.Foreigner, s.FormCFiledAt, s.Foreigner && s.Status == StayStatus.CheckedIn && s.FormCFiledAt is null, s.Rate, s.Source, s.SourceRef, s.Commission,
            roomTotal, charges, roomTotal + charges.Sum(c => c.Amount), s.BillId, s.Note, s.BookingId);
    }

    private static async Task<IReadOnlyList<StayDto>> ToDtosAsync(AppDbContext db, IQueryable<Stay> query, CancellationToken ct)
    {
        var stays = await query.AsNoTracking().Include(s => s.Charges).ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        return stays.Select(s => ToDto(s, rooms.GetValueOrDefault(s.RoomId) ?? "")).ToList();
    }

    private static async Task<IResult> AddRoomAsync(RoomRequest request, AppDbContext db, CancellationToken ct)
    {
        if (request.Name?.Trim() is not { Length: >= 1 and <= 30 })
        {
            return Invalid("Name", "Name or number the room (101, Deluxe 2).");
        }

        if (request.Rate < 0 || (request.Rate > 0 && !Money.IsValidAmount(request.Rate)))
        {
            return Invalid("Rate", "Enter the room's rate per night.");
        }

        if (await db.Rooms.AnyAsync(r => r.IsActive && r.Name.ToLower() == request.Name.Trim().ToLower() && r.Id != request.Id, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A room with this name already exists.");
        }

        if (!await db.Rooms.AnyAsync(r => r.Id == request.Id, ct))
        {
            db.Rooms.Add(new Room { Id = request.Id, Name = request.Name.Trim(), Kind = string.IsNullOrWhiteSpace(request.Kind) ? null : request.Kind.Trim(), Rate = request.Rate });
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(new RoomDto(request.Id, request.Name.Trim(), request.Kind, request.Rate));
    }

    /// <summary>Each room today: a guest in, a guest arriving, or free.</summary>
    private static async Task<IReadOnlyList<RoomBoardRow>> BoardAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = Today(clock);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(ct);
        var live = await ToDtosAsync(db, db.Stays.Where(s => (s.Status == StayStatus.CheckedIn) || (s.Status == StayStatus.Booked && s.CheckIn <= today && s.CheckOut > today)), ct);
        return rooms.Select(r =>
        {
            var room = new RoomDto(r.Id, r.Name, r.Kind, r.Rate);
            var stay = live.Where(s => s.RoomId == r.Id).OrderByDescending(s => s.Status == StayStatus.CheckedIn).FirstOrDefault();
            return new RoomBoardRow(room, stay is null ? "free" : stay.Status == StayStatus.CheckedIn ? (stay.CheckOut <= today ? "leaving" : "occupied") : "arriving", stay);
        }).ToList();
    }

    /// <summary>Stays touching the coming days (default: from today, the next 30 days), and any guest still checked in.</summary>
    private static async Task<IReadOnlyList<StayDto>> ListAsync(DateOnly? from, Guid? bookingId, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (bookingId is { } function)
        {
            return (await ToDtosAsync(db, db.Stays.Where(s => s.BookingId == function && s.Status != StayStatus.Cancelled), ct)).OrderBy(s => s.RoomName).ToList();
        }

        var start = from ?? Today(clock);
        var end = start.AddDays(30);
        return (await ToDtosAsync(db, db.Stays.Where(s => s.Status == StayStatus.CheckedIn
                || ((s.Status == StayStatus.Booked) && s.CheckOut > start && s.CheckIn < end)), ct))
            .OrderBy(s => s.CheckIn).ThenBy(s => s.RoomName).ToList();
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct) =>
        (await ToDtosAsync(db, db.Stays.Where(s => s.Id == id), ct)).SingleOrDefault() is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound();

    /// <summary>Whether a live stay already has this room for any of these nights. Run after locking the room row.</summary>
    private static async Task<string?> ClashAsync(AppDbContext db, Guid stayId, Guid roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM rooms WHERE id = {roomId} FOR UPDATE", ct);
        var clash = await db.Stays.AsNoTracking()
            .Where(s => s.Id != stayId && s.RoomId == roomId && (s.Status == StayStatus.Booked || s.Status == StayStatus.CheckedIn) && s.CheckIn < checkOut && checkIn < s.CheckOut)
            .Select(s => new { s.Number, s.GuestName }).FirstOrDefaultAsync(ct);
        return clash is null ? null : $"This room is already taken on these nights (stay #{clash.Number}, {clash.GuestName}).";
    }

    private static async Task<IResult> CreateAsync(StayRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Stays.AnyAsync(s => s.Id == request.Id, ct))
        {
            return await GetAsync(request.Id, db, ct);
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.RoomId && r.IsActive, ct);
        if (room is null) errors["RoomId"] = ["Choose the room."];
        if (request.CheckIn < Today(clock).AddDays(-1)) errors["CheckIn"] = ["Choose a check-in date from today on."];
        if (request.CheckOut <= request.CheckIn) errors["CheckOut"] = ["Check-out is after check-in."];
        if (request.CheckOut.DayNumber - request.CheckIn.DayNumber > 365) errors["CheckOut"] = ["A stay is up to a year."];
        if (request.Guests is < 1 or > 20) errors["Guests"] = ["How many guests?"];
        if (request.Rate is { } own && (own <= 0 || !Money.IsValidAmount(own))) errors["Rate"] = ["Enter the rate per night."];
        if (room is { Rate: <= 0 } && request.Rate is null) errors["Rate"] = ["Enter the rate per night."];
        if (request.BookingId is { } function && !await db.Bookings.AnyAsync(b => b.Id == function && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Settled, ct))
        {
            errors["BookingId"] = ["The function isn't open."];
        }
        if (request.Commission < 0 || (request.Commission > 0 && !Money.IsValidAmount(request.Commission))) errors["Commission"] = ["Enter the booking site's commission."];
        string? name = request.GuestName?.Trim();
        if (request.CustomerId is { } customer)
        {
            name ??= await db.Customers.AsNoTracking().Where(c => c.Id == customer).Select(c => c.Name).SingleOrDefaultAsync(ct);
            if (!await db.Customers.AnyAsync(c => c.Id == customer, ct)) errors["CustomerId"] = ["Customer not found."];
        }

        if (name is not { Length: >= 2 and <= 80 }) errors["GuestName"] = ["Whose stay is it?"];
        if (request.SourceRef?.Length > 40 || request.Note?.Length > 200) errors["Note"] = ["Keep the notes short."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await ClashAsync(db, request.Id, request.RoomId, request.CheckIn, request.CheckOut, ct) is { } clash)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: clash);
        }

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var nightRates = request.Rate is { } fixedRate ? Enumerable.Repeat(fixedRate, request.CheckOut.DayNumber - request.CheckIn.DayNumber).ToList()
            : await NightRatesAsync(db, room!, request.CheckIn, request.CheckOut, ct);
        var rate = decimal.Round(nightRates.Average(), 2);
        db.Stays.Add(new Stay
        {
            RoomCharge = nightRates.Sum(), BookingId = request.BookingId,
            Id = request.Id, Number = (await db.Stays.MaxAsync(s => (int?)s.Number, ct) ?? 0) + 1, RoomId = request.RoomId, CheckIn = request.CheckIn, CheckOut = request.CheckOut,
            CustomerId = request.CustomerId, GuestName = name!, Phone = PhoneNumber.Normalize(request.Phone) ?? Clean(request.Phone), Guests = request.Guests, Rate = rate,
            Source = request.Source ?? StaySource.Direct, SourceRef = Clean(request.SourceRef), Commission = request.Commission, Foreigner = request.Foreigner, Note = Clean(request.Note),
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(request.Id, db, ct);
    }

    /// <summary>The guest arrives: the ID seen is noted (type and last four characters only; the full number is not kept).</summary>
    private static async Task<IResult> CheckInAsync(Guid id, CheckInRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var stay = await db.Stays.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (stay is null)
        {
            return TypedResults.NotFound();
        }

        if (stay.Status == StayStatus.Booked)
        {
            var number = new string((request.IdNumber ?? "").Where(char.IsLetterOrDigit).ToArray());
            if (request.IdType?.Trim() is not { Length: >= 2 and <= 30 } || number.Length < 4)
            {
                return Invalid("IdNumber", "Note the guest's ID (type and number) at check-in.");
            }

            (stay.Status, stay.CheckedInAt, stay.IdType, stay.IdLast4) = (StayStatus.CheckedIn, clock.GetUtcNow(), request.IdType.Trim(), number[^4..].ToUpperInvariant());
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, db, ct);
    }

    /// <summary>Marks Form C as filed on the FRRO portal for a foreign guest.</summary>
    private static async Task<IResult> FormCAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var stay = await db.Stays.SingleOrDefaultAsync(s => s.Id == id && s.Foreigner, ct);
        if (stay is null)
        {
            return TypedResults.NotFound();
        }

        stay.FormCFiledAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, ct);
    }

    private static async Task<IResult> ChargeAsync(Guid id, StayChargeRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.StayCharges.AnyAsync(c => c.Id == request.Id, ct))
        {
            if (!await db.Stays.AnyAsync(s => s.Id == id && (s.Status == StayStatus.Booked || s.Status == StayStatus.CheckedIn), ct))
            {
                return TypedResults.NotFound();
            }

            if (request.Name?.Trim() is not { Length: >= 2 and <= 60 } || request.Amount <= 0 || !Money.IsValidAmount(request.Amount))
            {
                return Invalid("Name", "Say what it is and how much.");
            }

            db.StayCharges.Add(new StayCharge { Id = request.Id, StayId = id, Name = request.Name.Trim(), Amount = request.Amount });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, db, ct);
    }

    /// <summary>An advance for the room: a payment on the guest's khata, taken off the room bill at checkout.</summary>
    private static async Task<IResult> AdvanceAsync(Guid id, StayAdvanceRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, CancellationToken ct)
    {
        var stay = await db.Stays.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id && (s.Status == StayStatus.Booked || s.Status == StayStatus.CheckedIn), ct);
        if (stay is null)
        {
            return TypedResults.NotFound();
        }

        if (stay.CustomerId is not { } customer)
        {
            return Invalid("CustomerId", "Choose a khata customer on the stay to take an advance.");
        }

        var result = await ledger.AddKhataEntryAsync(
            new KhataEntryRequest(Guid.CreateVersion7(), customer, KhataEntryType.Payment, request.Amount, request.Mode, $"Advance: room stay #{stay.Number}", null),
            user.UserId()!.Value, ct, IsOwner(user));
        return result.Value is null ? result.ToHttp("") : await GetAsync(id, db, ct);
    }

    /// <summary>
    /// Checkout: the room bill (nights × rate, then each extra) for the guest; an advance on the khata comes off it, and a
    /// guest without a khata pays it all now. A booking site's commission goes in the kharcha book as a cost.
    /// </summary>
    private static async Task<IResult> CheckoutAsync(Guid id, CheckoutRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var stay = await db.Stays.Include(s => s.Charges).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (stay is null)
        {
            return TypedResults.NotFound();
        }

        if (stay.Status == StayStatus.CheckedOut)
        {
            return await GetAsync(id, db, ct);
        }

        if (stay.Status != StayStatus.CheckedIn)
        {
            return Invalid("Status", "Check the guest in before checkout.");
        }

        var room = await db.Rooms.AsNoTracking().Where(r => r.Id == stay.RoomId).Select(r => r.Name).SingleAsync(ct);
        var (lines, tax) = await BillLinesAsync(db, stay, room, ct);
        var result = await ledger.CreateBillAsync(new BillRequest(request.BillId, stay.CustomerId, lines, request.PaidAmount, request.Mode, null), user.UserId()!.Value, ct,
            isOwner: IsOwner(user), taxSnapshot: tax);
        if (result.Value is not { } bill)
        {
            return result.ToHttp("");
        }

        if (stay.Commission > 0)
        {
            db.Expenses.Add(new Expense
            {
                OccurredAt = clock.GetUtcNow(), Amount = stay.Commission, Category = ExpenseCategory.Other, Mode = PaymentMode.Upi,
                Note = $"{stay.Source} commission: stay #{stay.Number}{(stay.SourceRef is null ? "" : $" ({stay.SourceRef})")}", CreatedByUserId = user.UserId()!.Value,
            });
        }

        (stay.Status, stay.BillId) = (StayStatus.CheckedOut, bill.Id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, ct);
    }

    /// <summary>
    /// GST on a room night by what the guest pays for it, as found from 22 Sep 2025: up to ₹1,000 exempt, up to ₹7,500
    /// 5% (no input credit), above that 18%. SAC 996311. To be confirmed by the shop's CA before relying on it.
    /// </summary>
    public static decimal RoomGstRate(decimal nightRate) => nightRate <= 1000 ? 0 : nightRate <= 7500 ? 5 : 18;

    public const string RoomSac = "996311";

    /// <summary>
    /// A stay's bill lines: the room (one line, nights at an even rate or the season total), then each extra. The room
    /// line carries room GST when the shop is GST-registered; extras take none (the shop sets GST on its own items).
    /// </summary>
    internal static async Task<(List<BillLineRequest> Lines, List<(decimal? GstRate, string? HsnCode)>? Tax)> BillLinesAsync(
        AppDbContext db, Stay stay, string room, CancellationToken ct)
    {
        var nights = Nights(stay);
        var total = stay.RoomCharge > 0 ? stay.RoomCharge : nights * stay.Rate;
        var even = total == nights * stay.Rate;
        var lines = new List<BillLineRequest>
        {
            even ? new(null, $"Room {room}: {nights} night(s), {stay.CheckIn:dd MMM} to {stay.CheckOut:dd MMM}", nights, Unit.Piece, stay.Rate)
                 : new(null, $"Room {room}: {nights} night(s), {stay.CheckIn:dd MMM} to {stay.CheckOut:dd MMM} (season rates)", 1, Unit.Piece, total),
        };
        lines.AddRange(stay.Charges.OrderBy(c => c.CreatedAt).Select(c => new BillLineRequest(null, c.Name, 1, Unit.Piece, c.Amount)));
        var registered = await db.Shops.AsNoTracking().Where(s => s.Id == stay.ShopId).Select(s => s.Gstin != null).SingleAsync(ct);
        if (!registered)
        {
            return (lines, null);
        }

        var tax = lines.Select((_, i) => i == 0 ? ((decimal?)RoomGstRate(decimal.Round(total / Math.Max(nights, 1), 2)), (string?)RoomSac) : ((decimal?)null, (string?)null)).ToList();
        return (lines, tax);
    }

    /// <summary>Each night's rate: a season price for this room, else one for all rooms, else the room's own rate.</summary>
    public static async Task<List<decimal>> NightRatesAsync(AppDbContext db, Room room, DateOnly checkIn, DateOnly checkOut, CancellationToken ct)
    {
        var periods = await db.RatePeriods.AsNoTracking().Where(p => p.IsActive && p.From < checkOut && p.To >= checkIn && (p.RoomId == null || p.RoomId == room.Id))
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var rates = new List<decimal>();
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            var season = periods.Where(p => p.From <= night && p.To >= night).OrderByDescending(p => p.RoomId != null).FirstOrDefault();
            rates.Add(season?.Rate ?? room.Rate);
        }

        return rates;
    }

    private static async Task<IResult> QuoteAsync(Guid id, DateOnly checkIn, DateOnly checkOut, AppDbContext db, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (room is null || checkOut <= checkIn || checkOut.DayNumber - checkIn.DayNumber > 365)
        {
            return TypedResults.NotFound();
        }

        var rates = await NightRatesAsync(db, room, checkIn, checkOut, ct);
        return TypedResults.Ok(new QuoteDto(rates.Count, rates.Sum(), rates));
    }

    private static async Task<IResult> AddRatePeriodAsync(RatePeriodRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Name?.Trim() is not { Length: >= 2 and <= 60 }) errors["Name"] = ["Name the season (Diwali, Wedding season)."];
        if (request.To < request.From || request.To.DayNumber - request.From.DayNumber > 366) errors["To"] = ["The last night is on or after the first, within a year."];
        if (request.Rate <= 0 || !Money.IsValidAmount(request.Rate)) errors["Rate"] = ["Enter the rate per night."];
        if (request.RoomId is { } roomId && !await db.Rooms.AnyAsync(r => r.Id == roomId, ct)) errors["RoomId"] = ["Room not found."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!await db.RatePeriods.AnyAsync(p => p.Id == request.Id, ct))
        {
            db.RatePeriods.Add(new RatePeriod { Id = request.Id, Name = request.Name!.Trim(), From = request.From, To = request.To, RoomId = request.RoomId, Rate = request.Rate });
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(new RatePeriodDto(request.Id, request.Name!.Trim(), request.From, request.To, request.RoomId, request.Rate));
    }

    /// <summary>Booking sites whose calendar links the app reads. Only these, over https: the server fetches the link.</summary>
    private static readonly string[] CalendarHosts =
        ["airbnb.com", "airbnb.co.in", "booking.com", "makemytrip.com", "goibibo.com", "oyorooms.com", "agoda.com", "expedia.com", "vrbo.com", "tripadvisor.com", "google.com"];

    private static StaySource SourceFor(string host) => host switch
    {
        _ when host.EndsWith("airbnb.com") || host.EndsWith("airbnb.co.in") => StaySource.Airbnb,
        _ when host.EndsWith("booking.com") => StaySource.BookingCom,
        _ when host.EndsWith("makemytrip.com") => StaySource.MakeMyTrip,
        _ when host.EndsWith("goibibo.com") => StaySource.Goibibo,
        _ when host.EndsWith("oyorooms.com") => StaySource.Oyo,
        _ => StaySource.Other,
    };

    /// <summary>Each room's calendar links: the booking site's (brought in) and this room's own (for the site to read).</summary>
    private static async Task<IReadOnlyList<RoomDto>> CalendarsAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var slug = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.Slug).SingleAsync(ct);
        var rooms = await db.Rooms.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(ct);
        foreach (var r in rooms.Where(r => r.IcalToken is null))
        {
            r.IcalToken = Secrets.NewRefreshToken()[..24];
        }

        await db.SaveChangesAsync(ct);
        return rooms.Select(r => new RoomDto(r.Id, r.Name, r.Kind, r.Rate, r.IcalImportUrl, $"/api/v1/store/{slug}/rooms/{r.IcalToken}.ics")).ToList();
    }

    private static async Task<IResult> SetIcalAsync(Guid id, IcalRequest request, AppDbContext db, CancellationToken ct)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (room is null)
        {
            return TypedResults.NotFound();
        }

        var url = request.ImportUrl?.Trim();
        if (!string.IsNullOrEmpty(url) && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !CalendarHosts.Any(h => uri.Host == h || uri.Host.EndsWith("." + h))))
        {
            return Invalid("ImportUrl", "Paste the booking site's calendar (iCal) link: an https link from Airbnb, Booking.com, MakeMyTrip, Goibibo, OYO, Agoda or Expedia.");
        }

        room.IcalImportUrl = string.IsNullOrEmpty(url) ? null : url;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Brings in the booking site's bookings for this room from its calendar link: each new one becomes a booked stay
    /// (named from the calendar, at the room's rates; fill in the guest at check-in). Ones already brought in, past ones,
    /// and ones clashing with a stay here are skipped and reported.
    /// </summary>
    private static async Task<IResult> SyncIcalAsync(Guid id, AppDbContext db, IHttpClientFactory http, TimeProvider clock, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (room?.IcalImportUrl is not { } url)
        {
            return Invalid("ImportUrl", "Add the booking site's calendar link first.");
        }

        string text;
        try
        {
            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            text = await client.GetStringAsync(url, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "The booking site's calendar couldn't be read. Check the link and try again.");
        }

        var source = SourceFor(new Uri(url).Host);
        var today = Today(clock);
        var (added, skipped, problems) = (0, 0, new List<string>());
        foreach (var e in Ical.Events(text).Where(e => e.End > today).Take(200))
        {
            var reference = e.Uid.Length > 40 ? e.Uid[..40] : e.Uid;
            if (await db.Stays.AnyAsync(s => s.RoomId == id && s.SourceRef == reference, ct))
            {
                skipped++;
                continue;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await ClashAsync(db, Guid.Empty, id, e.Start, e.End, ct) is { } clash)
            {
                problems.Add($"{e.Start:dd MMM}–{e.End:dd MMM}: {clash}");
                continue;
            }

            var rates = await NightRatesAsync(db, room, e.Start, e.End, ct);
            var name = string.IsNullOrWhiteSpace(e.Summary) || e.Summary.Length < 2 ? $"{source} guest" : e.Summary.Trim()[..Math.Min(80, e.Summary.Trim().Length)];
            db.Stays.Add(new Stay
            {
                Number = (await db.Stays.MaxAsync(s => (int?)s.Number, ct) ?? 0) + 1, RoomId = id, CheckIn = e.Start, CheckOut = e.End, GuestName = name, Guests = 1,
                Rate = decimal.Round(rates.Average(), 2), RoomCharge = rates.Sum(), Source = source, SourceRef = reference, Note = "Brought in from the booking site's calendar",
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            added++;
        }

        return TypedResults.Ok(new IcalSyncResult(added, skipped, problems.Take(20).ToList()));
    }

    /// <summary>This room's calendar for booking sites to read: every night booked here shows as taken (no guest details).</summary>
    private static async Task<IResult> ExportIcalAsync(string slug, string token, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (await Planner.StoreEndpoints.OpenShopAsync(db, tenant, slug, ct) is null
            || await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.IcalToken == token, ct) is not { } room)
        {
            return TypedResults.NotFound();
        }

        var today = Today(clock);
        var stays = await db.Stays.AsNoTracking()
            .Where(s => s.RoomId == room.Id && (s.Status == StayStatus.Booked || s.Status == StayStatus.CheckedIn) && s.CheckOut > today).ToListAsync(ct);
        var body = Ical.Write($"Room {room.Name}", stays.Select(s => ($"stay-{s.Id}@groceryapp", s.CheckIn, s.CheckOut)), clock.GetUtcNow());
        return TypedResults.Text(body, "text/calendar; charset=utf-8");
    }

    /// <summary>A booking that won't happen frees its nights. An advance taken stays on the khata, to give back or keep as a charge there.</summary>
    private static async Task<IResult> CancelAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var stay = await db.Stays.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (stay is null)
        {
            return TypedResults.NotFound();
        }

        if (stay.Status == StayStatus.Booked)
        {
            stay.Status = StayStatus.Cancelled;
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, db, ct);
    }
}

/// <summary>The little of iCal that booking sites' calendars need: all-day events read, all-day events written.</summary>
public static class Ical
{
    public sealed record Event(string Uid, DateOnly Start, DateOnly End, string Summary);

    /// <summary>VEVENTs with a start and end date (DTSTART;VALUE=DATE:20261105 or a date-time); folded lines are joined.</summary>
    public static IEnumerable<Event> Events(string text)
    {
        var lines = text.Replace("\r\n ", "").Replace("\n ", "").Replace("\r", "").Split('\n');
        string? uid = null, summary = null;
        DateOnly? start = null, end = null;
        foreach (var line in lines)
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var (key, value) = (line[..colon].Split(';')[0].ToUpperInvariant(), line[(colon + 1)..].Trim());
            switch (key)
            {
                case "BEGIN" when value == "VEVENT": (uid, summary, start, end) = (null, null, null, null); break;
                case "UID": uid = value; break;
                case "SUMMARY": summary = value; break;
                case "DTSTART": start = Date(value); break;
                case "DTEND": end = Date(value); break;
                case "END" when value == "VEVENT" && start is { } s && end is { } e && e > s:
                    yield return new Event(uid ?? $"{s:yyyyMMdd}-{e:yyyyMMdd}", s, e, summary ?? ""); break;
            }
        }
    }

    private static DateOnly? Date(string value) =>
        value.Length >= 8 && DateOnly.TryParseExact(value[..8], "yyyyMMdd", out var d) ? d : null;

    public static string Write(string name, IEnumerable<(string Uid, DateOnly Start, DateOnly End)> events, DateTimeOffset now)
    {
        var b = new System.Text.StringBuilder();
        b.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Apna Kaarobar//Rooms//EN\r\nCALSCALE:GREGORIAN\r\n").Append($"X-WR-CALNAME:{name}\r\n");
        foreach (var (uid, start, end) in events)
        {
            b.Append("BEGIN:VEVENT\r\n").Append($"UID:{uid}\r\n").Append($"DTSTAMP:{now.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\n")
                .Append($"DTSTART;VALUE=DATE:{start:yyyyMMdd}\r\n").Append($"DTEND;VALUE=DATE:{end:yyyyMMdd}\r\n").Append("SUMMARY:Booked\r\n").Append("END:VEVENT\r\n");
        }

        return b.Append("END:VCALENDAR\r\n").ToString();
    }
}
