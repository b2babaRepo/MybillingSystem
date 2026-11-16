using System.Globalization;
using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// What the shop has and what it owes at the end of a day: cash in hand, bank, stock at cost, what customers owe and
/// suppliers were paid ahead, against what is owed to suppliers and customers' advances. The owner's capital is what is
/// left over, so the two sides always balance. Each figure is the same one its own screen shows (cash ledger, bank
/// book, stock statement, khata, suppliers), so the CA can trace every line.
/// </summary>
public sealed record BalanceSheetDto(
    DateOnly On,
    decimal Cash, decimal Bank, decimal Stock, decimal CustomersOwe, decimal SupplierAdvances, decimal TotalAssets,
    decimal SuppliersOwed, decimal CustomerAdvances, decimal TotalLiabilities,
    decimal Capital, int CustomersOwing, int SuppliersOwedCount, int UnvaluedItems);

public static class BalanceSheet
{
    public static IEndpointRouteBuilder MapBalanceSheet(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/balance-sheet", GetAsync).WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> GetAsync(string? on, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
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

        var customersOwe = customers.Where(b => b > 0).Sum();
        var customerAdvances = -customers.Where(b => b < 0).Sum();
        var suppliersOwed = suppliers.Where(b => b > 0).Sum();
        var supplierAdvances = -suppliers.Where(b => b < 0).Sum();

        var assets = cash + bank + (stock?.Value ?? 0) + customersOwe + supplierAdvances;
        var liabilities = suppliersOwed + customerAdvances;
        return TypedResults.Ok(new BalanceSheetDto(day, cash, bank, stock?.Value ?? 0, customersOwe, supplierAdvances, assets,
            suppliersOwed, customerAdvances, liabilities, assets - liabilities, customers.Count(b => b > 0), suppliers.Count(b => b > 0), stock?.Unvalued ?? 0));
    }

    private static T? Value<T>(IResult result) where T : class => result is IValueHttpResult { Value: T value } ? value : null;
}
