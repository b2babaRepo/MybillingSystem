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
}
