using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>The evening count of the galla: opening + cash in − cash spent should be in the box; any difference is kept.</summary>
[Collection(ApiCollection.Name)]
public sealed class CashCountTests(ApiFactory api)
{
    [Fact]
    public async Task The_count_shows_what_should_be_in_the_box_and_records_short_or_extra()
    {
        var (shop, _) = await api.SignUpAsync();
        // ₹500 cash sale, ₹200 by UPI (not in the box), ₹40 chai paid in cash.
        await Bill(shop, 500m, "cash");
        await Bill(shop, 200m, "upi");
        await shop.PostJsonAsync("/api/v1/expenses", new { id = Guid.CreateVersion7(), amount = 40m, note = "chai", mode = "cash" });

        var before = await shop.GetJsonAsync<CashCountDto>("/api/v1/cash-count");
        Assert.False(before.OpeningFromCount); // first ever count: the screen asks for the opening cash
        Assert.Equal((500m, 40m, 460m), (before.CashIn, before.CashOut, before.Expected));

        var counted = await (await shop.PostJsonAsync("/api/v1/cash-count", new { opening = 1000m, counted = 1450m, note = "₹10 change given extra" }))
            .ReadAsync<CashCountDto>();
        Assert.Equal((1000m, 1460m, 1450m, -10m), (counted.Opening, counted.Expected, counted.Counted!.Value, counted.Difference!.Value));
        Assert.Equal("Ramesh Sharma", counted.CountedBy);

        // Counting again the same day replaces the count, keeping the opening.
        var again = await (await shop.PostJsonAsync("/api/v1/cash-count", new { counted = 1460m })).ReadAsync<CashCountDto>();
        Assert.Equal((1000m, 0m), (again.Opening, again.Difference!.Value));
    }

    [Fact]
    public async Task A_day_cannot_be_counted_before_it_comes_and_shops_never_see_each_others_counts()
    {
        var (sharma, _) = await api.SignUpAsync("Sharma General Store");
        var (gupta, _) = await api.SignUpAsync("Gupta Kirana");
        var tomorrow = DateTime.UtcNow.AddHours(5.5).AddDays(1).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.BadRequest, (await sharma.PostJsonAsync("/api/v1/cash-count", new { date = tomorrow, counted = 10m })).StatusCode);

        await sharma.PostJsonAsync("/api/v1/cash-count", new { opening = 300m, counted = 300m });
        var other = await gupta.GetJsonAsync<CashCountDto>("/api/v1/cash-count");
        Assert.Null(other.Counted);
        Assert.Equal(0m, other.Opening);
    }

    private static async Task Bill(HttpClient shop, decimal amount, string mode) =>
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(),
            lines = new[] { new { name = "Atta", quantity = 1m, unit = "kg", unitPrice = amount } },
            paidAmount = amount,
            paymentMode = mode,
        })).ReadAsync<BillDto>();
}
