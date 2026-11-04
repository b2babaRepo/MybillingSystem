using System.Globalization;
using System.Security.Claims;
using System.Text;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A machine the workshop owns (panel saw, planer, edge bander). It wears out a little every year: that wear
/// (depreciation, written-down value) is a cost in the year's profit and in each job's share of running costs.
/// </summary>
public sealed class Machine : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public DateOnly BoughtOn { get; set; }

    public decimal Cost { get; set; }

    /// <summary>The yearly rate on the written-down value: 15% for plant and machinery under income tax. A CA should confirm.</summary>
    public decimal RatePercent { get; set; } = 15;

    /// <summary>Sold or scrapped: no wear from that year on.</summary>
    public DateOnly? DisposedOn { get; set; }
}

/// <summary>
/// Goods sent to an outside karigar to work on (cutting, polishing, upholstery) and what came back. GST asks for this
/// register on form ITC-04, and inputs not back within a year count as supplied by the shop.
/// </summary>
public sealed class KarigarChallan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The shop's own running challan number.</summary>
    public int Number { get; set; }

    public required string Karigar { get; set; }

    /// <summary>The karigar's GSTIN, when registered; ITC-04 asks for it.</summary>
    public string? KarigarGstin { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The item's cost when sent: the value ITC-04 shows.</summary>
    public decimal UnitCost { get; set; }

    public Guid? JobId { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset SentAt { get; set; }

    public decimal? ReceivedQuantity { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }
}

public sealed record MachineRequest(Guid Id, string? Name, DateOnly? BoughtOn, decimal Cost, decimal? RatePercent);

public sealed record DisposeRequest(DateOnly? On);

public sealed record MachineDto(Guid Id, string Name, DateOnly BoughtOn, decimal Cost, decimal RatePercent, DateOnly? DisposedOn, decimal ThisYear, decimal BookValue);

public sealed record MachinesDto(string FinancialYear, decimal ThisYear, decimal PerMonth, IReadOnlyList<MachineDto> Machines);

public sealed record KarigarChallanRequest(Guid Id, string? Karigar, string? KarigarGstin, Guid ProductId, decimal Quantity, Guid? JobId, string? Note);

public sealed record ReceiveRequest(decimal Quantity);

public sealed record KarigarChallanDto(
    Guid Id, int Number, string Karigar, string? KarigarGstin, Guid ProductId, string ProductName, Unit Unit, decimal Quantity, decimal UnitCost,
    Guid? JobId, string? Note, DateTimeOffset SentAt, decimal? ReceivedQuantity, DateTimeOffset? ReceivedAt, bool OverAYear);

/// <summary>One karigar's wage payments this financial year, against the contractor TDS limits (section 194C).</summary>
public sealed record TdsPayeeDto(string Karigar, decimal Total, decimal Largest, int Payments, string? Alert);

public sealed record TdsDto(string FinancialYear, decimal SingleLimit, decimal YearLimit, IReadOnlyList<TdsPayeeDto> Karigars);

public static class Workshop
{
    /// <summary>
    /// Contractor TDS (194C): due when one payment passes ₹30,000 or the year's payments to one contractor pass
    /// ₹1,00,000, at 1% (individual or HUF contractor) or 2% (others). Only shops already liable to deduct TDS
    /// (broadly, an individual's business over ₹1 crore last year) must; the app warns, the CA decides. The new
    /// Income Tax Act 2025 renumbers it from 2026-27; the limits are as found, to be confirmed.
    /// </summary>
    public const decimal TdsSingleLimit = 30_000m;

    public const decimal TdsYearLimit = 100_000m;

    /// <summary>Inputs sent for job work must come back within a year, or GST treats them as supplied by the shop.</summary>
    public static readonly TimeSpan ReturnWithin = TimeSpan.FromDays(365);

    public static IEndpointRouteBuilder MapWorkshop(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/workshop").WithTags("Workshop").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/machines", MachinesAsync);
        group.MapPost("/machines", AddMachineAsync);
        group.MapPost("/machines/{id:guid}/dispose", DisposeAsync);
        group.MapGet("/karigar", ChallansAsync);
        group.MapPost("/karigar", SendAsync);
        group.MapPost("/karigar/{id:guid}/receive", ReceiveAsync);
        group.MapGet("/itc04.csv", Itc04Async);
        group.MapGet("/tds", TdsAsync);
        return app;
    }

    /// <summary>The financial year (April to March) a date falls in, by its starting year.</summary>
    private static int FinancialYearOf(DateOnly date) => date.Month >= 4 ? date.Year : date.Year - 1;

    /// <summary>
    /// A machine's wear in the financial year starting in <paramref name="startYear"/>, and its value after it, on the
    /// written-down value: the year it is bought it wears half if used under 180 days (bought after early October).
    /// </summary>
    public static (decimal Wear, decimal BookValue) WearIn(Machine m, int startYear)
    {
        var value = m.Cost;
        var rate = m.RatePercent / 100m;
        var first = FinancialYearOf(m.BoughtOn);
        var disposedYear = m.DisposedOn is { } d ? FinancialYearOf(d) : int.MaxValue;
        var wear = 0m;
        for (var year = first; year <= startYear; year++)
        {
            if (year >= disposedYear)
            {
                return (0, value);
            }

            var yearEnd = new DateOnly(year + 1, 3, 31);
            var factor = year == first && yearEnd.DayNumber - m.BoughtOn.DayNumber + 1 < 180 ? 0.5m : 1m;
            wear = decimal.Round(value * rate * factor, 2, MidpointRounding.AwayFromZero);
            value -= wear;
        }

        return startYear < first ? (0, m.Cost) : (wear, value);
    }

    /// <summary>All machines' wear in a financial year: a cost in that year's profit and loss.</summary>
    public static async Task<decimal> WearForYearAsync(AppDbContext db, int startYear, CancellationToken ct) =>
        (await db.Machines.AsNoTracking().ToListAsync(ct)).Sum(m => WearIn(m, startYear).Wear);

    private static int ThisYear(TimeProvider clock) => FinancialYearOf(DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime));

    private static async Task<MachinesDto> MachinesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var year = ThisYear(clock);
        var machines = (await db.Machines.AsNoTracking().OrderBy(m => m.BoughtOn).ToListAsync(ct))
            .Select(m => { var (wear, value) = WearIn(m, year); return new MachineDto(m.Id, m.Name, m.BoughtOn, m.Cost, m.RatePercent, m.DisposedOn, wear, value); })
            .ToList();
        var total = machines.Sum(m => m.ThisYear);
        return new MachinesDto($"{year}-{(year + 1) % 100:D2}", total, decimal.Round(total / 12, 2), machines);
    }

    private static async Task<IResult> AddMachineAsync(MachineRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.Machines.AnyAsync(m => m.Id == request.Id, ct))
        {
            var errors = new Dictionary<string, string[]>();
            if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
            if (request.Name?.Trim() is not { Length: >= 2 and <= 80 }) errors["Name"] = ["Name the machine."];
            if (request.BoughtOn is not { } on || on > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) errors["BoughtOn"] = ["When was it bought?"];
            if (request.Cost <= 0 || !Money.IsValidAmount(request.Cost)) errors["Cost"] = ["Enter what it cost."];
            if (request.RatePercent is { } r && (r <= 0 || r > 100)) errors["RatePercent"] = ["A yearly rate between 1 and 100%."];
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            db.Machines.Add(new Machine { Id = request.Id, Name = request.Name!.Trim(), BoughtOn = request.BoughtOn!.Value, Cost = request.Cost, RatePercent = request.RatePercent ?? 15 });
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(await MachinesAsync(db, clock, ct));
    }

    private static async Task<IResult> DisposeAsync(Guid id, DisposeRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var machine = await db.Machines.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (machine is null)
        {
            return TypedResults.NotFound();
        }

        machine.DisposedOn ??= request.On ?? DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await MachinesAsync(db, clock, ct));
    }

    private static async Task<IReadOnlyList<KarigarChallanDto>> ListAsync(AppDbContext db, TimeProvider clock, IQueryable<KarigarChallan> query, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await (from c in query.AsNoTracking()
                      join p in db.Products.AsNoTracking() on c.ProductId equals p.Id
                      orderby c.ReceivedAt != null, c.Number descending
                      select new KarigarChallanDto(c.Id, c.Number, c.Karigar, c.KarigarGstin, c.ProductId, p.Name, p.Unit, c.Quantity, c.UnitCost, c.JobId, c.Note,
                          c.SentAt, c.ReceivedQuantity, c.ReceivedAt, c.ReceivedAt == null && c.SentAt < now - ReturnWithin))
            .Take(500).ToListAsync(ct);
    }

    /// <summary>Goods out with karigars first (oldest overdue ones stand out), then those back.</summary>
    private static Task<IReadOnlyList<KarigarChallanDto>> ChallansAsync(AppDbContext db, TimeProvider clock, CancellationToken ct) => ListAsync(db, clock, db.KarigarChallans, ct);

    /// <summary>Sends goods to a karigar on a numbered challan: they leave the shelf until they come back.</summary>
    private static async Task<IResult> SendAsync(KarigarChallanRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.KarigarChallans.AnyAsync(c => c.Id == request.Id, ct))
        {
            var errors = new Dictionary<string, string[]>();
            if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
            if (request.Karigar?.Trim() is not { Length: >= 2 and <= 80 }) errors["Karigar"] = ["Write the karigar's name."];
            if (!string.IsNullOrWhiteSpace(request.KarigarGstin) && !GstExport.IsValidGstin(request.KarigarGstin.Trim().ToUpperInvariant())) errors["KarigarGstin"] = ["This GSTIN doesn't look right."];
            if (request.Quantity <= 0 || request.Quantity > Money.MaxQuantity || decimal.Round(request.Quantity, 3) != request.Quantity) errors["Quantity"] = ["Enter how much is sent."];
            if (request.Note?.Length > 200) errors["Note"] = ["Keep the note under 200 letters."];
            var product = await db.Products.AsNoTracking().Where(p => p.Id == request.ProductId).Select(p => new { p.CostPrice }).SingleOrDefaultAsync(ct);
            if (product is null) errors["ProductId"] = ["Item not found."];
            if (request.JobId is { } job && !await db.Jobs.AnyAsync(j => j.Id == job, ct)) errors["JobId"] = ["Job not found."];
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var now = clock.GetUtcNow();
            var number = (await db.KarigarChallans.MaxAsync(c => (int?)c.Number, ct) ?? 0) + 1;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.KarigarChallans.Add(new KarigarChallan
            {
                Id = request.Id, Number = number, Karigar = request.Karigar!.Trim(), KarigarGstin = string.IsNullOrWhiteSpace(request.KarigarGstin) ? null : request.KarigarGstin.Trim().ToUpperInvariant(),
                ProductId = request.ProductId, Quantity = request.Quantity, UnitCost = product!.CostPrice ?? 0, JobId = request.JobId,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), SentAt = now,
            });
            db.StockMovements.Add(new StockMovement
            {
                ProductId = request.ProductId, Kind = StockMovementKind.SentToKarigar, Quantity = -request.Quantity, CostPrice = product.CostPrice,
                Note = $"Karigar challan #{number}", OccurredAt = now, CreatedByUserId = user.UserId()!.Value,
            });
            await db.SaveChangesAsync(ct);
            await db.Products.Where(p => p.Id == request.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - request.Quantity), ct);
            await transaction.CommitAsync(ct);
        }

        return TypedResults.Ok((await ListAsync(db, clock, db.KarigarChallans.Where(c => c.Id == request.Id), ct)).Single());
    }

    /// <summary>The goods come back (worked on): they go back on the shelf. Less than was sent means the rest was used up.</summary>
    private static async Task<IResult> ReceiveAsync(Guid id, ReceiveRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var challan = await db.KarigarChallans.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (challan is null)
        {
            return TypedResults.NotFound();
        }

        if (challan.ReceivedAt is null)
        {
            if (request.Quantity < 0 || request.Quantity > challan.Quantity || decimal.Round(request.Quantity, 3) != request.Quantity)
            {
                return Invalid("Quantity", "Enter how much came back, up to what was sent.");
            }

            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            (challan.ReceivedQuantity, challan.ReceivedAt) = (request.Quantity, now);
            if (request.Quantity > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = challan.ProductId, Kind = StockMovementKind.BackFromKarigar, Quantity = request.Quantity, CostPrice = challan.UnitCost,
                    Note = $"Karigar challan #{challan.Number}", OccurredAt = now, CreatedByUserId = user.UserId()!.Value,
                });
            }

            await db.SaveChangesAsync(ct);
            if (request.Quantity > 0)
            {
                await db.Products.Where(p => p.Id == challan.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + request.Quantity), ct);
            }

            await transaction.CommitAsync(ct);
        }

        return TypedResults.Ok((await ListAsync(db, clock, db.KarigarChallans.Where(c => c.Id == id), ct)).Single());
    }

    /// <summary>The karigar a wage line is for: the name before the first comma ("Ramesh, 8 days" → Ramesh).</summary>
    public static string PayeeOf(string description) => description.Split(',')[0].Trim();

    /// <summary>Karigar wages paid on jobs this financial year, by karigar, with a warning near or past the TDS limits.</summary>
    private static async Task<TdsDto> TdsAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var year = ThisYear(clock);
        var start = new DateTimeOffset(new DateTime(year, 4, 1), IndiaTime.Offset).ToUniversalTime();
        var lines = await db.JobEntries.AsNoTracking().Where(e => e.Kind == JobEntryKind.Labour && e.OccurredAt >= start)
            .Select(e => new { e.Description, e.Amount }).ToListAsync(ct);
        var karigars = lines.GroupBy(l => PayeeOf(l.Description), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var (total, largest) = (g.Sum(l => l.Amount), g.Max(l => l.Amount));
                var alert = largest > TdsSingleLimit ? "single" : total > TdsYearLimit ? "year" : total >= TdsYearLimit * 0.8m ? "near" : null;
                return new TdsPayeeDto(g.First().Description.Split(',')[0].Trim(), total, largest, g.Count(), alert);
            })
            .OrderByDescending(k => k.Alert is not null).ThenByDescending(k => k.Total).ToList();
        return new TdsDto($"{year}-{(year + 1) % 100:D2}", TdsSingleLimit, TdsYearLimit, karigars);
    }

    /// <summary>
    /// The ITC-04 working sheet for a period: each challan sent in it, and each one received back in it, with the
    /// karigar, item, quantity and value. For the CA to file from; the form's own layout is theirs to fill.
    /// </summary>
    private static async Task<IResult> Itc04Async(DateOnly? from, DateOnly? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var year = ThisYear(clock);
        var start = new DateTimeOffset((from ?? new DateOnly(year, 4, 1)).ToDateTime(TimeOnly.MinValue), IndiaTime.Offset);
        var end = new DateTimeOffset((to ?? new DateOnly(year + 1, 3, 31)).ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).AddDays(1);
        // Postgres takes UTC times only.
        (start, end) = (start.ToUniversalTime(), end.ToUniversalTime());
        var rows = await ListAsync(db, clock, db.KarigarChallans.Where(c => (c.SentAt >= start && c.SentAt < end) || (c.ReceivedAt >= start && c.ReceivedAt < end)), ct);
        static string Cell(string? s) => s is null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
        static string Num(decimal d) => d.ToString("0.###", CultureInfo.InvariantCulture);
        var csv = new StringBuilder("Challan,Karigar,Karigar GSTIN,Item,Unit,Sent on,Quantity sent,Value sent,Received on,Quantity received,Used up,Out over a year\n");
        foreach (var r in rows.OrderBy(r => r.Number))
        {
            csv.AppendJoin(',', r.Number.ToString(CultureInfo.InvariantCulture), Cell(r.Karigar), Cell(r.KarigarGstin), Cell(r.ProductName), r.Unit.ToString(),
                r.SentAt.ToOffset(IndiaTime.Offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Num(r.Quantity), Num(decimal.Round(r.Quantity * r.UnitCost, 2)),
                r.ReceivedAt?.ToOffset(IndiaTime.Offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", r.ReceivedQuantity is { } q ? Num(q) : "",
                r.ReceivedQuantity is { } back ? Num(r.Quantity - back) : "", r.OverAYear ? "yes" : "").Append('\n');
        }

        return TypedResults.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv",
            $"itc04-{start.ToOffset(IndiaTime.Offset):yyyy-MM-dd}-to-{end.ToOffset(IndiaTime.Offset).AddDays(-1):yyyy-MM-dd}.csv");
    }
}
