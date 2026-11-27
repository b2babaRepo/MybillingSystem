using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>Registered tills: numbered, named on their bills, and turned off by the owner.</summary>
[Collection(ApiCollection.Name)]
public sealed class TerminalTests(ApiFactory api)
{
    [Fact]
    public async Task Tills_are_numbered_named_on_bills_and_a_turned_off_till_makes_no_new_bills()
    {
        var (owner, _) = await api.SignUpAsync("Till Mart");
        var one = await (await owner.PostJsonAsync("/api/v1/terminals", new { id = Guid.CreateVersion7(), name = "Counter 1" })).ReadAsync<TerminalDto>();
        var two = await (await owner.PostJsonAsync("/api/v1/terminals", new { id = Guid.CreateVersion7(), name = "Counter 2", mallMachineId = "PHX-0042" })).ReadAsync<TerminalDto>();
        Assert.Equal(("T01", "T02", "PHX-0042"), (one.Code, two.Code, two.MallMachineId));
        // Sent again: the same till.
        Assert.Equal("T01", (await (await owner.PostJsonAsync("/api/v1/terminals", new { id = one.Id, name = "Counter 1" })).ReadAsync<TerminalDto>()).Code);

        object Bill(Guid till) => new { id = Guid.CreateVersion7(), terminalId = till, counter = "typed name", lines = new[] { new { name = "Tea", quantity = 1m, unitPrice = 20m } }, paidAmount = 20m, paymentMode = "cash" };
        var bill = await (await owner.PostJsonAsync("/api/v1/bills", Bill(two.Id))).ReadAsync<BillDto>();
        Assert.Equal("Counter 2 (T02)", bill.Counter);
        Assert.NotNull((await owner.GetJsonAsync<List<TerminalDto>>("/api/v1/terminals")).Single(t => t.Id == two.Id).LastBillAt);

        (await owner.PutAsJsonAsync($"/api/v1/terminals/{two.Id}", new { name = "Counter 2", mallMachineId = "PHX-0042", isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostJsonAsync("/api/v1/bills", Bill(two.Id))).StatusCode);
        (await owner.PostJsonAsync("/api/v1/bills", Bill(one.Id))).EnsureSuccessStatusCode();
    }
}
