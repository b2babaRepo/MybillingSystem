using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A bridal (or other home) booking: one customer, several events (haldi, mehndi, wedding, reception) on their own dates,
/// often at the customer's home with a travel charge, and sometimes a paid trial first. A job carries its money: the
/// trial fee and advances go on the khata, products and helpers are its costs, and the final bill closes it.
/// </summary>
public sealed class BridalBooking : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid CustomerId { get; set; }

    public required string Title { get; set; }

    public bool AtHome { get; set; }

    public string? Address { get; set; }

    public decimal TravelCharge { get; set; }

    public DateOnly? TrialDate { get; set; }

    public BridalStatus Status { get; set; }

    public Guid JobId { get; set; }

    public Guid? BillId { get; set; }

    public string? Note { get; set; }

    public List<BridalEvent> Events { get; set; } = [];
}

/// <summary>One event of a bridal booking: when, how long, who does it and its price.</summary>
public sealed class BridalEvent : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BookingId { get; set; }

    public required string Name { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Start { get; set; }

    public int Minutes { get; set; }

    public decimal Price { get; set; }

    /// <summary>The stylist's membership; their time is kept for it, so no appointment can take it.</summary>
    public Guid? StylistId { get; set; }
}

public enum BridalStatus
{
    Booked,
    Done,
    Cancelled,
}

public sealed record BridalEventRequest(string? Name, DateOnly Date, TimeOnly Start, int Minutes, decimal Price, Guid? StylistId);

public sealed record BridalRequest(
    Guid Id, Guid CustomerId, string? Title, bool AtHome, string? Address, decimal TravelCharge, DateOnly? TrialDate, string? Note, IReadOnlyList<BridalEventRequest>? Events);

public sealed record BridalPaymentRequest(decimal Amount, PaymentMode? Mode, bool Trial = false);

public sealed record BridalCompleteRequest(Guid BillId, decimal PaidAmount, PaymentMode? Mode);

public sealed record BridalCancelRequest(string? Reason, decimal Keep);

public sealed record BridalEventDto(Guid Id, string Name, DateOnly Date, TimeOnly Start, TimeOnly End, decimal Price, Guid? StylistId, string? StylistName);

public sealed record BridalDto(
    Guid Id, int Number, Guid CustomerId, string CustomerName, string? CustomerPhone, string Title, bool AtHome, string? Address, decimal TravelCharge, DateOnly? TrialDate,
    BridalStatus Status, Guid JobId, Guid? BillId, string? Note, IReadOnlyList<BridalEventDto> Events, decimal Total, decimal Advance);

