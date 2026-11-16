using System.Globalization;
using System.Security.Claims;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

public enum BalanceItemKind
{
    /// <summary>Furniture, racks, a computer, a vehicle: kept for years, not for sale.</summary>
    FixedAsset,

    /// <summary>A bank or family loan, still to be paid back.</summary>
    Loan,

    /// <summary>A deposit, a security paid to the landlord, anything else the shop owns.</summary>
    OtherAsset,

    /// <summary>Rent or salary not yet paid, anything else the shop owes.</summary>
    OtherLiability,
}

/// <summary>
/// Something on the balance sheet that the shop's day-to-day records don't hold: furniture, a loan, a deposit. Its
/// value is what the owner says it is now; it counts from <see cref="Since"/> until <see cref="Until"/> (a repaid loan,
/// a sold almirah), so past balance sheets stay right.
/// </summary>
public sealed class BalanceItem : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public BalanceItemKind Kind { get; set; }

    public required string Name { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Since { get; set; }

    public DateOnly? Until { get; set; }
}

public sealed record BalanceItemRequest(Guid Id, BalanceItemKind? Kind, string? Name, decimal Amount, DateOnly? Since, DateOnly? Until = null);

public sealed record BalanceItemDto(Guid Id, BalanceItemKind Kind, string Name, decimal Amount, DateOnly Since, DateOnly? Until);

/// <summary>
/// How the capital got to where it is this financial year: what it was at the start, plus profit and money the owner
/// brought in, less what was taken home. <see cref="Unexplained"/> is whatever the records don't account for (stock
/// counted without a cost, a loan's money added as cash); a CA looks at it first. <see cref="StartedWith"/> is what a
/// shop that began using the app this year entered as its starting stock, khata and bank balance: the owner's money too.
/// Null for past dates, because the year's profit runs to today.
/// </summary>
public sealed record CapitalSplitDto(string FinancialYear, decimal Opening, decimal Profit, decimal BroughtIn, decimal TakenHome, decimal Unexplained, decimal StartedWith = 0, decimal ItemsNet = 0);

/// <summary>
/// What the shop has and what it owes at the end of a day: cash in hand, bank, stock at cost, what customers owe and
/// suppliers were paid ahead, plus fixed assets and deposits, against what is owed to suppliers, customers' advances,
/// loans, other dues and this month's GST still to pay. The owner's capital is what is left over, so the two sides
/// always balance. Each figure is the same one its own screen shows, so the CA can trace every line.
/// </summary>
public sealed record BalanceSheetDto(
    DateOnly On,
    decimal Cash, decimal Bank, decimal Stock, decimal CustomersOwe, decimal SupplierAdvances, decimal TotalAssets,
    decimal SuppliersOwed, decimal CustomerAdvances, decimal TotalLiabilities,
    decimal Capital, int CustomersOwing, int SuppliersOwedCount, int UnvaluedItems,
    decimal FixedAssets = 0, decimal OtherAssets = 0, decimal Loans = 0, decimal OtherLiabilities = 0, decimal GstToPay = 0,
    IReadOnlyList<BalanceItemDto>? Items = null, CapitalSplitDto? CapitalSplit = null);

public static class BalanceSheet
{
    public static IEndpointRouteBuilder MapBalanceSheet(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/balance-sheet", GetAsync).WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        var items = app.MapGroup("/api/v1/balance-items").WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        items.MapPut("/{id:guid}", SaveItemAsync);
        items.MapDelete("/{id:guid}", DeleteItemAsync);
        return app;
    }

    private static async Task<IResult> SaveItemAsync(Guid id, BalanceItemRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (request.Kind is not { } kind || !Enum.IsDefined(kind) || name is not { Length: >= 2 and <= 80 }
            || request.Amount <= 0 || request.Amount > Money.MaxAmount || decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Invalid("Name", "Give it a name and an amount.");
        }

        var since = request.Since ?? IndiaTime.Today(clock);
        if (request.Until is { } until && until < since)
        {
            return Invalid("Until", "The end date can't be before the start.");
        }

        var item = await db.Set<BalanceItem>().SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item is null)
        {
            item = new BalanceItem { Id = id, Name = name };
            db.Add(item);
        }

