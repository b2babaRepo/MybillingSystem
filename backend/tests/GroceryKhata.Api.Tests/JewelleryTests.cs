using System.Net;
using System.Net.Http.Headers;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Jewellery billing by weight and purity: the worked bills from the research, rates, tags, old gold, PAN and the cash limit.</summary>
[Collection(ApiCollection.Name)]
public sealed class JewelleryTests(ApiFactory api)
{
    // IBJA 24K, 27 Oct 2026: ₹1,41,082 per 10 g.
    private const decimal Pure = 14_108.20m;

    [Fact]
    public void Every_IS_2112_2025_silver_grade_can_be_stocked()
    {
        foreach (var grade in new[] { 999, 990, 970, 958, 925, 900, 800 })
            Assert.True(JewelleryMath.ValidPurity("silver", grade), $"silver {grade}");
        Assert.False(JewelleryMath.ValidPurity("silver", 916));
        Assert.Equal(76m, JewelleryMath.Derive(95m, 800));
    }

    [Fact]
    public void A_22K_chain_is_worked_out_like_the_shop_does_it()
    {
        var rate = JewelleryMath.Derive(Pure, 916);
        Assert.Equal(12_923.11m, rate);
        var line = JewelleryMath.Line(1, null, null, "Chain", "gold", 916, 20.450m, 0, rate, 8m, "per_gram", 350m, 0, 0, "AB12CD", null);
        Assert.Equal((264_277.60m, 21_142.21m, 7_157.50m), (line.MetalValue, line.WastageValue, line.MakingAmount));
        Assert.Equal((292_577.31m, 8_777.32m), (line.Taxable, line.Gst));
        var (_, _, roundOff, total) = JewelleryMath.Sum([line]);
        Assert.Equal((301_355m, 0.37m), (total, roundOff));
    }

    [Fact]
    public void An_18K_diamond_ring_and_old_gold_taken_against_it()
    {
        var line = JewelleryMath.Line(1, null, null, "Ring", "gold", 750, 4.200m, 0.060m, JewelleryMath.Derive(Pure, 750), 0, "percent", 15m, 18_000m, 0, "XY34ZQ", null);
        Assert.Equal(4.140m, line.Net);
        Assert.Equal((43_805.96m, 6_570.89m, 68_376.85m, 2_051.31m), (line.MetalValue, line.MakingAmount, line.Taxable, line.Gst));
        var old = JewelleryMath.Old("Old bangle", "gold", 5.000m, 87.5m, Pure, 2m);
        Assert.Equal((4.375m, 1_234.47m, 60_488.91m), (old.Fine, old.Deduction, old.Value));
    }

    [Fact]
    public void Silver_payal_pays_3_percent_gst_on_the_whole_value()
    {
        var line = JewelleryMath.Line(1, null, null, "Payal pair", "silver", 925, 120m, 0, 235m, 0, "per_gram", 20m, 0, 0, null, null);
        Assert.Equal((30_600m, 918m), (line.Taxable, line.Gst));
    }

