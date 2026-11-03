using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A room the lodge lets out: "101", "Deluxe 2". Its rate is the usual per-night price.</summary>
public sealed class Room : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public string? Kind { get; set; }

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

public sealed record RoomDto(Guid Id, string Name, string? Kind, decimal Rate);

public sealed record StayRequest(
    Guid Id, Guid RoomId, DateOnly CheckIn, DateOnly CheckOut, Guid? CustomerId, string? GuestName, string? Phone, int Guests, decimal? Rate,
    StaySource? Source, string? SourceRef, decimal Commission, bool Foreigner, string? Note);

public sealed record CheckInRequest(string? IdType, string? IdNumber);

public sealed record StayChargeRequest(Guid Id, string? Name, decimal Amount);

public sealed record StayAdvanceRequest(decimal Amount, PaymentMode? Mode);

public sealed record CheckoutRequest(Guid BillId, decimal PaidAmount, PaymentMode? Mode);

public sealed record StayChargeDto(Guid Id, string Name, decimal Amount);

public sealed record StayDto(
    Guid Id, int Number, Guid RoomId, string RoomName, DateOnly CheckIn, DateOnly CheckOut, int Nights, StayStatus Status, Guid? CustomerId, string GuestName,
    string? Phone, int Guests, string? IdType, string? IdLast4, bool Foreigner, DateTimeOffset? FormCFiledAt, bool FormCDue, decimal Rate, StaySource Source,
    string? SourceRef, decimal Commission, decimal RoomTotal, IReadOnlyList<StayChargeDto> Charges, decimal Total, Guid? BillId, string? Note);

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

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);

    private static int Nights(Stay s) => s.CheckOut.DayNumber - s.CheckIn.DayNumber;

    private static StayDto ToDto(Stay s, string room)
    {
        var roomTotal = Nights(s) * s.Rate;
        var charges = s.Charges.OrderBy(c => c.CreatedAt).Select(c => new StayChargeDto(c.Id, c.Name, c.Amount)).ToList();
        return new StayDto(s.Id, s.Number, s.RoomId, room, s.CheckIn, s.CheckOut, Nights(s), s.Status, s.CustomerId, s.GuestName, s.Phone, s.Guests, s.IdType, s.IdLast4,
            s.Foreigner, s.FormCFiledAt, s.Foreigner && s.Status == StayStatus.CheckedIn && s.FormCFiledAt is null, s.Rate, s.Source, s.SourceRef, s.Commission,
            roomTotal, charges, roomTotal + charges.Sum(c => c.Amount), s.BillId, s.Note);
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
    private static async Task<IReadOnlyList<StayDto>> ListAsync(DateOnly? from, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
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
        var rate = request.Rate ?? room?.Rate ?? 0;
        if (rate <= 0 || !Money.IsValidAmount(rate)) errors["Rate"] = ["Enter the rate per night."];
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
        db.Stays.Add(new Stay
        {
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
        var lines = new List<BillLineRequest> { new(null, $"Room {room}: {Nights(stay)} night(s), {stay.CheckIn:dd MMM} to {stay.CheckOut:dd MMM}", Nights(stay), Unit.Piece, stay.Rate) };
        lines.AddRange(stay.Charges.OrderBy(c => c.CreatedAt).Select(c => new BillLineRequest(null, c.Name, 1, Unit.Piece, c.Amount)));
        var result = await ledger.CreateBillAsync(new BillRequest(request.BillId, stay.CustomerId, lines, request.PaidAmount, request.Mode, null), user.UserId()!.Value, ct, isOwner: IsOwner(user));
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