        (item.Kind, item.Name, item.Amount, item.Since, item.Until) = (kind, name, request.Amount, since, request.Until);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new BalanceItemDto(item.Id, item.Kind, item.Name, item.Amount, item.Since, item.Until));
    }

    private static async Task<IResult> DeleteItemAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        await db.Set<BalanceItem>().Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(string? on, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var day = today;
        if (on is not null && !DateOnly.TryParseExact(on, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["on"] = ["Use a date like 2026-11-16."] });
        }

        if (day > today)
        {
            day = today;
        }

        var sheet = await SheetAsync(day, user, db, tenant, entitlements, clock, ct);

        // The year's story of the capital, for today's sheet: the sheet on the eve of the year, the year's profit, and the
        // owner's own money in and out. The year's profit runs to today, so a past date gets no split.
        if (day == today)
        {
            var fyStart = today.Month >= 4 ? today.Year : today.Year - 1;
            var eve = new DateOnly(fyStart, 4, 1).AddDays(-1);
            var opening = (await SheetAsync(eve, user, db, tenant, entitlements, clock, ct)).Capital;
            var pnl = await ReportEndpoints.BuildProfitLossAsync(db, clock, fyStart, ct);
            var (from, to) = (IndiaTime.Day(new DateOnly(fyStart, 4, 1)).Start, IndiaTime.Day(today).End);
            var moves = await db.CashMoves.AsNoTracking().Where(m => m.OccurredAt >= from && m.OccurredAt < to && m.StruckAt == null)
                .GroupBy(m => m.Kind).Select(g => new { g.Key, Total = g.Sum(m => m.Amount) }).ToListAsync(ct);
            var broughtIn = moves.Where(m => m.Key == CashMoveKind.Added).Sum(m => m.Total);
            var takenHome = pnl.TakenHome + moves.Where(m => m.Key == CashMoveKind.Removed).Sum(m => m.Total);
            var startedWith = await StartedWithAsync(from, to, db, tenant, clock, ct);
            // Furniture, deposits and loans entered by hand are the owner's own, less what is borrowed; the eve's sheet
            // already holds those that started before the year.
            var itemsNet = (sheet.Items ?? []).Where(i => i.Since >= new DateOnly(fyStart, 4, 1))
                .Sum(i => i.Kind is BalanceItemKind.FixedAsset or BalanceItemKind.OtherAsset ? i.Amount : -i.Amount);
            var explained = opening + startedWith + itemsNet + pnl.NetProfit + broughtIn - takenHome;
            sheet = sheet with { CapitalSplit = new CapitalSplitDto(pnl.FinancialYear, opening, pnl.NetProfit, broughtIn, takenHome, decimal.Round(sheet.Capital - explained, 2), startedWith, itemsNet) };
        }

        return TypedResults.Ok(sheet);
    }

    /// <summary>
    /// The starting figures entered this year when the shop began with the app: opening stock at its cost, opening khata
    /// and supplier balances, and the bank's starting balance. They are the owner's money already in the business.
    /// </summary>
    private static async Task<decimal> StartedWithAsync(DateTimeOffset yearStart, DateTimeOffset upTo, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var stock = await (from m in db.StockMovements.AsNoTracking()
                           where m.Kind == StockMovementKind.Opening && m.OccurredAt >= yearStart && m.OccurredAt < upTo
                           join p in db.Products.AsNoTracking() on m.ProductId equals p.Id
                           select m.Quantity * (m.CostPrice ?? p.CostPrice ?? 0)).SumAsync(ct);
        var khata = await db.LedgerEntries.AsNoTracking().Where(e => e.Kind == LedgerEntryKind.Opening && e.OccurredAt >= yearStart && e.OccurredAt < upTo).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var owed = await db.SupplierEntries.AsNoTracking().Where(e => e.Kind == SupplierEntryKind.Opening && e.OccurredAt >= yearStart && e.OccurredAt < upTo).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var book = Value<BankBookDto>(await BankBook.GetAsync(null, null, null, db, tenant, clock, ct));
        var start = DateOnly.FromDateTime(yearStart.ToOffset(IndiaTime.Offset).DateTime);
        var bank = book?.StartingBalance is { } b && book.StartingDate is { } d && d >= start ? b : 0m;
        return decimal.Round(stock + khata - owed + bank, 2);
    }

    private static async Task<BalanceSheetDto> SheetAsync(DateOnly day, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        var text = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = IndiaTime.Day(day).End;

        // The same closing figures their own screens show.
        var cash = Value<CashLedgerDto>(await CashMoves.LedgerAsync(text, text, db, clock, ct))?.Closing ?? 0;
        var main = Value<BankBookDto>(await BankBook.GetAsync(text, text, null, db, tenant, clock, ct));
        var bank = main?.Closing ?? 0;
        foreach (var account in main?.Accounts?.Where(a => a.Id != main.Account) ?? [])
        {
            bank += Value<BankBookDto>(await BankBook.GetAsync(text, text, account.Id, db, tenant, clock, ct))?.Closing ?? 0;
        }

        var stock = Value<StockStatement>(await StockLedger.StatementAsync(day, day, user, db, tenant, clock, ct));

        // Khata and supplier balances as they stood at the end of the day: every entry up to then, per party.
        var customers = await db.LedgerEntries.AsNoTracking().Where(e => e.OccurredAt < end)
            .GroupBy(e => e.CustomerId).Select(g => g.Sum(e => e.Amount)).ToListAsync(ct);
        var suppliers = await db.SupplierEntries.AsNoTracking().Where(e => e.OccurredAt < end)
            .GroupBy(e => e.SupplierId).Select(g => g.Sum(e => e.Amount)).ToListAsync(ct);

        // What the owner keeps on the sheet by hand: furniture, loans, deposits, standing on that day.
        var items = await db.Set<BalanceItem>().AsNoTracking().Where(i => i.Since <= day && (i.Until == null || i.Until > day))
            .OrderBy(i => i.Kind).ThenBy(i => i.Name).Select(i => new BalanceItemDto(i.Id, i.Kind, i.Name, i.Amount, i.Since, i.Until)).ToListAsync(ct);
        decimal Sum(BalanceItemKind kind) => items.Where(i => i.Kind == kind).Sum(i => i.Amount);

        // This month's GST still to pay (the GST page's working); earlier months are taken as paid on time.
        var gst = Value<GstMonthDto>(await GstExport.MonthAsync(day.ToString("yyyy-MM", CultureInfo.InvariantCulture), db, tenant, entitlements, clock, ct))?.Payable;
        var gstToPay = gst is null ? 0 : gst.PayCgst + gst.PaySgst + gst.PayIgst + (gst.CompositionTax ?? 0);

        var customersOwe = customers.Where(b => b > 0).Sum();
        var customerAdvances = -customers.Where(b => b < 0).Sum();
        var suppliersOwed = suppliers.Where(b => b > 0).Sum();
        var supplierAdvances = -suppliers.Where(b => b < 0).Sum();

        var assets = cash + bank + (stock?.Value ?? 0) + customersOwe + supplierAdvances + Sum(BalanceItemKind.FixedAsset) + Sum(BalanceItemKind.OtherAsset);
        var liabilities = suppliersOwed + customerAdvances + Sum(BalanceItemKind.Loan) + Sum(BalanceItemKind.OtherLiability) + gstToPay;
        return new BalanceSheetDto(day, cash, bank, stock?.Value ?? 0, customersOwe, supplierAdvances, assets,
            suppliersOwed, customerAdvances, liabilities, assets - liabilities, customers.Count(b => b > 0), suppliers.Count(b => b > 0), stock?.Unvalued ?? 0,
            Sum(BalanceItemKind.FixedAsset), Sum(BalanceItemKind.OtherAsset), Sum(BalanceItemKind.Loan), Sum(BalanceItemKind.OtherLiability), gstToPay, items);
    }

    private static T? Value<T>(IResult result) where T : class => result is IValueHttpResult { Value: T value } ? value : null;
}
