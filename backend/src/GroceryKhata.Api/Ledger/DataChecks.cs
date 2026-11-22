using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>A thing that doesn't add up, named so the owner can find it: a customer, an item or a bill.</summary>
public sealed record DataProblem(string Name, string Detail, Guid? Id = null);

/// <summary>One check: <see cref="Code"/> is khata, stock, billNumbers, billPaid or noCost. <see cref="Warning"/>: a gap, not an error.</summary>
public sealed record DataCheckResult(string Code, int Checked, int Problems, bool Warning, IReadOnlyList<DataProblem> Samples);

public sealed record DataCheckDto(DateTimeOffset CheckedAt, IReadOnlyList<DataCheckResult> Checks);

/// <summary>
/// "Verify my data": rechecks the books on the spot and lists anything that doesn't add up. It only reads; fixing is the
/// owner's (or support's) call. The khata check is the nightly one, run now and kept in its history.
/// </summary>
public static class DataChecks
{
    private const int MaxSamples = 20;

    public static IEndpointRouteBuilder MapDataChecks(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/data-check", RunAsync).WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> RunAsync(AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        var checks = new List<DataCheckResult>();

        // 1. Each customer's balance against the sum of their khata entries (read-only here; the nightly check records it).
        var mismatches = await ledger.ReconcileAsync(ct);
        checks.Add(new("khata", await db.Customers.CountAsync(ct), mismatches.Count, false,
            mismatches.Take(MaxSamples).Select(m => new DataProblem(m.Name, $"{m.StoredBalance:0.##} / {m.SumOfEntries:0.##}", m.CustomerId)).ToList()));

        // 2. Each counted item's stock against the sum of its stock movements.
        var products = await db.Products.AsNoTracking().Where(p => !p.IsService && !p.Uncounted).Select(p => new { p.Id, p.Name, p.Stock }).ToListAsync(ct);
        var moved = await db.StockMovements.AsNoTracking().GroupBy(m => m.ProductId).Select(g => new { g.Key, Sum = g.Sum(m => m.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var stockOff = products.Where(p => Math.Abs(p.Stock - moved.GetValueOrDefault(p.Id)) > 0.001m).ToList();
        checks.Add(new("stock", products.Count, stockOff.Count, false,
            stockOff.Take(MaxSamples).Select(p => new DataProblem(p.Name, $"{p.Stock:0.###} / {moved.GetValueOrDefault(p.Id):0.###}", p.Id)).ToList()));

        // 3. Bill numbers: none twice, none missing, in each series (prefix and financial year). GST wants an unbroken series.
        var numbers = await db.Bills.AsNoTracking().Select(b => new { b.Prefix, b.FinancialYear, b.Number }).ToListAsync(ct);
        var numberProblems = new List<DataProblem>();
        foreach (var series in numbers.GroupBy(b => (b.Prefix, b.FinancialYear)))
        {
            foreach (var twice in series.GroupBy(b => b.Number).Where(g => g.Count() > 1))
            {
                numberProblems.Add(new(Bill.No(series.Key.Prefix, series.Key.FinancialYear, twice.Key), "twice"));
            }

            var used = series.Select(b => b.Number).ToHashSet();
            for (var n = used.Min(); n < used.Max(); n++)
            {
                if (!used.Contains(n)) numberProblems.Add(new(Bill.No(series.Key.Prefix, series.Key.FinancialYear, n), "missing"));
            }
        }

        checks.Add(new("billNumbers", numbers.Count, numberProblems.Count, false, numberProblems.Take(MaxSamples).ToList()));

        // 4. What was paid on each bill: never below zero, never more than the bill.
        var badPaid = await db.Bills.AsNoTracking().Where(b => b.CancelledAt == null && (b.PaidAmount < 0 || b.PaidAmount > b.Total + 0.01m))
            .OrderBy(b => b.OccurredAt).Select(b => new { b.Id, b.Prefix, b.FinancialYear, b.Number, b.PaidAmount, b.Total }).ToListAsync(ct);
        checks.Add(new("billPaid", numbers.Count, badPaid.Count, false,
            badPaid.Take(MaxSamples).Select(b => new DataProblem(Bill.No(b.Prefix, b.FinancialYear, b.Number), $"{b.PaidAmount:0.##} / {b.Total:0.##}", b.Id)).ToList()));

        // 5. Items on sale with no cost: profit can't be worked out on them. A gap to fill, not a broken record.
        var noCost = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.IsService && (p.CostPrice == null || p.CostPrice == 0)).OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name }).ToListAsync(ct);
        checks.Add(new("noCost", products.Count, noCost.Count, true, noCost.Take(MaxSamples).Select(p => new DataProblem(p.Name, "", p.Id)).ToList()));

        return TypedResults.Ok(new DataCheckDto(clock.GetUtcNow(), checks));
    }
}
