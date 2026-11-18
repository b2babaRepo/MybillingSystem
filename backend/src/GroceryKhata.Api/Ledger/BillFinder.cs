using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>One bill in the search: enough to recognise it before opening it to cancel, return or exchange.</summary>
public sealed record FoundBill(Guid Id, string BillNo, DateTimeOffset OccurredAt, decimal Total, string? CustomerName, string? CustomerPhone,
    int Lines, string Items, bool Cancelled, decimal Due = 0, string Status = "paid");

/// <summary>
/// The bills of a period with what came in and what is still due on them: <see cref="Received"/> + <see cref="Due"/> =
/// <see cref="Total"/> (cancelled bills left out). <see cref="Bills"/> is newest first, at most 200.
/// </summary>
public sealed record BillList(decimal Total, decimal Received, decimal Due, int Count, int PaidCount, int PartialCount, int DueCount, int CancelledCount, IReadOnlyList<FoundBill> Bills);

/// <summary>
/// Find an old bill when a customer comes back: by bill number, customer name or mobile, or an item on the bill,
/// within a date range. Newest first, at most 50.
/// </summary>
public static class BillFinder
{
    private static readonly TimeSpan India = TimeSpan.FromHours(5.5);

    public static IEndpointRouteBuilder MapBillFinder(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/bills/find", FindAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        app.MapGet("/api/v1/bills/list", ListAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    /// <summary>
    /// The bill list: every bill of the period (and search) with its status today. A credit bill's due is what is still
    /// unpaid of its khata part, khata payments settling the oldest udhaar first (the same rule as dues by age), so a
    /// bill paid off later through the khata shows Paid. Walk-in bills are paid at the counter.
    /// </summary>
    private static async Task<BillList> ListAsync(string? q, DateOnly? from, DateOnly? to, string? status, AppDbContext db, CancellationToken ct)
    {
        var bills = Filter(db, q, from, to);
        var rows = await bills.OrderByDescending(b => b.OccurredAt)
            .Select(b => new
            {
                b.Id, b.Prefix, b.FinancialYear, b.Number, b.OccurredAt, b.Total, b.CustomerId, Cancelled = b.CancelledAt != null,
                Customer = db.Customers.Where(c => c.Id == b.CustomerId).Select(c => new { c.Name, c.Phone }).FirstOrDefault(),
                Lines = b.Lines.Count,
                Names = b.Lines.OrderBy(l => l.LineNo).Select(l => l.Name).Take(3).ToList(),
            }).Take(2000).ToListAsync(ct);

        var dueOf = await DueNowAsync(db, rows.Where(r => r.CustomerId != null).Select(r => r.CustomerId!.Value).Distinct().ToList(), ct);
        var found = rows.Select(b =>
        {
            var due = b.Cancelled ? 0 : dueOf.GetValueOrDefault(b.Id);
            var state = b.Cancelled ? "cancelled" : due <= 0 ? "paid" : due < b.Total ? "partial" : "due";
            return new FoundBill(b.Id, Bill.No(b.Prefix, b.FinancialYear, b.Number), b.OccurredAt, b.Total, b.Customer?.Name, b.Customer?.Phone,
                b.Lines, string.Join(", ", b.Names) + (b.Lines > 3 ? $" +{b.Lines - 3}" : ""), b.Cancelled, due, state);
        }).ToList();

        var live = found.Where(b => !b.Cancelled).ToList();
        var total = live.Sum(b => b.Total);
        var dueTotal = live.Sum(b => b.Due);
        var shown = string.IsNullOrEmpty(status) || status == "all" ? found : found.Where(b => b.Status == status).ToList();
        return new BillList(total, total - dueTotal, dueTotal, live.Count, found.Count(b => b.Status == "paid"), found.Count(b => b.Status == "partial"),
            found.Count(b => b.Status == "due"), found.Count(b => b.Cancelled), shown.Take(200).ToList());
    }

    /// <summary>
    /// What is still unpaid today of each bill's khata part, for these customers: their khata worked through oldest first,
    /// payments clearing the oldest udhaar. A cancelled bill's entries cancel each other and are left out.
    /// </summary>
    private static async Task<Dictionary<Guid, decimal>> DueNowAsync(AppDbContext db, List<Guid> customerIds, CancellationToken ct)
    {
        var due = new Dictionary<Guid, decimal>();
        if (customerIds.Count == 0)
        {
            return due;
        }

        var cancelled = (await db.Bills.AsNoTracking().Where(b => b.CancelledAt != null && b.CustomerId != null && customerIds.Contains(b.CustomerId.Value))
            .Select(b => b.Id).ToListAsync(ct)).ToHashSet();
        var entries = await db.LedgerEntries.AsNoTracking().Where(e => customerIds.Contains(e.CustomerId))
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt)
            .Select(e => new { e.CustomerId, e.BillId, e.Amount }).ToListAsync(ct);

        foreach (var customer in entries.Where(e => e.BillId is null || !cancelled.Contains(e.BillId.Value)).GroupBy(e => e.CustomerId))
        {
            var open = new LinkedList<(Guid? Bill, decimal Left)>();
            var advance = 0m;
            foreach (var e in customer)
            {
                if (e.Amount > 0)
                {
                    var left = e.Amount - Math.Min(advance, e.Amount);
                    advance -= e.Amount - left;
                    if (left > 0) open.AddLast((e.BillId, left));
                    continue;
                }

                var paying = -e.Amount;
                while (paying > 0 && open.First is { } oldest)
                {
                    var used = Math.Min(paying, oldest.Value.Left);
                    paying -= used;
                    if (used == oldest.Value.Left) open.RemoveFirst();
                    else oldest.Value = (oldest.Value.Bill, oldest.Value.Left - used);
                }

                advance += paying;
            }

            foreach (var (bill, left) in open.Where(o => o.Bill is not null))
            {
                due[bill!.Value] = due.GetValueOrDefault(bill.Value) + left;
            }
        }

        return due;
    }

