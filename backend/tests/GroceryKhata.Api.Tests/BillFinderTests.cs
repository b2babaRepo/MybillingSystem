using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BillFinderTests(ApiFactory api)
{
    [Fact]
    public async Task An_old_bill_is_found_by_number_customer_mobile_or_item()
    {
        var (shop, _) = await api.SignUpAsync("Find Bill Store");
        var customer = await (await shop.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = "Sunita Patel", phone = "9876501234" })).ReadAsync<CustomerDto>();
        var kurti = await (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer.Id,
            lines = new[] { new { name = "Anarkali Kurti", quantity = 1m, unitPrice = 1299m } }, paidAmount = 1299m, paymentMode = "cash" })).ReadAsync<BillDto>();
        (await shop.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(),
            lines = new[] { new { name = "Cotton Saree", quantity = 1m, unitPrice = 899m } }, paidAmount = 899m, paymentMode = "cash" })).EnsureSuccessStatusCode();

        async Task<List<FoundBill>> Find(string q) => await shop.GetJsonAsync<List<FoundBill>>($"/api/v1/bills/find?q={Uri.EscapeDataString(q)}");
        foreach (var q in new[] { "sunita", "01234", "anarkali", kurti.BillNo!, "1" })
        {
            var found = Assert.Single(await Find(q));
            Assert.Equal((kurti.Id, "Sunita Patel", "Anarkali Kurti", 1299m), (found.Id, found.CustomerName, found.Items, found.Total));
        }

        Assert.Equal(2, (await shop.GetJsonAsync<List<FoundBill>>("/api/v1/bills/find")).Count);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        Assert.Empty(await shop.GetJsonAsync<List<FoundBill>>($"/api/v1/bills/find?to={today.AddDays(-2):yyyy-MM-dd}"));
        Assert.Equal(2, (await shop.GetJsonAsync<List<FoundBill>>($"/api/v1/bills/find?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}")).Count);
    }
}
