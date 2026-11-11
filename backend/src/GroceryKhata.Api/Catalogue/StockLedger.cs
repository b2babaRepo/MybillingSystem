using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>A saved count: what the books said, what was counted, and what the difference was worth at cost.</summary>
public sealed class Stocktake : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public DateTimeOffset TakenAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>The lines (<see cref="StocktakeLine"/>), as JSON: a count is read whole, never queried line by line.</summary>
    public required string Lines { get; set; }

    public int Items { get; set; }

    public int Differences { get; set; }

    /// <summary>Counted minus books, at cost: negative is stock missing.</summary>
    public decimal ValueDifference { get; set; }
}

public sealed record StocktakeLine(Guid ProductId, string Name, decimal Books, decimal Counted, decimal Difference, decimal? CostPrice);

public sealed record StocktakeSummary(Guid Id, int Number, DateTimeOffset TakenAt, string By, int Items, int Differences, decimal ValueDifference);

public sealed record StocktakeView(StocktakeSummary Summary, IReadOnlyList<StocktakeLine> Lines);

/// <summary>One line of an item's stock account: what came in or went out, the balance after it, who did it, and why.</summary>
public sealed record LedgerLine(
    Guid Id, DateTimeOffset OccurredAt, StockMovementKind Kind, StockReason? Reason, decimal In, decimal Out, decimal Balance,
    decimal? CostPrice, string? Note, string By, string? DocType, Guid? DocId, string? DocLabel);

public sealed record ItemLedger(Guid ProductId, string Name, Unit Unit, DateOnly From, DateOnly To, decimal Opening, decimal In, decimal Out, decimal Closing, IReadOnlyList<LedgerLine> Lines);

/// <summary>One item in the stock statement: opening, in, out, closing, and the closing value at average cost.</summary>
public sealed record StockStatementLine(
    Guid ProductId, string Name, string? Category, Unit Unit, decimal Opening, decimal Purchased, decimal Sold, decimal OtherIn, decimal OtherOut,
    decimal Closing, decimal? CostPrice, decimal? Value, decimal Lost);

public sealed record StockStatement(DateOnly From, DateOnly To, decimal Value, int Unvalued, decimal LostValue, IReadOnlyList<StockStatementLine> Items);

/// <summary>Sales grouped one way: a category, a supplier or a customer.</summary>
public sealed record SalesGroup(string Key, string Name, decimal Sales, decimal Quantity, int Bills, decimal? Profit, decimal Share);

public sealed record SalesBy(string By, DateOnly From, DateOnly To, decimal Total, IReadOnlyList<SalesGroup> Groups);

/// <summary>
/// The stock account the law and a careful owner want to see (GST rule 56: opening, receipts, supplies, losses,
/// closing, for each item): the item ledger, the stock statement, saved stocktakes, and sales by category, supplier
/// or customer. All read from the movements and bills already kept; nothing here changes stock.
/// </summary>
public static class StockLedger
{
    private const int MaxDays = 400;
    private const int MaxLines = 3000;

