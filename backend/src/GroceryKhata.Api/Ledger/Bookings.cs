using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A hall, lawn or room the shop books out for functions (main hall, garden, party room).</summary>
public sealed class Venue : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One function booked at a venue on a date and slot: a wedding, reception, birthday, anniversary. A hold keeps the
/// slot for a few days while the family decides; a confirmed booking gets a job, so the advance (on the khata), what is
/// spent on it (food from stock, decorator, tent, DJ, waiters) and the final bill all reuse the jobs module.
/// </summary>
public sealed class Booking : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid VenueId { get; set; }

    public DateOnly Date { get; set; }

    public BookingSlot Slot { get; set; }

    public BookingStatus Status { get; set; }

    /// <summary>A hold stops keeping the slot after this.</summary>
    public DateTimeOffset? HoldUntil { get; set; }

    public Guid? CustomerId { get; set; }

    public Occasion Occasion { get; set; }

    /// <summary>The guaranteed guests: the bill is for at least this many plates.</summary>
    public int Guests { get; set; }

    public decimal PlateRate { get; set; }

    public decimal HallRent { get; set; }

    /// <summary>Security deposit asked for (damage), held and given back after the function. Not income.</summary>
    public decimal Deposit { get; set; }

    public decimal DepositTaken { get; set; }

    public decimal? DepositReturned { get; set; }

    public int? ActualGuests { get; set; }

    public Guid? JobId { get; set; }

    public string? Note { get; set; }

    public string? CancelReason { get; set; }
}

public enum BookingSlot
{
    Morning,
    Evening,
    FullDay,
}

public enum BookingStatus
{
    Hold,
    Confirmed,
    Cancelled,
    Settled,
}

public enum Occasion
{
    Wedding,
    Reception,
    Engagement,
    Birthday,
    Anniversary,
    Other,
}

public sealed record VenueRequest(Guid Id, string? Name, int? Capacity);

public sealed record VenueDto(Guid Id, string Name, int? Capacity);

public sealed record BookingRequest(
    Guid Id, Guid VenueId, DateOnly Date, BookingSlot? Slot, Guid? CustomerId, Occasion? Occasion, int Guests, decimal PlateRate, decimal HallRent,
    decimal Deposit, string? Note, bool Hold = false, int? HoldDays = null);

public sealed record BookingCancelRequest(string? Reason, decimal Keep);

public sealed record DepositRequest(decimal Amount);

public sealed record BookingExtra(string? Name, decimal Amount);

public sealed record SettleRequest(Guid BillId, int ActualGuests, IReadOnlyList<BookingExtra>? Extras, decimal Damage, decimal PaidAmount, PaymentMode? Mode);

/// <summary>A booking as the calendar shows it. <see cref="Estimate"/>: hall rent plus guaranteed plates.</summary>
public sealed record BookingDto(
    Guid Id, int Number, Guid VenueId, string VenueName, DateOnly Date, BookingSlot Slot, string Status, DateTimeOffset? HoldUntil, Guid? CustomerId,
    string? CustomerName, Occasion Occasion, int Guests, decimal PlateRate, decimal HallRent, decimal Estimate, decimal Deposit, decimal DepositTaken,
    decimal? DepositReturned, int? ActualGuests, Guid? JobId, decimal Advance, string? Note, string? CancelReason);

