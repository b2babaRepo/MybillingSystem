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

    // As the app sends it: midnight in India, not UTC. The database only takes UTC, so this must be converted.
    private static readonly DateTimeOffset Day = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-1).Date, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(5.5));

    [Fact]
    public async Task A_statement_imports_once_moves_no_balance_and_two_equal_payments_both_count()
    {
        var (shop, _) = await api.SignUpAsync("Statement Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 900m);
        var body = new
        {
            rows = new object[]
            {
                new { amount = 900m, utr = "432112345678", payerName = "RAMESH", receivedAt = Day, narration = "UPI/CR/432112345678/RAMESH" },
                new { amount = 40m, utr = (string?)null, payerName = (string?)null, receivedAt = Day, narration = "NEFT 1" },
                new { amount = 40m, utr = (string?)null, payerName = (string?)null, receivedAt = Day, narration = "NEFT 1" },
                new { amount = -5m, utr = (string?)null, payerName = (string?)null, receivedAt = Day, narration = "bad" },
            },
        };

        var first = await (await shop.PostJsonAsync("/api/v1/receipts/import", body)).ReadAsync<StatementImportResult>();
        Assert.Equal((3, 0, 1), (first.Added, first.Known, first.Problems.Count));
        var again = await (await shop.PostJsonAsync("/api/v1/receipts/import", body)).ReadAsync<StatementImportResult>();
        Assert.Equal((0, 3), (again.Added, again.Known));

        Assert.Equal(900m, await Balance(shop, customer));
        var tray = await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts");
        Assert.Equal(3, tray.Count);
        Assert.Equal((customer, "amount"), Pick(tray.Single(r => r.Utr is not null).Suggestion));
    }

    [Fact]
    public async Task A_typed_utr_payment_is_not_imported_again_and_a_typed_payment_without_one_is_linked_not_doubled()
    {
        var (shop, _) = await api.SignUpAsync("No Double Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 1000m);
        (await Pay(shop, customer, 300m, "432112340000")).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 200m, paymentMode = "upi" })).EnsureSuccessStatusCode();

        var result = await (await shop.PostJsonAsync("/api/v1/receipts/import", new
        {
            rows = new object[]
            {
                new { amount = 300m, utr = "432112340000", payerName = "RAMESH", receivedAt = Day, narration = "a" },
                new { amount = 200m, utr = "432112340001", payerName = "RAMESH", receivedAt = Day, narration = "b" },
            },
        })).ReadAsync<StatementImportResult>();
        Assert.Equal((1, 1), (result.Added, result.Known));

        var receipt = (await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts")).Single();
        Assert.Equal("entered", receipt.Already!.Kind);
        Assert.Null(receipt.Suggestion);

        (await shop.PostJsonAsync($"/api/v1/receipts/{receipt.Id}/link", new { entryId = receipt.Already.EntryId })).EnsureSuccessStatusCode();
        Assert.Equal(500m, await Balance(shop, customer)); // 1000 less 300 less 200: linked, not paid twice
        Assert.Empty(await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts"));
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/receipts/{receipt.Id}/link", new { entryId = receipt.Already.EntryId })).StatusCode);
    }

    private static async Task<(string Slug, string Token)> Link(HttpClient shop, Guid customer)
    {
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        return (link.ShopSlug, link.Token);
    }

    [Fact]
    public async Task A_customers_claim_moves_nothing_until_the_shop_matches_it_and_the_bank_can_confirm_it()
    {
        var (shop, _) = await api.SignUpAsync("Claim Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 700m);
        var (slug, token) = await Link(shop, customer);
        var guest = api.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await guest.PostJsonAsync($"/api/v1/khata/{slug}/{customer}/paid?t=wrong", new { id = Guid.CreateVersion7(), amount = 700m })).StatusCode);
        (await guest.PostJsonAsync($"/api/v1/khata/{slug}/{customer}/paid?t={token}", new { id = Guid.CreateVersion7(), amount = 300m })).EnsureSuccessStatusCode();

        Assert.Equal(700m, await Balance(shop, customer));
        var claim = (await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts")).Single();
        Assert.Equal((ReceiptSource.Claim, customer, "claim"), (claim.Source, claim.Suggestion!.CustomerId, claim.Suggestion.Why));
        Assert.True((await shop.GetJsonAsync<DuesReport>("/api/v1/reports/dues")).Customers.Single().MoneyWaiting);

        // The bank's row proves the claim: one receipt, not two, so it can't be matched twice.
        var result = await (await shop.PostJsonAsync("/api/v1/receipts/import", new
        {
            rows = new object[] { new { amount = 300m, utr = "432112349999", payerName = "RAMESH", receivedAt = Day, narration = "x" } },
        })).ReadAsync<StatementImportResult>();
        Assert.Equal((0, 1), (result.Added, result.Confirmed));
        var proved = (await shop.GetJsonAsync<List<ReceiptDto>>("/api/v1/receipts")).Single();
        Assert.Equal((claim.Id, ReceiptSource.Statement, "432112349999"), (proved.Id, proved.Source, proved.Utr));

        (await Allocate(shop, proved.Id, customer, 300m)).EnsureSuccessStatusCode();
        Assert.Equal(400m, await Balance(shop, customer));
        Assert.Empty(await shop.GetJsonAsync<List<BalanceMismatch>>("/api/v1/ledger/reconcile"));
    }

    [Fact]
    public async Task A_customer_cannot_flood_the_tray_with_claims()
    {
        var (shop, _) = await api.SignUpAsync("Flood Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Ramesh");
        var (slug, token) = await Link(shop, customer);
        var guest = api.CreateClient();
        Task<HttpResponseMessage> Claim() => guest.PostJsonAsync($"/api/v1/khata/{slug}/{customer}/paid?t={token}", new { id = Guid.CreateVersion7(), amount = 10m });

        for (var i = 0; i < Receipts.MaxOpenClaims; i++)
        {
            (await Claim()).EnsureSuccessStatusCode();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await Claim()).StatusCode);
    }

    [Fact]
    public void Money_already_in_the_books_is_recognised_only_when_it_is_clear()
    {
        var now = DateTimeOffset.UtcNow;
        var one = Guid.NewGuid();
        var entries = new List<(Guid, string, decimal, DateTimeOffset)> { (one, "Ramesh", 200m, now) };

        Assert.Equal(("entered", one), Kind(Receipts.Already(200m, now.AddHours(-20), entries, [], new HashSet<Guid>())));
        Assert.Null(Receipts.Already(200m, now.AddDays(-5), entries, [], new HashSet<Guid>())); // too far apart
        Assert.Null(Receipts.Already(200m, now, [.. entries, (Guid.NewGuid(), "Suresh", 200m, now)], [], new HashSet<Guid>())); // two fit

        var claimed = new HashSet<Guid>();
        Assert.NotNull(Receipts.Already(200m, now, entries, [], claimed));
        Assert.Null(Receipts.Already(200m, now, entries, [], claimed)); // one entry can't explain two receipts

        Assert.Equal("sale", Receipts.Already(150m, now, [], [(150m, now)], new HashSet<Guid>())!.Kind);
    }

    private static (string, Guid?) Kind(ReceiptAlready? a) => (a!.Kind, a.EntryId);

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
