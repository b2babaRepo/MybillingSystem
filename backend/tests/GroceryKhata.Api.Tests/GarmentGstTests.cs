using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

public class GarmentGstTests
{
    [Theory]
    [InlineData("6204", 5, 999, 5)]
    [InlineData("6204", 5, 2625, 5)]   // 2,625 ÷ 1.05 = 2,500: still 5%
    [InlineData("6203", 5, 2626, 18)]
    [InlineData("6109", 18, 1200, 5)]  // an item set at 18% sold cheap goes down to 5%
    [InlineData("6402", 5, 4000, 18)]  // footwear follows the same rule
    public void Garments_are_taxed_by_the_piece_price(string hsn, int itemRate, int price, int expected) =>
        Assert.Equal(expected, GarmentGst.RateFor(hsn, itemRate, price));

    [Theory]
    [InlineData("1001", 5, 5000, 5)]   // not a garment
    [InlineData("5208", 5, 5000, 5)]   // fabric by the metre
    [InlineData("6204", 0, 5000, 0)]   // a rate the shop set on purpose
    public void Other_items_keep_their_rate(string hsn, int itemRate, int price, int expected) =>
        Assert.Equal(expected, GarmentGst.RateFor(hsn, itemRate, price));

    [Fact]
    public void An_item_with_no_rate_stays_without_one() => Assert.Null(GarmentGst.RateFor("6204", null, 3000));
}
