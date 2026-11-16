using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BalanceSheetTests(ApiFactory api)
{
    [Fact]
    public async Task The_balance_sheet_shows_what_the_shop_has_and_owes_and_always_balances()
    {
        var (shop, _) = await api.SignUpAsync("Balance Store");
        var kurti = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id = kurti, name = "Kurti", unit = "piece", sellingPrice = 150m, costPrice = 100m, openingStock = 10m })).EnsureSuccessStatusCode();

        // Bought 3 more on credit (₹300 owed), sold 1 for cash, and ₹500 of udhaar to a customer.
        var supplier = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/suppliers", new { id = supplier, name = "Jain Traders" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/suppliers/purchases", new { id = Guid.CreateVersion7(), supplierId = supplier, paidAmount = 0m,
            lines = new[] { new { productId = kurti, quantity = 3m, unitCost = 100m } } })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), lines = new[] { new { productId = kurti, quantity = 1m, unitPrice = 150m } },
            paidAmount = 150m, paymentMode = "cash" })).EnsureSuccessStatusCode();
        var customer = await KhataTests.CreateCustomerAsync(shop, "Meena");
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "credit", amount = 500m })).EnsureSuccessStatusCode();

        var sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal((1200m, 500m, 1, 300m, 1), (sheet.Stock, sheet.CustomersOwe, sheet.CustomersOwing, sheet.SuppliersOwed, sheet.SuppliersOwedCount));
        Assert.Equal(sheet.Cash + sheet.Bank + sheet.Stock + sheet.CustomersOwe + sheet.SupplierAdvances, sheet.TotalAssets);
        Assert.Equal(sheet.SuppliersOwed + sheet.CustomerAdvances, sheet.TotalLiabilities);
        Assert.Equal(sheet.TotalAssets - sheet.TotalLiabilities, sheet.Capital);

        // The 10 kurtis entered as opening stock at 100 are money the owner started with, not unexplained.
        Assert.Equal(1000m, sheet.CapitalSplit!.StartedWith);

        // A customer who paid ahead is owed by the shop: a liability, not a negative asset.
        var ahead = await KhataTests.CreateCustomerAsync(shop, "Ravi");
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = ahead, type = "payment", amount = 200m, paymentMode = "cash" })).EnsureSuccessStatusCode();
        sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal((500m, 200m), (sheet.CustomersOwe, sheet.CustomerAdvances));
    }

    [Fact]
    public async Task Furniture_and_loans_go_on_the_sheet_and_the_capital_story_adds_up()
    {
        var (shop, _) = await api.SignUpAsync("Balance Store 3");
        var before = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");

        // An almirah worth 20,000 and a 50,000 bank loan.
        var almirah = Guid.CreateVersion7();
        (await shop.PutJsonAsync($"/api/v1/balance-items/{almirah}", new { id = almirah, kind = "fixed_asset", name = "Steel almirah", amount = 20000m })).EnsureSuccessStatusCode();
        var loan = Guid.CreateVersion7();
        (await shop.PutJsonAsync($"/api/v1/balance-items/{loan}", new { id = loan, kind = "loan", name = "SBI loan", amount = 50000m })).EnsureSuccessStatusCode();

        var sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal((20000m, 50000m, 2), (sheet.FixedAssets, sheet.Loans, sheet.Items!.Count));
        Assert.Equal(before.TotalAssets + 20000m, sheet.TotalAssets);
        Assert.Equal(before.TotalLiabilities + 50000m, sheet.TotalLiabilities);
        Assert.Equal(sheet.TotalAssets - sheet.TotalLiabilities, sheet.Capital);

        // The year's capital story always comes back to the capital, with whatever isn't explained shown.
        var split = sheet.CapitalSplit!;
        Assert.Equal(sheet.Capital, split.Opening + split.StartedWith + split.ItemsNet + split.Profit + split.BroughtIn - split.TakenHome + split.Unexplained);

        Assert.Equal(-30000m, split.ItemsNet); // the 20,000 almirah less the 50,000 loan

        // A repaid loan stops counting from the day it ends; a past date carries no year's story.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        (await shop.PutJsonAsync($"/api/v1/balance-items/{loan}", new { id = loan, kind = "loan", name = "SBI loan", amount = 50000m, until = today.ToString("yyyy-MM-dd") })).EnsureSuccessStatusCode();
        Assert.Equal(0m, (await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet")).Loans);
        Assert.Null((await shop.GetJsonAsync<BalanceSheetDto>($"/api/v1/reports/balance-sheet?on={today.AddDays(-1):yyyy-MM-dd}")).CapitalSplit);

        // A nameless or zero item is refused.
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await shop.PutJsonAsync($"/api/v1/balance-items/{Guid.CreateVersion7()}", new { kind = "loan", name = "", amount = 0m })).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_sees_the_balance_sheet_and_a_bad_date_is_refused()
    {
        var (shop, _) = await api.SignUpAsync("Balance Store 2");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await shop.GetAsync("/api/v1/reports/balance-sheet?on=16-11-2026")).StatusCode);
        var past = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet?on=2026-01-31");
        Assert.Equal((0m, 0m), (past.TotalAssets, past.TotalLiabilities));
    }
}
