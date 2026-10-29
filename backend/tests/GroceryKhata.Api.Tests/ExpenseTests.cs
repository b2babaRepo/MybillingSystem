using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>The kharcha book: write a line like in the notebook, check it, save; struck lines stay but stop counting.</summary>
[Collection(ApiCollection.Name)]
public sealed class ExpenseTests(ApiFactory api)
{
    [Theory]
    [InlineData("chai 40, auto 120, thaili 250 upi", "Tea 40 Cash|Transport 120 Cash|Packaging 250 Upi")]
    [InlineData("chai 40 auto 120", "Tea 40 Cash|Transport 120 Cash")]
    [InlineData("kiraya 5000 aur bijli 1200 online", "Rent 5000 Cash|Electricity 1200 Upi")]
    [InlineData("₹300 mandi tempo", "Transport 300 Cash")]
    [InlineData("चाय 30 और किराया 4000", "Tea 30 Cash|Rent 4000 Cash")]
    [InlineData("do sau mistri", "Repair 200 Cash")]
    [InlineData("ghar ke liye 500", "Home 500 Cash")]
    [InlineData("pen copy 60", "Other 60 Cash")]
    public void A_notebook_line_is_read_into_amounts_headings_and_cash_or_upi(string text, string expected)
    {
        var read = ExpenseReader.Read(text);
        Assert.Equal(expected, string.Join('|', read.Select(r => $"{r.Category} {r.Amount:0.##} {r.Mode}")));
    }

    [Fact]
    public async Task Lines_add_up_on_the_page_struck_lines_stop_counting_and_the_cash_book_shows_money_out()
    {
        var (shop, _) = await api.SignUpAsync();
        var chai = Guid.CreateVersion7();
        await Add(shop, chai, 40m, "chai", "cash");
        await Add(shop, Guid.CreateVersion7(), 250m, "thaili", "upi");
        var mistake = Guid.CreateVersion7();
        await Add(shop, mistake, 900m, "auto", "cash");

        // Sent twice (an offline retry): still one line.
        Assert.Equal(HttpStatusCode.OK, (await shop.PostJsonAsync("/api/v1/expenses", new { id = chai, amount = 40m, note = "chai", mode = "cash" })).StatusCode);

        var struck = await (await shop.PostJsonAsync($"/api/v1/expenses/{mistake}/strike", new { })).ReadAsync<ExpenseDto>();
        Assert.NotNull(struck.StruckAt);

        var page = await shop.GetJsonAsync<ExpensePageDto>("/api/v1/expenses");
        Assert.Equal(3, page.Lines.Count); // the struck line stays on the page, crossed out
        Assert.Equal((40m, 250m, 290m), (page.Cash, page.Upi, page.Total));
        Assert.Equal(ExpenseCategory.Tea, page.Lines[0].Category); // heading guessed from the note

        var book = await shop.GetJsonAsync<CashBookDto>("/api/v1/cash-book");
        Assert.Equal((40m, 250m), (book.SpentCash, book.SpentUpi));
        Assert.Contains(book.Lines, l => l.Source == CashBookSource.Expense && l.Amount == -40m);
    }

    [Fact]
    public async Task The_month_report_takes_running_costs_off_profit_but_not_stock_bought_or_money_taken_home()
    {
        var (shop, _) = await api.SignUpAsync();
        await Add(shop, Guid.CreateVersion7(), 5000m, "kiraya", "cash");
        await Add(shop, Guid.CreateVersion7(), 20000m, "maal mandi se", "upi");
        await Add(shop, Guid.CreateVersion7(), 3000m, "ghar", "cash");

        var month = await shop.GetJsonAsync<MonthReport>($"/api/v1/reports/month?month={DateTime.UtcNow.AddHours(5.5):yyyy-MM}");
        Assert.Equal((5000m, 20000m, 3000m), (month.Expenses, month.StockPurchases, month.TakenHome));
        Assert.Equal(month.EstimatedProfit - 5000m, month.NetProfit);
    }

    [Fact]
    public async Task One_shop_never_sees_another_shops_expenses()
    {
        var (sharma, _) = await api.SignUpAsync("Sharma General Store");
        var (gupta, _) = await api.SignUpAsync("Gupta Kirana");
        var id = Guid.CreateVersion7();
        await Add(sharma, id, 70m, "chai", "cash");

        Assert.Empty((await gupta.GetJsonAsync<ExpensePageDto>("/api/v1/expenses")).Lines);
        Assert.Equal(HttpStatusCode.NotFound, (await gupta.PostJsonAsync($"/api/v1/expenses/{id}/strike", new { })).StatusCode);
    }

    private static async Task Add(HttpClient shop, Guid id, decimal amount, string note, string mode) =>
        Assert.Equal(HttpStatusCode.Created, (await shop.PostJsonAsync("/api/v1/expenses", new { id, amount, note, mode })).StatusCode);
}
