using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

/// <summary>The shop's CA signs in with their own phone, sees the owner's reports, and can change nothing.</summary>
[Collection(ApiCollection.Name)]
public sealed class AccountantAccessTests(ApiFactory api)
{
    [Fact]
    public async Task The_accountant_reads_the_owners_reports_and_every_change_is_refused()
    {
        var (owner, _) = await api.SignUpAsync("CA Access Store");
        var customer = Guid.CreateVersion7();
        (await owner.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Ramesh" })).EnsureSuccessStatusCode();

        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Sharma CA", phone, role = "accountant" })).EnsureSuccessStatusCode();

        // The CA signs in with an SMS code, like any owner.
        var client = api.CreateClient();
        (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone })).EnsureSuccessStatusCode();
        var code = api.Services.GetRequiredService<DevSmsSender>().LastCodeFor(PhoneNumber.Normalize(phone)!);
        var auth = await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code })).ReadAsync<AuthResponse>();
        if (auth.ShopId is null)
        {
            auth = await (await Authed(auth.AccessToken).PostJsonAsync("/api/v1/auth/select-shop", new { membershipId = auth.Memberships.Single().MembershipId })).ReadAsync<AuthResponse>();
        }

        Assert.Equal(ShopRole.Accountant, auth.Role);
        var ca = Authed(auth.AccessToken);

        // Reads: owner-only reports too.
        Assert.Equal(HttpStatusCode.OK, (await ca.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ca.GetAsync("/api/v1/bills/list")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ca.GetAsync("/api/v1/suppliers")).StatusCode);

        // Writes: refused, the owner's own endpoints and the counter's alike.
        var bill = await ca.PostJsonAsync("/api/v1/bills", new { id = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { name = "Atta", quantity = 1m, unitPrice = 50m } }, paidAmount = 0m });
        Assert.Equal(HttpStatusCode.Forbidden, bill.StatusCode);
        Assert.Contains("read-only", await bill.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await ca.PostJsonAsync("/api/v1/customers", new { id = Guid.CreateVersion7(), name = "New" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ca.PutAsJsonAsync("/api/v1/shop", new { name = "Taken over" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ca.PostJsonAsync("/api/v1/shop/staff", new { name = "Another", phone = ApiFactory.NewPhone(), role = "owner" })).StatusCode);

        // Nothing changed.
        Assert.Single(System.Text.Json.JsonDocument.Parse(await owner.GetStringAsync("/api/v1/customers")).RootElement.EnumerateArray());

        // At most two CAs.
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Second CA", phone = ApiFactory.NewPhone(), role = "accountant" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Third CA", phone = ApiFactory.NewPhone(), role = "accountant" })).StatusCode);
    }

    private HttpClient Authed(string token)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
