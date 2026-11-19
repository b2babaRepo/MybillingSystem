using GroceryKhata.Api.Billing;
using System.Globalization;
using System.Text;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// One month of the year report. <see cref="Turnover"/> is sales without the GST inside them; <see cref="NoRate"/> is
/// sales on GST bills with no rate set, counted in turnover as they are, so a CA can check them.
/// </summary>
public sealed record YearMonthRow(string Month, int Bills, decimal Sales, decimal Gst, decimal Turnover, decimal NoRate, decimal Cash, decimal Digital);

/// <summary>A financial year (April to March) for the owner and their CA. A working aid: not a return.</summary>
public sealed record YearReport(string FinancialYear, IReadOnlyList<YearMonthRow> Months, YearMonthRow Total);

/// <summary>
/// A financial year's profit and loss. Opening and closing stock are rebuilt from the append-only stock movements,
/// so 31 March can be read back at any time. <see cref="ItemsWithoutCost"/> are left out of stock value.
/// </summary>
public sealed record ProfitLossReport(
    string FinancialYear, decimal Sales, decimal Turnover, decimal Gst, decimal GoodsProfit, decimal Returns, decimal StockLoss, decimal Expenses, decimal NetProfit,
    decimal TakenHome, decimal Purchases, decimal OpeningStock, decimal ClosingStock, DateOnly ClosingDate, decimal SupplierDue, decimal CustomerDue, int ItemsWithoutCost, decimal ProfitCoverage,
    decimal WorkInProgress = 0, decimal MachineWear = 0, decimal FinishedGoods = 0,
    decimal CashReceipts = 0, decimal DigitalReceipts = 0, decimal PresumedProfit = 0, decimal LastYearTurnover = 0, IReadOnlyList<TurnoverAlert>? TurnoverAlerts = null,
    IReadOnlyList<KharchaLine>? ExpenseLines = null, IReadOnlyList<KharchaLine>? NotInProfit = null, decimal BagsSold = 0, decimal BagsBought = 0);

/// <summary>A turnover limit the shop has crossed or is near: <see cref="ReportEndpoints.TurnoverLimits"/>.</summary>
public sealed record TurnoverAlert(string Key, decimal Limit, decimal Turnover, bool Crossed);

/// <summary>What the owner should look at today: old dues to chase and what suppliers are owed.</summary>
public sealed record DigestDto(int OldDueCustomers, decimal OldDueAmount, decimal SupplierDue);

/// <summary>One customer's dues by age. The three buckets always add up to <see cref="Balance"/>.</summary>
public sealed record DueDto(
    Guid CustomerId,
    string Name,
    string? Phone,
    decimal Balance,
    decimal Days0To7,
    decimal Days8To30,
    decimal Over30,
    int OldestDays,
    DateTimeOffset? LastPaymentAt,
    DateTimeOffset? LastRemindedAt,
    bool MoneyWaiting = false,
    // Over30 split the way accounts read it: 31-60, 61-90 and over 90 days.
    decimal Days31To60 = 0,
    decimal Days61To90 = 0,
    decimal Over90 = 0,
    // With credit days: the udhaar past its due date, and when the oldest unpaid udhaar is (or was) due.
    int? CreditDays = null,
    decimal Overdue = 0,
    DateOnly? DueOn = null);

public sealed record DuesReport(decimal Total, decimal Days0To7, decimal Days8To30, decimal Over30, IReadOnlyList<DueDto> Customers,
    decimal Days31To60 = 0, decimal Days61To90 = 0, decimal Over90 = 0, decimal Overdue = 0);

public sealed record DailyTotals(DateOnly Date, decimal Sales, decimal Received);

public sealed record TopItem(string Name, decimal Quantity, Catalogue.Unit Unit, decimal Amount);

/// <summary>One item's month: sales without GST, what it cost, the profit and margin. Cost and profit are null when no cost price is known.</summary>
public sealed record ItemMargin(Guid? ProductId, string Name, decimal Quantity, Catalogue.Unit Unit, decimal Sales, decimal? Cost, decimal? Profit, decimal? MarginPercent);

/// <summary>One person's month: lines and bills they sold, the sales (with GST, as billed), and their commission on it.</summary>
public sealed record StaffSales(Guid UserId, string Name, int Bills, int Lines, decimal Sales, decimal? CommissionPercent, decimal Commission);

/// <summary>One line sold below its list price: who, when, which bill, by how much, and whether an offer did it.</summary>
public sealed record DiscountLine(DateTimeOffset At, string? BillNo, string Item, decimal Quantity, decimal ListPrice, decimal SoldAt, decimal Discount, string? By, string? Offer);

/// <summary>A cashier's month of price cuts: by hand (their own) apart from planned offers.</summary>
public sealed record CashierDiscounts(Guid UserId, string Name, int Bills, decimal Sales, decimal ByHand, int ByHandLines, decimal ByOffer, decimal ByHandPercent);

public sealed record DiscountReport(string Month, decimal ByHand, decimal ByOffer, IReadOnlyList<CashierDiscounts> Cashiers, IReadOnlyList<DiscountLine> Lines);

public sealed record ItemMarginReport(string Month, decimal Sales, decimal Profit, decimal ProfitCoverage, IReadOnlyList<ItemMargin> Items);

/// <summary>
/// One row of profit grouped another way: a customer (<see cref="Id"/> null for walk-in sales), a bill or a category.
/// <see cref="Profit"/> counts only lines with a cost, as in the item report; <see cref="Uncosted"/> is the sale without one.
/// </summary>
public sealed record ProfitRow(string Key, Guid? Id, string Name, string? Detail, int Bills, decimal Sales, decimal? Cost, decimal? Profit, decimal? MarginPercent, decimal Uncosted, DateTimeOffset? At = null);

public sealed record ProfitByReport(string Month, string By, decimal Sales, decimal Profit, IReadOnlyList<ProfitRow> Rows);

/// <summary>
/// One month at a glance. <see cref="EstimatedProfit"/> counts only bill lines whose item has a cost price;
/// <see cref="ProfitCoverage"/> says what share of sales that is, so a low number reads as "add cost prices".
/// </summary>
public sealed record MonthReport(
    string Month,
    decimal Sales,
    int Bills,
    decimal Cash,
    decimal Upi,
    decimal UdhaarGiven,
    decimal KhataCollected,
    decimal Outstanding,
    decimal EstimatedProfit,
    decimal ProfitCoverage,
    IReadOnlyList<TopItem> TopItems,
    IReadOnlyList<DailyTotals> Days,
    decimal Expenses = 0,
    decimal StockPurchases = 0,
    decimal TakenHome = 0,
    decimal Returns = 0,
    decimal StockLoss = 0,
    decimal StockValue = 0,
    decimal SupplierDue = 0,
    MonthStatement? Statement = null,
    decimal Card = 0,
    decimal Cheque = 0,
    decimal BagsSold = 0,
    decimal BagsBought = 0)
{
    /// <summary>
    /// Estimated profit less running costs, plus what empty bags fetched less what bags cost. Stock bought isn't taken
    /// off (sales already count goods at cost); money taken home isn't a cost.
    /// </summary>
    public decimal NetProfit => EstimatedProfit - Expenses + BagsSold - BagsBought;
}

