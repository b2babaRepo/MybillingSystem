using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A table kept for a party at a time: a family dinner, or a birthday or anniversary with a cake and decoration. A
/// deposit, when taken, is a payment on the customer's khata, so the table bill on the night takes it off by itself.
/// </summary>
public sealed class TableBooking : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Time { get; set; }

    public int People { get; set; }

    /// <summary>The khata customer, when the party has one (needed for a deposit). Otherwise just a name and phone.</summary>
    public Guid? CustomerId { get; set; }

    public required string Name { get; set; }

    public string? Phone { get; set; }

    public PartyOccasion Occasion { get; set; }

    /// <summary>"1 kg chocolate, 'Happy Birthday Riya'".</summary>
    public string? Cake { get; set; }

    public bool Decoration { get; set; }

    /// <summary>"Table 4", "Rooftop", or empty for any table.</summary>
    public string? Table { get; set; }

    public string? Note { get; set; }

    public TableBookingStatus Status { get; set; }

    public decimal Deposit { get; set; }
}

public enum PartyOccasion
{
    None,
    Birthday,
    Anniversary,
    Other,
}

public enum TableBookingStatus
{
    Booked,
    Seated,
    NoShow,
    Cancelled,
}

public sealed record TableBookingRequest(
    Guid Id, DateOnly Date, TimeOnly Time, int People, Guid? CustomerId, string? Name, string? Phone, PartyOccasion? Occasion, string? Cake, bool Decoration,
    string? Table, string? Note);

public sealed record TableStatusRequest(TableBookingStatus? Status);

public sealed record TableDepositRequest(decimal Amount, PaymentMode? Mode);

public sealed record TableBookingDto(
    Guid Id, int Number, DateOnly Date, TimeOnly Time, int People, Guid? CustomerId, string Name, string? Phone, PartyOccasion Occasion, string? Cake,
    bool Decoration, string? Table, string? Note, TableBookingStatus Status, decimal Deposit);

/// <summary>A day's table bookings and how many people are expected (booked and seated parties).</summary>
public sealed record TableDayDto(DateOnly Date, int People, IReadOnlyList<TableBookingDto> Bookings);

public static class TableBookings
{
    public static IEndpointRouteBuilder MapTableBookings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/table-bookings").WithTags("Bookings").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", DayAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/{id:guid}/status", StatusAsync);
        group.MapPost("/{id:guid}/deposit", DepositAsync);
        return app;
    }

    private static TableBookingDto ToDto(TableBooking b) => new(b.Id, b.Number, b.Date, b.Time, b.People, b.CustomerId, b.Name, b.Phone, b.Occasion, b.Cake,
        b.Decoration, b.Table, b.Note, b.Status, b.Deposit);

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);

    /// <summary>One day's bookings by time (default today).</summary>
    private static async Task<TableDayDto> DayAsync(DateOnly? date, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var day = date ?? Today(clock);
        var list = await db.TableBookings.AsNoTracking().Where(b => b.Date == day).OrderBy(b => b.Time).ThenBy(b => b.Number).ToListAsync(ct);
        return new TableDayDto(day, list.Where(b => b.Status is TableBookingStatus.Booked or TableBookingStatus.Seated).Sum(b => b.People), list.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateAsync(TableBookingRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.TableBookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok(ToDto(existing));
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
        if (request.Date < Today(clock)) errors["Date"] = ["Choose a date from today on."];
        if (request.People is < 1 or > 500) errors["People"] = ["How many people?"];
        string? name = request.Name?.Trim();
        if (request.CustomerId is { } customer)
        {
            name ??= await db.Customers.AsNoTracking().Where(c => c.Id == customer).Select(c => c.Name).SingleOrDefaultAsync(ct);
            if (!await db.Customers.AnyAsync(c => c.Id == customer, ct)) errors["CustomerId"] = ["Customer not found."];
        }

        if (name is not { Length: >= 2 and <= 80 }) errors["Name"] = ["Whose booking is it?"];
        if (request.Cake?.Length > 120 || request.Note?.Length > 200 || request.Table?.Length > 30) errors["Note"] = ["Keep the notes short."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var booking = new TableBooking
        {
            Id = request.Id, Number = (await db.TableBookings.MaxAsync(b => (int?)b.Number, ct) ?? 0) + 1, Date = request.Date, Time = request.Time,
            People = request.People, CustomerId = request.CustomerId, Name = name!, Phone = PhoneNumber.Normalize(request.Phone) ?? Clean(request.Phone),
            Occasion = request.Occasion ?? PartyOccasion.None, Cake = Clean(request.Cake), Decoration = request.Decoration, Table = Clean(request.Table), Note = Clean(request.Note),
        };
        db.TableBookings.Add(booking);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(booking));
    }

    /// <summary>On the night: the party came (seated), didn't come (no-show), or cancelled. A deposit stays on the khata either way.</summary>
    private static async Task<IResult> StatusAsync(Guid id, TableStatusRequest request, AppDbContext db, CancellationToken ct)
    {
        var booking = await db.TableBookings.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Status is null)
        {
            return Invalid("Status", "Choose seated, no-show or cancelled.");
        }

        booking.Status = request.Status.Value;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(booking));
    }

    /// <summary>A deposit to hold a big party's table: a khata payment, taken off the table bill when it is made for this customer.</summary>
    private static async Task<IResult> DepositAsync(Guid id, TableDepositRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, CancellationToken ct)
    {
        var booking = await db.TableBookings.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (booking.CustomerId is not { } customer)
        {
            return Invalid("CustomerId", "Choose a khata customer on the booking to take a deposit.");
        }

        var result = await ledger.AddKhataEntryAsync(
            new KhataEntryRequest(Guid.CreateVersion7(), customer, KhataEntryType.Payment, request.Amount, request.Mode, $"Deposit: table booking #{booking.Number}", null),
            user.UserId()!.Value, ct, user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner)));
        if (result.Value is null)
        {
            return result.ToHttp("");
        }

        booking.Deposit += request.Amount;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(booking));
    }
}
