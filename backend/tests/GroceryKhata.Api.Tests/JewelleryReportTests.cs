using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>Stock by karat, and cash per customer for PMLA and the SFT return.</summary>
[Collection(ApiCollection.Name)]
public sealed class JewelleryReportTests(ApiFactory api)
{
    [Fact]
    public void Kerala_is_known_by_state_name_or_gstin()
    {
        Assert.True(JewelleryReportEndpoints.IsKerala("Kerala", null));
        Assert.True(JewelleryReportEndpoints.IsKerala(null, "32ABCDE1234F1Z5"));
        Assert.False(JewelleryReportEndpoints.IsKerala("Gujarat", "24ABCDE1234F1Z5"));
        Assert.Equal((2026, new DateOnly(2026, 4, 1)), (JewelleryReportEndpoints.Year(2026, TimeProvider.System).Fy, JewelleryReportEndpoints.Year(2026, TimeProvider.System).From));
    }

    [Fact]
    public async Task Stock_is_shown_by_karat_with_fine_weight_and_cash_is_added_up_per_customer()
    {
        var (shop, _) = await api.SignUpAsync("Kanak Jewellers");
        await shop.PostJsonAsync("/api/v1/jewellery/rates", new { rates = new[] { new { metal = "gold", purity = 999, sellPerGram = 10_000m } } });
        async Task<JewelleryPieceDto> Piece(decimal gross, string huid) =>
            await (await shop.PostJsonAsync("/api/v1/jewellery/pieces", new { name = "Chain", metal = "gold", purity = 916, gross, huid })).ReadAsync<JewelleryPieceDto>();
        var big = await Piece(20m, "AA11BB");
        await Piece(5m, "CC22DD");
        var small = await (await shop.PostJsonAsync("/api/v1/jewellery/pieces", new { name = "Nose pin", metal = "gold", purity = 916, gross = 1.5m })).ReadAsync<JewelleryPieceDto>();

        var stock = await shop.GetJsonAsync<StockReportDto>("/api/v1/jewellery/reports/stock");
        var k22 = stock.Lines.Single(l => l.Metal == "gold" && l.Purity == 916);
        Assert.Equal((3, 26.5m, 24.274m), (k22.Pieces, k22.Net, k22.Fine));
        Assert.Equal(decimal.Round(26.5m * k22.RatePerGram!.Value, 0), k22.Value);

        // Under 2 g, no HUID is needed.
        var quote = await (await shop.PostJsonAsync("/api/v1/jewellery/quote", new { lines = new[] { new { pieceId = small.Id } } })).ReadAsync<JewelleryQuoteDto>();
        Assert.DoesNotContain(quote.Problems, p => p.Contains("HUID"));

        var customer = Guid.CreateVersion7();
        (await shop.PostJsonAsync("/api/v1/customers", new { id = customer, name = "Meena" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/jewellery/bills", new { billId = Guid.CreateVersion7(), customerId = customer, lines = new[] { new { pieceId = big.Id } }, moneyPaid = 150_000m, mode = "cash" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 30_000m, paymentMode = "cash" })).EnsureSuccessStatusCode();
        (await shop.PostJsonAsync("/api/v1/khata-entries", new { id = Guid.CreateVersion7(), customerId = customer, type = "payment", amount = 25_000m, paymentMode = "upi" })).EnsureSuccessStatusCode();

        var cash = await shop.GetJsonAsync<CashReportDto>("/api/v1/jewellery/reports/cash");
        var meena = cash.Customers.Single();
        Assert.Equal(("Meena", 1, 150_000m, 30_000m, 180_000m), (meena.Name, meena.Bills, meena.BillsCash, meena.KhataCash, meena.Total));
        Assert.Equal(180_000m, meena.Months.Sum());
        Assert.False(meena.Sft);
        Assert.False(meena.PmlaYear);

        var sales = await shop.GetJsonAsync<SalesReportDto>("/api/v1/jewellery/reports/sales");
        Assert.Equal((1, 20m), (sales.Lines.Single().Pieces, sales.Lines.Single().Net));
    }
}
