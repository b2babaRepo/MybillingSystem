using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Bills held on the server: any till sees them, and only one till can pick each one up.</summary>
[Collection(ApiCollection.Name)]
public sealed class HeldBillTests(ApiFactory api)
{
    private async Task<HttpClient> HelperOfAsync(HttpClient owner)
    {
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return helper;
    }

    private static object Hold(string label) => new
    {
        counter = "Counter 1", label, total = 230m, lines = 2,
        payload = new { cart = new[] { new { name = "Atta", quantity = 2m, unitPrice = 90m }, new { name = "Dal", quantity = 1m, unitPrice = 50m } }, customerId = "" },
    };

    [Fact]
    public async Task A_bill_held_on_one_till_is_picked_up_once_on_another()
    {
        var (owner, _) = await api.SignUpAsync("Held Mart");
        var till2 = await HelperOfAsync(owner);
        var id = Guid.CreateVersion7();

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/held-bills/{id}", Hold("Asha"), ApiFactory.Json)).StatusCode);
        // Sent again (a retry): still one.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/held-bills/{id}", Hold("Asha"), ApiFactory.Json)).StatusCode);

        var seen = Assert.Single(await till2.GetJsonAsync<List<HeldBillDto>>("/api/v1/held-bills"));
        Assert.Equal(("Counter 1", "Asha", 230m, 2), (seen.Counter, seen.Label, seen.Total, seen.Lines));
        Assert.Equal(2, seen.Payload.GetProperty("cart").GetArrayLength());

        // Two tills pick it up at the same moment: one gets it, the other is told it's gone.
        var takes = await Task.WhenAll(till2.PostAsync($"/api/v1/held-bills/{id}/take", null), owner.PostAsync($"/api/v1/held-bills/{id}/take", null));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.NotFound], takes.Select(t => t.StatusCode).Order());
        var taken = await takes.Single(t => t.IsSuccessStatusCode).ReadAsync<HeldBillDto>();
        Assert.Equal("Atta", taken.Payload.GetProperty("cart")[0].GetProperty("name").GetString());
        Assert.Empty(await owner.GetJsonAsync<List<HeldBillDto>>("/api/v1/held-bills"));
    }

    [Fact]
    public async Task Another_shop_neither_sees_nor_takes_a_held_bill_and_a_bad_one_is_refused()
    {
        var (owner, _) = await api.SignUpAsync("Held Mart 2");
        var (other, _) = await api.SignUpAsync("Held Mart 3");
        var id = Guid.CreateVersion7();
        (await owner.PutAsJsonAsync($"/api/v1/held-bills/{id}", Hold("Ravi"), ApiFactory.Json)).EnsureSuccessStatusCode();

        Assert.Empty(await other.GetJsonAsync<List<HeldBillDto>>("/api/v1/held-bills"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/held-bills/{id}/take", null)).StatusCode);
        Assert.Single(await owner.GetJsonAsync<List<HeldBillDto>>("/api/v1/held-bills"));

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/v1/held-bills/{Guid.CreateVersion7()}", new { total = 1m, lines = 1, payload = "text" }, ApiFactory.Json)).StatusCode);
        var huge = new { total = 1m, lines = 1, payload = new { cart = new string('x', HeldBillEndpoints.MaxPayload) } };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/v1/held-bills/{Guid.CreateVersion7()}", huge, ApiFactory.Json)).StatusCode);
    }
}
