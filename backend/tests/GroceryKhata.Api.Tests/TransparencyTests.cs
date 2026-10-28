using System.Net;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Ledger;
using Npgsql;

namespace GroceryKhata.Api.Tests;

/// <summary>A payment can't quietly disappear: late removals need the owner and a reason, and show who did it.</summary>
[Collection(ApiCollection.Name)]
public sealed class TransparencyTests(ApiFactory api)
{
    [Fact]
    public async Task After_fifteen_minutes_removing_a_payment_needs_a_reason_and_shows_who_removed_it()
    {
        var (shop, auth) = await api.SignUpAsync("Sharma General Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 500m);
        var paid = await KhataTests.AddEntryAsync(shop, customer, "payment", 200m);

        // The payment was recorded an hour ago (written as the database owner: the app can't backdate rows).
        await using (var admin = new NpgsqlConnection(api.AdminConnectionString + ";Database=grocery_khata"))
        {
            await admin.OpenAsync();
            await using var backdate = new NpgsqlCommand("UPDATE ledger_entries SET created_at = now() - interval '1 hour' WHERE id = @id", admin);
            backdate.Parameters.AddWithValue("id", paid.Entry.Id);
            await backdate.ExecuteNonQueryAsync();
        }

        var noReason = await shop.PostJsonAsync($"/api/v1/ledger-entries/{paid.Entry.Id}/reverse", new { id = Guid.CreateVersion7() });
        var withReason = await shop.PostJsonAsync($"/api/v1/ledger-entries/{paid.Entry.Id}/reverse", new { id = Guid.CreateVersion7(), note = "Cheque bounced" });

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withReason.StatusCode);
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        var removal = statement.Entries.Single(e => e.Kind == LedgerEntryKind.Reversal);
        Assert.Equal(("Cheque bounced", auth.User.FullName), (removal.Note, removal.RecordedBy));
        Assert.Equal(500m, statement.ClosingBalance);
    }

