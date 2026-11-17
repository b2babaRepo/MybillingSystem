using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

public sealed class MorningNoteLanguageTests
{
    private static readonly OrderTodayDto Today = new(2,
    [
        new OrderTodayItem(Guid.NewGuid(), "Tata salt 1kg", 0m, null, 10m, 2, "Salt", "नमक"),
        new OrderTodayItem(Guid.NewGuid(), "Kurti L Blue", 3m, 2.6m, 5m, 3),
    ]);

    [Fact]
    public void Hindi_phone_gets_hindi_words_and_hindi_item_names()
    {
        var (title, body) = StockAlerts.Message("hi", Today, 1, (1, 0, 2), ("Diwali", 5, 12));
        Assert.Equal("आज ऑर्डर करें: 2 सामान", title);
        Assert.Equal("नमक (खत्म), Kurti L Blue (2 दिन) · 1 रोका सामान तारीख पार · माल: 1 ऑर्डर लेट, 2 कल आने हैं · Diwali 5 दिन में: 12 सामान का स्टॉक भरें", body);
    }

    [Fact]
    public void Gujarati_phone_gets_gujarati_words_and_english_list_names()
    {
        var (title, body) = StockAlerts.Message("gu", Today, 0, (0, 1, 0), null);
        Assert.Equal("આજે ઓર્ડર કરો: 2 વસ્તુ", title);
        Assert.Equal("Salt (ખતમ), Kurti L Blue (2 દિવસ) · માલ: 1 આજે આવશે", body);
    }

    [Fact]
    public void English_stays_as_before()
    {
        var (title, body) = StockAlerts.Message("en", new OrderTodayDto(0, []), 0, (0, 0, 0), ("Holi", 1, 1));
        Assert.Equal("Stock is fine today", title);
        Assert.Equal("Nothing is running out. · Holi tomorrow: stock up 1 item", body);
    }
}
