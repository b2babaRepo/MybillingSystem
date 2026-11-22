using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PartyImportTests(ApiFactory api)
{
    private static object Rows(params object[] rows) => new { rows };

    [Fact]
    public async Task Customers_and_what_they_owe_come_in_once_and_stay_out_of_todays_udhaar()
    {
        var (shop, _) = await api.SignUpAsync("Moving Store");
        var ramesh = ApiFactory.NewPhone();
        var body = Rows(
            new { name = "Ramesh", phone = ramesh, balance = 1200m },
            new { name = "Suresh (advance)", phone = (string?)null, balance = -300m },
            new { name = "Nil Balance", phone = (string?)null, balance = 0m },
            new { name = "", phone = (string?)null, balance = 50m },
            new { name = "Bad Phone", phone = "123", balance = 50m });

        var first = await (await shop.PostJsonAsync("/api/v1/customers/import", body)).ReadAsync<PartyImportResult>();
        Assert.Equal((3, 0, 1200m, 2), (first.Created, first.Skipped, first.TotalOwed, first.Problems.Count));

        var again = await (await shop.PostJsonAsync("/api/v1/customers/import", body)).ReadAsync<PartyImportResult>();
        Assert.Equal((0, 3), (again.Created, again.Skipped));

        var customers = await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers");
        Assert.Equal(1200m, customers.Single(c => c.Name == "Ramesh").Balance);
        Assert.Equal(-300m, customers.Single(c => c.Name.StartsWith("Suresh")).Balance);

        Assert.Empty(await shop.GetJsonAsync<List<BalanceMismatch>>("/api/v1/ledger/reconcile"));
        var today = await shop.GetJsonAsync<System.Text.Json.JsonElement>("/api/v1/dashboard/today");
        Assert.Equal(0m, today.GetProperty("udhaar").GetDecimal());
    }

    [Fact]
    public async Task A_wrong_opening_balance_can_be_reversed_and_still_stays_out_of_todays_udhaar()
    {
        var (shop, _) = await api.SignUpAsync("Fixing Store");
        await (await shop.PostJsonAsync("/api/v1/customers/import", Rows(new { name = "Ramesh", phone = (string?)null, balance = 900m }))).ReadAsync<PartyImportResult>();
        var ramesh = (await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers")).Single();
        var opening = (await shop.GetJsonAsync<StatementDto>($"/api/v1/customers/{ramesh.Id}/statement")).Entries.Single();

        var undo = Guid.CreateVersion7();
        (await shop.PostJsonAsync($"/api/v1/ledger-entries/{opening.Id}/reverse", new { id = undo })).EnsureSuccessStatusCode();

        Assert.Equal(0m, (await shop.GetJsonAsync<List<CustomerDto>>("/api/v1/customers")).Single().Balance);
        var today = await shop.GetJsonAsync<System.Text.Json.JsonElement>("/api/v1/dashboard/today");
        Assert.Equal(0m, today.GetProperty("udhaar").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await shop.PostJsonAsync($"/api/v1/ledger-entries/{undo}/reverse", new { id = Guid.CreateVersion7() })).StatusCode);
    }
}
