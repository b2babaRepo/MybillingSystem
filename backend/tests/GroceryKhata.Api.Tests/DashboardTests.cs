using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DashboardTests(ApiFactory api)
{
    [Fact]
    public async Task Today_adds_up_sales_money_received_and_new_udhaar()
    {
        var (client, _) = await api.SignUpAsync();
        var suresh = await KhataTests.CreateCustomerAsync(client, "Suresh");
        var ramesh = await KhataTests.CreateCustomerAsync(client, "Ramesh");

        // A ₹130 bill on Suresh's khata, ₹30 paid by UPI; a ₹48 walk-in cash sale.
        await (await client.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            customerId = suresh,
            lines = new[] { new { name = "Atta", quantity = 1m, unit = "kg", unitPrice = 130m } },
            paidAmount = 30m,
            paymentMode = "upi",
        })).ReadAsync<BillDto>();
        await (await client.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            lines = new[] { new { name = "Sugar", quantity = 1m, unit = "kg", unitPrice = 48m } },
            paidAmount = 48m,
        })).ReadAsync<BillDto>();

        // Ramesh: ₹340 udhaar, ₹100 paid in cash, and a mistaken ₹50 udhaar reversed.
        await KhataTests.AddEntryAsync(client, ramesh, "credit", 340m);
        await KhataTests.AddEntryAsync(client, ramesh, "payment", 100m);
        var mistake = await KhataTests.AddEntryAsync(client, ramesh, "credit", 50m);
        await (await client.PostJsonAsync($"/api/v1/ledger-entries/{mistake.Entry.Id}/reverse", new { id = Guid.CreateVersion7() }))
            .ReadAsync<KhataEntryResponse>();

        var today = await client.GetJsonAsync<TodayDto>("/api/v1/dashboard/today");

        // One comparison, so a failure shows every figure at once (it failed once in a full run and never again).
        Assert.Equal(
            (Sales: 178m, Bills: 2, Cash: 148m, Upi: 30m, Udhaar: 440m, Outstanding: 340m, Owing: 2, Top: "Ramesh, Suresh"),
            (today.Sales, today.Bills, today.Cash, today.Upi, today.Udhaar, today.Outstanding, today.CustomersOwing, string.Join(", ", today.TopDues.Select(c => c.Name))));
    }
}
