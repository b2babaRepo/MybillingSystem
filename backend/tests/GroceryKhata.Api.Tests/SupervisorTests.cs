using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>A supervisor's PIN lets a cashier cancel a bill, take goods back, or sell below the helper limit.</summary>
[Collection(ApiCollection.Name)]
public sealed class SupervisorTests(ApiFactory api)
{
    private async Task<(HttpClient Client, Guid MembershipId)> HelperOfAsync(HttpClient owner, string pin)
    {
        var phone = ApiFactory.NewPhone();
        var added = await (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin })).ReadAsync<StaffDto>();
        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = pin })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (helper, added.MembershipId);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, object body, string? pin = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (pin is not null) message.Headers.Add(Supervisor.Header, pin);
        return await client.SendAsync(message);
    }

    [Fact]
    public async Task A_cashier_needs_a_supervisors_pin_to_cancel_take_back_or_sell_below_the_limit()
    {
        var (owner, _) = await api.SignUpAsync("Super Mart");
        var (cashier, _) = await HelperOfAsync(owner, "4826");
        var (super, superId) = await HelperOfAsync(owner, "7391");
        (await owner.PostJsonAsync($"/api/v1/shop/staff/{superId}/supervisor", new { isSupervisor = true })).EnsureSuccessStatusCode();
        var item = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/products", new { id = item, name = "Kettle", unit = "piece", sellingPrice = 1000m, openingStock = 10m })).EnsureSuccessStatusCode();
        object Bill(decimal price) => new { id = Guid.CreateVersion7(), lines = new[] { new { productId = item, quantity = 1m, unitPrice = price } }, paidAmount = price, paymentMode = "cash" };

        // 30% off is past a helper's 10%: refused alone or with a cashier's own PIN; a supervisor's PIN approves it.
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(cashier, "/api/v1/bills", Bill(700m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(cashier, "/api/v1/bills", Bill(700m), "4826")).StatusCode);
        var low = await (await SendAsync(cashier, "/api/v1/bills", Bill(700m), "7391")).ReadAsync<BillDto>();

        // Cancelling: refused without a supervisor, then approved.
        var cancel = new { reason = "Wrong item" };
        var refused = await SendAsync(cashier, $"/api/v1/bills/{low.Id}/cancel", cancel);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains(Supervisor.ProblemType, await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(cashier, $"/api/v1/bills/{low.Id}/cancel", cancel, "0000")).StatusCode);
        (await SendAsync(cashier, $"/api/v1/bills/{low.Id}/cancel", cancel, "7391")).EnsureSuccessStatusCode();

        // Goods back on another bill: the same.
        var sold = await (await SendAsync(cashier, "/api/v1/bills", Bill(1000m))).ReadAsync<BillDto>();
        var back = new { id = Guid.CreateVersion7(), reason = "Faulty", lines = new[] { new { lineNo = 1, quantity = 1m } }, refundMode = "cash" };
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(cashier, $"/api/v1/bills/{sold.Id}/credit-notes", back)).StatusCode);
        (await SendAsync(cashier, $"/api/v1/bills/{sold.Id}/credit-notes", back, "7391")).EnsureSuccessStatusCode();

        // Not a supervisor any more: their PIN approves nothing.
        (await owner.PostJsonAsync($"/api/v1/shop/staff/{superId}/supervisor", new { isSupervisor = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(cashier, "/api/v1/bills", Bill(700m), "7391")).StatusCode);
        Assert.Contains(await owner.GetJsonAsync<List<StaffDto>>("/api/v1/shop/staff"), s => s.MembershipId == superId && !s.IsSupervisor);
        _ = super;
    }
}
