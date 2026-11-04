using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// An annual maintenance contract (AMC): a fee for a set number of service visits over some months, for one customer's
/// unit (inverter and battery, pump, AC). The fee is an ordinary bill; each visit is a service job tied to the contract,
/// so its parts come off stock and it closes without a bill. Visits fall due at even gaps through the contract, and
/// contracts near their end are listed for a renewal reminder.
/// </summary>
public sealed class Amc : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>What it covers ("Inverter + battery, yearly").</summary>
    public required string Title { get; set; }

    public string? SerialNo { get; set; }

    public DateOnly StartOn { get; set; }

    public int Months { get; set; }

    public int Visits { get; set; }

    public decimal Fee { get; set; }

    /// <summary>The fee's bill.</summary>
    public Guid BillId { get; set; }

    /// <summary>Set on the old contract when it is renewed, so it leaves the renewal list.</summary>
    public Guid? RenewedById { get; set; }
}

public sealed record AmcRequest(Guid Id, Guid CustomerId, string? Title, string? SerialNo, DateOnly? StartOn, int Months, int Visits, decimal Fee, decimal PaidAmount, PaymentMode? Mode);

public sealed record AmcRenewRequest(Guid Id, decimal? Fee, decimal PaidAmount, PaymentMode? Mode);

public sealed record AmcVisitRequest(Guid JobId);

/// <summary><paramref name="NextDue"/>: the next visit's date, null once all are done. <paramref name="EndsOn"/>: the last day covered.</summary>
public sealed record AmcDto(
    Guid Id, int Number, Guid CustomerId, string CustomerName, string? CustomerPhone, string Title, string? SerialNo, DateOnly StartOn, DateOnly EndsOn,
    int Months, int Visits, int VisitsDone, DateOnly? NextDue, decimal Fee, Guid BillId, bool Renewed);

