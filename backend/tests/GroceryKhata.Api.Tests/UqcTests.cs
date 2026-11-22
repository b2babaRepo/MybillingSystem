using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Ledger;

namespace GroceryKhata.Api.Tests;

public class UqcTests
{
    [Theory]
    [InlineData(Unit.SqFt, "SQF")]
    [InlineData(Unit.Cft, "CBM")]
    [InlineData(Unit.Brass, "CBM")]
    [InlineData(Unit.RunningFt, "MTR")]
    [InlineData(Unit.Quintal, "QTL")]
    [InlineData(Unit.Carton, "CTN")]
    [InlineData(Unit.Bottle, "BTL")]
    [InlineData(Unit.Set, "SET")]
    [InlineData(Unit.Strip, "OTH")]
    [InlineData(Unit.Jar, "OTH")]
    public void Each_unit_has_its_GST_code(Unit unit, string uqc) => Assert.Equal(uqc, GstReturns.Uqc(unit));

    // The portal's UQC master: every code the app sends must be on it.
    private static readonly HashSet<string> PortalCodes = ["BAG", "BAL", "BDL", "BKL", "BOU", "BOX", "BTL", "BUN", "CAN", "CBM", "CCM", "CMS", "CTN", "DOZ", "DRM", "GGK",
        "GMS", "GRS", "GYD", "KGS", "KLR", "KME", "LTR", "MLT", "MTR", "MTS", "NOS", "OTH", "PAC", "PCS", "PRS", "QTL", "ROL", "SET", "SQF", "SQM", "SQY", "TBS", "TGM",
        "THD", "TON", "TUB", "UGS", "UNT", "YDS"];

    [Fact]
    public void Every_unit_maps_to_a_code_the_GST_portal_accepts() =>
        Assert.All(Enum.GetValues<Unit>(), u => Assert.Contains(GstReturns.Uqc(u), PortalCodes));

    [Fact]
    public void Feet_go_to_GSTR1_converted_not_relabelled()
    {
        Assert.Equal(2.832m, GstReturns.InUqc(Unit.Cft, 100m));
        Assert.Equal(2.832m, GstReturns.InUqc(Unit.Brass, 1m));
        Assert.Equal(3.048m, GstReturns.InUqc(Unit.RunningFt, 10m));
        Assert.Equal(7m, GstReturns.InUqc(Unit.Kg, 7m));
    }
}
