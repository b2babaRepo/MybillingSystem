using System.Globalization;
using System.Security.Claims;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GroceryKhata.Api.Ledger;

/// <summary>One line of the trial balance: debit or credit, never both.</summary>
public sealed record TrialLine(string Key, string Group, decimal Debit, decimal Credit);

/// <summary>
/// The trial balance for a financial year, up to <see cref="On"/>: every balance from the balance sheet and every
/// income and expense from the profit and loss, debits on one side and credits on the other. Capital is the owner's
/// money at the start of the year plus what they brought in (what the records explain), so the two sides agree.
/// </summary>
public sealed record TrialBalanceDto(string FinancialYear, DateOnly On, IReadOnlyList<TrialLine> Lines, decimal TotalDebit, decimal TotalCredit);

/// <summary>A month's figures in the annual GST summary.</summary>
public sealed record GstYearMonth(string Month, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal B2bSales, decimal CreditNoteValue, decimal InputGst, decimal Paid);

/// <summary>
/// GSTR-9 (annual return) working: the year's outward supplies split B2B and B2C (table 4), credit notes, tax on them,
/// input tax credit (table 6), tax paid in cash (table 9), and the year's HSN summary (table 17), added up from the
/// months as GSTR-1 and 3B showed them. A composition shop files GSTR-4 instead: <see cref="Composition"/>.
/// </summary>
public sealed record GstAnnualDto(
    string FinancialYear, bool Composition, IReadOnlyList<GstYearMonth> Months,
    decimal Taxable, decimal B2bSales, decimal B2cSales, decimal CreditNoteValue, decimal CreditNoteTax,
    decimal Cgst, decimal Sgst, decimal Igst, decimal InputCgst, decimal InputSgst, decimal InputIgst,
    decimal PaidCgst, decimal PaidSgst, decimal PaidIgst, decimal CompositionTurnover, decimal CompositionTax,
    IReadOnlyList<HsnRow> Hsn, int MonthsWithIssues);

