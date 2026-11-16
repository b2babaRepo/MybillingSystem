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

        // A customer who paid ahead is owed by the shop: a liability, not a negative asset.
        var ahead = await KhataTests.CreateCustomerAsync(shop, "Ravi");
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = ahead, type = "payment", amount = 200m, paymentMode = "cash" })).EnsureSuccessStatusCode();
        sheet = await shop.GetJsonAsync<BalanceSheetDto>("/api/v1/reports/balance-sheet");
        Assert.Equal((500m, 200m), (sheet.CustomersOwe, sheet.CustomerAdvances));
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
