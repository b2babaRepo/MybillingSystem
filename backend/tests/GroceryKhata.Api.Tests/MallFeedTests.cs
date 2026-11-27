using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>A mall's sales-only key: it reads one shop's sales and nothing else, until the owner stops it.</summary>
[Collection(ApiCollection.Name)]
public sealed class MallFeedTests(ApiFactory api)
{
    [Fact]
    public async Task A_mall_key_reads_its_shops_sales_only_until_it_is_stopped()
    {
        var (owner, _) = await api.SignUpAsync("Mall Tenant");
        var created = await (await owner.PostJsonAsync("/api/v1/mall-feeds", new { name = "Phoenix Marketcity" })).ReadAsync<MallFeedCreated>();
        Assert.StartsWith("gkm_", created.Key);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostJsonAsync("/api/v1/mall-feeds", new { name = "X", pushUrl = "http://not-https.example" })).StatusCode);

        // Two sales, one cancelled, and goods back on one of them.
        Task<BillDto> BillAsync(decimal price) => owner.PostJsonAsync("/api/v1/bills",
            new { id = Guid.CreateVersion7(), lines = new[] { new { name = "Shirt", quantity = 1m, unitPrice = price } }, paidAmount = price, paymentMode = "cash" }).ContinueWith(t => t.Result.ReadAsync<BillDto>()).Unwrap();
        var kept = await BillAsync(1000m);
        var voided = await BillAsync(500m);
        (await owner.PostJsonAsync($"/api/v1/bills/{voided.Id}/cancel", new { reason = "Wrong size" })).EnsureSuccessStatusCode();
        (await owner.PostJsonAsync($"/api/v1/bills/{kept.Id}/credit-notes", new { id = Guid.CreateVersion7(), reason = "Faulty", lines = new[] { new { lineNo = 1, quantity = 1m } }, refundMode = "cash" })).EnsureSuccessStatusCode();

        var mall = api.CreateClient();
        var today = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.Unauthorized, (await mall.GetAsync($"/api/mall/v1/daily?from={today}")).StatusCode);
        mall.DefaultRequestHeaders.Add(MallFeeds.KeyHeader, "gkm_wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await mall.GetAsync($"/api/mall/v1/daily?from={today}")).StatusCode);
        mall.DefaultRequestHeaders.Remove(MallFeeds.KeyHeader);
        mall.DefaultRequestHeaders.Add(MallFeeds.KeyHeader, created.Key);

        var day = Assert.Single(await mall.GetJsonAsync<List<MallDay>>($"/api/mall/v1/daily?from={today}&to={today}"));
        Assert.Equal((1, 1000m, 1, 1000m, 1, 500m, 0m), (day.Bills, day.NetSales, day.Returns, day.ReturnsValue, day.Cancelled, day.CancelledValue, day.NetAfterReturnsExGst));
        var detail = await mall.GetJsonAsync<MallDayDetail>($"/api/mall/v1/bills?date={today}");
        Assert.Equal(2, detail.Bills.Count); // the cancelled one is listed, marked
        Assert.NotNull(detail.Bills.Single(b => b.Total == 500m).CancelledAt);
        Assert.Contains("bill_no,time,till", await mall.GetStringAsync($"/api/mall/v1/bills?date={today}&format=csv"));

        // Another shop's key sees none of this; the owner's own sign-in is not a mall key.
        var (other, _) = await api.SignUpAsync("Other Tenant");
        var otherKey = (await (await other.PostJsonAsync("/api/v1/mall-feeds", new { name = "Phoenix Marketcity" })).ReadAsync<MallFeedCreated>()).Key;
        var otherMall = api.CreateClient();
        otherMall.DefaultRequestHeaders.Add(MallFeeds.KeyHeader, otherKey);
        Assert.Equal(0, Assert.Single(await otherMall.GetJsonAsync<List<MallDay>>($"/api/mall/v1/daily?from={today}")).Bills);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync($"/api/mall/v1/daily?from={today}")).StatusCode);

        (await owner.PostAsync($"/api/v1/mall-feeds/{created.Feed.Id}/revoke", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await mall.GetAsync($"/api/mall/v1/daily?from={today}")).StatusCode);
        Assert.NotNull(Assert.Single(await owner.GetJsonAsync<List<MallFeedDto>>("/api/v1/mall-feeds")).RevokedAt);
    }
}
