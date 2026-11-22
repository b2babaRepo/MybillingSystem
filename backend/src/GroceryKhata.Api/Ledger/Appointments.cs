using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A salon appointment: a customer, a stylist (or anyone free), a start time and the services, which set how long it
/// takes. One stylist can't have two appointments at once. A walk-in is an appointment starting now. When done, the
/// services go on an ordinary bill.
/// </summary>
public sealed class Appointment : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }

    /// <summary>The stylist's membership in this shop; null when any stylist will do.</summary>
    public Guid? StylistId { get; set; }

    public Guid? CustomerId { get; set; }

    public required string Name { get; set; }

    public string? Phone { get; set; }

    public AppointmentStatus Status { get; set; }

    public string? Note { get; set; }

    public Guid? BillId { get; set; }

    public List<AppointmentService> Services { get; set; } = [];
}

/// <summary>One service on an appointment, with its price and minutes as they were when booked.</summary>
public sealed class AppointmentService : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid ProductId { get; set; }

    public required string Name { get; set; }

    public decimal Price { get; set; }

    public int Minutes { get; set; }

    /// <summary>Its place in the booking (1, 2…), so quotes and bills list the services in the order booked.</summary>
    public int LineNo { get; set; }
}

public enum AppointmentStatus
{
    Booked,
    Arrived,
    Done,
    NoShow,
    Cancelled,
}

public sealed record AppointmentRequest(
    Guid Id, DateOnly Date, TimeOnly? Start, Guid? StylistId, Guid? CustomerId, string? Name, string? Phone, IReadOnlyList<Guid>? ServiceIds, string? Note, bool WalkIn = false);

public sealed record AppointmentStatusRequest(AppointmentStatus? Status);

/// <summary><see cref="StylistId"/>: who did it, when the appointment was for any stylist. <see cref="Tip"/>: left for the stylist, on top of the bill.</summary>
public sealed record AppointmentBillRequest(Guid BillId, decimal PaidAmount, PaymentMode? Mode, Guid? StylistId = null, decimal Tip = 0);

public sealed record AppointmentServiceDto(Guid ProductId, string Name, decimal Price, int Minutes);

public sealed record AppointmentDto(
    Guid Id, int Number, DateOnly Date, TimeOnly Start, TimeOnly End, Guid? StylistId, string? StylistName, Guid? CustomerId, string Name, string? Phone,
    AppointmentStatus Status, string? Note, Guid? BillId, IReadOnlyList<AppointmentServiceDto> Services, decimal Total);

public sealed record StylistDto(Guid Id, string Name);

/// <summary>A day in the salon: who works (owners and helpers) and every appointment, by time.</summary>
public sealed record SalonDayDto(DateOnly Date, IReadOnlyList<StylistDto> Stylists, IReadOnlyList<AppointmentDto> Appointments);

public static class Appointments
{
    /// <summary>A service with no minutes set is booked for this long.</summary>
    public const int DefaultMinutes = 30;

