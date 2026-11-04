using System.Net;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

/// <summary>A sales & service shop's helpers: AI output is bounded and matched to the shop's own items, and only proposes.</summary>
public sealed class ServiceAssistantTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public void A_spoken_note_becomes_job_card_fields_with_parts_matched_to_stock()
    {
        var guess = ServiceAssistant.ParseNote("""
            {"title":"Pump repair","complaint":"Motor garam hota hai, pani kam","serialNo":"crp 77","customerName":"ramdeen",
             "parts":[{"name":"capacitor 36 mfd","quantity":1},{"name":"Mechanical seal","quantity":0},{"name":"","quantity":2}]}
            """);
        Assert.Equal(("CRP77", 2, 1m), (guess.SerialNo, guess.Parts.Count, guess.Parts[1].Quantity));

        var capacitor = Guid.NewGuid();
        var farmer = Guid.NewGuid();
        var draft = ServiceAssistant.MatchNote(guess, [(capacitor, "Capacitor 36 mfd"), (Guid.NewGuid(), "Ball bearing 6203")], [(farmer, "Ramdeen")]);
        Assert.Equal((farmer, capacitor), (draft.CustomerId!.Value, draft.Parts[0].ProductId!.Value));
        // No such item in stock: left for the technician to pick, never guessed.
        Assert.Null(draft.Parts[1].ProductId);
    }

    [Fact]
    public void Price_list_rows_are_bounded_and_matched_by_name()
    {
        var rows = ServiceAssistant.ParsePriceList("""
            {"rows":[{"name":"Capacitor 36 MFD (pack of 1)","mrp":220,"dealerPrice":150},{"name":"Kirloskar KOS-115","mrp":-5,"dealerPrice":null},{"name":" ","mrp":1,"dealerPrice":1}]}
            """);
        Assert.Equal((2, (decimal?)null), (rows.Count, rows[1].Mrp));

        var capacitor = Guid.NewGuid();
        var lines = ServiceAssistant.MatchPriceList(rows, [(capacitor, "Capacitor 36 mfd", 180m, 120m)]);
        Assert.Equal((capacitor, 180m, 220m), (lines[0].ProductId!.Value, lines[0].SellingPrice!.Value, lines[0].Mrp!.Value));
        Assert.Null(lines[1].ProductId);
    }

    [Fact]
    public async Task A_new_shop_has_no_flags_and_a_too_short_note_is_refused()
    {
        var shop = (await api.SignUpAsync()).Client;
        Assert.Empty(await shop.GetJsonAsync<List<ServiceFlag>>("/api/v1/service/flags"));
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.PostJsonAsync("/api/v1/service/job-note", new { note = "x" })).StatusCode);
    }
}
