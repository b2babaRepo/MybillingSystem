using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>Staff: the owner adds a helper with a counter PIN; the helper signs in with phone + PIN.</summary>
[Collection(ApiCollection.Name)]
public sealed class StaffTests(ApiFactory api)
{
    [Fact]
    public async Task A_helper_signs_in_with_the_pin_into_that_shop_and_cannot_open_owner_screens()
    {
        var (owner, ownerAuth) = await api.SignUpAsync("Sharma General Store");
        var phone = ApiFactory.NewPhone();

        var added = await (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).ReadAsync<StaffDto>();
        Assert.Equal((ShopRole.Staff, true, true), (added.Role, added.IsActive, added.HasPin));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).StatusCode);

        var auth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        Assert.Equal((ownerAuth.ShopId, ShopRole.Staff), (auth.ShopId, auth.Role));

        var helper = Client(auth);
        Assert.Equal(HttpStatusCode.OK, (await helper.GetAsync("/api/v1/customers")).StatusCode); // khata and billing: yes
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.GetAsync("/api/v1/shop/staff")).StatusCode); // owner screens: no
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.GetAsync("/api/v1/shop/export")).StatusCode);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    [InlineData("9876")]
    [InlineData("12a4")]
    [InlineData("123")]
    public async Task Easy_or_malformed_pins_are_refused(string pin)
    {
        var (owner, _) = await api.SignUpAsync("Sharma General Store");

        var response = await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone = ApiFactory.NewPhone(), pin });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_pins_lock_the_helper_out_and_a_new_pin_from_the_owner_lets_them_back_in()
    {
        var (owner, _) = await api.SignUpAsync("Sharma General Store");
        var phone = ApiFactory.NewPhone();
        var added = await (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).ReadAsync<StaffDto>();
        var anyone = api.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anyone.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "1357" })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await anyone.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).StatusCode);

        await owner.PostJsonAsync($"/api/v1/shop/staff/{added.MembershipId}/pin", new { pin = "7391" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anyone.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anyone.PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "7391" })).StatusCode);
    }

    [Fact]
    public async Task Turning_a_helpers_access_off_signs_them_out_at_once()
    {
        var (owner, _) = await api.SignUpAsync("Sharma General Store");
        var phone = ApiFactory.NewPhone();
        var added = await (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).ReadAsync<StaffDto>();
        var helper = Client(await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>());
        Assert.Equal(HttpStatusCode.OK, (await helper.GetAsync("/api/v1/customers")).StatusCode);

        await owner.PostJsonAsync($"/api/v1/shop/staff/{added.MembershipId}/active?active=false", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, (await helper.GetAsync("/api/v1/customers")).StatusCode);
        var list = await owner.GetJsonAsync<List<StaffDto>>("/api/v1/shop/staff");
        Assert.False(list.Single(s => s.MembershipId == added.MembershipId).IsActive);
        var ownerRow = list.Single(s => s.Role == ShopRole.Owner);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync($"/api/v1/shop/staff/{ownerRow.MembershipId}/active?active=false", new { })).StatusCode);
    }

    private HttpClient Client(AuthResponse auth)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