    public static IEndpointRouteBuilder MapStockLedger(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/products/{id:guid}/ledger", LedgerAsync);
        group.MapGet("/reports/stock", StatementAsync);
        group.MapGet("/reports/sales-by", SalesByAsync);
        group.MapGet("/stocktakes", async (AppDbContext db, CancellationToken ct) =>
        {
            var takes = await db.Stocktakes.AsNoTracking().OrderByDescending(t => t.Number).Take(100).ToListAsync(ct);
            var names = await NamesAsync(db, takes.Select(t => t.CreatedByUserId), ct);
            return takes.Select(t => Summary(t, names)).ToList();
        });
        group.MapGet("/stocktakes/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var take = await db.Stocktakes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
            if (take is null)
            {
                return Results.NotFound();
            }

            var names = await NamesAsync(db, [take.CreatedByUserId], ct);
            return Results.Ok(new StocktakeView(Summary(take, names), JsonSerializer.Deserialize<List<StocktakeLine>>(take.Lines) ?? []));
        });
        return app;
    }

    /// <summary>
    /// Weighted average purchase cost through an item's movements in time order: each purchase with a cost averages
    /// into what is held. The rule supplier bills and manual goods-in keep, so valuation agrees with them.
    /// </summary>
    public static decimal? AverageCost(IEnumerable<(StockMovementKind Kind, decimal Quantity, decimal? CostPrice, DateTimeOffset OccurredAt)> moves)
    {
        decimal held = 0;
        decimal? cost = null;
        foreach (var m in moves.OrderBy(m => m.OccurredAt))
        {
            if (m.Kind == StockMovementKind.Purchase && m.CostPrice is { } paid && m.Quantity > 0)
            {
                cost = cost is { } old && held > 0 ? ((held * old) + (m.Quantity * paid)) / (held + m.Quantity) : paid;
            }

            held += m.Quantity;
        }

        return cost is { } c ? decimal.Round(c, 2) : null;
    }

    private static readonly StockReason[] Losses = [StockReason.Damage, StockReason.Wastage, StockReason.Lost, StockReason.Stolen, StockReason.Destroyed, StockReason.WrittenOff, StockReason.Gift, StockReason.FreeSample];

    private static (DateOnly From, DateOnly To, DateTimeOffset Start, DateTimeOffset End)? Period(DateOnly? from, DateOnly? to, TimeProvider clock)
    {
        var today = IndiaTime.Today(clock);
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (start > end || end.DayNumber - start.DayNumber > MaxDays)
        {
            return null;
        }

        return (start, end, IndiaTime.Day(start).Start, IndiaTime.Day(end).End);
    }

    private static async Task<Dictionary<Guid, string>> NamesAsync(AppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => wanted.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static StocktakeSummary Summary(Stocktake t, Dictionary<Guid, string> names) =>
        new(t.Id, t.Number, t.TakenAt, names.GetValueOrDefault(t.CreatedByUserId, "—"), t.Items, t.Differences, t.ValueDifference);

    private static async Task<IResult> LedgerAsync(Guid id, DateOnly? from, DateOnly? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (Period(from, to, clock) is not { } p)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["From"] = [$"Pick dates in order, at most {MaxDays} days apart."] });
        }

        var product = await db.Products.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Name, x.Unit }).SingleOrDefaultAsync(ct);
        if (product is null)
        {
            return Results.NotFound();
        }

        var opening = await db.StockMovements.Where(m => m.ProductId == id && m.OccurredAt < p.Start).SumAsync(m => (decimal?)m.Quantity, ct) ?? 0m;
        var moves = await db.StockMovements.AsNoTracking().Where(m => m.ProductId == id && m.OccurredAt >= p.Start && m.OccurredAt < p.End)
            .OrderBy(m => m.OccurredAt).ThenBy(m => m.CreatedAt).Take(MaxLines).ToListAsync(ct);
        var names = await NamesAsync(db, moves.Select(m => m.CreatedByUserId), ct);
        var billIds = moves.Where(m => m.BillId != null).Select(m => m.BillId!.Value).Distinct().ToList();
        var billNos = await db.Bills.AsNoTracking().Where(b => billIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BillNo, ct);
        var purchaseIds = moves.Where(m => m.RefType == "purchase" && m.RefId != null).Select(m => m.RefId!.Value).Distinct().ToList();
        var purchases = await (from x in db.Purchases.AsNoTracking()
                               join s in db.Suppliers.AsNoTracking() on x.SupplierId equals s.Id
                               where purchaseIds.Contains(x.Id)
                               select new { x.Id, x.BillNo, s.Name }).ToDictionaryAsync(x => x.Id, ct);

        var balance = opening;
        var lines = new List<LedgerLine>(moves.Count);
        foreach (var m in moves)
        {
            balance += m.Quantity;
            var (docType, docId, label) = m.BillId is { } bill
                ? ("bill", (Guid?)bill, billNos.GetValueOrDefault(bill) is { } no ? $"Bill {no}" : "Bill")
                : m.RefType == "purchase" && m.RefId is { } pid && purchases.TryGetValue(pid, out var pur)
                    ? ("purchase", pid, $"{pur.Name}{(pur.BillNo is { } pno ? $" · {pno}" : "")}")
                    : (m.RefType, m.RefId, (string?)null);
            lines.Add(new LedgerLine(m.Id, m.OccurredAt, m.Kind, m.Reason, Math.Max(0, m.Quantity), Math.Max(0, -m.Quantity), balance,
                m.CostPrice, m.Note, names.GetValueOrDefault(m.CreatedByUserId, "—"), docType, docId, label));
        }

        return Results.Ok(new ItemLedger(id, product.Name, product.Unit, p.From, p.To, opening, lines.Sum(l => l.In), lines.Sum(l => l.Out), balance, lines));
    }

    private static async Task<IResult> StatementAsync(DateOnly? from, DateOnly? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (Period(from, to, clock) is not { } p)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["From"] = [$"Pick dates in order, at most {MaxDays} days apart."] });
        }

        var moves = await db.StockMovements.AsNoTracking().Where(m => m.OccurredAt < p.End)
            .Select(m => new { m.ProductId, m.Kind, m.Reason, m.Quantity, m.CostPrice, m.OccurredAt }).ToListAsync(ct);
        var products = await db.Products.AsNoTracking().Where(x => !x.IsService)
            .Select(x => new { x.Id, x.Name, x.Category, x.Unit, x.CostPrice, x.IsActive }).ToListAsync(ct);
        var byProduct = moves.ToLookup(m => m.ProductId);

        var items = new List<StockStatementLine>();
        foreach (var x in products)
        {
            var all = byProduct[x.Id].ToList();
            var before = all.Where(m => m.OccurredAt < p.Start).Sum(m => m.Quantity);
            var within = all.Where(m => m.OccurredAt >= p.Start).ToList();
            if (within.Count == 0 && before == 0 && !x.IsActive)
            {
                continue;
            }

            var purchased = within.Where(m => m.Kind is StockMovementKind.Purchase or StockMovementKind.Opening && m.Quantity > 0).Sum(m => m.Quantity);
            var sold = -within.Where(m => m.Kind == StockMovementKind.Sale).Sum(m => m.Quantity);
            var lost = -within.Where(m => m.Kind == StockMovementKind.Adjustment && m.Reason is { } r && Losses.Contains(r) && m.Quantity < 0).Sum(m => m.Quantity);
            var rest = within.Where(m => m.Kind is not (StockMovementKind.Purchase or StockMovementKind.Opening or StockMovementKind.Sale)).ToList();
            var otherIn = rest.Where(m => m.Quantity > 0).Sum(m => m.Quantity);
            var otherOut = -rest.Where(m => m.Quantity < 0).Sum(m => m.Quantity);
            var closing = before + within.Sum(m => m.Quantity);
            var cost = AverageCost(all.Select(m => (m.Kind, m.Quantity, m.CostPrice, m.OccurredAt))) ?? x.CostPrice;
            items.Add(new StockStatementLine(x.Id, x.Name, x.Category, x.Unit, before, purchased, sold, otherIn, otherOut, closing, cost,
                cost is { } c && closing > 0 ? decimal.Round(closing * c, 2) : null, lost));
        }

        items = items.OrderBy(i => i.Category ?? "~").ThenBy(i => i.Name).ToList();
        var lostValue = items.Sum(i => i.Lost * (i.CostPrice ?? 0));
        return Results.Ok(new StockStatement(p.From, p.To, items.Sum(i => i.Value ?? 0), items.Count(i => i.Closing > 0 && i.Value is null), decimal.Round(lostValue, 2), items));
    }

    private static async Task<IResult> SalesByAsync(string? by, DateOnly? from, DateOnly? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        by = by is "supplier" or "customer" ? by : "category";
        if (Period(from, to, clock) is not { } p)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["From"] = [$"Pick dates in order, at most {MaxDays} days apart."] });
        }

        var lines = await (from l in db.BillLines.AsNoTracking()
                           join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                           where b.CancelledAt == null && b.OccurredAt >= p.Start && b.OccurredAt < p.End
                           select new { l.BillId, l.ProductId, l.Amount, l.Quantity, l.UnitCost, b.CustomerId }).ToListAsync(ct);

        Dictionary<string, (string Key, string Name)> keyOf;
        Func<Guid?, Guid?, string> pick;
        if (by == "category")
        {
            var categories = await db.Products.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Category, ct);
            pick = (product, _) => product is { } id && categories.GetValueOrDefault(id) is { Length: > 0 } c ? c : "";
            keyOf = [];
        }
        else if (by == "supplier")
        {
            // The supplier an item was last bought from: who to talk to about what sells.
            var last = await (from pl in db.PurchaseLines.AsNoTracking()
                              join x in db.Purchases.AsNoTracking() on pl.PurchaseId equals x.Id
                              where x.CancelledAt == null
                              select new { pl.ProductId, x.SupplierId, x.OccurredAt }).ToListAsync(ct);
            var supplierOf = last.GroupBy(r => r.ProductId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.OccurredAt).First().SupplierId.ToString());
            pick = (product, _) => product is { } id && supplierOf.GetValueOrDefault(id) is { } s ? s : "";
            keyOf = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id.ToString(), s => (s.Id.ToString(), s.Name), ct);
        }
        else
        {
            pick = (_, customer) => customer?.ToString() ?? "";
            keyOf = await db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id.ToString(), c => (c.Id.ToString(), c.Name), ct);
        }

        var total = lines.Sum(l => l.Amount);
        var groups = lines.GroupBy(l => pick(l.ProductId, l.CustomerId)).Select(g =>
        {
            var costed = g.All(l => l.UnitCost != null);
            var name = g.Key.Length == 0 ? "" : keyOf.TryGetValue(g.Key, out var k) ? k.Name : g.Key;
            return new SalesGroup(g.Key, name, g.Sum(l => l.Amount), g.Sum(l => l.Quantity), g.Select(l => l.BillId).Distinct().Count(),
                costed ? decimal.Round(g.Sum(l => l.Amount - (l.Quantity * l.UnitCost!.Value)), 2) : null,
                total > 0 ? decimal.Round(g.Sum(l => l.Amount) * 100 / total, 1) : 0);
        }).OrderByDescending(g => g.Sales).Take(200).ToList();
        return Results.Ok(new SalesBy(by, p.From, p.To, total, groups));
    }
}