    [Fact]
    public async Task The_shop_page_shows_todays_gold_and_silver_rates()
    {
        var (shop, auth) = await api.SignUpAsync("Rate Jewellers");
        var slug = auth.Memberships.Single().ShopSlug;
        var guest = api.CreateClient();
        Assert.Null((await guest.GetJsonAsync<GroceryKhata.Api.Planner.StoreDto>($"/api/v1/store/{slug}")).Rates);

        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new object[] { new { metal = "gold", purity = 999, sellPerGram = 10_000m }, new { metal = "silver", purity = 999, sellPerGram = 240m } } });
        var rates = (await guest.GetJsonAsync<GroceryKhata.Api.Planner.StoreDto>($"/api/v1/store/{slug}")).Rates!;
        // 24K, 22K and 18K (worked out from the pure rate) and silver; the buying rate is never shown.
        Assert.Equal([("gold", 999, 10_000m), ("gold", 916, 9_160m), ("gold", 750, 7_500m), ("silver", 999, 240m)], rates.Select(r => (r.Metal, r.Purity, r.PerGram)).ToArray());
    }

    [Fact]
    public async Task A_tagged_piece_sells_once_and_old_gold_pays_part_of_the_bill()
    {
        var (shop, _) = await api.SignUpAsync("Soni Jewellers");
        var rates = await (await shop.PostJsonAsync("/api/v1/jewellery/rates", new
        {
            rates = new object[] { new { metal = "gold", purity = 999, sellPerGram = Pure, buyPerGram = Pure }, new { metal = "silver", purity = 999, sellPerGram = 240m } },
        })).ReadAsync<MetalRatesDto>();
        // 22K was not set: it comes from the pure rate.
        Assert.Contains(rates.Current, r => r.Metal == "gold" && r.Purity == 916 && r.SellPerGram == 12_923.11m && r.Derived);

        var piece = await (await shop.PostJsonAsync("/api/v1/jewellery/pieces", new { name = "Ring", metal = "gold", purity = 750, gross = 4.2m, stone = 0.06m, makingType = "percent", makingValue = 15m, stoneValue = 18_000m, huid = "xy34zq" }))
            .ReadAsync<JewelleryPieceDto>();
        Assert.Equal((1, 4.140m, "XY34ZQ"), (piece.TagNo, piece.Net, piece.Huid));
        // The tag's QR reads "GKJ:1".
        Assert.Single(await shop.GetJsonAsync<List<JewelleryPieceDto>>("/api/v1/jewellery/pieces?q=GKJ:1"));

        var customer = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Meena Devi", phone = "9876501234" });
        var billId = Guid.CreateVersion7();
        var bill = await (await shop.PostJsonAsync("/api/v1/jewellery/bills", new
        {
            billId, customerId = customer, lines = new[] { new { pieceId = piece.Id } },
            oldMetal = new[] { new { description = "Old bangle", metal = "gold", gross = 5m, reading = 87.5m, deductionPercent = 2m } },
            moneyPaid = 9_939m, mode = "cash",
        })).ReadAsync<JewelleryBillDto>();
        Assert.Equal((60_488.91m, 1), (bill.OldMetalValue, bill.Lines.Count));
        Assert.Equal(68_376.85m, bill.Lines[0].Taxable);

        // The piece is sold and cannot be billed again.
        Assert.Empty(await shop.GetJsonAsync<List<JewelleryPieceDto>>("/api/v1/jewellery/pieces"));
        var again = await shop.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), lines = new[] { new { pieceId = piece.Id } }, moneyPaid = 70_428m, mode = "upi" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // The drawer: the whole bill came in, the old gold "bought" went out, so only the ₹9,939 that changed hands remains.
        var drawer = await shop.GetJsonAsync<CashCountDto>("/api/v1/cash-count");
        Assert.Equal(9_939m, drawer.CashIn - drawer.CashOut);
    }

    [Fact]
    public async Task Old_gold_bought_without_a_sale_gets_a_numbered_voucher_and_needs_PAN_from_two_lakh()
    {
        var (shop, _) = await api.SignUpAsync("Swarn Kala");
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = Pure, buyPerGram = 13_800m } } });
        var ring = new { description = "Old ring", metal = "gold", gross = 10m, reading = 91.6m, deductionPercent = 2m };

        var id = Guid.CreateVersion7();
        var voucher = await (await shop.PostJsonAsync("/api/v1/jewellery/old-metal-vouchers", new { id, sellerName = "Kamla Devi", sellerPhone = "9876500011", mode = "cash", oldMetal = new[] { ring } }))
            .ReadAsync<OldMetalVoucherDto>();
        // 10 g × 91.6% = 9.160 g fine × ₹13,800 = ₹1,26,408, less 2% = ₹1,23,879.84.
        Assert.Equal((1, "Kamla Devi", 123_879.84m), (voucher.Number, voucher.SellerName, voucher.Total));
        Assert.Contains(voucher.Problems, p => p.Contains("40A(3)"));
        var again = await (await shop.PostJsonAsync("/api/v1/jewellery/old-metal-vouchers", new { id, sellerName = "Kamla Devi", mode = "cash", oldMetal = new[] { ring } })).ReadAsync<OldMetalVoucherDto>();
        Assert.Equal(voucher.Id, again.Id);

        var big = new { description = "Old bangles", metal = "gold", gross = 20m, reading = 91.6m };
        var noPan = await shop.PostJsonAsync("/api/v1/jewellery/old-metal-vouchers", new { id = Guid.CreateVersion7(), sellerName = "Rani", mode = "bank", oldMetal = new[] { big } });
        Assert.Equal(HttpStatusCode.BadRequest, noPan.StatusCode);
        var withPan = await (await shop.PostJsonAsync("/api/v1/jewellery/old-metal-vouchers", new { id = Guid.CreateVersion7(), sellerName = "Rani", pan = "abcde1234f", mode = "bank", oldMetal = new[] { big } }))
            .ReadAsync<OldMetalVoucherDto>();
        Assert.Equal((2, "ABCDE1234F"), (withPan.Number, withPan.Pan));
        Assert.Empty(withPan.Problems);
        Assert.Equal(2, (await shop.GetJsonAsync<List<OldMetalVoucherDto>>("/api/v1/jewellery/old-metal-vouchers")).Count);
    }

    [Fact]
    public async Task A_rate_booked_on_an_order_protects_the_customer_when_gold_goes_up()
    {
        var (shop, _) = await api.SignUpAsync("Akshaya Jewellers");
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 916, sellPerGram = 7_000m }, new { metal = "gold", purity = 999, sellPerGram = 7_600m } } });
        var customer = Guid.CreateVersion7();
        await shop.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Sunita", phone = "9876501299" });
        var order = await shop.PostJsonAsync("/api/v1/jewellery/orders", new { kind = "order", customerId = customer, description = "Bridal set", metal = "gold", purity = 916, weight = 40m, fixedRate = 7_000m, dueOn = "2027-05-01" });
        Assert.Equal(HttpStatusCode.OK, order.StatusCode);

        // Gold goes up: the booked ₹7,000/g applies.
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 916, sellPerGram = 7_400m } } });
        var line = new { name = "Bridal set", metal = "gold", purity = 916, gross = 40m, huid = "BR1D4L" };
        var up = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { customerId = customer, lines = new[] { line } })).ReadAsync<JewelleryQuoteDto>();
        Assert.Equal(7_000m, up.Lines[0].RatePerGram);
        Assert.Contains(up.Problems, p => p.Contains("booked rate"));

        // Gold comes down below the booking: the customer pays today's lower rate. Walk-ins pay today's rate as always.
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 916, sellPerGram = 6_800m } } });
        var down = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { customerId = customer, lines = new[] { line } })).ReadAsync<JewelleryQuoteDto>();
        Assert.Equal(6_800m, down.Lines[0].RatePerGram);
        var walkIn = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { lines = new[] { line } })).ReadAsync<JewelleryQuoteDto>();
        Assert.Equal(6_800m, walkIn.Lines[0].RatePerGram);
    }

    [Fact]
    public async Task Staff_can_only_cut_making_and_wastage_as_far_as_the_owner_allows()
    {
        var (owner, _) = await api.SignUpAsync("Kundan Jewellers");
        await owner.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = Pure } } });
        var piece = await (await owner.PostJsonAsync("/api/v1/jewellery/pieces", new { name = "Chain", metal = "gold", purity = 916, gross = 10m, wastagePercent = 8m, makingType = "per_gram", makingValue = 500m, huid = "KJ12AB" }))
            .ReadAsync<JewelleryPieceDto>();
        var helperPhone = ApiFactory.NewPhone();
        await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone = helperPhone, pin = "4826" });
        var helperAuth = await (await api.CreateClient().PostJsonAsync("/api/v1/auth/sign-in", new { phone = helperPhone, password = "4826" })).ReadAsync<AuthResponse>();
        var helper = api.CreateClient();
        helper.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", helperAuth.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await helper.PutJsonAsync("/api/v1/jewellery/settings", new { maxOffPercent = 50m })).StatusCode);
        Assert.Equal(10m, (await (await owner.PutJsonAsync("/api/v1/jewellery/settings", new { maxOffPercent = 10m })).ReadAsync<JewellerySettingsDto>()).MaxOffPercent);

        // ₹500/g making may go down to ₹450/g for staff, not ₹400/g; the owner may give any discount.
        object Bill(decimal making) => new { billId = Guid.CreateVersion7(), lines = new[] { new { pieceId = piece.Id, makingValue = making } }, moneyPaid = 0m, mode = "upi" };
        var tooMuch = await helper.PostJsonAsync("/api/v1/jewellery/bills", Bill(400m));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("at most 10%", await tooMuch.Content.ReadAsStringAsync());
        var quote = await (await helper.PostJsonAsync("/api/v1/jewellery/quote", new { lines = new[] { new { pieceId = piece.Id, makingValue = 450m } } })).ReadAsync<JewelleryQuoteDto>();
        Assert.Equal(HttpStatusCode.OK, (await helper.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), lines = new[] { new { pieceId = piece.Id, makingValue = 450m } }, moneyPaid = quote.Total, mode = "upi" })).StatusCode);
    }

    [Fact]
    public async Task Two_lakh_needs_PAN_and_cash_stops_below_two_lakh()
    {
        var (shop, _) = await api.SignUpAsync("Verma Jewellers");
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = Pure } } });
        var chain = new { name = "Chain", metal = "gold", purity = 916, gross = 20.45m, wastagePercent = 8m, makingType = "per_gram", makingValue = 350m, huid = "AB12CD" };

        var quote = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { lines = new[] { chain } })).ReadAsync<JewelleryQuoteDto>();
        Assert.Equal((301_355m, true, 199_999m), (quote.Total, quote.PanNeeded, quote.CashLimit));

        var noPan = await shop.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), lines = new[] { chain }, moneyPaid = 301_355m, mode = "upi" });
        Assert.Equal(HttpStatusCode.BadRequest, noPan.StatusCode);

        var allCash = await shop.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), lines = new[] { chain }, moneyPaid = 301_355m, mode = "cash", pan = "ABCDE1234F" });
        Assert.Equal(HttpStatusCode.BadRequest, allCash.StatusCode);

        var ok = await shop.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), lines = new[] { chain }, moneyPaid = 301_355m, mode = "upi", pan = "abcde1234f" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("ABCDE1234F", (await ok.ReadAsync<JewelleryBillDto>()).Pan);
    }

    [Fact]
    public async Task Gold_without_a_rate_or_HUID_is_flagged_and_helpers_cannot_set_rates_for_other_shops()
    {
        var (shop, _) = await api.SignUpAsync("Rao Jewellers");
        var noRate = await shop.PostJsonAsync("/api/v1/jewellery/quote", new { lines = new[] { new { name = "Stud", metal = "gold", purity = 916, gross = 2.1m } } });
        Assert.Equal(HttpStatusCode.BadRequest, noRate.StatusCode);

        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = Pure } } });
        var quote = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { lines = new[] { new { name = "Stud", metal = "gold", purity = 916, gross = 2.1m } } })).ReadAsync<JewelleryQuoteDto>();
        Assert.Contains(quote.Problems, p => p.Contains("HUID"));

        var (other, _) = await api.SignUpAsync("Other Shop");
        var otherRates = await other.GetJsonAsync<MetalRatesDto>("/api/v1/jewellery/rates");
        Assert.Empty(otherRates.Current);
    }
}