/// <summary>One heading's money in the month statement.</summary>
public sealed record KharchaLine(ExpenseCategory Category, decimal Amount);

/// <summary>
/// The month on one page, the way an accountant reads it: sales, less GST, less what the goods cost, gives gross
/// profit; less each running cost gives net profit. Money out that is not a cost is listed after, apart.
/// Gross profit counts only items with a cost written, so the sales with no cost are a line of their own.
/// </summary>
public sealed record MonthStatement(
    decimal SalesWithGst,
    decimal RoundOff,
    decimal GstInSales,
    decimal Sales,
    decimal NoCostSales,
    decimal CostOfGoods,
    decimal Returns,
    decimal StockLoss,
    decimal GrossProfit,
    IReadOnlyList<KharchaLine> Running,
    decimal Expenses,
    decimal NetProfit,
    IReadOnlyList<KharchaLine> NotInProfit,
    decimal BagsSold = 0,
    decimal BagsBought = 0);

/// <summary>
/// Ageing by first-in, first-out: every payment (and every reversal of udhaar) pays off the oldest udhaar first,
/// so what is still unpaid is the newest udhaar, aged by the day it was given.
/// </summary>
public static class Ageing
{
    public sealed record Entry(DateTimeOffset At, decimal Amount);

    /// <summary>The unpaid pieces of udhaar, each with the day it was given. Their sum is the balance (when positive).</summary>
    public static IReadOnlyList<Entry> Unpaid(IEnumerable<Entry> entries)
    {
        var open = new LinkedList<Entry>();
        var credit = 0m; // payments made before there was udhaar to pay: an advance, used up by later udhaar
        foreach (var entry in entries.OrderBy(e => e.At))
        {
            if (entry.Amount > 0)
            {
                var amount = entry.Amount;
                var fromAdvance = Math.Min(credit, amount);
                credit -= fromAdvance;
                amount -= fromAdvance;
                if (amount > 0)
                {
                    open.AddLast(entry with { Amount = amount });
                }
            }
            else
            {
                var paying = -entry.Amount;
                while (paying > 0 && open.First is { } oldest)
                {
                    var used = Math.Min(paying, oldest.Value.Amount);
                    paying -= used;
                    if (used == oldest.Value.Amount)
                    {
                        open.RemoveFirst();
                    }
                    else
                    {
                        oldest.Value = oldest.Value with { Amount = oldest.Value.Amount - used };
                    }
                }

                credit += paying;
            }
        }

        return [.. open];
    }
}

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Reports");
        group.MapGet("/reports/dues", (AppDbContext db, TimeProvider clock, CancellationToken ct) => DuesAsync(db, clock, ct))
            .RequireAuthorization(AppPolicies.ShopMember);
        // The month report and downloads are in Basic and Pro; the dues list (collecting money owed) is in every plan.
        group.MapGet("/reports/month", MonthAsync).RequireAuthorization(AppPolicies.ShopMember).RequireFeature(Billing.Features.Reports);
        group.MapPost("/customers/{id:guid}/reminded", RemindedAsync).RequireAuthorization(AppPolicies.ShopMember);
        // Downloads hold every customer's dues or the month's figures: the owner's data to take away.
        group.MapGet("/reports/dues.csv", DuesCsvAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/reports/supplier-dues.csv", SupplierDuesCsvAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/digest", async (AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var dues = await DuesAsync(db, clock, ct);
            return new DigestDto(dues.Customers.Count(d => d.Over30 > 0), dues.Over30, await db.SupplierEntries.AsNoTracking().SumAsync(e => (decimal?)e.Amount, ct) ?? 0m);
        }).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/reports/bill-speed", async (AppDbContext db, TimeProvider clock, CancellationToken ct) => TypedResults.Ok(await BillSpeed.ReportAsync(db, clock, ct)))
            .RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/pnl", ProfitLossAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/pnl.csv", ProfitLossCsvAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/year", YearAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/year.csv", YearCsvAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/month.csv", MonthCsvAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/staff", StaffSalesAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/discounts", DiscountsAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/items", ItemMarginsAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        group.MapGet("/reports/profit-by", ProfitByAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Reports);
        return app;
    }

    public static async Task<DuesReport> DuesAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking().Where(c => c.Balance > 0).ToListAsync(ct);
        var ids = customers.Select(c => c.Id).ToList();
        var entries = (await db.LedgerEntries.AsNoTracking().Where(e => ids.Contains(e.CustomerId))
                .Select(e => new { e.CustomerId, e.OccurredAt, e.Amount, e.PaymentMode }).ToListAsync(ct))
            .ToLookup(e => e.CustomerId);
        var today = IndiaTime.Today(clock);
        // Money seen lately that may be theirs: chasing someone who just paid is what loses a customer.
        var recent = clock.GetUtcNow().AddHours(-72);
        var waiting = (await Receipts.OpenAsync(db, ct)).Where(x => x.Receipt.ReceivedAt >= recent).ToList();

        var dues = customers.Select(c =>
        {
            var own = entries[c.Id].ToList();
            var unpaid = Ageing.Unpaid(own.Select(e => new Ageing.Entry(e.OccurredAt, e.Amount)));
            int Age(Ageing.Entry e) => today.DayNumber - DateOnly.FromDateTime(e.At.ToOffset(IndiaTime.Offset).DateTime).DayNumber;
            var lastPayment = own.Where(e => e.Amount < 0 && e.PaymentMode != null).Select(e => (DateTimeOffset?)e.OccurredAt).Max();
            // With credit days, each unpaid udhaar is due that many days after it was taken.
            var overdue = c.CreditDays is { } days ? unpaid.Where(e => Age(e) > days).Sum(e => e.Amount) : 0m;
            DateOnly? dueOn = c.CreditDays is { } d2 && unpaid.Count > 0 ? today.AddDays(d2 - Age(unpaid[0])) : null;
            return new DueDto(
                c.Id, c.Name, c.Phone, c.Balance,
                unpaid.Where(e => Age(e) <= 7).Sum(e => e.Amount),
                unpaid.Where(e => Age(e) is > 7 and <= 30).Sum(e => e.Amount),
                unpaid.Where(e => Age(e) > 30).Sum(e => e.Amount),
                unpaid.Count == 0 ? 0 : Age(unpaid[0]),
                lastPayment,
                c.LastRemindedAt,
                waiting.Any(x => x.Receipt.ClaimedByCustomerId == c.Id || x.Left == c.Balance),
                unpaid.Where(e => Age(e) is > 30 and <= 60).Sum(e => e.Amount),
                unpaid.Where(e => Age(e) is > 60 and <= 90).Sum(e => e.Amount),
                unpaid.Where(e => Age(e) > 90).Sum(e => e.Amount),
                c.CreditDays, overdue, dueOn);
        })
        // Overdue first (past what was agreed), then the oldest money.
        .OrderByDescending(d => d.Overdue).ThenByDescending(d => d.Over90).ThenByDescending(d => d.Over30).ThenByDescending(d => d.Days8To30).ThenByDescending(d => d.Balance)
        .ToList();

        return new DuesReport(dues.Sum(d => d.Balance), dues.Sum(d => d.Days0To7), dues.Sum(d => d.Days8To30), dues.Sum(d => d.Over30), dues,
            dues.Sum(d => d.Days31To60), dues.Sum(d => d.Days61To90), dues.Sum(d => d.Over90), dues.Sum(d => d.Overdue));
    }

    private static async Task<IResult> RemindedAsync(Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // A direct update: a reminder is bookkeeping, not an edit to audit.
        var updated = await db.Customers.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.LastRemindedAt, now), ct);
        if (updated == 0)
        {
            return TypedResults.NotFound();
        }

        var owed = await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Balance).SingleAsync(ct);
        db.ReminderLogs.Add(new ReminderLog { CustomerId = id, Amount = owed, UserId = user.UserId()!.Value, SentAt = now });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> MonthAsync(string? month, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        ParseOrThisMonth(month, clock) is { } period
            // Profit shows what goods cost: for the owner, not helpers.
            ? TypedResults.Ok(await BuildMonthAsync(db, period, ct) is var report && user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner))
                ? report
                : report with { Statement = null, EstimatedProfit = 0, ProfitCoverage = 0, Expenses = 0, StockPurchases = 0, TakenHome = 0, Returns = 0, StockLoss = 0, StockValue = 0, SupplierDue = 0 })
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });

    public static async Task<MonthReport> BuildMonthAsync(AppDbContext db, (int Year, int Month) period, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines)
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null)
            .ToListAsync(ct);
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => (e.OccurredAt >= start && e.OccurredAt < end))
            .Select(e => new { e.OccurredAt, e.Amount, e.PaymentMode, e.BillId, e.Kind })
            .ToListAsync(ct);
        var productIds = bills.SelectMany(b => b.Lines).Where(l => l.ProductId != null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var costs = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id) && p.CostPrice != null)
            .ToDictionaryAsync(p => p.Id, p => p.CostPrice!.Value, ct);

        // Empty bags sold to the bardana dealer (money in, other income) and bags bought (a cost), as in the cash book.
        var bagMoney = await db.BagMoves.AsNoTracking().Where(m => m.OccurredAt >= start && m.OccurredAt < end && m.StruckAt == null && m.Amount != null && m.Mode != null)
            .Select(m => new { m.Kind, m.Mode, Amount = m.Amount!.Value }).ToListAsync(ct);
        var bagsSold = bagMoney.Where(m => m.Kind == BagMoveKind.Sold).Sum(m => m.Amount);
        var bagsBought = bagMoney.Where(m => m.Kind == BagMoveKind.Bought).Sum(m => m.Amount);

        // Money in, the cash-book way: bills paid at the counter, khata payments (and their reversals) not tied to a bill, and bags sold.
        decimal Received(PaymentMode mode) =>
            bills.Sum(b => b.PaidBy(mode))
            - entries.Where(e => e.PaymentMode == mode && e.BillId == null).Sum(e => e.Amount)
            + bagMoney.Where(m => m.Kind == BagMoveKind.Sold && m.Mode == mode).Sum(m => m.Amount);
        var khataCollected = -entries.Where(e => e.PaymentMode != null && e.BillId == null).Sum(e => e.Amount);
        var udhaar = entries.Where(e => e.PaymentMode == null && e.Kind != LedgerEntryKind.Opening).Sum(e => e.Amount)
            + entries.Where(e => e.PaymentMode != null && e.BillId != null).Sum(e => e.Amount);

        var lines = bills.SelectMany(b => b.Lines).ToList();
        // The cost when sold; bills from before that was kept use today's. Lines with neither aren't counted.
        decimal? Cost(BillLine l) => l.UnitCost ?? (l.ProductId is { } id && costs.TryGetValue(id, out var cost) ? cost : null);
        var costed = lines.Where(l => Cost(l) is not null).ToList();
        var taxed = bills.Where(b => b.Gstin != null && !b.BillOfSupply).Select(b => b.Id).ToHashSet();
        // GST inside the price belongs to the government, not the shop.
        // The value without GST as stored when billed; older bills work it out from the rate. A composition shop's bill of
        // supply carries no GST, so its whole amount is the shop's.
        decimal WithoutGst(BillLine l) => !taxed.Contains(l.BillId) ? l.Amount : l.TaxableValue ?? (l.GstRate is > 0 ? decimal.Round(l.Amount / (1 + (l.GstRate.Value / 100)), 2) : l.Amount);
        // Returns without a credit note (order disputes): goods never reached the customer, so the whole refund is lost.
        var returns = -await db.LedgerEntries.AsNoTracking().Where(e => e.Kind == LedgerEntryKind.Return && e.OccurredAt >= start && e.OccurredAt < end
            && (e.Note == null || !(e.Note.StartsWith("CN/") || e.Note.StartsWith("RN/")))).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        // Credit notes: the sale (without its GST) is undone; goods put back on the shelf keep their cost, so only the margin goes.
        var notes = await db.CreditNotes.AsNoTracking().Include(n => n.Lines).Where(n => n.OccurredAt >= start && n.OccurredAt < end).ToListAsync(ct);
        foreach (var note in notes)
        {
            var noteBill = await db.BillLines.AsNoTracking().Where(l => l.BillId == note.BillId).ToDictionaryAsync(l => l.LineNo, ct);
            foreach (var line in note.Lines)
            {
                var sold = noteBill.GetValueOrDefault(line.BillLineNo);
                var cost = sold is null ? null : sold.UnitCost ?? (sold.ProductId is { } pid && costs.TryGetValue(pid, out var c) ? c : (decimal?)null);
                returns += note.Restocked && cost is { } each ? line.TaxableValue - Money.LineAmount(line.Quantity, each) : line.TaxableValue;
            }
        }
        StockReason[] lossReasons = [StockReason.Damage, StockReason.Wastage, StockReason.CountCorrection];
        var moved = await (from m in db.StockMovements.AsNoTracking()
                           where m.Kind == StockMovementKind.Adjustment && m.OccurredAt >= start && m.OccurredAt < end && m.Reason != null && lossReasons.Contains(m.Reason.Value)
                           join p in db.Products.AsNoTracking() on m.ProductId equals p.Id
                           select new { m.Quantity, Cost = m.CostPrice ?? p.CostPrice }).ToListAsync(ct);
        var stockLoss = moved.Where(m => m.Cost != null).Sum(m => decimal.Round(-m.Quantity * m.Cost!.Value, 2));
        var profit = costed.Sum(l => WithoutGst(l) - Money.LineAmount(l.Quantity, Cost(l)!.Value)) - returns - stockLoss;
        var sales = bills.Sum(b => b.Total);

        var days = Enumerable.Range(0, DateTime.DaysInMonth(period.Year, period.Month)).Select(i =>
        {
            var date = new DateOnly(period.Year, period.Month, 1).AddDays(i);
            var (dayStart, dayEnd) = IndiaTime.Day(date);
            var dayBills = bills.Where(b => b.OccurredAt >= dayStart && b.OccurredAt < dayEnd).ToList();
            var received = dayBills.Sum(b => b.PaidAmount)
                - entries.Where(e => e.PaymentMode != null && e.BillId == null && e.OccurredAt >= dayStart && e.OccurredAt < dayEnd).Sum(e => e.Amount);
            return new DailyTotals(date, dayBills.Sum(b => b.Total), received);
        }).ToList();

        var top = lines.GroupBy(l => (Key: l.ProductId?.ToString() ?? l.Name.ToLowerInvariant(), l.Unit))
            .Select(g => new TopItem(g.First().Name, g.Sum(l => l.Quantity), g.Key.Unit, g.Sum(l => l.Amount)))
            .OrderByDescending(t => t.Amount).Take(10).ToList();

        var outstanding = await db.Customers.AsNoTracking().Where(c => c.Balance > 0).SumAsync(c => (decimal?)c.Balance, ct) ?? 0m;
        var spent = await db.Expenses.AsNoTracking()
            // A walk-in refund on a credit note is already counted as a return (Kharcha.IsReturnRefund).
            .Where(e => e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null && (e.Note == null || !(e.Note.StartsWith("Refund CN/") || e.Note.StartsWith("Refund RN/"))))
            .GroupBy(e => e.Category).Select(g => new { g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(g => g.Key, g => g.Total, ct);
        decimal Spent(ExpenseCategory c) => spent.GetValueOrDefault(c);
        var running = spent.Where(x => !Kharcha.OutsideProfit.Contains(x.Key) && x.Value != 0).OrderByDescending(x => x.Value).Select(x => new KharchaLine(x.Key, x.Value)).ToList();
        var notInProfit = Kharcha.OutsideProfit.Where(c => Spent(c) != 0).Select(c => new KharchaLine(c, Spent(c))).ToList();
        var expenses = running.Sum(x => x.Amount);
        var salesWithoutGst = lines.Sum(WithoutGst);
        var linesTotal = lines.Sum(l => l.Amount);
        var statement = new MonthStatement(
            sales, sales - linesTotal, linesTotal - salesWithoutGst, salesWithoutGst,
            salesWithoutGst - costed.Sum(WithoutGst), costed.Sum(l => Money.LineAmount(l.Quantity, Cost(l)!.Value)),
            returns, stockLoss, profit, running, expenses, profit - expenses + bagsSold - bagsBought, notInProfit, bagsSold, bagsBought);
        // Today's figures, whichever month is open: stock at cost (items with a count and a cost) and what suppliers are owed.
        var stockValue = await db.Products.AsNoTracking().Where(p => p.IsActive && !p.Uncounted && p.Stock > 0 && p.CostPrice != null).SumAsync(p => (decimal?)(p.Stock * p.CostPrice!.Value), ct) ?? 0m;
        var supplierDue = await db.SupplierEntries.AsNoTracking().SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        return new MonthReport(
            $"{period.Year:D4}-{period.Month:D2}", sales, bills.Count, Received(PaymentMode.Cash), Received(PaymentMode.Upi), udhaar, khataCollected,
            outstanding, profit, sales == 0 ? 0 : decimal.Round(costed.Sum(l => l.Amount) / sales, 2), top, days,
            expenses, Spent(ExpenseCategory.Purchase), Spent(ExpenseCategory.Home), returns, stockLoss, stockValue, supplierDue, statement,
            Received(PaymentMode.Card), Received(PaymentMode.Cheque), bagsSold, bagsBought);
    }

    /// <summary>
    /// Each item's sales, cost, profit and margin for a month, on the same rules as the month's profit: the cost when
    /// sold (today's for older bills), GST taken out of taxed bills. Items with no cost price are listed without a profit.
    /// </summary>
    /// <summary>
    /// Sales by salesperson for a month (cancelled bills left out): each line counts for whoever sold it, or for whoever
    /// made the bill when no one was named. Commission is their percent of those sales.
    /// </summary>
    private static async Task<IResult> StaffSalesAsync(string? month, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (ParseOrThisMonth(month, clock) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var lines = await (from l in db.BillLines.AsNoTracking()
                           join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                           where b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null
                           select new { Seller = l.SalespersonId ?? b.CreatedByUserId, l.BillId, l.Amount }).ToListAsync(ct);
        var people = await (from m in db.Memberships.AsNoTracking()
                            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                            where m.ShopId == tenant.ShopId
                            select new { u.Id, u.FullName, m.CommissionPercent }).ToListAsync(ct);
        var rows = lines.GroupBy(l => l.Seller).Select(g =>
        {
            var who = people.FirstOrDefault(p => p.Id == g.Key);
            var sales = g.Sum(l => l.Amount);
            var percent = who?.CommissionPercent;
            return new StaffSales(g.Key, who?.FullName ?? "—", g.Select(l => l.BillId).Distinct().Count(), g.Count(), sales, percent,
                percent is { } p ? decimal.Round(sales * p / 100, 2) : 0);
        }).OrderByDescending(r => r.Sales).ToList();
        return TypedResults.Ok(rows);
    }

    /// <summary>
    /// Every line sold below its list price in a month (cancelled bills left out), by cashier: the salesperson named on
    /// the line, else who made the bill. Price cuts by hand are kept apart from planned offers, so a cashier who gives
    /// away more than others stands out.
    /// </summary>
    private static async Task<IResult> DiscountsAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ParseOrThisMonth(month, clock) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var lines = await (from l in db.BillLines.AsNoTracking()
                           join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                           where b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null
                           select new { b.Id, b.BillNo, b.OccurredAt, Seller = l.SalespersonId ?? b.CreatedByUserId, l.Name, l.Quantity, l.UnitPrice, l.ListPrice, l.Amount, l.OfferName, l.OfferId })
            .ToListAsync(ct);
        var sellerIds = lines.Select(l => l.Seller).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => sellerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var cut = lines.Where(l => l.ListPrice is { } list && list > l.UnitPrice)
            .Select(l => new { l, Discount = Money.LineAmount(l.Quantity, l.ListPrice!.Value - l.UnitPrice) }).ToList();
        var cashiers = lines.GroupBy(l => l.Seller).Select(g =>
        {
            var mine = cut.Where(c => c.l.Seller == g.Key).ToList();
            var byHand = mine.Where(c => c.l.OfferId == null).ToList();
            var sales = g.Sum(l => l.Amount);
            return new CashierDiscounts(g.Key, names.GetValueOrDefault(g.Key) ?? "—", g.Select(l => l.Id).Distinct().Count(), sales,
                byHand.Sum(c => c.Discount), byHand.Count, mine.Where(c => c.l.OfferId != null).Sum(c => c.Discount),
                sales + byHand.Sum(c => c.Discount) == 0 ? 0 : decimal.Round(byHand.Sum(c => c.Discount) * 100 / (sales + byHand.Sum(c => c.Discount)), 1));
        }).OrderByDescending(c => c.ByHand).ToList();
        var detail = cut.OrderByDescending(c => c.l.OfferId == null).ThenByDescending(c => c.Discount).Take(300)
            .Select(c => new DiscountLine(c.l.OccurredAt, c.l.BillNo, c.l.Name, c.l.Quantity, c.l.ListPrice!.Value, c.l.UnitPrice, c.Discount, names.GetValueOrDefault(c.l.Seller), c.l.OfferName)).ToList();
        return TypedResults.Ok(new DiscountReport($"{period.Year:D4}-{period.Month:D2}", cashiers.Sum(c => c.ByHand), cashiers.Sum(c => c.ByOffer), cashiers, detail));
    }

    /// <summary>
    /// The month's profit by customer ("party"), by bill or by category: who and what earns the money. Sales are without
    /// GST, cost is what each line cost when sold (else the item's cost today), and cancelled bills don't count. Returns
    /// (credit notes) are in the month's profit and the item report, not here.
    /// </summary>
    private static async Task<IResult> ProfitByAsync(string? month, string? by, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ParseOrThisMonth(month, clock) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var grouping = (by ?? "party").ToLowerInvariant();
        if (grouping is not ("party" or "bill" or "category"))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["by"] = ["Group by party, bill or category."] });
        }

        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines)
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null).ToListAsync(ct);
        var productIds = bills.SelectMany(b => b.Lines).Where(l => l.ProductId != null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).Select(p => new { p.Id, p.CostPrice, p.Category }).ToDictionaryAsync(p => p.Id, ct);
        var customerIds = bills.Where(b => b.CustomerId != null).Select(b => b.CustomerId!.Value).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        decimal? Cost(BillLine l) => l.UnitCost ?? (l.ProductId is { } id && products.TryGetValue(id, out var p) ? p.CostPrice : null);
        var taxed = bills.Where(b => b.Gstin != null && !b.BillOfSupply).Select(b => b.Id).ToHashSet();
        decimal WithoutGst(BillLine l) => !taxed.Contains(l.BillId) ? l.Amount : l.TaxableValue ?? (l.GstRate is > 0 ? decimal.Round(l.Amount / (1 + (l.GstRate.Value / 100)), 2) : l.Amount);

        var lines = bills.SelectMany(b => b.Lines.Select(l => (Bill: b, Line: l))).ToList();
        ProfitRow Row(string key, Guid? id, string name, string? detail, IReadOnlyCollection<(Bill Bill, BillLine Line)> part)
        {
            var sales = part.Sum(x => WithoutGst(x.Line));
            var costed = part.Where(x => Cost(x.Line) is not null).ToList();
            decimal? cost = costed.Count == 0 ? null : costed.Sum(x => Money.LineAmount(x.Line.Quantity, Cost(x.Line)!.Value));
            var costedSales = costed.Sum(x => WithoutGst(x.Line));
            decimal? profit = cost is null ? null : costedSales - cost;
            decimal? margin = profit is null || costedSales == 0 ? null : decimal.Round(profit.Value / costedSales * 100, 1);
            return new ProfitRow(key, id, name, detail, part.Select(x => x.Bill.Id).Distinct().Count(), sales, cost, profit, margin, sales - costedSales);
        }

        var rows = grouping switch
        {
            "party" => lines.GroupBy(x => x.Bill.CustomerId)
                .Select(g => Row(g.Key?.ToString() ?? "walk-in", g.Key, g.Key is { } cid ? customers.GetValueOrDefault(cid, "?") : "", null, g.ToList())),
            "bill" => lines.GroupBy(x => x.Bill)
                .Select(g => Row(g.Key.Id.ToString(), g.Key.Id, g.Key.BillNo, g.Key.CustomerId is { } cid ? customers.GetValueOrDefault(cid) : null, g.ToList())
                    with { At = g.Key.OccurredAt }),
            _ => lines.GroupBy(x => x.Line.ProductId is { } pid && products.TryGetValue(pid, out var p) && !string.IsNullOrWhiteSpace(p.Category) ? p.Category!.Trim() : "")
                .Select(g => Row(g.Key, null, g.Key, null, g.ToList())),
        };
        var list = rows.OrderByDescending(r => r.Profit ?? decimal.MinValue).ThenByDescending(r => r.Sales).ToList();
        return TypedResults.Ok(new ProfitByReport($"{period.Year:D4}-{period.Month:D2}", grouping, list.Sum(r => r.Sales), list.Sum(r => r.Profit ?? 0), list));
    }

    private static async Task<IResult> ItemMarginsAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ParseOrThisMonth(month, clock) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines)
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null)
            .ToListAsync(ct);
        var lines = bills.SelectMany(b => b.Lines).ToList();
        var productIds = lines.Where(l => l.ProductId != null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var costs = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id) && p.CostPrice != null)
            .ToDictionaryAsync(p => p.Id, p => p.CostPrice!.Value, ct);
        decimal? Cost(BillLine l) => l.UnitCost ?? (l.ProductId is { } id && costs.TryGetValue(id, out var cost) ? cost : null);
        var taxed = bills.Where(b => b.Gstin != null && !b.BillOfSupply).Select(b => b.Id).ToHashSet();
        // The value without GST as stored when billed; older bills work it out from the rate. A composition shop's bill of
        // supply carries no GST, so its whole amount is the shop's.
        decimal WithoutGst(BillLine l) => !taxed.Contains(l.BillId) ? l.Amount : l.TaxableValue ?? (l.GstRate is > 0 ? decimal.Round(l.Amount / (1 + (l.GstRate.Value / 100)), 2) : l.Amount);

        var items = lines.GroupBy(l => (Key: l.ProductId?.ToString() ?? l.Name.Trim().ToLowerInvariant(), l.Unit))
            .Select(g =>
            {
                var sales = g.Sum(WithoutGst);
                var costedLines = g.Where(l => Cost(l) is not null).ToList();
                // Only lines with a cost count towards the profit; an item partly costed shows the profit on that part.
                decimal? cost = costedLines.Count == 0 ? null : costedLines.Sum(l => Money.LineAmount(l.Quantity, Cost(l)!.Value));
                decimal? profit = cost is null ? null : costedLines.Sum(WithoutGst) - cost;
                var costedSales = costedLines.Sum(WithoutGst);
                decimal? margin = profit is null || costedSales == 0 ? null : decimal.Round(profit.Value / costedSales * 100, 1);
                return new ItemMargin(g.First().ProductId, g.First().Name, g.Sum(l => l.Quantity), g.Key.Unit, sales, cost, profit, margin);
            })
            .OrderByDescending(i => i.Profit ?? decimal.MinValue).ThenByDescending(i => i.Sales)
            .ToList();
        // The month's returns (credit notes) come off each item: its units, its sale without GST and, for goods put back
        // on the shelf, their cost; goods not put back lose their whole sale. Same rule as the month's profit.
        var notes = await db.CreditNotes.AsNoTracking().Include(n => n.Lines).Where(n => n.OccurredAt >= start && n.OccurredAt < end).ToListAsync(ct);
        var noteBillIds = notes.Select(n => n.BillId).Distinct().ToList();
        var sold = (await db.BillLines.AsNoTracking().Where(l => noteBillIds.Contains(l.BillId)).ToListAsync(ct)).ToDictionary(l => (l.BillId, l.LineNo));
        var back = notes.SelectMany(n => n.Lines.Select(l => (Note: n, Line: l, Sold: sold.GetValueOrDefault((n.BillId, l.BillLineNo)))))
            .Where(x => x.Sold is not null)
            .GroupBy(x => (Key: x.Sold!.ProductId?.ToString() ?? x.Sold.Name.Trim().ToLowerInvariant(), x.Sold.Unit))
            .ToDictionary(g => g.Key, g => (
                Quantity: g.Sum(x => x.Line.Quantity),
                Sales: g.Sum(x => x.Line.TaxableValue),
                Cost: g.Where(x => x.Note.Restocked && Cost(x.Sold!) is not null).Sum(x => Money.LineAmount(x.Line.Quantity, Cost(x.Sold!)!.Value))));
        items = items.Select(i =>
        {
            var key = (i.ProductId?.ToString() ?? i.Name.Trim().ToLowerInvariant(), i.Unit);
            if (!back.TryGetValue(key, out var r))
            {
                return i;
            }

            var cost = i.Cost is { } c ? c - r.Cost : (decimal?)null;
            var profit = i.Profit is { } p ? p - r.Sales + r.Cost : (decimal?)null;
            var sales = i.Sales - r.Sales;
            return i with { Quantity = i.Quantity - r.Quantity, Sales = sales, Cost = cost, Profit = profit, MarginPercent = profit is { } pr && sales > 0 ? decimal.Round(pr / sales * 100, 1) : null };
        }).OrderByDescending(i => i.Profit ?? decimal.MinValue).ThenByDescending(i => i.Sales).ToList();
        var totalSales = items.Sum(i => i.Sales);
        var covered = items.Where(i => i.Profit != null).Sum(i => i.Sales);
        return TypedResults.Ok(new ItemMarginReport($"{period.Year:D4}-{period.Month:D2}", totalSales, items.Sum(i => i.Profit ?? 0),
            totalSales == 0 ? 0 : decimal.Round(covered / totalSales, 2), items));
    }

    private static async Task<IResult> SupplierDuesCsvAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var csv = new StringBuilder("Supplier,Phone,We owe (minus = they owe us),0-15 days,16-30 days,30+ days,Oldest unpaid bill (days)\n");
        foreach (var x in await SupplierEndpoints.SupplierDuesAsync(db, clock, ct))
        {
            csv.AppendJoin(',', Cell(x.Name), Cell(x.Phone), Num(x.Due), Num(x.Days0To15), Num(x.Days16To30), Num(x.Over30), x.OldestDays).Append('\n');
        }

        return Csv(csv, $"supplier-dues-{IndiaTime.Today(clock):yyyy-MM-dd}.csv");
    }

    private static async Task<IResult> DuesCsvAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var report = await DuesAsync(db, clock, ct);
        var csv = new StringBuilder("Name,Phone,Customer owes (minus = advance),0-7 days,8-30 days,30+ days,31-60 days,61-90 days,90+ days,Oldest due (days),Credit days,Overdue,Due on,Last payment,Last reminded\n");
        foreach (var d in report.Customers)
        {
            csv.AppendJoin(',', Cell(d.Name), Cell(d.Phone), Num(d.Balance), Num(d.Days0To7), Num(d.Days8To30), Num(d.Over30), Num(d.Days31To60), Num(d.Days61To90), Num(d.Over90), d.OldestDays,
                d.CreditDays?.ToString() ?? "", Num(d.Overdue), d.DueOn?.ToString("yyyy-MM-dd") ?? "", Day(d.LastPaymentAt), Day(d.LastRemindedAt)).Append('\n');
        }

        return Csv(csv, $"dues-{IndiaTime.Today(clock):yyyy-MM-dd}.csv");
    }

    private static async Task<IResult> ProfitLossAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await BuildProfitLossAsync(db, clock, fy ?? ThisFinancialYear(clock), ct));

    private static async Task<IResult> ProfitLossCsvAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var r = await BuildProfitLossAsync(db, clock, fy ?? ThisFinancialYear(clock), ct);
        var csv = new StringBuilder("Line,Amount\n");
        foreach (var (label, amount) in (List<(string Label, decimal Amount)>)
        [
            ("Sales with GST", r.Sales), ("GST in sales", r.Gst), ("Turnover without GST", r.Turnover), ("Profit on goods", r.GoodsProfit), ("Returns", -r.Returns), ("Stock lost", -r.StockLoss),
            ("Running expenses", -r.Expenses), .. (r.ExpenseLines ?? []).Select(x => ($"  of which {Heading(x.Category)}", -x.Amount)), ("Machine wear (depreciation)", -r.MachineWear), ("Net profit", r.NetProfit), ("Taken home", r.TakenHome), ("Supplier bills", r.Purchases), ("Opening stock at cost", r.OpeningStock),
            ($"Closing stock at cost {r.ClosingDate:yyyy-MM-dd}", r.ClosingStock), ("Owed to suppliers today", r.SupplierDue), ("Owed by customers today", r.CustomerDue), .. (r.NotInProfit ?? []).Where(x => x.Category != ExpenseCategory.Home).Select(x => ($"Not in profit: {Heading(x.Category)}", x.Amount)),
            ("Jobs being made, at cost (today)", r.WorkInProgress), ("Furniture ready, at cost (today)", r.FinishedGoods),
            ("Received in cash", r.CashReceipts), ("Received by UPI or bank", r.DigitalReceipts), ("Presumed profit under 44AD (8% cash, 6% digital)", r.PresumedProfit),
        ])
        {
            csv.AppendJoin(',', label, Num(amount)).Append('\n');
        }

        csv.AppendJoin(',', "Items without a cost", r.ItemsWithoutCost.ToString(CultureInfo.InvariantCulture)).Append('\n');
        return Csv(csv, $"profit-loss-{r.FinancialYear}.csv");
    }

    /// <summary>
    /// Turnover limits a small business should watch (aggregate turnover without GST, for the financial year): the
    /// composition scheme stops at ₹1.5 crore, the annual GST return (GSTR-9) is optional up to ₹2 crore, and e-invoices
    /// are needed once any year since 2017-18 passed ₹5 crore. Some states have lower composition limits; a CA confirms.
    /// </summary>
    public static readonly (string Key, decimal Limit)[] TurnoverLimits = [("composition", 15_000_000m), ("annual_return", 20_000_000m), ("einvoice", 50_000_000m)];

    /// <summary>Each limit crossed this year or last, or within 80% of it this year.</summary>
    public static IReadOnlyList<TurnoverAlert> TurnoverAlerts(decimal thisYear, decimal lastYear) =>
        TurnoverLimits
            .Select(l => (l, Turnover: Math.Max(thisYear, l.Key == "einvoice" ? lastYear : thisYear)))
            .Where(x => x.Turnover >= x.l.Limit * 0.8m)
            .Select(x => new TurnoverAlert(x.l.Key, x.l.Limit, x.Turnover, x.Turnover > x.l.Limit))
            .ToList();

    /// <summary>
    /// Presumed profit under section 44AD (small businesses that don't keep full books): 6% of money received by
    /// UPI, bank or card, 8% of money received in cash. Up to ₹2 crore turnover (₹3 crore when cash is 5% or less).
    /// </summary>
    public static decimal PresumedProfit(decimal cash, decimal digital) =>
        decimal.Round(cash * 0.08m + digital * 0.06m, 2, MidpointRounding.AwayFromZero);

    public static async Task<ProfitLossReport> BuildProfitLossAsync(AppDbContext db, TimeProvider clock, int startYear, CancellationToken ct)
    {
        var (start, _) = IndiaTime.Month(startYear, 4);
        var (end, _) = IndiaTime.Month(startYear + 1, 4);
        var months = new List<MonthReport>();
        for (var i = 0; i < 12; i++)
        {
            months.Add(await BuildMonthAsync(db, i < 9 ? (startYear, i + 4) : (startYear + 1, i - 8), ct));
        }

        var year = await BuildYearAsync(db, startYear, ct);
        var lastYear = await BuildYearAsync(db, startYear - 1, ct);
        var now = clock.GetUtcNow();
        var closeAt = end < now ? end : now;
        var returns = months.Sum(m => m.Returns);
        var loss = months.Sum(m => m.StockLoss);
        var profit = months.Sum(m => m.EstimatedProfit);
        var (opening, _) = await StockValueAtAsync(db, start, ct);
        var (closing, missing) = await StockValueAtAsync(db, closeAt, ct);
        var expenses = months.Sum(m => m.Expenses);
        var wear = await Workshop.WearForYearAsync(db, startYear, ct);
        var coveredSales = months.Sum(m => m.ProfitCoverage * m.Sales);
        // The year's kharcha, heading by heading, from the month statements, so the year adds up the same way.
        static List<KharchaLine> ByHeading(IEnumerable<KharchaLine> lines) =>
            lines.GroupBy(x => x.Category).Select(g => new KharchaLine(g.Key, g.Sum(x => x.Amount))).OrderByDescending(x => x.Amount).ToList();
        var expenseLines = ByHeading(months.SelectMany(m => m.Statement?.Running ?? []));
        var notInProfit = ByHeading(months.SelectMany(m => m.Statement?.NotInProfit ?? []));
        return new ProfitLossReport(
            year.FinancialYear, year.Total.Sales, year.Total.Turnover, year.Total.Gst, profit + returns + loss, returns, loss, expenses, profit - expenses - wear + months.Sum(m => m.BagsSold - m.BagsBought),
            months.Sum(m => m.TakenHome),
            await db.Purchases.AsNoTracking().Where(p => p.OccurredAt >= start && p.OccurredAt < end && p.CancelledAt == null).SumAsync(p => (decimal?)p.Total, ct) ?? 0m,
            opening, closing, DateOnly.FromDateTime(closeAt.AddTicks(-1).ToOffset(IndiaTime.Offset).DateTime),
            await db.SupplierEntries.AsNoTracking().SumAsync(e => (decimal?)e.Amount, ct) ?? 0m,
            await db.Customers.AsNoTracking().Where(c => c.Balance > 0).SumAsync(c => (decimal?)c.Balance, ct) ?? 0m,
            missing, year.Total.Sales == 0 ? 0 : decimal.Round(coveredSales / year.Total.Sales, 2),
            await Jobs.WorkInProgressAsync(db, ct), wear, await Jobs.FinishedGoodsAsync(db, ct),
            year.Total.Cash, year.Total.Digital, PresumedProfit(year.Total.Cash, year.Total.Digital), lastYear.Total.Turnover,
            TurnoverAlerts(year.Total.Turnover, lastYear.Total.Turnover), expenseLines, notInProfit, months.Sum(m => m.BagsSold), months.Sum(m => m.BagsBought));
    }

    /// <summary>
    /// Stock at cost just before a moment: each item's count from its movements, at its weighted average purchase cost
    /// by then (the same rule supplier bills keep), else its cost today. Items held with no cost are counted, not valued.
    /// </summary>
    private static async Task<(decimal Value, int Missing)> StockValueAtAsync(AppDbContext db, DateTimeOffset at, CancellationToken ct)
    {
        var moves = await db.StockMovements.AsNoTracking().Where(m => m.OccurredAt < at)
            .Select(m => new { m.ProductId, m.Quantity, m.CostPrice, m.Kind, m.OccurredAt }).ToListAsync(ct);
        var today = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.CostPrice, ct);
        var (value, missing) = (0m, 0);
        foreach (var item in moves.GroupBy(m => m.ProductId))
        {
            var held = item.Sum(m => m.Quantity);
            if (held <= 0)
            {
                continue;
            }

            var cost = Catalogue.StockLedger.AverageCost(item.Select(m => (m.Kind, m.Quantity, m.CostPrice, m.OccurredAt))) ?? today.GetValueOrDefault(item.Key);
            if (cost is { } known)
            {
                value += decimal.Round(held * known, 2);
            }
            else
            {
                missing++;
            }
        }

        return (value, missing);
    }

    private static async Task<IResult> YearAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await BuildYearAsync(db, fy ?? ThisFinancialYear(clock), ct));

    private static async Task<IResult> YearCsvAsync(int? fy, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var report = await BuildYearAsync(db, fy ?? ThisFinancialYear(clock), ct);
        var csv = new StringBuilder("Month,Bills,Sales with GST,GST,Turnover without GST,GST rate not set,Cash received,Digital received\n");
        foreach (var m in report.Months.Append(report.Total))
        {
            csv.AppendJoin(',', m.Month, m.Bills.ToString(CultureInfo.InvariantCulture), Num(m.Sales), Num(m.Gst), Num(m.Turnover), Num(m.NoRate), Num(m.Cash), Num(m.Digital)).Append('\n');
        }

        return Csv(csv, $"year-{report.FinancialYear}.csv");
    }

    private static int ThisFinancialYear(TimeProvider clock)
    {
        var today = IndiaTime.Today(clock);
        return today.Month >= 4 ? today.Year : today.Year - 1;
    }

    /// <summary>The financial year starting 1 April of <paramref name="startYear"/>. Cancelled bills don't count.</summary>
    public static async Task<YearReport> BuildYearAsync(AppDbContext db, int startYear, CancellationToken ct)
    {
        var (start, _) = IndiaTime.Month(startYear, 4);
        var (end, _) = IndiaTime.Month(startYear + 1, 4);
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines)
            .Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null).ToListAsync(ct);
        var khata = await db.LedgerEntries.AsNoTracking()
            .Where(e => (e.OccurredAt >= start && e.OccurredAt < end) && e.BillId == null && e.PaymentMode != null)
            .Select(e => new { e.OccurredAt, e.Amount, e.PaymentMode }).ToListAsync(ct);

        var rows = new List<YearMonthRow>();
        for (var i = 0; i < 12; i++)
        {
            var (year, month) = i < 9 ? (startYear, i + 4) : (startYear + 1, i - 8);
            var (from, to) = IndiaTime.Month(year, month);
            var own = bills.Where(b => b.OccurredAt >= from && b.OccurredAt < to).ToList();
            var ownKhata = khata.Where(e => e.OccurredAt >= from && e.OccurredAt < to).ToList();
            // GST exists only on bills of a registered shop; prices include it.
            var gstLines = own.Where(b => b.Gstin != null && !b.BillOfSupply).SelectMany(b => b.Lines).ToList();
            // The tax kept on each bill since it was stored; older bills work it out from the rates.
            var gst = gstLines.Where(l => l.TaxableValue != null).Sum(l => (l.Cgst ?? 0) + (l.Sgst ?? 0) + (l.Igst ?? 0))
                + gstLines.Where(l => l.TaxableValue == null && l.GstRate is > 0).GroupBy(l => l.GstRate!.Value)
                    .Sum(g => { var amount = g.Sum(l => l.Amount); return amount - decimal.Round(amount / (1 + (g.Key / 100)), 2); });
            decimal Received(PaymentMode mode) => own.Sum(b => b.PaidBy(mode)) - ownKhata.Where(e => e.PaymentMode == mode).Sum(e => e.Amount);
            var sales = own.Sum(b => b.Total);
            rows.Add(new YearMonthRow($"{year:D4}-{month:D2}", own.Count, sales, gst, sales - gst, gstLines.Where(l => l.GstRate == null).Sum(l => l.Amount), Received(PaymentMode.Cash),
                // Digital for 44AD: UPI, card and cleared cheques.
                Received(PaymentMode.Upi) + Received(PaymentMode.Card) + Received(PaymentMode.Cheque)));
        }

        var total = new YearMonthRow("Total", rows.Sum(r => r.Bills), rows.Sum(r => r.Sales), rows.Sum(r => r.Gst), rows.Sum(r => r.Turnover), rows.Sum(r => r.NoRate), rows.Sum(r => r.Cash), rows.Sum(r => r.Digital));
        return new YearReport($"{startYear}-{(startYear + 1) % 100:D2}", rows, total);
    }

    /// <summary>A kharcha heading in English, for the CA's file.</summary>
    private static string Heading(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Tea => "Tea and snacks",
        ExpenseCategory.Transport => "Transport",
        ExpenseCategory.Rent => "Rent",
        ExpenseCategory.Electricity => "Electricity",
        ExpenseCategory.Salary => "Salary",
        ExpenseCategory.Purchase => "Stock paid for",
        ExpenseCategory.Packaging => "Packing",
        ExpenseCategory.Repair => "Repairs",
        ExpenseCategory.Home => "Taken home",
        ExpenseCategory.Tax => "Tax paid",
        ExpenseCategory.Loan => "Loan EMI",
        ExpenseCategory.Interest => "Interest on loans",
        ExpenseCategory.Equipment => "Equipment bought",
        ExpenseCategory.Deposit => "Cash to bank",
        ExpenseCategory.Advance => "Staff advance",
        _ => "Other expenses",
    };

    private static async Task<IResult> MonthCsvAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (ParseOrThisMonth(month, clock) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var report = await BuildMonthAsync(db, period, ct);
        var csv = new StringBuilder("Date,Sales,Received\n");
        foreach (var day in report.Days)
        {
            csv.AppendJoin(',', day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Num(day.Sales), Num(day.Received)).Append('\n');
        }

        csv.Append('\n').AppendJoin(',', "Total", Num(report.Sales), Num(report.Cash + report.Upi)).Append('\n');

        // The month's results are not daily sales: they get their own heading, so they don't read as a "Sales" column.
        csv.Append('\n').Append("Statement,Amount\n");
        var st = report.Statement!;
        var rows = new List<(string, decimal)> { ("Sales with GST", st.SalesWithGst) };
        if (st.RoundOff != 0)
        {
            rows.Add(("Less round-off and bill discounts", -st.RoundOff));
        }

        rows.AddRange([("Less GST in sales", -st.GstInSales), ("Sales", st.Sales), ("Less sales with no cost written", -st.NoCostSales), ("Less cost of goods sold", -st.CostOfGoods),
            ("Less returns", -st.Returns), ("Less stock lost", -st.StockLoss), ("Gross profit", st.GrossProfit)]);
        rows.AddRange(st.Running.Select(x => ($"Less {Heading(x.Category)}", -x.Amount)));
        rows.AddRange([("Total running expenses", -st.Expenses), ("Net profit", st.NetProfit)]);
        foreach (var (label, amount) in rows)
        {
            csv.AppendJoin(',', label, Num(amount)).Append('\n');
        }

        csv.Append('\n').Append("Not in profit,Amount\n");
        foreach (var x in st.NotInProfit)
        {
            csv.AppendJoin(',', Heading(x.Category), Num(x.Amount)).Append('\n');
        }
        return Csv(csv, $"month-{report.Month}.csv");
    }

    private static (int Year, int Month)? ParseOrThisMonth(string? month, TimeProvider clock)
    {
        if (string.IsNullOrEmpty(month))
        {
            var today = IndiaTime.Today(clock);
            return (today.Year, today.Month);
        }

        return IndiaTime.ParseMonth(month);
    }

    /// <summary>UTF-8 with a byte-order mark, so Excel shows Hindi names correctly when the file is opened.</summary>
    private static IResult Csv(StringBuilder csv, string fileName) =>
        TypedResults.File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())], "text/csv; charset=utf-8", fileName);

    private static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Day(DateTimeOffset? at) => at?.ToOffset(IndiaTime.Offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    /// <summary>Quotes a text cell, and defuses values Excel would run as a formula (= + - @).</summary>
    private static string Cell(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var safe = text[0] is '=' or '+' or '-' or '@' ? "'" + text : text;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
