using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Money received is proof kept apart from whose it is, so a wrong match can be put right without losing the proof.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReceiptTests(ApiFactory api)
{
    private static Task<HttpResponseMessage> Pay(HttpClient shop, Guid customer, decimal amount, string upiRef) => shop.PostJsonAsync("/api/v1/khata-entries", new
    {
        id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount, paymentMode = "upi", upiRef,
    });

    private static async Task<decimal> Balance(HttpClient shop, Guid customer) =>
        (await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers")).Single(c => c.Id == customer).Balance;

    private static async Task<Guid> Note(HttpClient shop, decimal amount, string? utr = null, string? payer = null)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/receipts", new { id, amount, utr, payerName = payer })).EnsureSuccessStatusCode();
        return id;
    }

    private static Task<HttpResponseMessage> Allocate(HttpClient shop, Guid receipt, Guid customer, decimal amount) =>
        shop.PostJsonAsync($"/api/v1/receipts/{receipt}/allocate", new { id = Guid.CreateVersion7(), customerId = customer, amount });

    [Fact]
    public async Task A_wrong_utr_payment_can_be_moved_to_the_right_customer_and_never_counted_twice()
    {
        var (shop, _) = await api.SignUpAsync("Moving Money Store");
        var wrong = await KhataTests.CreateCustomerAsync(shop, "Wrong Ramesh");
        var right = await KhataTests.CreateCustomerAsync(shop, "Right Ramesh");
        await KhataTests.AddEntryAsync(shop, wrong, "credit", 500m);
        await KhataTests.AddEntryAsync(shop, right, "credit", 500m);

        (await Pay(shop, wrong, 500m, "UTR123456")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Pay(shop, right, 500m, "utr123456")).StatusCode);

        var entry = (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{wrong}/statement")).Entries.Single(e => e.Amount < 0);

        // The reason is the customer's explanation, so it can't be skipped.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/ledger-entries/{entry.Id}/reverse", new { id = Guid.CreateVersion7() })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/ledger-entries/{entry.Id}/reverse", new { id = Guid.CreateVersion7(), note = "Wrong Ramesh" })).EnsureSuccessStatusCode();
        Assert.Equal(500m, await Balance(shop, wrong));

        var tray = await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts");
        var freed = tray.Single();
        Assert.Equal(("UTR123456", 500m), (freed.Utr, freed.Left));

        (await Allocate(shop, freed.Id, right, 500m)).EnsureSuccessStatusCode();
        Assert.Equal(0m, await Balance(shop, right));
        Assert.Empty(await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts"));
        Assert.Equal(HttpStatusCode.Conflict, (await Allocate(shop, freed.Id, wrong, 1m)).StatusCode);
        Assert.Empty(await shop.GetJsonAsync<List<BalanceMismatch>>("/api/v1/ledger/reconcile"));
    }

    [Fact]
    public async Task One_payment_can_be_split_between_two_customers_but_never_for_more_than_arrived()
    {
        var (shop, _) = await api.SignUpAsync("Split Store");
        var husband = await KhataTests.CreateCustomerAsync(shop, "Husband");
        var wife = await KhataTests.CreateCustomerAsync(shop, "Wife");
        await KhataTests.AddEntryAsync(shop, husband, "credit", 300m);
        await KhataTests.AddEntryAsync(shop, wife, "credit", 300m);
        var receipt = await Note(shop, 500m, "UTR777777");

        Assert.Equal(300m, await Balance(shop, wife)); // noted money touches no balance
        (await Allocate(shop, receipt, husband, 300m)).EnsureSuccessStatusCode();
        Assert.Equal(200m, (await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts")).Single().Left);
        Assert.Equal(HttpStatusCode.Conflict, (await Allocate(shop, receipt, wife, 201m)).StatusCode);
        (await Allocate(shop, receipt, wife, 200m)).EnsureSuccessStatusCode();

        Assert.Equal((0m, 100m), (await Balance(shop, husband), await Balance(shop, wife)));
        Assert.Empty(await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts"));
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync("/api/v1/receipts", new { id = Guid.CreateVersion7(), amount = 500m, utr = "UTR777777" })).StatusCode);
    }

    [Fact]
    public async Task A_counter_sale_leaves_the_tray_and_money_on_a_khata_cannot_be_dismissed()
    {
        var (shop, _) = await api.SignUpAsync("Counter Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 100m);
        var sale = await Note(shop, 80m);
        var part = await Note(shop, 90m);
        (await Allocate(shop, part, customer, 50m)).EnsureSuccessStatusCode();

        (await shop.PostJsonAsync($"/api/v1/receipts/{sale}/dismiss", new { @as = "counterSale" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/receipts/{part}/dismiss", new { @as = "notKhata" })).StatusCode);
        Assert.Equal(part, (await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts")).Single().Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Allocate(shop, sale, customer, 10m)).StatusCode);
    }

    [Fact]
    public void A_match_is_only_suggested_when_exactly_one_customer_fits()
    {
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        (Guid, string, decimal)[] people = [(a, "Ramesh Kumar", 500m), (b, "Suresh", 500m), (c, "Priya Agarwal", 240m)];

        Assert.Equal((c, "amount"), Pick(Receipts.Suggest(240m, null, people)));
        Assert.Null(Receipts.Suggest(500m, null, people));                       // two owe exactly this
        Assert.Null(Receipts.Suggest(500m, "RAMESH KUMAR", people));             // the amount's doubt isn't settled by a name
        Assert.Equal((a, "name"), Pick(Receipts.Suggest(123m, "ramesh kumar upi", people)));
        Assert.Null(Receipts.Suggest(123m, "unknown person", people));
    }

    private static (Guid, string) Pick(ReceiptSuggestion? s) => (s!.CustomerId, s.Why);
}
