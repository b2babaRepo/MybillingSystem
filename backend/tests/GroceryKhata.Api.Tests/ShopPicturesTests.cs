using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;

namespace GroceryKhata.Api.Tests;

/// <summary>The shop's own pictures for its menu's departments and categories.</summary>
[Collection(ApiCollection.Name)]
public sealed class ShopPicturesTests(ApiFactory api)
{
    // A 1×1 PNG.
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private async Task<(HttpClient Owner, HttpClient Helper, Guid ShopId)> ShopAsync()
    {
        var owner = api.CreateClient();
        var auth = await (await owner.PostJsonAsync("/api/v1/auth/sign-up",
            new { shopName = "Arora Stores", fullName = "Vikas Arora", phone = ApiFactory.NewPhone(), password = "secret123", language = "hi", type = "kirana" })).ReadAsync<AuthResponse>();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var phone = ApiFactory.NewPhone();
        (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone, pin = "4826" })).EnsureSuccessStatusCode();
        var helperAuth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helperAuth.AccessToken);
        return (owner, helper, auth.ShopId!.Value);
    }

    [Fact]
    public async Task The_owner_sets_a_department_picture_everyone_can_see_and_only_this_shop_lists_it()
    {
        var (owner, helper, shopId) = await ShopAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/v1/shop/pictures/Not A Key", new { dataUrl = Png }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/v1/shop/pictures/staples", new { dataUrl = "data:text/plain;base64,aGk=" }, ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PutAsJsonAsync("/api/v1/shop/pictures/staples", new { dataUrl = Png }, ApiFactory.Json)).StatusCode);

        (await owner.PutAsJsonAsync("/api/v1/shop/pictures/staples", new { dataUrl = Png }, ApiFactory.Json)).EnsureSuccessStatusCode();
        Assert.Equal("staples", Assert.Single(await helper.GetJsonAsync<List<ShopPictureDto>>("/api/v1/shop/pictures")).Key);

        var picture = await api.CreateClient().GetAsync($"/api/v1/pictures/{shopId}/staples");
        Assert.Equal(("image/png", "public, max-age=31536000, immutable"), (picture.Content.Headers.ContentType?.MediaType, picture.Headers.CacheControl?.ToString()));

        var (other, _, _) = await ShopAsync();
        Assert.Empty(await other.GetJsonAsync<List<ShopPictureDto>>("/api/v1/shop/pictures"));

        (await owner.DeleteAsync("/api/v1/shop/pictures/staples")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/api/v1/pictures/{shopId}/staples")).StatusCode);
    }
}