    private static IQueryable<Bill> Filter(AppDbContext db, string? q, DateOnly? from, DateOnly? to)
    {
        var bills = db.Bills.AsNoTracking().AsQueryable();
        if (from is { } f)
        {
            var start = new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), India).ToUniversalTime();
            bills = bills.Where(b => b.OccurredAt >= start);
        }

        if (to is { } t)
        {
            var end = new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), India).ToUniversalTime();
            bills = bills.Where(b => b.OccurredAt < end);
        }

        var text = q?.Trim() ?? "";
        if (text.Length > 0)
        {
            var parts = text.Split('/');
            int? number = int.TryParse(parts[^1], out var n) && n > 0 ? n : null;
            var year = parts.Length >= 2 ? parts[^2] : null;
            var like = $"%{text.Replace("%", "").Replace("_", "")}%";
            bills = bills.Where(b =>
                (number != null && b.Number == number && (year == null || b.FinancialYear == year))
                || db.Customers.Any(c => c.Id == b.CustomerId && (EF.Functions.ILike(c.Name, like) || (c.Phone != null && EF.Functions.ILike(c.Phone, like))))
                || b.Lines.Any(l => EF.Functions.ILike(l.Name, like)));
        }

        return bills;
    }

    private static async Task<IReadOnlyList<FoundBill>> FindAsync(string? q, DateOnly? from, DateOnly? to, AppDbContext db, CancellationToken ct)
    {
        // "0042", "42", "2026-27/0042" or "MB/2026-27/0042": the bill number. Digits also match a mobile number.
        var bills = Filter(db, q, from, to);
        var found = await bills.OrderByDescending(b => b.OccurredAt).Take(50)
            .Select(b => new
            {
                b.Id, b.Prefix, b.FinancialYear, b.Number, b.OccurredAt, b.Total, Cancelled = b.CancelledAt != null,
                Customer = db.Customers.Where(c => c.Id == b.CustomerId).Select(c => new { c.Name, c.Phone }).FirstOrDefault(),
                Lines = b.Lines.Count,
                Names = b.Lines.OrderBy(l => l.LineNo).Select(l => l.Name).Take(3).ToList(),
            }).ToListAsync(ct);
        return found.Select(b => new FoundBill(b.Id, Bill.No(b.Prefix, b.FinancialYear, b.Number), b.OccurredAt, b.Total, b.Customer?.Name, b.Customer?.Phone,
            b.Lines, string.Join(", ", b.Names) + (b.Lines > 3 ? $" +{b.Lines - 3}" : ""), b.Cancelled)).ToList();
    }
}
