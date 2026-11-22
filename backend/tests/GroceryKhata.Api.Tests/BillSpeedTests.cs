using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BillSpeedTests(ApiFactory api)
{
    [Fact]
    public async Task The_owner_sees_how_fast_bills_go_by_size_and_credit()
    {
        var (shop, _) = await api.SignUpAsync("Quick Store");
        var buyer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        async Task Bill(int? seconds, Guid? customer = null, int lines = 1, decimal paid = 100m)
        {
            var id = Guid.CreateVersion7();
            (await shop.PostJsonAsync("/api/v1/bills", new
            {
                id, customerId = customer, paidAmount = paid * lines, entrySeconds = seconds,
                lines = Enumerable.Range(1, lines).Select(i => new { name = $"Item {i}", quantity = 1m, unitPrice = 100m }).ToArray(),
            })).EnsureSuccessStatusCode();
        }

        await Bill(10);
        await Bill(20);
        await Bill(60, lines: 6);
        await Bill(30, buyer, paid: 0m);
        await Bill(null);      // resumed from hold: not timed
        await Bill(99999);     // can't be real: kept out

        var report = await shop.GetJsonAsync<BillSpeedDto>("/api/v1/reports/bill-speed");

        Assert.Equal((4, 25), (report.Bills, report.MedianSeconds));
        Assert.Equal(new BillSpeedRow("small", 3, 20), report.Rows.Single(r => r.Group == "small"));
        Assert.Equal(new BillSpeedRow("medium", 1, 60), report.Rows.Single(r => r.Group == "medium"));
        Assert.Equal(new BillSpeedRow("credit", 1, 30), report.Rows.Single(r => r.Group == "credit"));
        Assert.DoesNotContain(report.Rows, r => r.Group == "large");
    }

    [Fact]
    public void Half_the_bills_are_faster_than_the_median()
    {
        Assert.Equal(20, BillSpeed.Median([40, 10, 20]));
        Assert.Equal(15, BillSpeed.Median([10, 20]));
    }
}
