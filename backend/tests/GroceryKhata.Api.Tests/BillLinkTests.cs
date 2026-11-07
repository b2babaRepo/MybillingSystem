using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BillLinkTests(ApiFactory api)
{
    private static async Task<Guid> Bill(HttpClient shop, Guid? customer, decimal paid)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/bills", new { id, customerId = customer, paidAmount = paid, lines = new[] { new { name = "Paint", quantity = 1m, unitPrice = 100m } } })).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task A_bill_link_shows_the_bill_and_nothing_else_about_the_customer()
    {
        var (shop, _) = await api.SignUpAsync("Link Store");
        var phone = ApiFactory.NewPhone();
        var customer = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Priya", phone })).EnsureSuccessStatusCode();
        await KhataTests.AddEntryAsync(shop, customer, "credit", 5000m); // what she owes overall must stay private
        var bill = await Bill(shop, customer, 40m);

        var link = await (await shop.PostJsonAsync($"/api/v1/bills/{bill}/link", new { })).ReadAsync<BillLinkDto>();
        var guest = api.CreateClient();
        var body = await guest.GetStringAsync($"/api/v1/bill/{link.ShopSlug}/{bill}?t={link.Token}");
        var seen = await guest.GetJsonAsync<PublicBillDto>($"/api/v1/bill/{link.ShopSlug}/{bill}?t={link.Token}");

        Assert.Equal(("Link Store", "Priya", 100m, 60m), (seen.ShopName, seen.CustomerName, seen.Bill.Total, seen.Bill.OnKhata));
        Assert.Null(seen.Bill.CustomerBalance);
        Assert.Null(seen.Bill.CustomerId);
        Assert.DoesNotContain("5060", body);
        Assert.DoesNotContain(phone[^6..], body);
    }

    [Fact]
    public void A_prescription_never_leaves_on_a_link()
    {
        var bill = new BillDto(Guid.NewGuid(), Guid.NewGuid(), 10m, 10m, 0m, null, DateTimeOffset.UtcNow, [], 900m, Prescription: new PrescriptionRequest("Dr A", null, null, "Patient", null, null));
        var view = new BillView(bill, new CustomerDto(Guid.NewGuid(), "Patient", "+919811111111", 900m));

        var seen = BillLinks.Public("Shop", view);

        Assert.Equal((null, null, null), ((object?)seen.Bill.Prescription, (object?)seen.Bill.CustomerBalance, (object?)seen.Bill.CustomerId));
    }

    [Fact]
    public async Task A_wrong_token_or_another_bills_token_finds_nothing()
    {
        var (shop, _) = await api.SignUpAsync("Guard Store");
        var (other, _) = await api.SignUpAsync("Other Store");
        var mine = await Bill(shop, null, 100m);
        var second = await Bill(shop, null, 100m);
        var theirs = await Bill(other, null, 100m);
        var link = await (await shop.PostJsonAsync($"/api/v1/bills/{mine}/link", new { })).ReadAsync<BillLinkDto>();
        var theirLink = await (await other.PostJsonAsync($"/api/v1/bills/{theirs}/link", new { })).ReadAsync<BillLinkDto>();
        var guest = api.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/bill/{link.ShopSlug}/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/bill/{link.ShopSlug}/{mine}?t=nope")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/bill/{link.ShopSlug}/{second}?t={link.Token}")).StatusCode);
        // A real token doesn't reach a bill through another shop's address.
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/v1/bill/{link.ShopSlug}/{theirs}?t={theirLink.Token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await shop.PostJsonAsync($"/api/v1/bills/{theirs}/link", new { })).StatusCode);
    }
}
