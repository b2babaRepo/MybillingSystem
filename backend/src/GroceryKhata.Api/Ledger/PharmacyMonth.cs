using System.Globalization;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A medical shop's month for the owner and the CA. GST: tax on sales, input credit from distributor bills, and the
/// difference to pay (or carry forward). Profit: sales before GST less what the medicines sold cost, less the month's
/// expiry losses. Receipts by cash and digital, which presumptive income tax (s.44AD) counts differently. A working
/// aid: the CA checks and files.
/// </summary>
public sealed record PharmacyMonthDto(
    string Month, decimal Sales, decimal SalesTaxable, decimal OutputGst, decimal InputGst, decimal GstPayable, decimal CreditCarried,
    decimal Purchases, decimal CostOfSales, decimal GrossProfit, decimal MarginPercent, decimal ExpiryLoss, decimal ExpiredOnShelf, decimal ProfitAfterExpiry,
    decimal CashReceipts, decimal DigitalReceipts, int LinesWithoutCost, bool Composition);

public static class PharmacyMonth
{
    public static IEndpointRouteBuilder MapPharmacyMonth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/pharmacy/month", MonthAsync).WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> MonthAsync(string? month, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        if (IndiaTime.ParseMonth(month ?? today.ToString("yyyy-MM", CultureInfo.InvariantCulture)) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var bills = await db.Bills.AsNoTracking().Include(b => b.Lines).Where(b => b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null).ToListAsync(ct);
        var composition = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => s.GstComposition).SingleAsync(ct);
        var lines = bills.SelectMany(b => b.Lines.Select(l => (Bill: b, Line: l))).ToList();
        // A bill without the shop's GSTIN (or a bill of supply) carries no tax: its whole amount is the sale.
        var taxed = lines.Select(x => x.Bill.Gstin is null || x.Bill.BillOfSupply ? (x.Line.Amount, 0m) : (GstExport.LineTax(x.Line).Taxable, GstExport.LineTax(x.Line).Cgst + GstExport.LineTax(x.Line).Sgst + GstExport.LineTax(x.Line).Igst)).ToList();
        var salesTaxable = taxed.Sum(t => t.Item1);
        var outputGst = taxed.Sum(t => t.Item2);

        var purchases = await db.Purchases.AsNoTracking().Include(p => p.Lines).Where(p => p.OccurredAt >= start && p.OccurredAt < end && p.CancelledAt == null).ToListAsync(ct);
        // A composition shop can't take input credit: GST paid to the distributor is part of the cost.
        var inputGst = composition ? 0 : purchases.Sum(p => p.Lines.Sum(l => l.Gst));
        var payable = Math.Max(0, outputGst - inputGst);

        var costOfSales = lines.Sum(x => x.Line.UnitCost is { } c ? Money.LineAmount(x.Line.Quantity, c) : 0);
        var noCost = lines.Count(x => x.Line.UnitCost is null && x.Line.ProductId is not null);
        var gross = salesTaxable - costOfSales;
        var settled = await db.SupplierReturns.AsNoTracking().Where(r => r.SettledAt >= start && r.SettledAt < end).Select(r => new { r.Value, r.CreditAmount }).ToListAsync(ct);
        var expiryLoss = settled.Sum(r => Math.Max(0, r.Value - (r.CreditAmount ?? 0)));
        var expired = await db.ProductBatches.AsNoTracking().Where(b => b.Quantity > 0 && b.Expiry < today).Select(b => new { b.Quantity, b.CostPrice }).ToListAsync(ct);

        // Money received: bills paid at the counter, and khata payments taken later.
        var counter = bills.Where(b => b.PaidAmount > 0).GroupBy(b => b.PaymentMode).ToDictionary(g => g.Key ?? PaymentMode.Cash, g => g.Sum(b => b.PaidAmount));
        var khata = await db.LedgerEntries.AsNoTracking().Where(e => e.Kind == LedgerEntryKind.Payment && e.BillId == null && e.OccurredAt >= start && e.OccurredAt < end)
            .GroupBy(e => e.PaymentMode).Select(g => new { g.Key, Total = -g.Sum(e => e.Amount) }).ToListAsync(ct);
        decimal Received(PaymentMode mode) => counter.GetValueOrDefault(mode) + khata.Where(k => (k.Key ?? PaymentMode.Cash) == mode).Sum(k => k.Total);

        return TypedResults.Ok(new PharmacyMonthDto(
            $"{period.Year:D4}-{period.Month:D2}", bills.Sum(b => b.Total), salesTaxable, outputGst, inputGst, payable, Math.Max(0, inputGst - outputGst),
            purchases.Sum(p => p.Total), costOfSales, gross, salesTaxable > 0 ? decimal.Round(gross * 100 / salesTaxable, 1) : 0, expiryLoss,
            expired.Sum(b => Money.LineAmount(b.Quantity, b.CostPrice ?? 0)), gross - expiryLoss, Received(PaymentMode.Cash), Received(PaymentMode.Upi), noCost, composition));
    }
}
