using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A warranty claim raised on the company (a supplier) for a unit repaired or replaced free: the parts given and the
/// labour or visit the company pays for. The claimed amount is owed to the shop from the day it is raised, so a free
/// warranty job doesn't look like a loss; it stays open until the company credits it (written off the shop's account
/// with them, like a credit note) or rejects it. Old open claims are where the money is lost, so they are aged.
/// </summary>
public sealed class WarrantyClaim : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    /// <summary>The company (kept in the supplier list).</summary>
    public Guid SupplierId { get; set; }

    /// <summary>The service job the claim comes from, when there is one.</summary>
    public Guid? JobId { get; set; }

    public string? SerialNo { get; set; }

    /// <summary>What was replaced or done ("Capacitor 36 mfd, PCB").</summary>
    public required string Description { get; set; }

    /// <summary>Parts given free, at the company's rate.</summary>
    public decimal Parts { get; set; }

    /// <summary>Labour or visit charge the company pays.</summary>
    public decimal Labour { get; set; }

    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>The courier or transport docket for the faulty part sent back.</summary>
    public string? DispatchNo { get; set; }

    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>What the company credited; 0 when rejected. Null while open.</summary>
    public decimal? CreditAmount { get; set; }

    public string? CreditNoteNo { get; set; }

    public string? RejectReason { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record WarrantyClaimRequest(Guid Id, Guid SupplierId, Guid? JobId, string? SerialNo, string? Description, decimal? Parts, decimal Labour);

public sealed record ClaimDispatchRequest(string? DispatchNo);

/// <summary>Amount 0 with a reason is a rejection; anything else is the company's credit.</summary>
public sealed record ClaimSettleRequest(decimal Amount, string? CreditNoteNo, string? RejectReason);

public sealed record WarrantyClaimDto(
    Guid Id, int Number, Guid SupplierId, string SupplierName, Guid? JobId, int? JobNumber, string? SerialNo, string Description, decimal Parts, decimal Labour,
    decimal Amount, DateTimeOffset RaisedAt, int DaysOpen, string? DispatchNo, decimal? CreditAmount, string? CreditNoteNo, string? RejectReason,
    DateTimeOffset? SettledAt, decimal Loss);

public static class WarrantyClaims
{
    public static IEndpointRouteBuilder MapWarrantyClaims(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/claims").WithTags("Warranty claims").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapPost("", RaiseAsync);
        group.MapPost("/{id:guid}/dispatch", DispatchAsync);
        group.MapPost("/{id:guid}/settle", SettleAsync);
        return app;
    }

    /// <summary>Open claims oldest first (the ones about to be lost); with <paramref name="all"/>, settled ones too, newest first.</summary>
    private static async Task<IReadOnlyList<WarrantyClaimDto>> ListAsync(bool? all, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var query = db.WarrantyClaims.AsNoTracking();
        query = all == true ? query.OrderByDescending(c => c.Number) : query.Where(c => c.SettledAt == null).OrderBy(c => c.Number);
        return await ToDtosAsync(db, query.Take(300), clock, ct);
    }

    private static async Task<IReadOnlyList<WarrantyClaimDto>> ToDtosAsync(AppDbContext db, IQueryable<WarrantyClaim> query, TimeProvider clock, CancellationToken ct)
    {
        var list = await query.ToListAsync(ct);
        var supplierIds = list.Select(c => c.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var jobIds = list.Where(c => c.JobId != null).Select(c => c.JobId!.Value).Distinct().ToList();
        var jobs = await db.Jobs.AsNoTracking().Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, j => j.Number, ct);
        var now = clock.GetUtcNow();
        return list.Select(c =>
        {
            var amount = c.Parts + c.Labour;
            return new WarrantyClaimDto(c.Id, c.Number, c.SupplierId, suppliers.GetValueOrDefault(c.SupplierId) ?? "", c.JobId, c.JobId is { } j ? jobs.GetValueOrDefault(j) : null,
                c.SerialNo, c.Description, c.Parts, c.Labour, amount, c.RaisedAt, (int)((c.SettledAt ?? now) - c.RaisedAt).TotalDays, c.DispatchNo, c.CreditAmount,
                c.CreditNoteNo, c.RejectReason, c.SettledAt, c.CreditAmount is { } credit ? amount - credit : 0);
        }).ToList();
    }

    private static string? Text(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(max, value.Trim().Length)];

    /// <summary>
    /// Raises a claim. From a job, the serial and the parts (at their cost on the job) are filled in, and can be changed
    /// to the company's rate. Safe to repeat.
    /// </summary>
    private static async Task<IResult> RaiseAsync(WarrantyClaimRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.WarrantyClaims.AnyAsync(c => c.Id == request.Id, ct))
        {
            return TypedResults.Ok((await ToDtosAsync(db, db.WarrantyClaims.AsNoTracking().Where(c => c.Id == request.Id), clock, ct))[0]);
        }

        if (request.Id == Guid.Empty) return Invalid("Id", "The app must send an ID for every new record.");
        if (!await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct)) return Invalid("SupplierId", "Choose the company.");
        var job = request.JobId is { } jobId ? await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId, ct) : null;
        if (request.JobId is not null && job is null) return Invalid("JobId", "Job not found.");

        var jobParts = job is null ? [] : await db.JobEntries.AsNoTracking().Where(e => e.JobId == job.Id && e.Kind == JobEntryKind.Material).ToListAsync(ct);
        var description = Text(request.Description, 200) ?? (jobParts.Count > 0 ? string.Join(", ", jobParts.Select(e => e.Description)) : null);
        if (description is not { Length: >= 2 }) return Invalid("Description", "Say what was replaced or done.");
        var parts = request.Parts ?? jobParts.Sum(e => e.Amount);
        if (parts < 0 || request.Labour < 0 || parts + request.Labour <= 0 || !Money.IsValidAmount(parts + request.Labour))
        {
            return Invalid("Labour", "Enter what the company owes for parts and labour.");
        }

        var claim = new WarrantyClaim
        {
            Id = request.Id, Number = (await db.WarrantyClaims.MaxAsync(c => (int?)c.Number, ct) ?? 0) + 1, SupplierId = request.SupplierId, JobId = job?.Id,
            SerialNo = request.SerialNo is { } s && !string.IsNullOrWhiteSpace(s) ? Catalogue.Serials.Clean(s) : job?.SerialNo, Description = description,
            Parts = parts, Labour = request.Labour, RaisedAt = clock.GetUtcNow(), CreatedByUserId = user.UserId()!.Value,
        };
        db.WarrantyClaims.Add(claim);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, db.WarrantyClaims.AsNoTracking().Where(c => c.Id == claim.Id), clock, ct))[0]);
    }

    /// <summary>The faulty part went back to the company: its docket number is noted (claims are refused without it).</summary>
    private static async Task<IResult> DispatchAsync(Guid id, ClaimDispatchRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var claim = await db.WarrantyClaims.SingleOrDefaultAsync(c => c.Id == id && c.SettledAt == null, ct);
        if (claim is null) return TypedResults.NotFound();
        (claim.DispatchNo, claim.DispatchedAt) = (Text(request.DispatchNo, 40), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>The company credited the claim (it comes off what the shop owes them) or rejected it (the claim is written off).</summary>
    private static async Task<IResult> SettleAsync(Guid id, ClaimSettleRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var claim = await db.WarrantyClaims.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (claim is null) return TypedResults.NotFound();
        if (claim.SettledAt is not null) return TypedResults.NoContent();
        if (request.Amount < 0 || request.Amount > (claim.Parts + claim.Labour) * 2 || decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Invalid("Amount", "Enter what the company credited, or 0 if they rejected it.");
        }

        var reason = Text(request.RejectReason, 200);
        if (request.Amount == 0 && reason is null) return Invalid("RejectReason", "Say why the company rejected it.");

        var now = clock.GetUtcNow();
        (claim.CreditAmount, claim.CreditNoteNo, claim.RejectReason, claim.SettledAt) = (request.Amount, Text(request.CreditNoteNo, 40), reason, now);
        if (request.Amount > 0)
        {
            db.SupplierEntries.Add(new SupplierEntry
            {
                SupplierId = claim.SupplierId, Kind = SupplierEntryKind.CreditNote, Amount = -request.Amount, Note = $"Warranty claim #{claim.Number}{(claim.CreditNoteNo is { } n ? $" · credit note {n}" : "")}",
                OccurredAt = now, CreatedByUserId = user.UserId()!.Value,
            });
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
