using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// One piece of work made to order (a sofa set, a bed, a wardrobe): quoted, taken as an order with an advance, made,
/// and delivered on a bill. What goes into it is kept against it, so the shop sees what each job really earned. It is
/// built on what the shop already has: the advance is a khata payment, wood and ply come off the stock list, karigar
/// wages and other spending go in the kharcha book, and delivery is an ordinary bill.
/// </summary>
public sealed class Job : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The shop's own running number: job #1, #2...</summary>
    public int Number { get; set; }

    /// <summary>The khata customer it is for; needed for an advance or delivery on credit.</summary>
    public Guid? CustomerId { get; set; }

    public required string Title { get; set; }

    public JobStatus Status { get; set; }

    /// <summary>The price agreed with the customer, GST included, as shops quote.</summary>
    public decimal Price { get; set; }

    /// <summary>What the shop expected it to cost when quoting: compared with the actual cost at the end.</summary>
    public decimal? EstimatedCost { get; set; }

    public DateOnly? DueOn { get; set; }

    public string? Note { get; set; }

    /// <summary>The delivery bill, once delivered.</summary>
    public Guid? BillId { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Service work (sales & service shop): the unit's serial, as written on it.</summary>
    public string? SerialNo { get; set; }

    /// <summary>The shop's own record of that unit, when the shop sold it.</summary>
    public Guid? ProductSerialId { get; set; }

    /// <summary>What the customer says is wrong ("motor hums, no water").</summary>
    public string? Complaint { get; set; }

    /// <summary>Under the shop's warranty on the day the job was opened: looked up from the sale.</summary>
    public bool InWarranty { get; set; }

    /// <summary>The field trip it was done on; the trip's travel, food and stay are shared across its jobs.</summary>
    public Guid? TripId { get; set; }

    /// <summary>A visit under an AMC: covered by the contract fee, so it closes without a bill.</summary>
    public Guid? AmcId { get; set; }
}

public enum JobStatus
{
    Quote,
    Order,
    Making,
    Ready,
    Delivered,
    Cancelled,
}

/// <summary>One thing that went into a job, or an advance taken for it. Each points at the record it made elsewhere.</summary>
public sealed class JobEntry : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid JobId { get; set; }

    public JobEntryKind Kind { get; set; }

    public required string Description { get; set; }

    /// <summary>Material: the item taken off stock, and how much.</summary>
    public Guid? ProductId { get; set; }

    public decimal? Quantity { get; set; }

    /// <summary>The cost (material at its cost price; wages and other spending as paid), or the advance taken.</summary>
    public decimal Amount { get; set; }

    public Guid? ExpenseId { get; set; }

    public Guid? LedgerEntryId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public enum JobEntryKind
{
    Material,
    Labour,
    Other,
    Advance,
}

public sealed record JobRequest(Guid Id, Guid? CustomerId, string? Title, decimal Price, decimal? EstimatedCost, DateOnly? DueOn, string? Note, JobStatus? Status,
    string? SerialNo = null, string? Complaint = null);

public sealed record JobStatusRequest(JobStatus? Status);

public sealed record JobAdvanceRequest(Guid Id, decimal Amount, PaymentMode? Mode);

public sealed record JobMaterialRequest(Guid Id, Guid ProductId, decimal Quantity);

/// <summary>Karigar wages (Labour) or any other spending on the job (Other): written in the kharcha book too.</summary>
public sealed record JobSpendRequest(Guid Id, string? Description, decimal Amount, PaymentMode? Mode);

public sealed record JobDeliverRequest(Guid BillId, decimal PaidAmount, PaymentMode? Mode);

public sealed record JobEntryDto(Guid Id, JobEntryKind Kind, string Description, Guid? ProductId, decimal? Quantity, decimal Amount, DateTimeOffset OccurredAt);

/// <summary>A job with its money: cost so far by kind, advance taken, and profit (price less cost).</summary>
public sealed record JobDto(
    Guid Id, int Number, Guid? CustomerId, string? CustomerName, string Title, JobStatus Status, decimal Price, decimal? EstimatedCost,
    DateOnly? DueOn, string? Note, Guid? BillId, decimal Material, decimal Labour, decimal Other, decimal Cost, decimal Advance, decimal Profit,
    DateTimeOffset CreatedAt, string? CustomerPhone = null, string? SerialNo = null, string? Complaint = null, bool InWarranty = false);

