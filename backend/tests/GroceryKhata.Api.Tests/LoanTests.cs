using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class LoanTests(ApiFactory api)
{
    [Fact]
    public async Task An_emi_splits_into_principal_and_interest_and_the_books_follow()
    {
        var (shop, _) = await api.SignUpAsync("Loan Store");
        var cashBefore = (await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet")).Cash;

        var id = Guid.CreateVersion7();
        var loan = await (await shop.PostJsonAsync("/api/v1/loans", new { id, lender = "SBI", amount = 100000m, ratePercent = 12m, emi = 5000m, receivedInto = "cash" })).ReadAsync<LoanDto>();
        Assert.Equal((100000m, 1000m), (loan.Outstanding, loan.NextInterest));

        var sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal(cashBefore + 100000m, sheet.Cash);
        Assert.Equal(100000m, sheet.Loans);
        Assert.Equal(0m, sheet.CapitalSplit!.BroughtIn); // borrowed, not the owner's money

        // First EMI: 1% a month on ₹1,00,000 is interest; the rest pays the loan down.
        var payment = Guid.CreateVersion7();
        loan = await (await shop.PostJsonAsync($"/api/v1/loans/{id}/payments", new { id = payment, amount = 5000m, mode = "cash" })).ReadAsync<LoanDto>();
        Assert.Equal((4000m, 1000m, 96000m, 960m), (loan.PrincipalPaid, loan.InterestPaid, loan.Outstanding, loan.NextInterest));

        sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal(cashBefore + 95000m, sheet.Cash);
        Assert.Equal(96000m, sheet.Loans);
        var pnl = await shop.GetJsonAsync<ProfitLossReport>("/api/v1/reports/pnl");
        Assert.Equal(1000m, pnl.ExpenseLines!.Single(l => l.Category == ExpenseCategory.Interest).Amount); // only the interest is a cost
        var tb = await shop.GetJsonAsync<TrialBalanceDto>("/api/v1/reports/trial-balance");
        Assert.Equal(tb.TotalDebit, tb.TotalCredit);

        var statement = await shop.GetJsonAsync<LoanStatementDto>($"/api/v1/loans/{id}");
        Assert.Equal(96000m, Assert.Single(statement.Payments).OutstandingAfter);

        // Can't pay more than is owed; a wrong EMI is struck and everything goes back.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/loans/{id}/payments", new { id = Guid.CreateVersion7(), amount = 200000m, interest = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/loans/{id}/strike", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync($"/api/v1/loans/{id}/payments/{payment}/strike", new { })).StatusCode);
        sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal((cashBefore + 100000m, 100000m), (sheet.Cash, sheet.Loans));

        // And the loan itself, entered by mistake.
        Assert.Equal(HttpStatusCode.NoContent, (await shop.PostJsonAsync($"/api/v1/loans/{id}/strike", new { })).StatusCode);
        sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal((cashBefore, 0m), (sheet.Cash, sheet.Loans));
    }
}