public static class Bookings
{
    public static IEndpointRouteBuilder MapBookings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Bookings").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/venues", async (AppDbContext db, CancellationToken ct) =>
            await db.Venues.AsNoTracking().Where(v => v.IsActive).OrderBy(v => v.Name).Select(v => new VenueDto(v.Id, v.Name, v.Capacity)).ToListAsync(ct));
        group.MapPost("/venues", AddVenueAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/bookings", ListAsync);
        group.MapGet("/bookings/{id:guid}", GetAsync);
        group.MapPost("/bookings", CreateAsync);
        group.MapPost("/bookings/{id:guid}/confirm", ConfirmAsync);
        group.MapPost("/bookings/{id:guid}/deposit", DepositAsync);
        group.MapPost("/bookings/{id:guid}/cancel", CancelAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/bookings/{id:guid}/settle", SettleAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));

    /// <summary>Morning and evening share a day; a full day takes both.</summary>
    public static bool Overlaps(BookingSlot a, BookingSlot b) => a == b || a == BookingSlot.FullDay || b == BookingSlot.FullDay;

    private static decimal Estimate(Booking b) => b.HallRent + (b.Guests * b.PlateRate);

    private static async Task<IResult> AddVenueAsync(VenueRequest request, AppDbContext db, CancellationToken ct)
    {
        if (request.Name?.Trim() is not { Length: >= 2 and <= 60 })
        {
            return Invalid("Name", "Name the hall (Main hall, Garden).");
        }

        if (!await db.Venues.AnyAsync(v => v.Id == request.Id, ct))
        {
            db.Venues.Add(new Venue { Id = request.Id, Name = request.Name.Trim(), Capacity = request.Capacity is > 0 ? request.Capacity : null });
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(new VenueDto(request.Id, request.Name.Trim(), request.Capacity));
    }

    private static async Task<IReadOnlyList<BookingDto>> ToDtosAsync(AppDbContext db, IQueryable<Booking> query, DateTimeOffset now, CancellationToken ct)
    {
        var list = await query.AsNoTracking().ToListAsync(ct);
        var venues = await db.Venues.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name, ct);
        var customerIds = list.Where(b => b.CustomerId != null).Select(b => b.CustomerId!.Value).Distinct().ToList();
        var names = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var jobIds = list.Where(b => b.JobId != null).Select(b => b.JobId!.Value).ToList();
        var advances = await db.JobEntries.AsNoTracking().Where(e => jobIds.Contains(e.JobId) && e.Kind == JobEntryKind.Advance)
            .GroupBy(e => e.JobId).Select(g => new { g.Key, Total = g.Sum(e => e.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return list.Select(b => new BookingDto(
            b.Id, b.Number, b.VenueId, venues.GetValueOrDefault(b.VenueId) ?? "", b.Date, b.Slot,
            b.Status == BookingStatus.Hold && b.HoldUntil <= now ? "expired" : b.Status.ToString().ToLowerInvariant(), b.HoldUntil,
            b.CustomerId, b.CustomerId is { } c ? names.GetValueOrDefault(c) : null, b.Occasion, b.Guests, b.PlateRate, b.HallRent, Estimate(b), b.Deposit,
            b.DepositTaken, b.DepositReturned, b.ActualGuests, b.JobId, b.JobId is { } j ? advances.GetValueOrDefault(j) : 0, b.Note, b.CancelReason)).ToList();
    }

    /// <summary>A month's bookings (yyyy-MM, default this month), by date and slot.</summary>
    private static async Task<IReadOnlyList<BookingDto>> ListAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        var first = DateOnly.TryParseExact(month, "yyyy-MM", out var m) ? m : new DateOnly(today.Year, today.Month, 1);
        var after = first.AddMonths(1);
        return await ToDtosAsync(db, db.Bookings.Where(b => b.Date >= first && b.Date < after).OrderBy(b => b.Date).ThenBy(b => b.Slot), clock.GetUtcNow(), ct);
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        (await ToDtosAsync(db, db.Bookings.Where(b => b.Id == id), clock.GetUtcNow(), ct)).SingleOrDefault() is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound();

    /// <summary>
    /// Whether another live booking (confirmed, or a hold not yet run out) takes this venue's date and slot. Run inside
    /// a transaction after locking the venue row, so two people booking the same evening can't both get it.
    /// </summary>
    private static async Task<string?> ClashAsync(AppDbContext db, Booking b, DateTimeOffset now, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM venues WHERE id = {b.VenueId} FOR UPDATE", ct);
        var taken = await db.Bookings.AsNoTracking()
            .Where(o => o.Id != b.Id && o.VenueId == b.VenueId && o.Date == b.Date
                && (o.Status == BookingStatus.Confirmed || o.Status == BookingStatus.Settled || (o.Status == BookingStatus.Hold && o.HoldUntil > now)))
            .Select(o => new { o.Slot, o.Number }).ToListAsync(ct);
        return taken.FirstOrDefault(o => Overlaps(o.Slot, b.Slot)) is { } clash ? $"This slot is already booked (booking #{clash.Number})." : null;
    }

    /// <summary>The job that carries a confirmed booking's money: its price is the estimate until the final bill.</summary>
    private static Job NewJob(Booking b, string venue) => new()
    {
        Number = 0, CustomerId = b.CustomerId, Title = $"{b.Occasion} · {venue} · {b.Date:dd MMM yyyy} {b.Slot}", Status = JobStatus.Order, Price = Estimate(b),
    };

    private static async Task<IResult> CreateAsync(BookingRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Bookings.AnyAsync(b => b.Id == request.Id, ct))
        {
            return await GetAsync(request.Id, db, clock, ct);
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
        var venue = await db.Venues.AsNoTracking().Where(v => v.Id == request.VenueId && v.IsActive).Select(v => v.Name).SingleOrDefaultAsync(ct);
        if (venue is null) errors["VenueId"] = ["Choose the hall."];
        if (request.Date < DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime)) errors["Date"] = ["Choose a date from today on."];
        if (request.Guests is < 0 or > 100_000) errors["Guests"] = ["Enter the guaranteed guests."];
        foreach (var (field, amount) in new[] { ("PlateRate", request.PlateRate), ("HallRent", request.HallRent), ("Deposit", request.Deposit) })
        {
            if (amount < 0 || (amount > 0 && !Money.IsValidAmount(amount))) errors[field] = ["Enter a valid amount."];
        }

        if (request.HallRent + (request.Guests * request.PlateRate) <= 0) errors["HallRent"] = ["Enter the hall rent or a per-plate rate."];
        if (!request.Hold && request.CustomerId is null) errors["CustomerId"] = ["Choose the customer to confirm a booking (the advance goes on their khata)."];
        if (request.CustomerId is { } customer && !await db.Customers.AnyAsync(c => c.Id == customer, ct)) errors["CustomerId"] = ["Customer not found."];
        if (request.Note?.Length > 300) errors["Note"] = ["Keep the note under 300 letters."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        var booking = new Booking
        {
            Id = request.Id, VenueId = request.VenueId, Date = request.Date, Slot = request.Slot ?? BookingSlot.Evening, CustomerId = request.CustomerId,
            Occasion = request.Occasion ?? Occasion.Wedding, Guests = request.Guests, PlateRate = request.PlateRate, HallRent = request.HallRent, Deposit = request.Deposit,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = request.Hold ? BookingStatus.Hold : BookingStatus.Confirmed,
            HoldUntil = request.Hold ? now.AddDays(Math.Clamp(request.HoldDays ?? 3, 1, 30)) : null,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await ClashAsync(db, booking, now, ct) is { } clash)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: clash);
        }

        booking.Number = (await db.Bookings.MaxAsync(b => (int?)b.Number, ct) ?? 0) + 1;
        if (booking.Status == BookingStatus.Confirmed)
        {
            await AttachJobAsync(db, booking, venue!, ct);
        }

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(booking.Id, db, clock, ct);
    }

    private static async Task AttachJobAsync(AppDbContext db, Booking booking, string venue, CancellationToken ct)
    {
        var job = NewJob(booking, venue);
        job.Number = (await db.Jobs.MaxAsync(j => (int?)j.Number, ct) ?? 0) + 1;
        db.Jobs.Add(job);
        booking.JobId = job.Id;
    }

    /// <summary>A hold becomes a booking: the slot is checked again (the hold may have run out and someone else taken it).</summary>
    private static async Task<IResult> ConfirmAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (booking.Status == BookingStatus.Hold)
        {
            if (booking.CustomerId is null)
            {
                return Invalid("CustomerId", "Choose the customer to confirm a booking.");
            }

            if (await ClashAsync(db, booking, clock.GetUtcNow(), ct) is { } clash)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: clash);
            }

            var venue = await db.Venues.Where(v => v.Id == booking.VenueId).Select(v => v.Name).SingleAsync(ct);
            (booking.Status, booking.HoldUntil) = (BookingStatus.Confirmed, null);
            await AttachJobAsync(db, booking, venue, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return await GetAsync(id, db, clock, ct);
    }

    /// <summary>Security deposit received: held for the shop to give back, so it is kept on the booking, not counted as sales.</summary>
    private static async Task<IResult> DepositAsync(Guid id, DepositRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id && b.Status == BookingStatus.Confirmed, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Amount <= 0 || !Money.IsValidAmount(request.Amount))
        {
            return Invalid("Amount", "Enter the deposit received.");
        }

        booking.DepositTaken += request.Amount;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, clock, ct);
    }

    /// <summary>
    /// Cancels a hold or booking. What the shop keeps from the advance (per its cancellation terms) goes on the khata as
    /// a cancellation charge; the rest of the advance shows on the khata as owed back to the customer.
    /// </summary>
    private static async Task<IResult> CancelAsync(Guid id, BookingCancelRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Settled)
        {
            return await GetAsync(id, db, clock, ct);
        }

        if (request.Keep < 0 || (request.Keep > 0 && !Money.IsValidAmount(request.Keep)))
        {
            return Invalid("Keep", "Enter what is kept as cancellation charge.");
        }

        if (request.Keep > 0)
        {
            if (booking.CustomerId is not { } customer)
            {
                return Invalid("Keep", "A hold with no customer has nothing to keep.");
            }

            var charge = await ledger.AddKhataEntryAsync(
                new KhataEntryRequest(Guid.CreateVersion7(), customer, KhataEntryType.Credit, request.Keep, null, $"Cancellation charge: booking #{booking.Number}", null),
                user.UserId()!.Value, ct, IsOwner(user));
            if (charge.Value is null)
            {
                return charge.ToHttp("");
            }
        }

        (booking.Status, booking.CancelReason) = (BookingStatus.Cancelled, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()[..Math.Min(200, request.Reason.Trim().Length)]);
        if (booking.JobId is { } jobId)
        {
            await db.Jobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, JobStatus.Cancelled), ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, clock, ct);
    }

    /// <summary>
    /// The final bill after the function: hall rent, plates for the larger of actual and guaranteed guests, extras, and any
    /// damage (taken from the security deposit first). The plates line carries the food and material cost, so the month's
    /// profit counts it; the job closes as delivered and the deposit left over is marked returned.
    /// </summary>
    private static async Task<IResult> SettleAsync(Guid id, SettleRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null)
        {
            return TypedResults.NotFound();
        }

        if (booking.Status == BookingStatus.Settled)
        {
            return await GetAsync(id, db, clock, ct);
        }

        if (booking.Status != BookingStatus.Confirmed || booking.JobId is not { } jobId)
        {
            return Invalid("Status", "Only a confirmed booking is settled.");
        }

        var extras = (request.Extras ?? []).Where(e => e.Amount != 0).ToList();
        if (request.ActualGuests is < 0 or > 100_000 || extras.Any(e => e.Name?.Trim() is not { Length: >= 2 } || e.Amount < 0 || (e.Amount > 0 && !Money.IsValidAmount(e.Amount)))
            || request.Damage < 0 || (request.Damage > 0 && !Money.IsValidAmount(request.Damage)) || request.PaidAmount < 0)
        {
            return Invalid("Extras", "Check the guests, extras and damage.");
        }

        var plates = Math.Max(request.ActualGuests, booking.Guests);
        var lines = new List<BillLineRequest>();
        if (booking.HallRent > 0) lines.Add(new BillLineRequest(null, "Hall rent", 1, Unit.Piece, booking.HallRent));
        if (plates > 0 && booking.PlateRate > 0) lines.Add(new BillLineRequest(null, $"Plates ({request.ActualGuests} came, {booking.Guests} guaranteed)", plates, Unit.Piece, booking.PlateRate));
        lines.AddRange(extras.Select(e => new BillLineRequest(null, e.Name!.Trim(), 1, Unit.Piece, e.Amount)));
        var fromDeposit = Math.Min(request.Damage, booking.DepositTaken);
        if (request.Damage > 0) lines.Add(new BillLineRequest(null, "Damage", 1, Unit.Piece, request.Damage));

        // Rooms taken for the function's guests (the baraat) go on the same final bill, and those stays close with it.
        var rooms = await db.Stays.Include(s => s.Charges).Where(s => s.BookingId == id && (s.Status == StayStatus.Booked || s.Status == StayStatus.CheckedIn)).ToListAsync(ct);
        var roomNames = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var tax = new List<(decimal? GstRate, string? HsnCode)>(lines.Select(_ => ((decimal?)null, (string?)null)));
        var registered = false;
        foreach (var stay in rooms.OrderBy(s => roomNames.GetValueOrDefault(s.RoomId)))
        {
            var (stayLines, stayTax) = await Stays.BillLinesAsync(db, stay, roomNames.GetValueOrDefault(stay.RoomId) ?? "", ct);
            lines.AddRange(stayLines);
            tax.AddRange(stayTax ?? stayLines.Select(_ => ((decimal?)null, (string?)null)).ToList());
            registered |= stayTax is not null;
        }

        var total = lines.Sum(l => Money.LineAmount(l.Quantity, l.UnitPrice));

        var result = await ledger.CreateBillAsync(
            new BillRequest(request.BillId, booking.CustomerId, lines, Math.Min(total, request.PaidAmount + fromDeposit), request.Mode, null),
            user.UserId()!.Value, ct, isOwner: IsOwner(user), taxSnapshot: registered ? tax : null);
        if (result.Value is not { } bill)
        {
            return result.ToHttp("");
        }

        // Food and material used carry on the plates line (cost per plate), or on the first line when there are no plates.
        var material = await db.JobEntries.Where(e => e.JobId == jobId && e.Kind == JobEntryKind.Material).SumAsync(e => e.Amount, ct);
        var hasPlates = plates > 0 && booking.PlateRate > 0;
        var costLineNo = hasPlates && booking.HallRent > 0 ? 2 : 1;
        var unitCost = hasPlates ? decimal.Round(material / plates, 2) : material;
        await db.BillLines.Where(l => l.BillId == bill.Id && l.LineNo == costLineNo).ExecuteUpdateAsync(s => s.SetProperty(l => l.UnitCost, unitCost), ct);
        await db.Jobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, JobStatus.Delivered).SetProperty(j => j.Price, total).SetProperty(j => j.BillId, bill.Id).SetProperty(j => j.DeliveredAt, clock.GetUtcNow()), ct);
        foreach (var stay in rooms)
        {
            (stay.Status, stay.BillId) = (StayStatus.CheckedOut, bill.Id);
        }

        (booking.Status, booking.ActualGuests, booking.DepositReturned) = (BookingStatus.Settled, request.ActualGuests, booking.DepositTaken - fromDeposit);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, clock, ct);
    }
}