public static class YearReports
{
    public static IEndpointRouteBuilder MapYearReports(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/trial-balance", TrialBalanceAsync).WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        app.MapGet("/api/v1/gst/annual", GstAnnualAsync).WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static int FyOf(DateOnly day) => day.Month >= 4 ? day.Year : day.Year - 1;

    private static async Task<IResult> TrialBalanceAsync(int? fy, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var year = fy ?? FyOf(today);
        if (year < 2000 || year > FyOf(today))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["fy"] = ["Choose this financial year or an earlier one."] });
        }

        var yearEnd = new DateOnly(year + 1, 3, 31);
        var on = yearEnd < today ? yearEnd : today;
        var sheet = await BalanceSheet.SheetAsync(on, user, db, tenant, entitlements, clock, ct);
        var pnl = await ReportEndpoints.BuildProfitLossAsync(db, clock, year, ct);

        var lines = new List<TrialLine>();
        void Dr(string key, string group, decimal amount) { if (amount != 0) lines.Add(amount > 0 ? new(key, group, amount, 0) : new(key, group, 0, -amount)); }
        void Cr(string key, string group, decimal amount) { if (amount != 0) lines.Add(amount > 0 ? new(key, group, 0, amount) : new(key, group, -amount, 0)); }

        // What the shop has.
        Dr("cash", "assets", sheet.Cash);
        Dr("bank", "assets", sheet.Bank);
        Dr("stock", "assets", sheet.Stock);
        Dr("debtors", "assets", sheet.CustomersOwe);
        Dr("supplierAdvances", "assets", sheet.SupplierAdvances);
        Dr("fixedAssets", "assets", sheet.FixedAssets);
        Dr("otherAssets", "assets", sheet.OtherAssets);
        // What it owes.
        Cr("creditors", "liabilities", sheet.SuppliersOwed);
        Cr("customerAdvances", "liabilities", sheet.CustomerAdvances);
        Cr("loans", "liabilities", sheet.Loans);
        Cr("otherLiabilities", "liabilities", sheet.OtherLiabilities);
        Cr("gstPayable", "liabilities", sheet.GstToPay);
        // The year's income and expenses.
        Cr("sales", "income", pnl.Turnover);
        Dr("costOfGoods", "expenses", pnl.Turnover - pnl.GoodsProfit);
        Dr("returns", "expenses", pnl.Returns);
        Dr("stockLoss", "expenses", pnl.StockLoss);
        foreach (var e in pnl.ExpenseLines ?? [])
        {
            Dr($"expense:{e.Category}", "expenses", e.Amount);
        }

        var listedExpenses = (pnl.ExpenseLines ?? []).Sum(e => e.Amount);
        Dr("otherExpenses", "expenses", pnl.Expenses - listedExpenses);
        Dr("depreciation", "expenses", pnl.MachineWear);
        // Anything else between the year's income and its profit (adjustments the P&L makes), so profit carries over exactly.
        var explainedProfit = pnl.GoodsProfit - pnl.Returns - pnl.StockLoss - pnl.Expenses - pnl.MachineWear;
        Dr("adjustments", "expenses", explainedProfit - pnl.NetProfit);
        Dr("drawings", "capital", pnl.TakenHome);
        // Capital: what is left once this year's profit and drawings are taken out of the closing capital.
        Cr("capital", "capital", sheet.Capital - pnl.NetProfit + pnl.TakenHome);

        var debit = decimal.Round(lines.Sum(l => l.Debit), 2);
        var credit = decimal.Round(lines.Sum(l => l.Credit), 2);
        return TypedResults.Ok(new TrialBalanceDto(pnl.FinancialYear, on, lines.Select(l => l with { Debit = decimal.Round(l.Debit, 2), Credit = decimal.Round(l.Credit, 2) }).ToList(), debit, credit));
    }

    private static async Task<IResult> GstAnnualAsync(int? fy, AppDbContext db, ITenantContext tenant, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var year = fy ?? FyOf(today);
        if (year < 2017 || year > FyOf(today))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["fy"] = ["Choose this financial year or an earlier one."] });
        }

        var months = new List<GstMonthDto>();
        for (var m = new DateOnly(year, 4, 1); m <= today && m <= new DateOnly(year + 1, 3, 1); m = m.AddMonths(1))
        {
            if (await GstExport.MonthAsync(m.ToString("yyyy-MM", CultureInfo.InvariantCulture), db, tenant, entitlements, clock, ct) is Ok<GstMonthDto> { Value: { } month })
            {
                months.Add(month);
            }
        }

        decimal Sum(Func<GstMonthDto, decimal> pick) => decimal.Round(months.Sum(pick), 2);
        decimal Pay(Func<TaxPayableDto, decimal> pick) => decimal.Round(months.Sum(m => m.Payable is { } p ? pick(p) : 0), 2);
        var taxable = Pay(p => p.OutputTaxable);
        var b2b = Sum(m => m.B2bSales);
        var hsn = months.SelectMany(m => m.Hsn ?? [])
            .GroupBy(h => (h.Section, h.Hsn, h.Rate, h.Uqc))
            .Select(g => new HsnRow(g.Key.Section, g.Key.Hsn, g.Key.Rate, g.Key.Uqc, g.Sum(h => h.Quantity), g.Sum(h => h.Taxable), g.Sum(h => h.Cgst), g.Sum(h => h.Sgst), g.Sum(h => h.Igst)))
            .OrderBy(h => h.Section).ThenBy(h => h.Hsn).ThenBy(h => h.Rate).ToList();
        var rows = months.Select(m => new GstYearMonth(m.Month, m.Payable?.OutputTaxable ?? m.Taxable, m.Payable?.OutputCgst ?? m.Cgst, m.Payable?.OutputSgst ?? m.Sgst, m.Payable?.OutputIgst ?? m.Igst,
            m.B2bSales, m.CreditNoteValue, m.InputGst, m.Payable is { } p ? p.PayCgst + p.PaySgst + p.PayIgst + (p.CompositionTax ?? 0) : 0)).ToList();
        return TypedResults.Ok(new GstAnnualDto(
            $"{year}-{(year + 1) % 100:00}", months.Any(m => m.Composition), rows,
            taxable, b2b, decimal.Round(taxable - b2b, 2), Sum(m => m.CreditNoteValue), Sum(m => m.CreditNoteTax),
            Pay(p => p.OutputCgst), Pay(p => p.OutputSgst), Pay(p => p.OutputIgst), Pay(p => p.InputCgst), Pay(p => p.InputSgst), Pay(p => p.InputIgst),
            Pay(p => p.PayCgst), Pay(p => p.PaySgst), Pay(p => p.PayIgst), Pay(p => p.CompositionTurnover ?? 0), Pay(p => p.CompositionTax ?? 0),
            hsn, months.Count(m => m.Checks.Any(c => c.Count > 0 && c.Code != "returns"))));
    }
}
