using System.Net;
using GroceryKhata.Api.Catalogue;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryKhata.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BarcodeLookupTests(ApiFactory api)
{
    private sealed class FakeLookup : IBarcodePhotos
    {
        public Task<BarcodePhoto?> FindAsync(string barcode, CancellationToken ct) => Task.FromResult<BarcodePhoto?>(null);

        public Task<BarcodeDetails?> DetailsAsync(string barcode, CancellationToken ct) =>
            Task.FromResult(barcode == "8904043901015" ? PhotoSources.Details("Salt", "Tata,Tata Consumer", "1 kg") : null);
    }

    [Theory]
    [InlineData("Salt", "Tata,Tata Consumer", "1 kg", "Tata Salt 1 kg")]
    [InlineData("Maggi 2-Minute Noodles 70 g", "Maggi", "70 g", "Maggi 2-Minute Noodles 70 g")] // brand and pack already in the name
    [InlineData("Parle-G", null, null, "Parle-G")]
    [InlineData("Maggi Noodles Masala 70Grm Pc", "Maggi", "1 unit", "Maggi Noodles Masala 70Grm Pc")] // already has a size
    public void A_new_items_name_is_brand_product_and_pack_once_each(string product, string? brands, string? pack, string expected) =>
        Assert.Equal(expected, PhotoSources.Details(product, brands, pack)!.Name);

    [Fact]
    public void No_product_name_means_nothing_to_fill() => Assert.Null(PhotoSources.Details(null, "Tata", "1 kg"));

    [Fact]
    public async Task A_scanned_barcode_brings_the_products_name_and_an_unknown_one_brings_nothing()
    {
        using var app = api.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IBarcodePhotos, FakeLookup>()));
        var (_, auth) = await api.SignUpAsync("Barcode Lookup Store");
        var shop = app.CreateClient();
        shop.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var found = await shop.GetJsonAsync<BarcodeDetails>("/api/v1/barcodes/8904043901015");
        Assert.Equal(("Tata Salt 1 kg", "Tata", "1 kg"), (found.Name, found.Brand, found.Pack));
        Assert.Equal(HttpStatusCode.NoContent, (await shop.GetAsync("/api/v1/barcodes/8900000000000")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.GetAsync("/api/v1/barcodes/12ab")).StatusCode);
    }
}