public static class Bridal
{
    public static IEndpointRouteBuilder MapBridal(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/bridal").WithTags("Appointments").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/{id:guid}/payment", PaymentAsync);
        group.MapPost("/{id:guid}/complete", CompleteAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));

    /// <summary>A stylist's events overlapping this time on this date (bridal and home bookings still on).</summary>
    public static async Task<string?> EventClashAsync(AppDbContext db, Guid stylistId, DateOnly date, TimeOnly start, TimeOnly end, Guid exceptBooking, CancellationToken ct)
    {
        var events = await (from e in db.BridalEvents.AsNoTracking()
                            join b in db.BridalBookings.AsNoTracking() on e.BookingId equals b.Id
                            where e.StylistId == stylistId && e.Date == date && b.Status == BridalStatus.Booked && b.Id != exceptBooking
                            select new { e.Name, e.Start, e.Minutes, b.Title }).ToListAsync(ct);
        var clash = events.FirstOrDefault(e => e.Start < end && start < e.Start.AddMinutes(e.Minutes));
        return clash is null ? null : $"This stylist is at {clash.Title} ({clash.Name}) {clash.Start:HH\\:mm}–{clash.Start.AddMinutes(clash.Minutes):HH\\:mm}.";
    }

    private static async Task<IReadOnlyList<BridalDto>> ToDtosAsync(AppDbContext db, ITenantContext tenant, IQueryable<BridalBooking> query, CancellationToken ct)
    {
        var list = await query.AsNoTracking().Include(b => b.Events).ToListAsync(ct);
        var customerIds = list.Select(b => b.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        var stylists = await (from m in db.Memberships.AsNoTracking() join u in db.Users.AsNoTracking() on m.UserId equals u.Id where m.ShopId == tenant.ShopId select new { m.Id, u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var jobIds = list.Select(b => b.JobId).ToList();
        var advances = await db.JobEntries.AsNoTracking().Where(e => jobIds.Contains(e.JobId) && e.Kind == JobEntryKind.Advance)
            .GroupBy(e => e.JobId).Select(g => new { g.Key, Total = g.Sum(e => e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return list.Select(b => new BridalDto(
            b.Id, b.Number, b.CustomerId, customers.GetValueOrDefault(b.CustomerId)?.Name ?? "", customers.GetValueOrDefault(b.CustomerId)?.Phone, b.Title, b.AtHome, b.Address, b.TravelCharge,
            b.TrialDate, b.Status, b.JobId, b.BillId, b.Note,
            b.Events.OrderBy(e => e.Date).ThenBy(e => e.Start).Select(e => new BridalEventDto(e.Id, e.Name, e.Date, e.Start, e.Start.AddMinutes(e.Minutes), e.Price, e.StylistId,
                e.StylistId is { } s ? stylists.GetValueOrDefault(s) : null)).ToList(),
            b.Events.Sum(e => e.Price) + b.TravelCharge, advances.GetValueOrDefault(b.JobId))).ToList();
    }

    /// <summary>Bookings still on, by their first event; or (with <paramref name="date"/>) the bookings with an event that day.</summary>
    private static async Task<IReadOnlyList<BridalDto>> ListAsync(DateOnly? date, bool? all, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var query = db.BridalBookings.AsQueryable();
        if (date is { } d)
        {
            query = query.Where(b => b.Status == BridalStatus.Booked && b.Events.Any(e => e.Date == d));
        }
        else if (all != true)
        {
            query = query.Where(b => b.Status == BridalStatus.Booked);
        }

        return (await ToDtosAsync(db, tenant, query.OrderByDescending(b => b.Number).Take(200), ct))
            .OrderBy(b => b.Events.Select(e => e.Date).DefaultIfEmpty(DateOnly.MaxValue).Min()).ToList();
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        (await ToDtosAsync(db, tenant, db.BridalBookings.Where(b => b.Id == id), ct)).SingleOrDefault() is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound();

    /// <summary>
    /// Books the events. Each stylist's time is checked against their other bridal events and their salon appointments,
    /// so a stylist sent to a wedding isn't also booked in the chair.
    /// </summary>
    private static async Task<IResult> CreateAsync(BridalRequest request, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (await db.BridalBookings.AnyAsync(b => b.Id == request.Id, ct))
        {
            return await GetAsync(request.Id, db, tenant, ct);
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        var events = request.Events ?? [];
        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct)) errors["CustomerId"] = ["Choose the customer (khata): advances go on it."];
        if (request.Title?.Trim() is not { Length: >= 2 and <= 120 }) errors["Title"] = ["Name it (Riya's wedding)."];
        if (events.Count is 0 or > 10) errors["Events"] = ["Add one to ten events."];
        if (events.Any(e => e.Name?.Trim() is not { Length: >= 2 and <= 40 } || e.Date < today || e.Minutes is < 15 or > 720 || e.Price < 0 || (e.Price > 0 && !Money.IsValidAmount(e.Price))))
        {
            errors["Events"] = ["Each event needs a name, a date from today on, 15 minutes to 12 hours, and a price."];
        }

        if (request.TravelCharge < 0 || (request.TravelCharge > 0 && !Money.IsValidAmount(request.TravelCharge))) errors["TravelCharge"] = ["Enter the travel charge, or 0."];
        if (request.Address?.Length > 200 || request.Note?.Length > 300) errors["Note"] = ["Keep the address and note short."];
        var stylistIds = events.Where(e => e.StylistId != null).Select(e => e.StylistId!.Value).Distinct().ToList();
        if (stylistIds.Count > 0 && await db.Memberships.CountAsync(m => stylistIds.Contains(m.Id) && m.ShopId == tenant.ShopId && m.IsActive, ct) != stylistIds.Count)
        {
            errors["Events"] = ["Choose stylists from this salon."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var e in events.Where(e => e.StylistId is not null))
        {
            var end = e.Start.AddMinutes(e.Minutes);
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM memberships WHERE id = {e.StylistId} FOR UPDATE", ct);
            var problem = await EventClashAsync(db, e.StylistId!.Value, e.Date, e.Start, end, request.Id, ct)
                ?? await Appointments.ClashAsync(db, e.StylistId.Value, e.Date, e.Start, end, ct)
                ?? (events.Any(o => o != e && o.StylistId == e.StylistId && o.Date == e.Date && o.Start < end && e.Start < o.Start.AddMinutes(o.Minutes)) ? "Two events of this booking overlap for the same stylist." : null);
            if (problem is not null)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: $"{e.Name} {e.Date:dd MMM}: {problem}");
            }
        }

        var booking = new BridalBooking
        {
            Id = request.Id, Number = (await db.BridalBookings.MaxAsync(b => (int?)b.Number, ct) ?? 0) + 1, CustomerId = request.CustomerId, Title = request.Title!.Trim(),
            AtHome = request.AtHome, Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(), TravelCharge = request.TravelCharge, TrialDate = request.TrialDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), Status = BridalStatus.Booked,
            Events = events.Select(e => new BridalEvent { Name = e.Name!.Trim(), Date = e.Date, Start = e.Start, Minutes = e.Minutes, Price = e.Price, StylistId = e.StylistId }).ToList(),
        };
        var job = new Job
        {
            Number = (await db.Jobs.MaxAsync(j => (int?)j.Number, ct) ?? 0) + 1, CustomerId = request.CustomerId, Title = $"Bridal: {booking.Title}", Status = JobStatus.Order,
            Price = booking.Events.Sum(e => e.Price) + booking.TravelCharge, DueOn = booking.Events.Min(e => e.Date),
        };
        db.Jobs.Add(job);
        booking.JobId = job.Id;
        db.BridalBookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(booking.Id, db, tenant, ct);
    }

    /// <summary>A trial fee or an advance: a payment on the customer's khata, kept against the booking (a trial fee counts toward the package).</summary>
    private static async Task<IResult> PaymentAsync(Guid id, BridalPaymentRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var booking = await db.BridalBookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id && b.Status == BridalStatus.Booked, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Amount <= 0 || !Money.IsValidAmount(request.Amount))
        {
            return Invalid("Amount", "Enter the amount received.");
        }

        var what = request.Trial ? "Trial fee" : "Advance";
        var paid = await ledger.AddKhataEntryAsync(
            new KhataEntryRequest(Guid.CreateVersion7(), booking.CustomerId, KhataEntryType.Payment, request.Amount, request.Mode, $"{what}: bridal #{booking.Number}", null),
            user.UserId()!.Value, ct, IsOwner(user));
        if (paid.Value is not { } entry)
        {
            return paid.ToHttp("");
        }

        db.JobEntries.Add(new JobEntry
        {
            JobId = booking.JobId, Kind = JobEntryKind.Advance, Description = what, Amount = request.Amount, LedgerEntryId = entry.Entry.Id, OccurredAt = clock.GetUtcNow(),
            CreatedByUserId = user.UserId()!.Value,
        });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, tenant, ct);
    }

    /// <summary>
    /// After the last event: one bill with each event and the travel charge. The trial fee and advances already sit on the
    /// khata, so the customer owes the rest. Each event's stylist earns their commission on it; the job closes.
    /// </summary>
    private static async Task<IResult> CompleteAsync(Guid id, BridalCompleteRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var booking = await db.BridalBookings.Include(b => b.Events).SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (booking.Status == BridalStatus.Done)
        {
            return await GetAsync(id, db, tenant, ct);
        }

        if (booking.Status != BridalStatus.Booked)
        {
            return Invalid("Status", "This booking was cancelled.");
        }

        var events = booking.Events.OrderBy(e => e.Date).ThenBy(e => e.Start).ToList();
        var lines = events.Where(e => e.Price > 0).Select(e => new BillLineRequest(null, $"{e.Name} ({e.Date:dd MMM})", 1, Unit.Piece, e.Price)).ToList();
        if (booking.TravelCharge > 0) lines.Add(new BillLineRequest(null, "Travel to the customer", 1, Unit.Piece, booking.TravelCharge));
        if (lines.Count == 0)
        {
            return Invalid("Events", "Nothing to bill.");
        }

        // Salon services: 5% GST under SAC 999722 (a CA should confirm); the travel line goes with the service.
        var tax = lines.Select(_ => ((decimal?)5m, (string?)"999722")).ToList();
        var result = await ledger.CreateBillAsync(new BillRequest(request.BillId, booking.CustomerId, lines, request.PaidAmount, request.Mode, null),
            user.UserId()!.Value, ct, isOwner: IsOwner(user), taxSnapshot: tax);
        if (result.Value is not { } bill)
        {
            return result.ToHttp("");
        }

        var now = clock.GetUtcNow();
        foreach (var e in events.Where(e => e.StylistId is not null && e.Price > 0))
        {
            await StylistPay.RecordAsync(db, e.StylistId!.Value, e.Id, bill.Id, e.Price, 0, now, ct);
        }

        var material = await db.JobEntries.Where(e => e.JobId == booking.JobId && e.Kind == JobEntryKind.Material).SumAsync(e => e.Amount, ct);
        await db.BillLines.Where(l => l.BillId == bill.Id && l.LineNo == 1).ExecuteUpdateAsync(s => s.SetProperty(l => l.UnitCost, material), ct);
        await db.Jobs.Where(j => j.Id == booking.JobId).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, JobStatus.Delivered).SetProperty(j => j.Price, lines.Sum(l => l.UnitPrice)).SetProperty(j => j.BillId, bill.Id).SetProperty(j => j.DeliveredAt, now), ct);
        (booking.Status, booking.BillId) = (BridalStatus.Done, bill.Id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, tenant, ct);
    }

    /// <summary>Cancels the booking and frees the stylists. What is kept (per the salon's terms) goes on the khata as a cancellation charge; the rest is owed back.</summary>
    private static async Task<IResult> CancelAsync(Guid id, BridalCancelRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, LedgerService ledger, CancellationToken ct)
    {
        var booking = await db.BridalBookings.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (booking.Status != BridalStatus.Booked)
        {
            return await GetAsync(id, db, tenant, ct);
        }

        if (request.Keep < 0 || (request.Keep > 0 && !Money.IsValidAmount(request.Keep)))
        {
            return Invalid("Keep", "Enter what is kept, or 0.");
        }

        if (request.Keep > 0)
        {
            var charge = await ledger.AddKhataEntryAsync(
                new KhataEntryRequest(Guid.CreateVersion7(), booking.CustomerId, KhataEntryType.Credit, request.Keep, null, $"Cancellation charge: bridal #{booking.Number}", null),
                user.UserId()!.Value, ct, IsOwner(user));
            if (charge.Value is null)
            {
                return charge.ToHttp("");
            }
        }

        booking.Status = BridalStatus.Cancelled;
        booking.Note = string.IsNullOrWhiteSpace(request.Reason) ? booking.Note : $"Cancelled: {request.Reason.Trim()}"[..Math.Min(300, $"Cancelled: {request.Reason.Trim()}".Length)];
        await db.Jobs.Where(j => j.Id == booking.JobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, JobStatus.Cancelled), ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, tenant, ct);
    }
}
