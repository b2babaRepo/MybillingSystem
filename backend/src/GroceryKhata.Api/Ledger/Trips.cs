using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A technician's field trip (to a village, a solar site): the cash advance taken, what was spent on travel, food and
/// stay, cash collected from customers, and the jobs it served. At settlement the spending goes into the kharcha book,
/// its cost is shared equally across the trip's jobs (so each job's profit counts its travel), and whatever the
/// technician still holds carries forward to their next trip, like a khata.
/// </summary>
public sealed class Trip : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public required string Technician { get; set; }

    public required string Place { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Cash the technician handed back at settlement (negative: the shop paid them what it owed).</summary>
    public decimal? Returned { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed class TripEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid TripId { get; set; }

    public TripEntryKind Kind { get; set; }

    public required string Description { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Cash collected: the customer it came from, and the khata payment made for it.</summary>
    public Guid? CustomerId { get; set; }

    public Guid? LedgerEntryId { get; set; }
}

/// <summary>The photo of a trip bill (bus ticket, dhaba, lodge), kept apart so lists stay light.</summary>
public sealed class TripPhoto : ITenantOwned
{
    public Guid EntryId { get; set; }

    public Guid ShopId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }
}

/// <summary>How a trip's cost is shared across its jobs.</summary>
public enum TripSplit
{
    Equal,
    /// <summary>By each job's price: a big job carries more of the travel.</summary>
    Value,
    /// <summary>By the km driven for each job.</summary>
    Distance,
}

public enum TripEntryKind
{
    /// <summary>Cash given to the technician.</summary>
    Advance,
    Travel,
    Food,
    Stay,
    Other,
    /// <summary>Cash taken from a customer on site; it still goes on that customer's bill or khata.</summary>
    Collected,
}

public sealed record TripRequest(Guid Id, string? Technician, string? Place);

public sealed record TripEntryRequest(Guid Id, TripEntryKind? Kind, string? Description, decimal Amount, Guid? CustomerId = null, string? PhotoDataUrl = null);

public sealed record TripJobRequest(Guid JobId);

public sealed record TripSettleRequest(decimal Returned, TripSplit? Split = null, IReadOnlyDictionary<Guid, decimal>? Km = null);

public sealed record TripEntryDto(Guid Id, TripEntryKind Kind, string Description, decimal Amount, DateTimeOffset OccurredAt, string? CustomerName = null, bool HasPhoto = false);

public sealed record TripJobDto(Guid Id, int Number, string Title, decimal Price = 0);

/// <summary>
/// <paramref name="BroughtForward"/>: what the technician still held from earlier trips. <paramref name="Holding"/>:
/// brought forward + advance + collected − spent (− returned, once settled), the cash they should have on them.
/// </summary>
public sealed record TripDto(
    Guid Id, int Number, string Technician, string Place, DateTimeOffset StartedAt, decimal Advance, decimal Spent, decimal Collected,
    decimal BroughtForward, decimal Holding, decimal? Returned, DateTimeOffset? SettledAt, IReadOnlyList<TripEntryDto> Entries, IReadOnlyList<TripJobDto> Jobs);

public static class Trips
{
    private static readonly TripEntryKind[] Spending = [TripEntryKind.Travel, TripEntryKind.Food, TripEntryKind.Stay, TripEntryKind.Other];

    public static IEndpointRouteBuilder MapTrips(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/trips").WithTags("Trips").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", OpenAsync);
        group.MapPost("/{id:guid}/entries", EntryAsync).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(700_000));
        group.MapGet("/photos/{entryId:guid}", PhotoAsync);
        group.MapPost("/{id:guid}/jobs", AttachAsync);
        group.MapPost("/{id:guid}/settle", SettleAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static (decimal Advance, decimal Spent, decimal Collected) Sums(IEnumerable<TripEntry> entries)
    {
        var list = entries.ToList();
        return (list.Where(e => e.Kind == TripEntryKind.Advance).Sum(e => e.Amount), list.Where(e => Spending.Contains(e.Kind)).Sum(e => e.Amount),
            list.Where(e => e.Kind == TripEntryKind.Collected).Sum(e => e.Amount));
    }

    private static decimal Carry(Trip trip, IEnumerable<TripEntry> entries)
    {
        var (advance, spent, collected) = Sums(entries);
        return advance + collected - spent - (trip.Returned ?? 0);
    }

    /// <summary>Open trips first, then settled ones, newest first.</summary>
    private static async Task<IReadOnlyList<TripDto>> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var trips = await db.Trips.AsNoTracking().OrderBy(t => t.SettledAt != null).ThenByDescending(t => t.Number).Take(200).ToListAsync(ct);
        return await ToDtosAsync(db, trips, ct);
    }

    private static async Task<IReadOnlyList<TripDto>> ToDtosAsync(AppDbContext db, IReadOnlyList<Trip> trips, CancellationToken ct)
    {
        var names = trips.Select(t => t.Technician).Distinct().ToList();
        // Every settled trip of these technicians, to carry their balance forward.
        var history = await db.Trips.AsNoTracking().Where(t => names.Contains(t.Technician)).ToListAsync(ct);
        var ids = history.Select(t => t.Id).Union(trips.Select(t => t.Id)).ToList();
        var entries = (await db.TripEntries.AsNoTracking().Where(e => ids.Contains(e.TripId)).OrderBy(e => e.OccurredAt).ToListAsync(ct)).ToLookup(e => e.TripId);
        var tripIds = trips.Select(t => t.Id).ToList();
        var jobs = (await db.Jobs.AsNoTracking().Where(j => j.TripId != null && tripIds.Contains(j.TripId.Value)).OrderBy(j => j.Number)
            .Select(j => new { j.TripId, j.Id, j.Number, j.Title, j.Price }).ToListAsync(ct)).ToLookup(j => j.TripId!.Value);
        var entryIds = trips.SelectMany(t => entries[t.Id]).Select(e => e.Id).ToList();
        var photos = (await db.TripPhotos.AsNoTracking().Where(p => entryIds.Contains(p.EntryId)).Select(p => p.EntryId).ToListAsync(ct)).ToHashSet();
        var customerIds = trips.SelectMany(t => entries[t.Id]).Where(e => e.CustomerId != null).Select(e => e.CustomerId!.Value).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return trips.Select(t =>
        {
            var broughtForward = history.Where(h => h.Technician == t.Technician && h.SettledAt != null && h.Number < t.Number).Sum(h => Carry(h, entries[h.Id]));
            var (advance, spent, collected) = Sums(entries[t.Id]);
            return new TripDto(t.Id, t.Number, t.Technician, t.Place, t.StartedAt, advance, spent, collected, broughtForward,
                broughtForward + Carry(t, entries[t.Id]), t.Returned, t.SettledAt,
                entries[t.Id].Select(e => new TripEntryDto(e.Id, e.Kind, e.Description, e.Amount, e.OccurredAt,
                    e.CustomerId is { } c ? customers.GetValueOrDefault(c) : null, photos.Contains(e.Id))).ToList(),
                jobs[t.Id].Select(j => new TripJobDto(j.Id, j.Number, j.Title, j.Price)).ToList());
        }).ToList();
    }

    private static async Task<IResult> OneAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.Trips.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct) is { } trip
            ? TypedResults.Ok((await ToDtosAsync(db, [trip], ct))[0])
            : TypedResults.NotFound();

    private static async Task<IResult> OpenAsync(TripRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Trips.AnyAsync(t => t.Id == request.Id, ct)) return await OneAsync(db, request.Id, ct);
        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (request.Technician?.Trim() is not { Length: >= 2 and <= 60 } technician) return Invalid("Technician", "Write the technician's name.");
        if (request.Place?.Trim() is not { Length: >= 2 and <= 80 } place) return Invalid("Place", "Write where the trip is to.");

        db.Trips.Add(new Trip
        {
            Id = request.Id, Number = (await db.Trips.MaxAsync(t => (int?)t.Number, ct) ?? 0) + 1, Technician = technician, Place = place,
            StartedAt = clock.GetUtcNow(), CreatedByUserId = user.UserId()!.Value,
        });
        await db.SaveChangesAsync(ct);
        return await OneAsync(db, request.Id, ct);
    }

    private static Task<Trip?> OpenTripAsync(AppDbContext db, Guid id, CancellationToken ct) => db.Trips.SingleOrDefaultAsync(t => t.Id == id && t.SettledAt == null, ct);

    /// <summary>
    /// An advance, a bill (travel, food, stay, other, with its photo if taken) or cash collected on site. Cash collected
    /// from a khata customer is a payment on their khata at once; it reaches the drawer when the technician hands it in.
    /// Safe to repeat.
    /// </summary>
    private static async Task<IResult> EntryAsync(Guid id, TripEntryRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.TripEntries.AnyAsync(e => e.Id == request.Id, ct)) return await OneAsync(db, id, ct);
        if (await OpenTripAsync(db, id, ct) is not { } trip) return TypedResults.NotFound();
        if (request.Kind is not { } kind) return Invalid("Kind", "Advance, travel, food, stay, other or collected.");
        if (request.Amount <= 0 || !Money.IsValidAmount(request.Amount)) return Invalid("Amount", "Enter the amount.");
        if (request.CustomerId is not null && kind != TripEntryKind.Collected) return Invalid("CustomerId", "Only cash collected comes from a customer.");
        var photo = request.PhotoDataUrl is null ? null : Photos.Decode(request.PhotoDataUrl, 400_000);
        if (request.PhotoDataUrl is not null && photo is null) return Invalid("PhotoDataUrl", "Send a WebP, JPEG or PNG photo under 400 KB.");
        var description = request.Description?.Trim() is { Length: > 0 } d ? d[..Math.Min(80, d.Length)] : kind.ToString();

        var entry = new TripEntry { Id = request.Id, TripId = id, Kind = kind, Description = description, Amount = request.Amount, OccurredAt = clock.GetUtcNow(), CustomerId = request.CustomerId };
        if (request.CustomerId is { } customer)
        {
            var paid = await ledger.AddKhataEntryAsync(
                new KhataEntryRequest(Guid.CreateVersion7(), customer, KhataEntryType.Payment, request.Amount, PaymentMode.Cash, $"Collected on trip #{trip.Number} by {trip.Technician}", null),
                user.UserId()!.Value, ct, user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner)));
            if (paid.Value is not { } made) return paid.ToHttp("");
            entry.LedgerEntryId = made.Entry.Id;
            // The cash is with the technician, not in the drawer: the cash book counts it when it is handed in.
            await db.LedgerEntries.Where(e => e.Id == made.Entry.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.TripId, id), ct);
        }

        db.TripEntries.Add(entry);
        if (photo is var (type, data)) db.TripPhotos.Add(new TripPhoto { EntryId = entry.Id, ContentType = type, Data = data });
        await db.SaveChangesAsync(ct);
        return await OneAsync(db, id, ct);
    }

    private static async Task<IResult> PhotoAsync(Guid entryId, AppDbContext db, CancellationToken ct) =>
        await db.TripPhotos.AsNoTracking().SingleOrDefaultAsync(p => p.EntryId == entryId, ct) is { } photo
            ? TypedResults.File(photo.Data, photo.ContentType)
            : TypedResults.NotFound();

    /// <summary>
    /// Each job's share of <paramref name="spent"/>: by weight (price, or km), equally when no weights are given or
    /// they are all 0. Paise left over go on the first job, so the shares add up exactly.
    /// </summary>
    public static IReadOnlyList<decimal> Shares(decimal spent, IReadOnlyList<decimal> weights)
    {
        var total = weights.Sum();
        var w = total > 0 ? weights : weights.Select(_ => 1m).ToList();
        total = w.Sum();
        var shares = w.Select(x => Math.Floor(spent * x / total * 100) / 100).ToList();
        if (shares.Count > 0) shares[0] += spent - shares.Sum();
        return shares;
    }

    /// <summary>Puts a job on this trip; its share of the trip's cost lands on it at settlement.</summary>
    private static async Task<IResult> AttachAsync(Guid id, TripJobRequest request, AppDbContext db, CancellationToken ct)
    {
        if (await OpenTripAsync(db, id, ct) is null) return TypedResults.NotFound();
        if (await db.Jobs.SingleOrDefaultAsync(j => j.Id == request.JobId, ct) is not { } job) return Invalid("JobId", "Job not found.");
        job.TripId = id;
        await db.SaveChangesAsync(ct);
        return await OneAsync(db, id, ct);
    }

    /// <summary>
    /// Closes the trip: each kind of spending becomes a kharcha line, each job gets an equal share of the total as its
    /// "other" cost, and the cash the technician hands back is noted. What they still hold carries to their next trip.
    /// </summary>
    private static async Task<IResult> SettleAsync(Guid id, TripSettleRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var trip = await db.Trips.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (trip is null) return TypedResults.NotFound();
        if (trip.SettledAt is not null) return await OneAsync(db, id, ct);
        if (decimal.Round(request.Returned, 2) != request.Returned || Math.Abs(request.Returned) > Money.MaxAmount) return Invalid("Returned", "Enter the cash handed back.");
        var split = request.Split ?? TripSplit.Equal;
        if (request.Km?.Values.Any(k => k < 0 || k > 10_000) == true) return Invalid("Km", "Enter the km for each job.");

        var (now, userId) = (clock.GetUtcNow(), user.UserId()!.Value);
        var entries = await db.TripEntries.AsNoTracking().Where(e => e.TripId == id).ToListAsync(ct);
        var jobs = await db.Jobs.AsNoTracking().Where(j => j.TripId == id).OrderBy(j => j.Number).ToListAsync(ct);
        foreach (var kind in entries.Where(e => Spending.Contains(e.Kind)).GroupBy(e => e.Kind))
        {
            db.Expenses.Add(new Expense
            {
                OccurredAt = now, Amount = kind.Sum(e => e.Amount), Category = kind.Key == TripEntryKind.Travel ? ExpenseCategory.Transport : ExpenseCategory.Other,
                Note = $"Trip #{trip.Number} ({trip.Technician}, {trip.Place}): {kind.Key.ToString().ToLowerInvariant()}", Mode = PaymentMode.Cash, CreatedByUserId = userId,
                TripId = trip.Id, // paid from the technician's advance, not the drawer
            });
        }

        var spent = Sums(entries).Spent;
        if (jobs.Count > 0 && spent > 0)
        {
            var weights = split switch
            {
                TripSplit.Value => jobs.Select(j => j.Price).ToList(),
                TripSplit.Distance => jobs.Select(j => request.Km?.GetValueOrDefault(j.Id) ?? 0).ToList(),
                _ => jobs.Select(_ => 1m).ToList(),
            };
            var shares = Shares(spent, weights);
            for (var i = 0; i < jobs.Count; i++)
            {
                if (shares[i] <= 0) continue;
                db.JobEntries.Add(new JobEntry
                {
                    JobId = jobs[i].Id, Kind = JobEntryKind.Other, Description = $"Trip #{trip.Number} share", Amount = shares[i], OccurredAt = now, CreatedByUserId = userId,
                });
            }
        }

        (trip.Returned, trip.SettledAt) = (request.Returned, now);
        await db.SaveChangesAsync(ct);
        return await OneAsync(db, id, ct);
    }
}