    public static IEndpointRouteBuilder MapAppointments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/appointments").WithTags("Appointments").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", DayAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/{id:guid}/status", StatusAsync);
        group.MapPost("/{id:guid}/bill", BillAsync);
        group.MapGet("/{id:guid}/quote", QuoteAsync);
        return app;
    }

    private static DateTimeOffset IndiaNow(TimeProvider clock) => clock.GetUtcNow().ToOffset(IndiaTime.Offset);

    private static async Task<List<StylistDto>> StylistsAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await (from m in db.Memberships.AsNoTracking()
               join u in db.Users.AsNoTracking() on m.UserId equals u.Id
               where m.ShopId == tenant.ShopId && m.IsActive
               orderby m.Role descending, u.FullName
               select new StylistDto(m.Id, u.FullName)).ToListAsync(ct);

    private static AppointmentDto ToDto(Appointment a, IReadOnlyDictionary<Guid, string> names) => new(
        a.Id, a.Number, a.Date, a.Start, a.End, a.StylistId, a.StylistId is { } s ? names.GetValueOrDefault(s) : null, a.CustomerId, a.Name, a.Phone, a.Status, a.Note,
        a.BillId, a.Services.OrderBy(x => x.LineNo).Select(x => new AppointmentServiceDto(x.ProductId, x.Name, x.Price, x.Minutes)).ToList(), a.Services.Sum(x => x.Price));

    private static async Task<SalonDayDto> DayAsync(DateOnly? date, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var day = date ?? DateOnly.FromDateTime(IndiaNow(clock).DateTime);
        var stylists = await StylistsAsync(db, tenant, ct);
        var names = stylists.ToDictionary(s => s.Id, s => s.Name);
        var list = await db.Appointments.AsNoTracking().Include(a => a.Services).Where(a => a.Date == day).OrderBy(a => a.Start).ThenBy(a => a.Number).ToListAsync(ct);
        return new SalonDayDto(day, stylists, list.Select(a => ToDto(a, names)).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var a = await db.Appointments.AsNoTracking().Include(x => x.Services).SingleOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(a, (await StylistsAsync(db, tenant, ct)).ToDictionary(s => s.Id, s => s.Name)));
    }

    /// <summary>
    /// Books (or, for a walk-in, starts now) an appointment. Its end is the start plus the services' minutes. A stylist
    /// already booked for any of that time is refused, so the same person is never in two chairs.
    /// </summary>
    internal static async Task<IResult> CreateAsync(AppointmentRequest request, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Appointments.AnyAsync(a => a.Id == request.Id, ct))
        {
            return await GetAsync(request.Id, db, tenant, ct);
        }

        var now = IndiaNow(clock);
        var today = DateOnly.FromDateTime(now.DateTime);
        var date = request.WalkIn ? today : request.Date;
        var start = request.WalkIn ? TimeOnly.FromDateTime(now.DateTime) : request.Start;
        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
        if (date < today) errors["Date"] = ["Choose a date from today on."];
        if (start is null) errors["Start"] = ["Choose a time."];
        var ids = (request.ServiceIds ?? []).Distinct().ToList();
        var services = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync(ct);
        if (ids.Count is 0 or > 20 || services.Count != ids.Count) errors["ServiceIds"] = ["Choose the services."];
        if (request.StylistId is { } stylist && !await db.Memberships.AnyAsync(m => m.Id == stylist && m.ShopId == tenant.ShopId && m.IsActive, ct))
        {
            errors["StylistId"] = ["Choose a stylist from this salon."];
        }

        string? name = request.Name?.Trim();
        if (request.CustomerId is { } customer)
        {
            name ??= await db.Customers.AsNoTracking().Where(c => c.Id == customer).Select(c => c.Name).SingleOrDefaultAsync(ct);
            if (!await db.Customers.AnyAsync(c => c.Id == customer, ct)) errors["CustomerId"] = ["Customer not found."];
        }

        if (name is null or { Length: 0 } && request.WalkIn) name = "Walk-in";
        if (name is not { Length: >= 2 and <= 80 }) errors["Name"] = ["Whose appointment is it?"];
        if (request.Note?.Length > 200) errors["Note"] = ["Keep the note under 200 letters."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var minutes = services.Sum(p => p.DurationMinutes ?? DefaultMinutes);
        var end = start!.Value.AddMinutes(minutes);
        if (end <= start.Value)
        {
            return Invalid("Start", "The appointment must finish the same day.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (request.StylistId is { } who)
        {
            // Lock the stylist's membership row so two bookings for the same stylist can't both pass the check.
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM memberships WHERE id = {who} FOR UPDATE", ct);
            var busy = await ClashAsync(db, who, date, start.Value, end, ct) ?? await Bridal.EventClashAsync(db, who, date, start.Value, end, Guid.Empty, ct);
            if (busy is not null)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: busy);
            }
        }

        var appointment = new Appointment
        {
            Id = request.Id, Number = (await db.Appointments.MaxAsync(a => (int?)a.Number, ct) ?? 0) + 1, Date = date, Start = start.Value, End = end,
            StylistId = request.StylistId, CustomerId = request.CustomerId, Name = name!, Phone = PhoneNumber.Normalize(request.Phone) ?? (string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim()),
            Status = request.WalkIn ? AppointmentStatus.Arrived : AppointmentStatus.Booked, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Services = ids.Select(i => services.Single(p => p.Id == i))
                .Select((p, i) => new AppointmentService { ProductId = p.Id, Name = p.Name, Price = p.SellingPrice, Minutes = p.DurationMinutes ?? DefaultMinutes, LineNo = i + 1 }).ToList(),
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(request.Id, db, tenant, ct);
    }

    /// <summary>The stylist's booked or arrived appointment overlapping this time, as a message; null when free.</summary>
    public static async Task<string?> ClashAsync(AppDbContext db, Guid stylistId, DateOnly date, TimeOnly start, TimeOnly end, CancellationToken ct)
    {
        var busy = await db.Appointments.AsNoTracking()
            .Where(a => a.StylistId == stylistId && a.Date == date && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Arrived)
                && a.Start < end && start < a.End)
            .Select(a => new { a.Start, a.End, a.Name }).FirstOrDefaultAsync(ct);
        return busy is null ? null : $"This stylist is busy {busy.Start:HH\\:mm}–{busy.End:HH\\:mm} ({busy.Name}).";
    }

    /// <summary>The customer arrived, didn't come, or cancelled. Done is set by billing.</summary>
    private static async Task<IResult> StatusAsync(Guid id, AppointmentStatusRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (appointment is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Status is null or AppointmentStatus.Done)
        {
            return Invalid("Status", "Bill the appointment to finish it.");
        }

        if (appointment.Status is AppointmentStatus.Done)
        {
            return Invalid("Status", "This appointment is billed.");
        }

        appointment.Status = request.Status.Value;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, tenant, ct);
    }

    /// <summary>
    /// The services done go on a bill for the customer (each service its own line, at the price booked, carrying its GST),
    /// paid now or put on the khata, and the appointment is done. Products sold alongside go on a separate bill.
    /// </summary>
    /// <summary>What the bill will be: each service at its price for this customer (package or membership applied).</summary>
    private static async Task<IResult> QuoteAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Services.OrderBy(s => s.LineNo)).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (appointment is null)
        {
            return TypedResults.NotFound();
        }

        var (lines, _) = await Prepaid.PriceAsync(db, appointment.CustomerId, appointment.Services, DateOnly.FromDateTime(IndiaNow(clock).DateTime), track: false, ct);
        return TypedResults.Ok(new AppointmentQuoteDto(lines, lines.Sum(l => l.Price)));
    }

    private static async Task<IResult> BillAsync(Guid id, AppointmentBillRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var appointment = await db.Appointments.Include(a => a.Services.OrderBy(s => s.LineNo)).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (appointment is null)
        {
            return TypedResults.NotFound();
        }

        if (appointment.Status == AppointmentStatus.Done)
        {
            return await GetAsync(id, db, tenant, ct);
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
        {
            return Invalid("Status", "This appointment didn't happen.");
        }

        if (appointment.Services.Any(s => s.Price <= 0))
        {
            return Invalid("Services", "Set a price for every service before billing.");
        }

        if (request.Tip < 0 || (request.Tip > 0 && !Money.IsValidAmount(request.Tip)))
        {
            return Invalid("Tip", "Enter the tip, or leave it empty.");
        }

        var stylist = appointment.StylistId ?? request.StylistId;
        if (request.StylistId is { } chosen && appointment.StylistId is null && !await db.Memberships.AnyAsync(m => m.Id == chosen && m.ShopId == tenant.ShopId && m.IsActive, ct))
        {
            return Invalid("StylistId", "Choose a stylist from this salon.");
        }

        if (request.Tip > 0 && stylist is null)
        {
            return Invalid("StylistId", "Choose who did it, to give them the tip.");
        }

        // A live package covers its service (billed at the session's share, paid from the advance); a membership takes its discount off.
        var today = DateOnly.FromDateTime(IndiaNow(clock).DateTime);
        var (priced, used) = await Prepaid.PriceAsync(db, appointment.CustomerId, appointment.Services, today, track: true, ct);
        var lines = priced.Select(l => new BillLineRequest(l.ProductId, l.Name, 1, Unit.Piece, l.Price)).ToList();
        var result = await ledger.CreateBillAsync(new BillRequest(request.BillId, appointment.CustomerId, lines, request.PaidAmount, request.Mode, null),
            user.UserId()!.Value, ct, isOwner: user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner)));
        if (result.Value is not { } bill)
        {
            return result.ToHttp("");
        }

        (appointment.Status, appointment.BillId, appointment.StylistId) = (AppointmentStatus.Done, bill.Id, stylist);
        foreach (var (plan, uses) in used)
        {
            plan.SessionsLeft -= uses;
        }

        if (stylist is { } who)
        {
            await StylistPay.RecordAsync(db, who, appointment.Id, bill.Id, priced.Sum(l => l.Price), request.Tip, DateTimeOffset.UtcNow, ct);
        }

        await db.SaveChangesAsync(ct);
        await SalonCards.UseProductsAsync(db, bill.Id, $"Appointment #{appointment.Number}", user.UserId()!.Value, clock.GetUtcNow(), ct);
        return await GetAsync(id, db, tenant, ct);
    }
}
