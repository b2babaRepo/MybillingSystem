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
    decimal? CostPrice, string? Note, string By, string? DocType, Guid? DocId, string? DocLabel, decimal? Bags = null, decimal BagBalance = 0);

public sealed record ItemLedger(Guid ProductId, string Name, Unit Unit, DateOnly From, DateOnly To, decimal Opening, decimal In, decimal Out, decimal Closing, IReadOnlyList<LedgerLine> Lines,
    decimal? BagSize = null, decimal OpeningBags = 0, decimal ClosingBags = 0);

/// <summary>One item in the stock statement: opening, in, out, closing, and the closing value at average cost.</summary>
public sealed record StockStatementLine(
    Guid ProductId, string Name, string? Category, Unit Unit, decimal Opening, decimal Purchased, decimal Sold, decimal OtherIn, decimal OtherOut,
    decimal Closing, decimal? CostPrice, decimal? Value, decimal Lost, decimal? BagSize = null, decimal? ClosingBags = null,
    // Today's picture, whatever the dates: in the shop now, held for online orders, promised (sale orders and counter
    // holds), coming on open orders, the shared 30-day sale rate, and what to search and filter by.
    decimal InShop = 0, decimal Reserved = 0, decimal Promised = 0, decimal Coming = 0, decimal DailySales = 0, bool LateOrder = false,
    string? Barcode = null, string? Hsn = null, IReadOnlyList<Guid>? Suppliers = null);

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
            else if (m.Kind == StockMovementKind.Purchase && m.CostPrice is { } back && m.Quantity < 0 && cost is { } now && held + m.Quantity > 0)
            {
                // A cancelled supplier bill: its units leave the average at the cost they came in at.
                var left = ((held * now) + (m.Quantity * back)) / (held + m.Quantity);
                cost = left > 0 ? left : now;
            }

            held += m.Quantity;
        }

        return cost is { } c ? decimal.Round(c, 2) : null;
    }

    private sealed record CostRow(Guid ProductId, decimal Quantity, decimal Paid, decimal HeldBefore);

    /// <summary>
    /// Each item's weighted average cost up to <paramref name="end"/>, as <see cref="AverageCost"/> works it out, without
    /// loading the ledger: the database returns only the purchases (with what was held just before each), and the
    /// running average is folded over those few rows.
    /// </summary>
    private static async Task<Dictionary<Guid, decimal?>> AverageCostsAsync(AppDbContext db, Guid shopId, DateTimeOffset end, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<CostRow>($"""
            SELECT product_id AS "ProductId", quantity AS "Quantity", cost_price AS "Paid", held_before AS "HeldBefore"
            FROM (
                SELECT product_id, kind, quantity, cost_price, occurred_at, id,
                       COALESCE(SUM(quantity) OVER (PARTITION BY product_id ORDER BY occurred_at, id ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS held_before
                FROM stock_movements
                WHERE shop_id = {shopId} AND occurred_at < {end}
            ) m
            WHERE kind = 'Purchase' AND cost_price IS NOT NULL AND quantity <> 0
            ORDER BY product_id, occurred_at, id
            """).ToListAsync(ct);
        var result = new Dictionary<Guid, decimal?>();
        foreach (var g in rows.GroupBy(r => r.ProductId))
        {
            decimal? cost = null;
            foreach (var r in g)
            {
                if (r.Quantity < 0)
                {
                    // A cancelled supplier bill takes its units out of the average (as AverageCost does).
                    if (cost is { } now && r.HeldBefore + r.Quantity > 0 && ((r.HeldBefore * now) + (r.Quantity * r.Paid)) / (r.HeldBefore + r.Quantity) is > 0 and var left)
                    {
                        cost = left;
                    }

                    continue;
                }

                cost = cost is { } old && r.HeldBefore > 0 ? ((r.HeldBefore * old) + (r.Quantity * r.Paid)) / (r.HeldBefore + r.Quantity) : r.Paid;
            }

            result[g.Key] = cost is { } c ? decimal.Round(c, 2) : null;
        }

        return result;
    }

    private static readonly StockReason[] Losses = [StockReason.Damage, StockReason.Wastage, StockReason.Lost, StockReason.Stolen, StockReason.Destroyed, StockReason.WrittenOff, StockReason.Gift, StockReason.FreeSample, StockReason.WeightLoss];

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

        var product = await db.Products.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Name, x.Unit, x.BagSize }).SingleOrDefaultAsync(ct);
        if (product is null)
        {
            return Results.NotFound();
        }

        var opening = await db.StockMovements.Where(m => m.ProductId == id && m.OccurredAt < p.Start).SumAsync(m => (decimal?)m.Quantity, ct) ?? 0m;
        var openingBags = await db.StockMovements.Where(m => m.ProductId == id && m.OccurredAt < p.Start && m.Bags != null).SumAsync(m => (decimal?)m.Bags, ct) ?? 0m;
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

        var receiptIds = moves.Where(m => m.RefType == "receipt" && m.RefId != null).Select(m => m.RefId!.Value).Distinct().ToList();
        var receipts = await (from r in db.GoodsReceipts.AsNoTracking()
                              join s in db.Suppliers.AsNoTracking() on r.SupplierId equals s.Id
                              where receiptIds.Contains(r.Id)
                              select new { r.Id, r.Number, s.Name }).ToDictionaryAsync(x => x.Id, ct);
        var balance = opening;
        var bagBalance = openingBags;
        var lines = new List<LedgerLine>(moves.Count);
        foreach (var m in moves)
        {
            balance += m.Quantity;
            bagBalance += m.Bags ?? 0;
            var (docType, docId, label) = m.BillId is { } bill
                ? ("bill", (Guid?)bill, billNos.GetValueOrDefault(bill) is { } no ? $"Bill {no}" : "Bill")
                : m.RefType == "purchase" && m.RefId is { } pid && purchases.TryGetValue(pid, out var pur)
                    ? ("purchase", pid, $"{pur.Name}{(pur.BillNo is { } pno ? $" · {pno}" : "")}")
                    : m.RefType == "receipt" && m.RefId is { } rid && receipts.TryGetValue(rid, out var rec)
                        ? ("receipt", rid, $"{rec.Name} · receipt #{rec.Number}")
                        : (m.RefType, m.RefId, (string?)null);
            lines.Add(new LedgerLine(m.Id, m.OccurredAt, m.Kind, m.Reason, Math.Max(0, m.Quantity), Math.Max(0, -m.Quantity), balance,
                m.CostPrice, m.Note, names.GetValueOrDefault(m.CreatedByUserId, "—"), docType, docId, label, m.Bags, bagBalance));
        }

        return Results.Ok(new ItemLedger(id, product.Name, product.Unit, p.From, p.To, opening, lines.Sum(l => l.In), lines.Sum(l => l.Out), balance, lines, product.BagSize, openingBags, bagBalance));
    }

    internal static async Task<IResult> StatementAsync(DateOnly? from, DateOnly? to, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (Period(from, to, clock) is not { } p)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["From"] = [$"Pick dates in order, at most {MaxDays} days apart."] });
        }

        // The database adds up each item's movements (one row per item, however many years of history), so the
        // statement stays quick as a shop's ledger grows. Losses are the write-off reasons; Opening counts as bought.
        var start = p.Start;
        var totals = await db.StockMovements.AsNoTracking().Where(m => m.OccurredAt < p.End).GroupBy(m => m.ProductId).Select(g => new
        {
            ProductId = g.Key,
            Before = g.Sum(m => m.OccurredAt < start ? m.Quantity : 0),
            Moves = g.Count(m => m.OccurredAt >= start),
            Purchased = g.Sum(m => m.OccurredAt >= start && (m.Kind == StockMovementKind.Purchase || (m.Kind == StockMovementKind.Opening && m.Quantity > 0)) ? m.Quantity : 0),
            // A cancelled bill's goods coming back undo its sale: they come off Sold, not into "other in".
            Sold = -g.Sum(m => m.OccurredAt >= start && (m.Kind == StockMovementKind.Sale || (m.Kind == StockMovementKind.Adjustment && m.BillId != null && m.Note == "Bill cancelled")) ? m.Quantity : 0),
            Lost = -g.Sum(m => m.OccurredAt >= start && m.Kind == StockMovementKind.Adjustment && m.Reason != null && Losses.Contains(m.Reason.Value) && m.Quantity < 0 ? m.Quantity : 0),
            OtherIn = g.Sum(m => m.OccurredAt >= start && m.Kind != StockMovementKind.Purchase && m.Kind != StockMovementKind.Opening && m.Kind != StockMovementKind.Sale
                && !(m.Kind == StockMovementKind.Adjustment && m.BillId != null && m.Note == "Bill cancelled") && m.Quantity > 0 ? m.Quantity : 0),
            OtherOut = -g.Sum(m => m.OccurredAt >= start && m.Kind != StockMovementKind.Purchase && m.Kind != StockMovementKind.Opening && m.Kind != StockMovementKind.Sale && m.Quantity < 0 ? m.Quantity : 0),
            Closing = g.Sum(m => m.Quantity),
            Bags = g.Sum(m => m.Bags ?? 0),
        }).ToDictionaryAsync(t => t.ProductId, ct);
        var costs = await AverageCostsAsync(db, tenant.ShopId, p.End, ct);
        var products = await db.Products.AsNoTracking().Where(x => !x.IsService)
            .Select(x => new { x.Id, x.Name, x.Category, x.Unit, x.CostPrice, x.IsActive, x.BagSize, x.Stock, x.Reserved, x.Barcode, x.HsnCode }).ToListAsync(ct);
        var promised = await Ledger.SaleOrders.PromisedAsync(db, ct);
        var coming = await Ledger.PurchaseOrders.PendingAsync(db, ct);
        var sold30 = await Cover.SoldAsync(db, clock, ct);
        var today = IndiaTime.Today(clock);
        var late = (await (from l in db.PurchaseOrderLines.AsNoTracking()
                           join o in db.PurchaseOrders.AsNoTracking() on l.OrderId equals o.Id
                           where o.Status == Ledger.PurchaseOrderStatus.Open && o.ExpectedOn != null && o.ExpectedOn < today && l.Received + l.ShortClosed < l.Quantity
                           select l.ProductId).ToListAsync(ct)).ToHashSet();
        var supplied = (await db.ArrivalLines.AsNoTracking().Where(a => a.CancelledAt == null).Select(a => new { a.ProductId, a.SupplierId }).Distinct().ToListAsync(ct))
            .GroupBy(a => a.ProductId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(a => a.SupplierId).ToList());

        var items = new List<StockStatementLine>();
        foreach (var x in products)
        {
            var t = totals.GetValueOrDefault(x.Id);
            var (before, closing) = (t?.Before ?? 0, t?.Closing ?? 0);
            if ((t?.Moves ?? 0) == 0 && before == 0 && !x.IsActive)
            {
                continue;
            }

            // Net of cancelled bills and receipts (negative purchases).
            var cost = costs.GetValueOrDefault(x.Id) ?? x.CostPrice;
            items.Add(new StockStatementLine(x.Id, x.Name, x.Category, x.Unit, before, t?.Purchased ?? 0, t?.Sold ?? 0, t?.OtherIn ?? 0, t?.OtherOut ?? 0, closing, cost,
                cost is { } c && closing > 0 ? decimal.Round(closing * c, 2) : null, t?.Lost ?? 0, x.BagSize, x.BagSize is null ? null : t?.Bags ?? 0,
                x.Stock, x.Reserved, promised.GetValueOrDefault(x.Id), coming.GetValueOrDefault(x.Id), decimal.Round(sold30.GetValueOrDefault(x.Id) / Cover.Days, 3), late.Contains(x.Id),
                x.Barcode, x.HsnCode, supplied.GetValueOrDefault(x.Id) ?? []));
        }

        items = items.OrderBy(i => i.Category ?? "~").ThenBy(i => i.Name).ToList();
        var lostValue = items.Sum(i => i.Lost * (i.CostPrice ?? 0));
        // What goods cost is the owner's: a helper sees the units, not the cost or the stock's value.
        if (!Ledger.LedgerEndpoints.IsOwner(user))
        {
            return Results.Ok(new StockStatement(p.From, p.To, 0, 0, 0, items.Select(i => i with { CostPrice = null, Value = null }).ToList()));
        }

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
            // Stock on the shelf is one pile, so a sale can't say whose goods it was. Each supplier gets the item's sales in
            // proportion to what they delivered of it in the period (or ever, when nothing came in the period).
            var came = await db.ArrivalLines.AsNoTracking().Where(a => a.CancelledAt == null)
                .Select(a => new { a.ProductId, a.SupplierId, a.Received, In = a.OccurredAt >= p.Start && a.OccurredAt < p.End }).ToListAsync(ct);
            var shares = came.GroupBy(a => a.ProductId).ToDictionary(g => g.Key, g =>
            {
                var rows = g.Any(a => a.In) ? g.Where(a => a.In).ToList() : g.ToList();
                var sum = rows.Sum(a => a.Received);
                return rows.GroupBy(a => a.SupplierId).Select(s => (Id: s.Key.ToString(), Share: sum > 0 ? s.Sum(a => a.Received) / sum : 0)).ToList();
            });
            keyOf = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id.ToString(), s => (s.Id.ToString(), s.Name), ct);
            var split = lines.SelectMany(l => l.ProductId is { } id && shares.TryGetValue(id, out var sh) && sh.Count > 0
                ? sh.Select(x => (Key: x.Id, l.BillId, Amount: decimal.Round(l.Amount * x.Share, 2), Quantity: l.Quantity * x.Share, Cost: l.UnitCost is { } c ? (decimal?)(c * l.Quantity * x.Share) : null))
                : [(Key: "", l.BillId, l.Amount, l.Quantity, Cost: l.UnitCost is { } c2 ? (decimal?)(c2 * l.Quantity) : null)]).ToList();
            return Results.Ok(SupplierShares(by, p, split, keyOf));
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

    /// <summary>Sales split by supplier share: each part of a sale line goes to a supplier by their share of the item's deliveries.</summary>
    private static SalesBy SupplierShares(string by, (DateOnly From, DateOnly To, DateTimeOffset Start, DateTimeOffset End) p,
        List<(string Key, Guid BillId, decimal Amount, decimal Quantity, decimal? Cost)> split, Dictionary<string, (string Key, string Name)> keyOf)
    {
        var total = split.Sum(l => l.Amount);
        var groups = split.GroupBy(l => l.Key).Select(g =>
        {
            var costed = g.All(l => l.Cost != null);
            var name = g.Key.Length == 0 ? "" : keyOf.TryGetValue(g.Key, out var k) ? k.Name : g.Key;
            return new SalesGroup(g.Key, name, g.Sum(l => l.Amount), decimal.Round(g.Sum(l => l.Quantity), 3), g.Select(l => l.BillId).Distinct().Count(),
                costed ? decimal.Round(g.Sum(l => l.Amount - l.Cost!.Value), 2) : null,
                total > 0 ? decimal.Round(g.Sum(l => l.Amount) * 100 / total, 1) : 0);
        }).OrderByDescending(g => g.Sales).Take(200).ToList();
        return new SalesBy(by, p.From, p.To, total, groups);
    }
}