/// <summary>A job's lines, and its estimated share of rent, electricity and repairs (<see cref="Jobs.OverheadShareAsync"/>).</summary>
public sealed record JobDetail(JobDto Job, IReadOnlyList<JobEntryDto> Entries, decimal Overhead = 0);

public static class Jobs
{
    public static IEndpointRouteBuilder MapJobs(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/jobs").WithTags("Jobs").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("/{id:guid}/status", StatusAsync);
        group.MapPost("/{id:guid}/advance", AdvanceAsync);
        group.MapPost("/{id:guid}/material", MaterialAsync);
        group.MapPost("/{id:guid}/labour", (Guid id, JobSpendRequest r, ClaimsPrincipal u, AppDbContext db, TimeProvider c, CancellationToken ct) => SpendAsync(id, r, JobEntryKind.Labour, u, db, c, ct));
        group.MapPost("/{id:guid}/other", (Guid id, JobSpendRequest r, ClaimsPrincipal u, AppDbContext db, TimeProvider c, CancellationToken ct) => SpendAsync(id, r, JobEntryKind.Other, u, db, c, ct));
        group.MapPost("/{id:guid}/deliver", DeliverAsync);
        return app;
    }

    private static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));

    private static async Task<IReadOnlyList<JobDto>> ToDtosAsync(AppDbContext db, IQueryable<Job> jobs, CancellationToken ct)
    {
        var list = await jobs.AsNoTracking().ToListAsync(ct);
        var ids = list.Select(j => j.Id).ToList();
        var sums = await db.JobEntries.AsNoTracking().Where(e => ids.Contains(e.JobId))
            .GroupBy(e => new { e.JobId, e.Kind }).Select(g => new { g.Key.JobId, g.Key.Kind, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        var customerIds = list.Where(j => j.CustomerId != null).Select(j => j.CustomerId!.Value).Distinct().ToList();
        var people = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        return list.Select(j =>
        {
            decimal Sum(JobEntryKind k) => sums.Where(s => s.JobId == j.Id && s.Kind == k).Sum(s => s.Total);
            var (material, labour, other) = (Sum(JobEntryKind.Material), Sum(JobEntryKind.Labour), Sum(JobEntryKind.Other));
            var cost = material + labour + other;
            return new JobDto(j.Id, j.Number, j.CustomerId, j.CustomerId is { } c ? people.GetValueOrDefault(c)?.Name : null, j.Title, j.Status, j.Price, j.EstimatedCost,
                j.DueOn, j.Note, j.BillId, material, labour, other, cost, Sum(JobEntryKind.Advance), j.Price - cost, j.CreatedAt,
                j.CustomerId is { } p ? people.GetValueOrDefault(p)?.Phone : null, j.SerialNo, j.Complaint, j.InWarranty);
        }).ToList();
    }

    /// <summary>Open jobs first (quotes and orders being made), then delivered and cancelled, newest first.</summary>
    private static async Task<IReadOnlyList<JobDto>> ListAsync(bool? done, AppDbContext db, CancellationToken ct)
    {
        var query = db.Jobs.AsQueryable();
        query = done == true
            ? query.Where(j => j.Status == JobStatus.Delivered || j.Status == JobStatus.Cancelled)
            : query.Where(j => j.Status != JobStatus.Delivered && j.Status != JobStatus.Cancelled);
        return await ToDtosAsync(db, query.OrderByDescending(j => j.Number).Take(300), ct);
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        if ((await ToDtosAsync(db, db.Jobs.Where(j => j.Id == id), ct)).SingleOrDefault() is not { } job)
        {
            return TypedResults.NotFound();
        }

        var entries = await db.JobEntries.AsNoTracking().Where(e => e.JobId == id).OrderBy(e => e.OccurredAt)
            .Select(e => new JobEntryDto(e.Id, e.Kind, e.Description, e.ProductId, e.Quantity, e.Amount, e.OccurredAt)).ToListAsync(ct);
        return TypedResults.Ok(new JobDetail(job, entries, await OverheadShareAsync(db, job.Labour, ct)));
    }

    /// <summary>
    /// The workshop's running costs a job should carry, spread by karigar wages: in the last 30 days, every rupee of
    /// wages came with this much rent, electricity, repairs and machine wear (a month's depreciation), so a job with
    /// ₹8,000 of wages carries 8,000 × that.
    /// An estimate for pricing; the month's profit already counts these costs in the kharcha book.
    /// </summary>
    public static async Task<decimal> OverheadShareAsync(AppDbContext db, decimal jobLabour, CancellationToken ct)
    {
        if (jobLabour <= 0)
        {
            return 0;
        }

        var since = DateTimeOffset.UtcNow.AddDays(-30);
        var lines = await db.Expenses.AsNoTracking().Where(e => e.OccurredAt >= since && e.StruckAt == null)
            .GroupBy(e => e.Category).Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) }).ToListAsync(ct);
        var wages = lines.Where(l => l.Category == ExpenseCategory.Salary).Sum(l => l.Total);
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(IndiaTime.Offset).DateTime);
        var wearPerMonth = await Workshop.WearForYearAsync(db, today.Month >= 4 ? today.Year : today.Year - 1, ct) / 12;
        var running = lines.Where(l => l.Category is ExpenseCategory.Rent or ExpenseCategory.Electricity or ExpenseCategory.Repair).Sum(l => l.Total) + wearPerMonth;
        return wages <= 0 ? 0 : decimal.Round(jobLabour * running / wages, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>What sits in jobs still being made (material, wages, other spending at cost): stock that left the shelf but isn't sold yet.</summary>
    public static Task<decimal> WorkInProgressAsync(AppDbContext db, CancellationToken ct) =>
        CostInJobsAsync(db, [JobStatus.Quote, JobStatus.Order, JobStatus.Making], ct);

    /// <summary>Furniture made and ready, not yet delivered, at what it cost: finished goods in stock.</summary>
    public static Task<decimal> FinishedGoodsAsync(AppDbContext db, CancellationToken ct) => CostInJobsAsync(db, [JobStatus.Ready], ct);

    private static async Task<decimal> CostInJobsAsync(AppDbContext db, JobStatus[] statuses, CancellationToken ct) =>
        await (from e in db.JobEntries.AsNoTracking()
               join j in db.Jobs.AsNoTracking() on e.JobId equals j.Id
               where e.Kind != JobEntryKind.Advance && statuses.Contains(j.Status)
               select (decimal?)e.Amount).SumAsync(ct) ?? 0m;

    private static async Task<IResult> CreateAsync(JobRequest request, AppDbContext db, CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(j => j.Id == request.Id, ct))
        {
            return await GetAsync(request.Id, db, ct);
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID for every new record."];
        if (request.Title?.Trim() is not { Length: >= 2 and <= 120 }) errors["Title"] = ["Say what is being made (2 to 120 letters)."];
        if (request.Price < 0 || (request.Price > 0 && !Money.IsValidAmount(request.Price))) errors["Price"] = ["Enter the agreed price."];
        if (request.EstimatedCost is { } e && (e < 0 || !Money.IsValidAmount(e))) errors["EstimatedCost"] = ["Enter a valid cost, or leave it empty."];
        if (request.Note?.Length > 300) errors["Note"] = ["Keep the note under 300 letters."];
        if (request.Status is not (null or JobStatus.Quote or JobStatus.Order)) errors["Status"] = ["A new job is a quote or an order."];
        if (request.CustomerId is { } customer && !await db.Customers.AnyAsync(c => c.Id == customer, ct)) errors["CustomerId"] = ["Customer not found."];
        if (request.Complaint?.Length > 300) errors["Complaint"] = ["Keep the complaint under 300 letters."];
        var serialNo = string.IsNullOrWhiteSpace(request.SerialNo) ? null : Serials.Clean(request.SerialNo);
        if (serialNo?.Length > 40) errors["SerialNo"] = ["A serial number is at most 40 letters."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // A unit the shop sold: the customer and the warranty come from that sale. A unit bought elsewhere is just noted.
        var unit = serialNo is null ? null
            : await db.ProductSerials.AsNoTracking().Where(s => s.SerialNo == serialNo).OrderByDescending(s => s.SoldOn).FirstOrDefaultAsync(ct);
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(IndiaTime.Offset).DateTime);
        var number = (await db.Jobs.MaxAsync(j => (int?)j.Number, ct) ?? 0) + 1;
        db.Jobs.Add(new Job
        {
            SerialNo = serialNo, ProductSerialId = unit?.Id, InWarranty = unit?.WarrantyUntil is { } until && until >= today,
            Complaint = string.IsNullOrWhiteSpace(request.Complaint) ? null : request.Complaint.Trim(),
            Id = request.Id, Number = number, CustomerId = request.CustomerId ?? unit?.CustomerId, Title = request.Title!.Trim(), Status = request.Status ?? JobStatus.Order,
            Price = request.Price, EstimatedCost = request.EstimatedCost, DueOn = request.DueOn, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        });
        await db.SaveChangesAsync(ct);
        return await GetAsync(request.Id, db, ct);
    }

    /// <summary>Moves a job along: quote → order → making → ready, or cancels it. Delivery goes through its own call (it makes the bill).</summary>
    private static async Task<IResult> StatusAsync(Guid id, JobStatusRequest request, AppDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, ct);
        if (job is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Status is null or JobStatus.Delivered)
        {
            return Invalid("Status", "Deliver the job to make its bill.");
        }

        if (job.Status is JobStatus.Delivered or JobStatus.Cancelled)
        {
            return Invalid("Status", "This job is closed.");
        }

        job.Status = request.Status.Value;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, ct);
    }

    private static async Task<Job?> OpenJobAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.Jobs.SingleOrDefaultAsync(j => j.Id == id && j.Status != JobStatus.Delivered && j.Status != JobStatus.Cancelled, ct);

    /// <summary>Money taken before the work is done: a payment on the customer's khata, marked for this job.</summary>
    private static async Task<IResult> AdvanceAsync(Guid id, JobAdvanceRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.JobEntries.AnyAsync(e => e.Id == request.Id, ct))
        {
            return await GetAsync(id, db, ct);
        }

        if (await OpenJobAsync(db, id, ct) is not { } job)
        {
            return TypedResults.NotFound();
        }

        if (job.CustomerId is not { } customer)
        {
            return Invalid("CustomerId", "Choose the customer on the job before taking an advance.");
        }

        var result = await ledger.AddKhataEntryAsync(
            new KhataEntryRequest(Guid.CreateVersion7(), customer, KhataEntryType.Payment, request.Amount, request.Mode, $"Advance: job #{job.Number}", null),
            user.UserId()!.Value, ct, IsOwner(user));
        if (result.Value is not { } entry)
        {
            return result.ToHttp("");
        }

        db.JobEntries.Add(new JobEntry
        {
            Id = request.Id, JobId = id, Kind = JobEntryKind.Advance, Description = "Advance", Amount = request.Amount, LedgerEntryId = entry.Entry.Id,
            OccurredAt = clock.GetUtcNow(), CreatedByUserId = user.UserId()!.Value,
        });
        if (job.Status == JobStatus.Quote)
        {
            job.Status = JobStatus.Order; // an advance turns a quote into an order
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, ct);
    }

    /// <summary>Wood, ply, foam or fittings taken off the stock list for this job, costed at the item's cost price.</summary>
    private static async Task<IResult> MaterialAsync(Guid id, JobMaterialRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.JobEntries.AnyAsync(e => e.Id == request.Id, ct))
        {
            return await GetAsync(id, db, ct);
        }

        if (await OpenJobAsync(db, id, ct) is not { } job)
        {
            return TypedResults.NotFound();
        }

        if (request.Quantity <= 0 || request.Quantity > Money.MaxQuantity || decimal.Round(request.Quantity, 3) != request.Quantity)
        {
            return Invalid("Quantity", "Enter how much was used, with at most three decimals.");
        }

        var product = await db.Products.AsNoTracking().Where(p => p.Id == request.ProductId && p.IsActive)
            .Select(p => new { p.Name, p.Unit, p.CostPrice }).SingleOrDefaultAsync(ct);
        if (product is null)
        {
            return Invalid("ProductId", "Item not found.");
        }

        if (product.CostPrice is not { } unitCost)
        {
            return Invalid("ProductId", "Set this item's cost price first, so the job's cost is right.");
        }

        var now = clock.GetUtcNow();
        var userId = user.UserId()!.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.StockMovements.Add(new StockMovement
        {
            RefType = "job", RefId = job.Id, ProductId = request.ProductId, Kind = StockMovementKind.UsedOnJob, Quantity = -request.Quantity, CostPrice = unitCost,
            Note = $"Job #{job.Number}", OccurredAt = now, CreatedByUserId = userId,
        });
        db.JobEntries.Add(new JobEntry
        {
            Id = request.Id, JobId = id, Kind = JobEntryKind.Material, Description = product.Name, ProductId = request.ProductId, Quantity = request.Quantity,
            Amount = Money.LineAmount(request.Quantity, unitCost), OccurredAt = now, CreatedByUserId = userId,
        });
        if (job.Status is JobStatus.Quote or JobStatus.Order)
        {
            job.Status = JobStatus.Making;
        }

        await db.SaveChangesAsync(ct);
        await db.Products.Where(p => p.Id == request.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - request.Quantity), ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, db, ct);
    }

    /// <summary>Karigar wages or other spending (transport, polish bought on the way): a kharcha line, kept against the job.</summary>
    private static async Task<IResult> SpendAsync(Guid id, JobSpendRequest request, JobEntryKind kind, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.JobEntries.AnyAsync(e => e.Id == request.Id, ct))
        {
            return await GetAsync(id, db, ct);
        }

        if (await OpenJobAsync(db, id, ct) is not { } job)
        {
            return TypedResults.NotFound();
        }

        var description = request.Description?.Trim();
        if (description is not { Length: >= 2 and <= 80 })
        {
            return Invalid("Description", kind == JobEntryKind.Labour ? "Write the karigar's name and work (\"Ramesh, 2 days\")." : "Say what it was for.");
        }

        if (request.Amount <= 0 || !Money.IsValidAmount(request.Amount))
        {
            return Invalid("Amount", "Enter the amount paid.");
        }

        if (request.Mode is not (null or PaymentMode.Cash or PaymentMode.Upi))
        {
            return Invalid("Mode", "Cash or UPI.");
        }

        var now = clock.GetUtcNow();
        var userId = user.UserId()!.Value;
        var expense = new Expense
        {
            OccurredAt = now, Amount = request.Amount, Category = kind == JobEntryKind.Labour ? ExpenseCategory.Salary : ExpenseCategory.Other,
            Note = $"Job #{job.Number}: {description}", Mode = request.Mode ?? PaymentMode.Cash, CreatedByUserId = userId,
        };
        db.Expenses.Add(expense);
        db.JobEntries.Add(new JobEntry
        {
            Id = request.Id, JobId = id, Kind = kind, Description = description, Amount = request.Amount, ExpenseId = expense.Id, OccurredAt = now, CreatedByUserId = userId,
        });
        if (job.Status is JobStatus.Quote or JobStatus.Order)
        {
            job.Status = JobStatus.Making;
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, ct);
    }

    /// <summary>
    /// Hands the job over on an ordinary bill for its price (advances already sit on the khata, so the customer owes
    /// the rest). The bill line carries the job's material cost, so the month's profit counts it like goods sold; wages
    /// and other spending are already in the kharcha book.
    /// </summary>
    private static async Task<IResult> DeliverAsync(Guid id, JobDeliverRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await OpenJobAsync(db, id, ct) is not { } job)
        {
            return await db.Jobs.AnyAsync(j => j.Id == id && j.BillId == request.BillId, ct) ? await GetAsync(id, db, ct) : TypedResults.NotFound();
        }

        if (job.Price <= 0 && (job.InWarranty || job.AmcId != null))
        {
            // Repaired free under warranty or an AMC: nothing to bill. Parts and wages stay on the job as its cost.
            (job.Status, job.DeliveredAt) = (JobStatus.Delivered, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return await GetAsync(id, db, ct);
        }

        if (job.Price <= 0)
        {
            return Invalid("Price", "Set the job's price before delivering it.");
        }

        var result = await ledger.CreateBillAsync(
            new BillRequest(request.BillId, job.CustomerId, [new BillLineRequest(null, $"{job.Title} (job #{job.Number})", 1, Unit.Piece, job.Price)],
                request.PaidAmount, request.Mode, null),
            user.UserId()!.Value, ct, isOwner: IsOwner(user));
        if (result.Value is not { } bill)
        {
            return result.ToHttp("");
        }

        var material = await db.JobEntries.Where(e => e.JobId == id && e.Kind == JobEntryKind.Material).SumAsync(e => e.Amount, ct);
        await db.BillLines.Where(l => l.BillId == bill.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.UnitCost, material), ct);
        (job.Status, job.BillId, job.DeliveredAt) = (JobStatus.Delivered, bill.Id, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, db, ct);
    }
}
