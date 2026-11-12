using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>Something dated in a closed month was about to be added, changed or cancelled.</summary>
public sealed class BooksClosedException(DateOnly closedUntil) : Exception($"The books are closed up to {closedUntil:d MMM yyyy}.")
{
    public DateOnly ClosedUntil { get; } = closedUntil;
}

public sealed record BooksDto(DateOnly? ClosedUntil, IReadOnlyList<BooksEventDto> History);

public sealed record BooksEventDto(DateTimeOffset At, string? By, string Action, string Month, string? Reason);

public sealed record CloseBooksRequest(string? Month);

public sealed record ReopenBooksRequest(string? Month, string? Reason);

/// <summary>
/// Closing the books: once the owner closes a month (after the CA has filed it), nothing dated in it or before can be
/// added, changed or cancelled, so every report, the GST figures and the Tally file for that month stay as filed. A
/// mistake found later is put right in the open month: a credit note, a payment, a stock correction. Only the owner can
/// reopen a month, with a reason, and both closing and reopening are kept in the audit log.
/// </summary>
public static class Books
{
    public static IEndpointRouteBuilder MapBooks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/books").WithTags("Reports").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", GetAsync);
        group.MapPost("/close", CloseAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/reopen", ReopenAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>The last day of the closed books, or null when nothing is closed.</summary>
    internal static Task<DateOnly?> ClosedUntilAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.BooksClosedUntil).SingleOrDefaultAsync(ct);

    /// <summary>Refuses a change to something that happened on <paramref name="at"/> when that day is in a closed month.</summary>
    internal static async Task EnsureOpenAsync(AppDbContext db, ITenantContext tenant, DateTimeOffset at, CancellationToken ct)
    {
        if (await ClosedUntilAsync(db, tenant, ct) is { } until && IndiaTime.DayOf(at) <= until)
        {
            throw new BooksClosedException(until);
        }
    }

    private static async Task<BooksDto> GetAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var rows = await (from a in db.AuditEntries.AsNoTracking()
                          where a.Table == "books"
                          join u in db.Users.AsNoTracking() on a.UserId equals u.Id into users
                          from u in users.DefaultIfEmpty()
                          orderby a.CreatedAt descending
                          select new { a.CreatedAt, a.Action, a.Changes, Name = u == null ? null : u.FullName }).Take(50).ToListAsync(ct);
        var history = rows.Select(r =>
        {
            var c = JsonSerializer.Deserialize<Dictionary<string, string?>>(r.Changes) ?? [];
            return new BooksEventDto(r.CreatedAt, r.Name, r.Action, c.GetValueOrDefault("month") ?? "", c.GetValueOrDefault("reason"));
        }).ToList();
        return new BooksDto(await ClosedUntilAsync(db, tenant, ct), history);
    }

    /// <summary>Closes the books up to the end of a month that is over (and every month before it).</summary>
    private static async Task<IResult> CloseAsync(CloseBooksRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (IndiaTime.ParseMonth(request.Month) is not { } period)
        {
            return Invalid("Month", "Choose the month to close.");
        }

        var last = new DateOnly(period.Year, period.Month, 1).AddMonths(1).AddDays(-1);
        if (last >= IndiaTime.Today(clock))
        {
            return Invalid("Month", "Only a month that is over can be closed.");
        }

        var shop = await db.Shops.SingleAsync(s => s.Id == tenant.ShopId, ct);
        if (shop.BooksClosedUntil is { } now && now >= last)
        {
            return TypedResults.Ok(await GetAsync(db, tenant, ct));
        }

        Log(db, tenant, user, "close", period, null, shop.BooksClosedUntil);
        shop.BooksClosedUntil = last;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await GetAsync(db, tenant, ct));
    }

    /// <summary>Reopens a closed month (and every later one): the books stay closed up to the month before it.</summary>
    private static async Task<IResult> ReopenAsync(ReopenBooksRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (IndiaTime.ParseMonth(request.Month) is not { } period)
        {
            return Invalid("Month", "Choose the month to reopen.");
        }

        if (reason is not { Length: >= 5 } || reason.Length > 200)
        {
            return Invalid("Reason", "Say why the month is being reopened (it is kept in the log).");
        }

        var shop = await db.Shops.SingleAsync(s => s.Id == tenant.ShopId, ct);
        var first = new DateOnly(period.Year, period.Month, 1);
        if (shop.BooksClosedUntil is not { } until || until < first)
        {
            return TypedResults.Ok(await GetAsync(db, tenant, ct));
        }

        // Back to where the books stood before this month was closed (closing October does not mean September was
        // closed on its own), but never later than the month before the one reopened.
        var closes = await db.AuditEntries.AsNoTracking().Where(a => a.Table == "books" && a.Action == "close").OrderByDescending(a => a.CreatedAt)
            .Select(a => a.Changes).ToListAsync(ct);
        var before = closes.Select(c => JsonSerializer.Deserialize<Dictionary<string, string?>>(c) ?? [])
            .FirstOrDefault(c => string.CompareOrdinal(c.GetValueOrDefault("month"), $"{period.Year:D4}-{period.Month:D2}") >= 0);
        DateOnly? earlier = before?.GetValueOrDefault("previous") is { } text && DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var d) ? d : null;
        shop.BooksClosedUntil = earlier is { } e && e < first ? e : before is null ? first.AddDays(-1) : null;
        Log(db, tenant, user, "reopen", period, reason);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await GetAsync(db, tenant, ct));
    }

    private static void Log(AppDbContext db, ITenantContext tenant, ClaimsPrincipal user, string action, (int Year, int Month) period, string? reason, DateOnly? previous = null) =>
        db.AuditEntries.Add(new AuditEntry
        {
            ShopId = tenant.ShopId, UserId = user.UserId()!.Value, Table = "books", RecordId = tenant.ShopId, Action = action,
            Changes = JsonSerializer.Serialize(new Dictionary<string, string?> { ["month"] = $"{period.Year:D4}-{period.Month:D2}", ["reason"] = reason, ["previous"] = previous?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }),
        });
}

