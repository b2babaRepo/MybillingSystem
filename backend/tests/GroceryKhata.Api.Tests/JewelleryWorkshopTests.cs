using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Karigar and trader metal accounts in fine grams, loose metal, stones on a piece, and tray counts.</summary>
[Collection(ApiCollection.Name)]
public sealed class JewelleryWorkshopTests(ApiFactory api)
{
    [Fact]
    public async Task A_karigar_owes_what_was_issued_less_what_came_back_and_the_wastage_allowed()
    {
        var (shop, _) = await api.SignUpAsync("Swarn Karigari");
        async Task<MetalPartyDto> Karigar(string name, bool onFine) =>
            await (await shop.PostJsonAsync("/api/v1/jewellery/parties", new { name, kind = "karigar", allowancePercent = 3m, allowanceOnFine = onFine, labourPerGram = 250m })).ReadAsync<MetalPartyDto>();
        async Task Settle(MetalPartyDto k)
        {
            (await shop.PostJsonAsync("/api/v1/jewellery/metal-moves", new { partyId = k.Id, kind = "issue", metal = "gold", gross = 100m, reading = 99.5m })).EnsureSuccessStatusCode();
            var back = await (await shop.PostJsonAsync("/api/v1/jewellery/metal-moves", new { partyId = k.Id, kind = "receive", metal = "gold", gross = 104m, reading = 91.6m })).ReadAsync<MetalMoveDto>();
            Assert.Equal((95.264m, 26_000m), (back.Fine, back.Labour));
        }

        // 99.500 fine out; 95.264 fine back; 3% of 104 g gross = 3.120 allowed: 1.116 g still with the karigar.
        var onGross = await Karigar("Ramesh", false);
        await Settle(onGross);
        // The same work with the allowance on fine weight: 3% of 95.264 = 2.858, so 1.378 g.
        var onFine = await Karigar("Suresh", true);
        await Settle(onFine);
        var parties = await shop.GetJsonAsync<List<MetalPartyDto>>("/api/v1/jewellery/parties");
        Assert.Equal(1.116m, parties.Single(p => p.Id == onGross.Id).Gold);
        Assert.Equal(1.378m, parties.Single(p => p.Id == onFine.Id).Gold);
        Assert.Equal((26_000m, 1_300m), (parties[0].LabourDue, parties[0].ReverseChargeGst)); // unregistered: 5% under reverse charge
        Assert.False(parties[0].YearPassed);

        // Finished work goes to tagging, not to loose metal: only the issue left the shop's loose gold.
        var loose = (await shop.GetJsonAsync<List<LooseMetalDto>>("/api/v1/jewellery/loose-metal")).Single(l => l.Metal == "gold");
        Assert.Equal(-199m, loose.Fine);
    }

