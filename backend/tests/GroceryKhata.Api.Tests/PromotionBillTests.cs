using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Promotions planned by the owner and the prices a helper may bill at under them.</summary>
[Collection(ApiCollection.Name)]
public sealed class PromotionBillTests(ApiFactory api)
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

    private static async Task<Guid> ItemAsync(HttpClient shop, string name, decimal price, string? category = null)
    {
        var id = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/products", new { id, name, unit = "piece", sellingPrice = price, openingStock = 100m, category })).EnsureSuccessStatusCode();
        return id;
    }

    private static object Offer(string kind, object extra, decimal value = 0m, string scope = "items", Guid[]? productIds = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var body = new Dictionary<string, object?>
        {
            ["id"] = Guid.CreateVersion7(), ["name"] = $"{kind} offer", ["kind"] = kind, ["value"] = value, ["startsOn"] = today.AddDays(-1).ToString("yyyy-MM-dd"),
            ["endsOn"] = today.AddDays(5).ToString("yyyy-MM-dd"), ["scope"] = scope, ["productIds"] = productIds ?? [],
        };
        foreach (var p in extra.GetType().GetProperties())
        {
            body[p.Name] = p.GetValue(extra);
        }

        return body;
    }

    [Fact]
    public async Task Each_kind_needs_its_own_numbers()
    {
        var (owner, _) = await api.SignUpAsync("Promo Mart");
        var maggi = await ItemAsync(owner, "Maggi", 14m);
        var cola = await ItemAsync(owner, "Cola", 40m);
        async Task<HttpStatusCode> CreateAsync(object body) => (await owner.PostJsonAsync("/api/v1/offers", body)).StatusCode;

        Assert.Equal(HttpStatusCode.OK, await CreateAsync(Offer("buyGet", new { buyQty = 2, getQty = 1 }, productIds: [maggi])));
        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(Offer("buyGet", new { buyQty = 2 }, productIds: [maggi])));
        Assert.Equal(HttpStatusCode.OK, await CreateAsync(Offer("combo", new { comboPrice = 45m }, productIds: [maggi, cola])));
        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(Offer("combo", new { comboPrice = 45m }, productIds: [maggi])));
        Assert.Equal(HttpStatusCode.OK, await CreateAsync(Offer("slab", new { minQty = 5m }, 10m, scope: "all")));
        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(Offer("slab", new { }, 10m, scope: "all")));
        Assert.Equal(HttpStatusCode.OK, await CreateAsync(Offer("billAmount", new { minBill = 1000m }, 50m, scope: "all")));
        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(Offer("billAmount", new { minBill = 40m }, 50m, scope: "all")));
        // Happy hours 5 to 7 pm, weekends only.
        Assert.Equal(HttpStatusCode.OK, await CreateAsync(Offer("percent", new { days = 65, fromMinute = 1020, toMinute = 1140 }, 20m, scope: "all")));
        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(Offer("percent", new { fromMinute = 1020 }, 20m, scope: "all")));

        var listed = await owner.GetJsonAsync<List<SaleOfferDto>>("/api/v1/offers");
        Assert.Contains(listed, o => o.Kind == SaleOfferKind.BuyGet && o.BuyQty == 2 && o.GetQty == 1);
        Assert.Contains(listed, o => o is { Kind: SaleOfferKind.Combo, ComboPrice: 45m } && o.ProductIds.Count == 2);
        Assert.Contains(listed, o => o is { Kind: SaleOfferKind.Percent, Days: 65, FromMinute: 1020, ToMinute: 1140 });
    }

    [Fact]
    public async Task A_helper_bills_at_what_the_promotion_gives_but_no_lower()
    {
        var (owner, _) = await api.SignUpAsync("Promo Mart 2");
        var helper = await HelperOfAsync(owner);
        var maggi = await ItemAsync(owner, "Maggi", 14m);
        var offer = Offer("buyGet", new { buyQty = 2, getQty = 1 }, productIds: [maggi]);
        var offerId = (Guid)((Dictionary<string, object?>)offer)["id"]!;
        (await owner.PostJsonAsync("/api/v1/offers", offer)).EnsureSuccessStatusCode();
        object Bill(int paid, int free) => new
        {
            id = Guid.CreateVersion7(), paymentMode = "cash", paidAmount = paid * 14m,
            lines = new object[] { new { productId = maggi, quantity = (decimal)paid, unitPrice = 14m }, new { productId = maggi, quantity = (decimal)free, unitPrice = 0m, offerId } },
        };

        // 6 packets: 2 free, as the app prices it.
        var bill = await (await helper.PostJsonAsync("/api/v1/bills", Bill(4, 2))).ReadAsync<BillDto>();
        Assert.Equal(56m, bill.Total);
        // 6 packets with 3 free is more than the offer gives, and past the helper's own limit.
        Assert.Equal(HttpStatusCode.BadRequest, (await helper.PostJsonAsync("/api/v1/bills", Bill(3, 3))).StatusCode);
        // The owner decides their own prices.
        (await owner.PostJsonAsync("/api/v1/bills", Bill(3, 3))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_bill_offer_is_kept_on_the_bill_and_counted()
    {
        var (owner, _) = await api.SignUpAsync("Promo Mart 3");
        var helper = await HelperOfAsync(owner);
        var tv = await ItemAsync(owner, "Kettle", 1200m);
        var offer = Offer("billAmount", new { minBill = 1000m }, 100m, scope: "all");
        var offerId = (Guid)((Dictionary<string, object?>)offer)["id"]!;
        (await owner.PostJsonAsync("/api/v1/offers", offer)).EnsureSuccessStatusCode();

        // ₹100 off ₹1200 is past a helper's 10% limit? No (8.3%), so take ₹150 off by hand: refused; the offer's ₹100: fine.
        object Bill(decimal price) => new { id = Guid.CreateVersion7(), paymentMode = "cash", paidAmount = price, billOfferId = offerId, lines = new[] { new { productId = tv, quantity = 1m, unitPrice = price } } };
        (await helper.PostJsonAsync("/api/v1/bills", Bill(1100m))).EnsureSuccessStatusCode();
        var listed = Assert.Single(await owner.GetJsonAsync<List<SaleOfferDto>>("/api/v1/offers"));
        Assert.Equal((1, 1100m, 100m), (listed.Stats!.Bills, listed.Stats.Sales, listed.Stats.Saved));
    }
}