    [Fact]
    public async Task A_UPI_payment_with_a_bank_reference_can_never_be_removed_or_entered_twice()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        await KhataTests.AddEntryAsync(shop, customer, "credit", 500m);
        var upi = new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 300m, paymentMode = "upi", upiRef = "4321abcd9876" };

        var paid = await (await shop.PostJsonAsync("/api/v1/khata-entries", upi)).ReadAsync<KhataEntryResponse>();
        var twice = await shop.PostJsonAsync("/api/v1/khata-entries", upi with { id = Guid.CreateVersion7() });
        var cashWithRef = await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 10m, paymentMode = "cash", upiRef = "1111222233" });
        var removed = await shop.PostJsonAsync($"/api/v1/ledger-entries/{paid.Entry.Id}/reverse", new { id = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cashWithRef.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, removed.StatusCode);
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal("4321ABCD9876", statement.Entries.Single(e => e.Kind == LedgerEntryKind.Payment).UpiRef);
        Assert.Equal(200m, statement.ClosingBalance);
    }

    [Fact]
    public async Task A_customer_disputes_an_entry_from_the_link_the_shop_sees_it_and_the_owners_answer_shows_on_the_khata()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        var credit = await KhataTests.AddEntryAsync(shop, customer, "credit", 750m);
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var anyone = api.CreateClient();
        var url = $"/api/v1/khata/{link.ShopSlug}/{customer}/disputes?t={link.Token}";

        var raised = await anyone.PostJsonAsync(url, new { entryId = credit.Entry.Id, message = "I took only 500 worth" });
        var again = await anyone.PostJsonAsync(url, new { entryId = credit.Entry.Id, message = "Same thing" });
        var badToken = await anyone.PostJsonAsync($"/api/v1/khata/{link.ShopSlug}/{customer}/disputes?t=nope", new { entryId = credit.Entry.Id, message = "Hello" });

        Assert.Equal(HttpStatusCode.Created, raised.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode); // still the one open dispute
        Assert.Equal(HttpStatusCode.NotFound, badToken.StatusCode);
        var open = Assert.Single(await shop.GetJsonAsync<List<DisputeDto>>("/api/v1/disputes"));
        Assert.Equal(("Anita", 750m, "I took only 500 worth"), (open.CustomerName, open.EntryAmount, open.Message));

        var resolved = await shop.PostJsonAsync($"/api/v1/disputes/{open.Id}/resolve", new { resolution = "Checked the bill: 2 kg ghee, ₹750 is right" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Empty(await shop.GetJsonAsync<List<DisputeDto>>("/api/v1/disputes"));
        var view = await anyone.GetJsonAsync<KhataViewDto>($"/api/v1/khata/{link.ShopSlug}/{customer}?t={link.Token}");
        var dispute = view.Statement.Entries.Single().Dispute!;
        Assert.Equal(("I took only 500 worth", "Checked the bill: 2 kg ghee, ₹750 is right"), (dispute.Message, dispute.Resolution));
    }

    [Fact]
    public async Task A_customer_ticks_bill_items_and_the_owner_settles_each_one_refunds_go_off_the_khata_and_back_on_the_shelf()
    {
        var (shop, _) = await api.SignUpAsync("Sharma General Store");
        await shop.PostJsonAsync("/api/v1/products/import", new
        {
            rows = new[]
            {
                new { name = "Mustard oil", unit = "litre", sellingPrice = 185m, openingStock = 10m },
                new { name = "Sugar", unit = "kg", sellingPrice = 48m, openingStock = 20m },
                new { name = "Tea", unit = "packet", sellingPrice = 140m, openingStock = 5m },
            },
        });
        var products = await shop.GetJsonAsync<List<ProductDto>>("/api/v1/products");
        Guid P(string name) => products.Single(p => p.Name == name).Id;
        var customer = await KhataTests.CreateCustomerAsync(shop, "Anita");
        await (await shop.PostJsonAsync("/api/v1/bills", new
        {
            id = Guid.CreateVersion7(), customerId = customer, paidAmount = 0m,
            lines = new[]
            {
                new { productId = P("Mustard oil"), quantity = 1m, unitPrice = 185m },
                new { productId = P("Sugar"), quantity = 3m, unitPrice = 48m },
                new { productId = P("Tea"), quantity = 1m, unitPrice = 140m },
            },
        })).ReadAsync<BillDto>();
        var statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        var udhaar = statement.Entries.Single();
        Guid Line(string name) => udhaar.Bill!.Items.Single(l => l.Name == name).Id;
        var link = await (await shop.PostJsonAsync($"/api/v1/customers/{customer}/khata-link", new { })).ReadAsync<KhataLinkDto>();
        var anyone = api.CreateClient();

        // The oil was left on the counter, and only 2 kg of the 3 kg sugar came home.
        var raised = await anyone.PostJsonAsync($"/api/v1/khata/{link.ShopSlug}/{customer}/disputes?t={link.Token}", new
        {
            entryId = udhaar.Id,
            message = "",
            items = new object[]
            {
                new { billLineId = Line("Mustard oil"), problem = "not_received" },
                new { billLineId = Line("Sugar"), problem = "short", receivedQuantity = 2m },
            },
        });
        Assert.Equal(HttpStatusCode.Created, raised.StatusCode);

        var open = Assert.Single(await shop.GetJsonAsync<List<DisputeDto>>("/api/v1/disputes"));
        Assert.Equal([185m, 48m], open.Items.Select(i => i.DisputedAmount));
        var oil = open.Items.Single(i => i.Name == "Mustard oil");
        var sugar = open.Items.Single(i => i.Name == "Sugar");

        // Disagreeing needs a reason; the oil turns up on the counter and is handed over; the sugar comes off the khata.
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/disputes/{open.Id}/items/{sugar.Id}/resolve", new { outcome = "disagreed" })).StatusCode);
        await shop.PostJsonAsync($"/api/v1/disputes/{open.Id}/items/{oil.Id}/resolve", new { outcome = "handed_over" });
        var done = await (await shop.PostJsonAsync($"/api/v1/disputes/{open.Id}/items/{sugar.Id}/resolve", new { outcome = "refunded" })).ReadAsync<DisputeView>();

        Assert.NotNull(done.ResolvedAt); // last item settled: the dispute closes
        Assert.Empty(await shop.GetJsonAsync<List<DisputeDto>>("/api/v1/disputes"));
        statement = await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{customer}/statement");
        Assert.Equal(469m - 48m, statement.ClosingBalance);
        Assert.Equal(-48m, statement.Entries.Single(e => e.Kind == LedgerEntryKind.Return).Amount);
        Assert.Equal(20m - 3m + 1m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{P("Sugar")}")).Stock);
        Assert.Equal(10m - 1m, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{P("Mustard oil")}")).Stock);
    }
}
