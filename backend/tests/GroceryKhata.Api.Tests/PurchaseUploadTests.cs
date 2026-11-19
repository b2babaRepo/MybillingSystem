using System.Net;
using System.Text.Json;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PurchaseUploadTests(ApiFactory api)
{
    // A 1×1 PNG: a real picture, so the image check passes.
    private const string Photo = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    [Fact]
    public async Task A_bill_photo_from_the_computer_or_a_phone_link_waits_until_it_becomes_a_purchase()
    {
        var (shop, _) = await api.SignUpAsync("Upload Store");
        (await shop.PostJsonAsync("/api/v1/suppliers/uploads", new { imageDataUrl = Photo })).EnsureSuccessStatusCode();

        var link = await (await shop.PostJsonAsync("/api/v1/suppliers/uploads/link", new { })).ReadAsync<PurchaseUploadLinkDto>();
        var phone = api.CreateClient();
        (await phone.PostJsonAsync($"/api/v1/upload/{link.Slug}?e={link.Expires}&t={link.Token}", new { imageDataUrl = Photo })).EnsureSuccessStatusCode();
        // A changed link, or a made-up one, is refused.
        Assert.Equal(HttpStatusCode.NotFound, (await phone.PostJsonAsync($"/api/v1/upload/{link.Slug}?e={link.Expires + 60}&t={link.Token}", new { imageDataUrl = Photo })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await phone.PostJsonAsync($"/api/v1/upload/{link.Slug}?e={link.Expires}&t=00000000000000000000000000000000", new { imageDataUrl = Photo })).StatusCode);

        var list = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/suppliers/uploads")).RootElement.EnumerateArray().ToList();
        Assert.Equal(["phone", "computer"], list.Select(u => u.GetProperty("source").GetString()));
        var first = list[0].GetProperty("id").GetGuid();
        var image = JsonDocument.Parse(await shop.GetStringAsync($"/api/v1/suppliers/uploads/{first}")).RootElement.GetProperty("imageDataUrl").GetString();
        Assert.Equal(Photo, image);

        (await shop.PostJsonAsync($"/api/v1/suppliers/uploads/{first}/used", new { purchaseId = (Guid?)null })).EnsureSuccessStatusCode();
        var after = JsonDocument.Parse(await shop.GetStringAsync("/api/v1/suppliers/uploads")).RootElement.EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == first);
        Assert.NotEqual(JsonValueKind.Null, after.GetProperty("usedAt").ValueKind);

        // A PDF bill is taken too; any other kind of file isn't.
        var pdf = "data:application/pdf;base64," + Convert.ToBase64String("%PDF-1.4\n%%EOF"u8.ToArray());
        (await shop.PostJsonAsync("/api/v1/suppliers/uploads", new { imageDataUrl = pdf })).EnsureSuccessStatusCode();
        var text = "data:text/plain;base64," + Convert.ToBase64String("hello"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/suppliers/uploads", new { imageDataUrl = text })).StatusCode);

        // Another shop sees none of them.
        var (other, _) = await api.SignUpAsync("Other Upload Store");
        Assert.Empty(JsonDocument.Parse(await other.GetStringAsync("/api/v1/suppliers/uploads")).RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/suppliers/uploads/{first}")).StatusCode);
    }
}