/// <summary>
/// Stops any money or stock record dated in a closed month from being added, changed or deleted, whatever screen or
/// job tries it. Bulk updates (ExecuteUpdate) skip this, so those paths check <see cref="Books.EnsureOpenAsync"/> themselves.
/// </summary>
public sealed class ClosedBooksInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    private static readonly Dictionary<Type, string> Dated = new()
    {
        [typeof(Bill)] = nameof(Bill.OccurredAt),
        [typeof(LedgerEntry)] = nameof(LedgerEntry.OccurredAt),
        [typeof(CreditNote)] = nameof(CreditNote.OccurredAt),
        [typeof(Expense)] = nameof(Expense.OccurredAt),
        [typeof(Purchase)] = nameof(Purchase.OccurredAt),
        [typeof(SupplierEntry)] = nameof(SupplierEntry.OccurredAt),
        [typeof(StockMovement)] = nameof(StockMovement.OccurredAt),
        [typeof(CashMove)] = nameof(CashMove.OccurredAt),
        [typeof(CashCount)] = nameof(CashCount.Day),
        [typeof(GoodsReceipt)] = nameof(GoodsReceipt.ReceivedAt),
        [typeof(BagMove)] = nameof(BagMove.OccurredAt),
        [typeof(StockTransfer)] = nameof(StockTransfer.OccurredAt),
    };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await CheckAsync(eventData.Context!, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CheckAsync(eventData.Context!, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Changes that settle an old record without altering what it said: a cheque clearing or bouncing later, goods
    /// counted in on a receipt getting their supplier bill later, a timestamp. These may touch a closed month.
    /// </summary>
    private static readonly HashSet<string> Settling =
    [
        nameof(Entity.UpdatedAt), nameof(LedgerEntry.ChequeStatus), nameof(LedgerEntry.ChequeSettledAt), nameof(GoodsReceipt.PurchaseId),
    ];

    private static DateOnly? DayOf(object? value) => value switch
    {
        DateTimeOffset at => IndiaTime.DayOf(at),
        DateOnly day => day,
        _ => null,
    };

    private async Task CheckAsync(DbContext context, CancellationToken ct)
    {
        if (tenant.ShopId == Guid.Empty || context is not AppDbContext db)
        {
            return;
        }

        var touched = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && Dated.ContainsKey(e.Entity.GetType()))
            .ToList();
        if (touched.Count == 0)
        {
            return;
        }

        if (await Books.ClosedUntilAsync(db, tenant, ct) is not { } until)
        {
            return;
        }

        foreach (var entry in touched.Where(e => e.State != EntityState.Modified || e.Properties.Any(p => p.IsModified && !Settling.Contains(p.Metadata.Name))))
        {
            var property = entry.Property(Dated[entry.Entity.GetType()]);
            // A change counts against the day it was on and the day it moves to.
            var days = entry.State == EntityState.Added ? [DayOf(property.CurrentValue)] : new[] { DayOf(property.OriginalValue), DayOf(property.CurrentValue) };
            if (days.Any(d => d is { } day && day <= until))
            {
                throw new BooksClosedException(until);
            }
        }
    }
}

/// <summary>Turns <see cref="BooksClosedException"/> into a 409 the screens show as it is.</summary>
public sealed class BooksClosedHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not BooksClosedException closed)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 409,
            Title = $"The books are closed up to {closed.ClosedUntil.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}. Put this right in the open month (a credit note, a payment or a stock correction), or ask the owner to reopen the month.",
        }, ct);
        return true;
    }
}