    [Fact]
    public async Task Rate_cut_and_labour_move_money_through_the_drawer()
    {
        var (shop, _) = await api.SignUpAsync("Bullion Bhandar");
        var karigar = await (await shop.PostJsonAsync("/api/v1/jewellery/parties", new { name = "Mohan", kind = "karigar", labourPerGram = 100m })).ReadAsync<MetalPartyDto>();
        var trader = await (await shop.PostJsonAsync("/api/v1/jewellery/parties", new { name = "Gold Traders", kind = "trader" })).ReadAsync<MetalPartyDto>();
        async Task<HttpResponseMessage> Move(object body) => await shop.PostJsonAsync("/api/v1/jewellery/metal-moves", body);

        // The karigar keeps 2 g fine of the shop's gold and pays for it at ₹10,000 a gram; the shop pays ₹500 labour.
        (await Move(new { partyId = karigar.Id, kind = "issue", metal = "gold", gross = 12m, reading = 100m })).EnsureSuccessStatusCode();
        (await Move(new { partyId = karigar.Id, kind = "receive", metal = "gold", gross = 5m, reading = 100m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Move(new { partyId = karigar.Id, kind = "rate_cut", metal = "gold", fine = 9m, ratePerGram = 10_000m, theyPay = true })).StatusCode);
        (await Move(new { partyId = karigar.Id, kind = "rate_cut", metal = "gold", fine = 2m, ratePerGram = 10_000m, theyPay = true })).EnsureSuccessStatusCode();
        (await Move(new { partyId = karigar.Id, kind = "labour_paid", metal = "gold", amount = 500m })).EnsureSuccessStatusCode();

        // Bullion bought on credit from the trader (the shop owes 49.75 g fine), half of it paid for in money.
        var bullion = await (await Move(new { partyId = trader.Id, kind = "receive", metal = "gold", gross = 50m, reading = 99.5m })).ReadAsync<MetalMoveDto>();
        Assert.Equal((-49.75m, 49.75m, false), (bullion.BalanceAfter, bullion.LooseEffect, bullion.Finished));
        (await Move(new { partyId = trader.Id, kind = "rate_cut", metal = "gold", fine = 25m, ratePerGram = 9_000m, theyPay = false })).EnsureSuccessStatusCode();

        var parties = await shop.GetJsonAsync<List<MetalPartyDto>>("/api/v1/jewellery/parties");
        Assert.Equal(5m, parties.Single(p => p.Id == karigar.Id).Gold);
        Assert.Equal(-24.75m, parties.Single(p => p.Id == trader.Id).Gold);
        var drawer = await shop.GetJsonAsync<CashCountDto>("/api/v1/cash-count");
        Assert.Equal(20_000m - 500m - 225_000m, drawer.CashIn - drawer.CashOut);
    }

    private static async Task<JewelleryPieceDto> Piece(HttpClient shop, string name, string? tray, object[]? stones = null) =>
        await (await shop.PostJsonAsync("/api/v1/jewellery/pieces", new { name, metal = "gold", purity = 916, gross = 5m, tray, stones })).ReadAsync<JewelleryPieceDto>();

    [Fact]
    public async Task Stones_set_the_stone_weight_and_lab_grown_ones_are_named_on_the_bill()
    {
        var (shop, _) = await api.SignUpAsync("Heera Jewellers");
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = 10_000m } } });
        var ring = await Piece(shop, "Solitaire ring", null, [new { kind = "Diamond", labGrown = true, count = 2, carat = 0.25m, ratePerCarat = 40_000m, certLab = "IGI", certNo = "LG123" }]);
        // 2 × 0.25 ct is entered as 0.25 ct in all = 0.050 g; value 0.25 × ₹40,000.
        Assert.Equal((0.05m, 10_000m), (ring.Stone, ring.StoneValue));
        Assert.Equal("IGI", ring.Stones!.Single().CertLab);

        var quote = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { lines = new[] { new { pieceId = ring.Id } } })).ReadAsync<JewelleryQuoteDto>();
        Assert.EndsWith("(lab-grown stones)", quote.Lines.Single().Name);
    }

    [Fact]
    public async Task A_tray_count_finds_missing_and_stray_tags_and_skips_pieces_on_approval()
    {
        var (shop, _) = await api.SignUpAsync("Tray Jewellers");
        var a = await Piece(shop, "Chain", "A1");
        var b = await Piece(shop, "Bangle", "A1");
        var c = await Piece(shop, "Ring", "A1");
        var other = await Piece(shop, "Pendant", "B2");
        (await shop.PostJsonAsync("/api/v1/jewellery/approvals", new { party = "Mrs Rao", pieceIds = new[] { c.Id } })).EnsureSuccessStatusCode();

        var trays = await shop.GetJsonAsync<List<TrayDto>>("/api/v1/jewellery/trays");
        Assert.Equal((2, 10m), (trays.Single(t => t.Tray == "A1").Pieces, trays.Single(t => t.Tray == "A1").Gross));

        var count = await (await shop.PostJsonAsync("/api/v1/jewellery/tray-counts", new { tray = "A1", kind = "closing", tags = new[] { $"J-{a.TagNo}", $"GKJ:{other.TagNo}" }, countedGross = 10m })).ReadAsync<TrayCountDto>();
        Assert.Equal([$"J-{b.TagNo}"], count.Missing);
        Assert.Equal([$"J-{other.TagNo}"], count.Extra);
        Assert.False(count.Clean);
    }
}
