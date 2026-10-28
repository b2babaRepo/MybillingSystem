using System.Net;
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
}