public static class Amcs
{
    public static IEndpointRouteBuilder MapAmcs(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/amc").WithTags("AMC").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/{id:guid}/visit", VisitAsync);
        group.MapPost("/{id:guid}/renew", RenewAsync);
        return app;
    }

    private static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);

    internal static DateOnly EndsOn(Amc a) => a.StartOn.AddMonths(a.Months).AddDays(-1);

    /// <summary>Visit k (1-based) falls at k/visits of the way through, the last one a little before the end.</summary>
    internal static DateOnly DueOn(Amc a, int k) => a.StartOn.AddDays((EndsOn(a).DayNumber - a.StartOn.DayNumber) * k / (a.Visits + 1));

    /// <summary>Contracts not yet renewed, the next visit due first.</summary>
    private static async Task<IReadOnlyList<AmcDto>> ListAsync(AppDbContext db, CancellationToken ct) =>
        (await ToDtosAsync(db, db.Amcs.AsNoTracking().Where(a => a.RenewedById == null), ct))
            .OrderBy(a => a.NextDue ?? a.EndsOn).ToList();

    private static async Task<IReadOnlyList<AmcDto>> ToDtosAsync(AppDbContext db, IQueryable<Amc> query, CancellationToken ct)
    {
        var list = await query.Take(500).ToListAsync(ct);
        var ids = list.Select(a => a.Id).ToList();
        var done = await db.Jobs.AsNoTracking().Where(j => j.AmcId != null && ids.Contains(j.AmcId.Value) && j.Status != JobStatus.Cancelled)
            .GroupBy(j => j.AmcId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var customerIds = list.Select(a => a.CustomerId).Distinct().ToList();
        var people = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        return list.Select(a =>
        {
            var visitsDone = done.GetValueOrDefault(a.Id);
            return new AmcDto(a.Id, a.Number, a.CustomerId, people.GetValueOrDefault(a.CustomerId)?.Name ?? "", people.GetValueOrDefault(a.CustomerId)?.Phone, a.Title,
                a.SerialNo, a.StartOn, EndsOn(a), a.Months, a.Visits, visitsDone, visitsDone < a.Visits ? DueOn(a, visitsDone + 1) : null, a.Fee, a.BillId, a.RenewedById != null);
        }).ToList();
    }

    private static async Task<IResult> OneAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        (await ToDtosAsync(db, db.Amcs.AsNoTracking().Where(a => a.Id == id), ct)).SingleOrDefault() is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound();

    /// <summary>Signs a contract and bills its fee to the customer (paid now, or on their khata).</summary>
    private static async Task<IResult> CreateAsync(AmcRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Amcs.AnyAsync(a => a.Id == request.Id, ct)) return await OneAsync(db, request.Id, ct);
        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct)) return Invalid("CustomerId", "Choose the customer.");
        if (request.Title?.Trim() is not { Length: >= 2 and <= 120 } title) return Invalid("Title", "Say what the contract covers.");
        if (request.Months is < 1 or > 60) return Invalid("Months", "Between 1 and 60 months.");
        if (request.Visits is < 1 or > 52) return Invalid("Visits", "Between 1 and 52 visits.");
        if (!Money.IsValidAmount(request.Fee)) return Invalid("Fee", "Enter the contract fee.");
        var serial = string.IsNullOrWhiteSpace(request.SerialNo) ? null : Serials.Clean(request.SerialNo);
        if (serial?.Length > 40) return Invalid("SerialNo", "A serial number is at most 40 letters.");

        return await SignAsync(db, ledger, user, new Amc
        {
            Id = request.Id, CustomerId = request.CustomerId, Title = title, SerialNo = serial, StartOn = request.StartOn ?? Today(clock),
            Months = request.Months, Visits = request.Visits, Fee = request.Fee,
        }, request.PaidAmount, request.Mode, null, ct);
    }

    private static async Task<IResult> SignAsync(AppDbContext db, LedgerService ledger, ClaimsPrincipal user, Amc amc, decimal paid, PaymentMode? mode, Amc? renewing, CancellationToken ct)
    {
        amc.Number = (await db.Amcs.MaxAsync(a => (int?)a.Number, ct) ?? 0) + 1;
        var bill = await ledger.CreateBillAsync(
            new BillRequest(Guid.CreateVersion7(), amc.CustomerId, [new BillLineRequest(null, $"AMC #{amc.Number}: {amc.Title} ({amc.StartOn:dd MMM yyyy} to {EndsOn(amc):dd MMM yyyy})", 1, Unit.Piece, amc.Fee)],
                paid, mode, null),
            user.UserId()!.Value, ct, isOwner: IsOwner(user));
        if (bill.Value is not { } made)
        {
            return bill.ToHttp("");
        }

        amc.BillId = made.Id;
        db.Amcs.Add(amc);
        if (renewing is not null)
        {
            renewing.RenewedById = amc.Id;
        }

        await db.SaveChangesAsync(ct);
        return await OneAsync(db, amc.Id, ct);
    }

    /// <summary>A visit under the contract: opens a service job tied to it (parts and wages go on the job; it closes free).</summary>
    private static async Task<IResult> VisitAsync(Guid id, AmcVisitRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(j => j.Id == request.JobId, ct)) return await OneAsync(db, id, ct);
        if (await db.Amcs.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) is not { } amc) return TypedResults.NotFound();
        var done = await db.Jobs.CountAsync(j => j.AmcId == id && j.Status != JobStatus.Cancelled, ct);
        if (done >= amc.Visits) return Invalid("Visits", "All visits in this contract are used. Charge the visit, or renew the contract.");
        if (Today(clock) > EndsOn(amc)) return Invalid("Visits", "This contract has ended. Renew it first.");

        db.Jobs.Add(new Job
        {
            Id = request.JobId, Number = (await db.Jobs.MaxAsync(j => (int?)j.Number, ct) ?? 0) + 1, CustomerId = amc.CustomerId, Status = JobStatus.Order,
            Title = $"AMC #{amc.Number} visit {done + 1} of {amc.Visits}: {amc.Title}", SerialNo = amc.SerialNo, AmcId = amc.Id,
        });
        await db.SaveChangesAsync(ct);
        return await OneAsync(db, id, ct);
    }

    /// <summary>Renews from the day after the old contract ends (or today, if it has lapsed), on the same terms; a new bill.</summary>
    private static async Task<IResult> RenewAsync(Guid id, AmcRenewRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Amcs.AnyAsync(a => a.Id == request.Id, ct)) return await OneAsync(db, request.Id, ct);
        if (await db.Amcs.SingleOrDefaultAsync(a => a.Id == id, ct) is not { } old) return TypedResults.NotFound();
        if (old.RenewedById is not null) return Invalid("Id", "This contract is already renewed.");
        var fee = request.Fee ?? old.Fee;
        if (!Money.IsValidAmount(fee)) return Invalid("Fee", "Enter the contract fee.");

        var next = EndsOn(old).AddDays(1);
        return await SignAsync(db, ledger, user, new Amc
        {
            Id = request.Id, CustomerId = old.CustomerId, Title = old.Title, SerialNo = old.SerialNo, StartOn = next < Today(clock) ? Today(clock) : next,
            Months = old.Months, Visits = old.Visits, Fee = fee,
        }, request.PaidAmount, request.Mode, old, ct);
    }
}
