using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Catalogue;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GroceryKhata.Api.Tests;

/// <summary>Item photos not taken in the app: small copies of older photos, Open Food Facts by barcode, and customers' reports.</summary>
[Collection(ApiCollection.Name)]
public sealed class PhotoSourceTests(ApiFactory api)
{
    private static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(200, 60, 40));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class FakeBarcodes : IBarcodePhotos
    {
        public Task<BarcodePhoto?> FindAsync(string barcode, CancellationToken ct) =>
            Task.FromResult(barcode == "8901725181222" ? new BarcodePhoto("Aashirvaad Atta 5 kg", Png(900, 1200)) : null);
    }

    [Fact]
    public void Server_copies_are_square_webp_on_white_and_never_enlarged()
    {
        var (type, data) = PhotoResize.Square(Png(900, 600), PhotoResize.Thumb);
        using var thumb = Image.Load(data);
        Assert.Equal(("image/webp", 320, 320), (type, thumb.Width, thumb.Height));
        using var small = Image.Load(PhotoResize.Square(Png(100, 80), PhotoResize.Full).Data);
        Assert.Equal(100, small.Width);
    }

    [Fact]
    public async Task An_older_photo_gets_its_small_copy_made_once_when_the_grid_asks()
    {
        var (shop, auth) = await api.SignUpAsync("Old Photos");
        var item = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Sugar 1 kg", unit = "packet", sellingPrice = 46m })).ReadAsync<ProductDto>();
        var png = Png(600, 600);
        (await shop.PutAsJsonAsync($"/api/v1/products/{item.Id}/image", new { dataUrl = "data:image/png;base64," + Convert.ToBase64String(png) }, ApiFactory.Json)).EnsureSuccessStatusCode();

        var guest = api.CreateClient();
        var thumb = await guest.GetAsync($"/api/v1/images/{auth.ShopId}/{item.Id}?v=1&size=thumb");
        Assert.Equal("image/webp", thumb.Content.Headers.ContentType!.MediaType);
        var bytes = await thumb.Content.ReadAsByteArrayAsync();
        using (var image = Image.Load(bytes)) Assert.Equal(320, image.Width);
        Assert.Equal(bytes, await guest.GetByteArrayAsync($"/api/v1/images/{auth.ShopId}/{item.Id}?v=2&size=thumb"));
        Assert.Equal(png.Length, (await guest.GetByteArrayAsync($"/api/v1/images/{auth.ShopId}/{item.Id}?v=1")).Length);
    }

    [Fact]
    public async Task A_barcode_brings_the_open_food_facts_photo_with_its_credit_and_customers_can_report_it()
    {
        using var app = api.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IBarcodePhotos, FakeBarcodes>()));
        var (_, auth) = await api.SignUpAsync("Barcode Store");
        var shop = app.CreateClient();
        shop.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var item = await (await shop.PostJsonAsync("/api/v1/products", new { id = Guid.CreateVersion7(), name = "Atta 5 kg", unit = "packet", sellingPrice = 245m, openingStock = 3m })).ReadAsync<ProductDto>();

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/products/{item.Id}/image/barcode", new { barcode = "12ab" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync($"/api/v1/products/{item.Id}/image/barcode", new { barcode = "8900000000000" })).StatusCode);
        (await shop.PostJsonAsync($"/api/v1/products/{item.Id}/image/barcode", new { barcode = "8901725181222" })).EnsureSuccessStatusCode();
        item = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{item.Id}");
        Assert.Equal(("8901725181222", PhotoSources.OpenFoodFactsCredit), (item.Barcode, item.ImageCredit));
        var store = await app.CreateClient().GetJsonAsync<GroceryKhata.Api.Planner.StoreDto>($"/api/v1/store/{auth.Memberships.Single().ShopSlug}");
        Assert.Equal(PhotoSources.OpenFoodFactsCredit, store.Items.Single().ImageCredit);

        // A customer says it's the wrong pack; the shop looks and keeps it.
        var guest = app.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await guest.PostJsonAsync($"/api/v1/images/{auth.ShopId}/{item.Id}/report", new { reason = "This is the 10 kg bag" })).StatusCode);
        item = await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{item.Id}");
        Assert.Equal((1, "This is the 10 kg bag"), (item.PhotoReports, item.PhotoReportNote));
        (await shop.DeleteAsync($"/api/v1/products/{item.Id}/image/reports")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await shop.GetJsonAsync<ProductDto>($"/api/v1/products/{item.Id}")).PhotoReports);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.PostJsonAsync($"/api/v1/images/{auth.ShopId}/{Guid.NewGuid()}/report", new { })).StatusCode);
    }
}
