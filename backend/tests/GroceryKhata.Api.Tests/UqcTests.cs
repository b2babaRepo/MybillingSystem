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
    public void Each_unit_has_its_GST_code(Unit unit, string uqc) => Assert.Equal(uqc, GstReturns.Uqc(unit));

    [Fact]
    public void Feet_go_to_GSTR1_converted_not_relabelled()
    {
        Assert.Equal(2.832m, GstReturns.InUqc(Unit.Cft, 100m));
        Assert.Equal(2.832m, GstReturns.InUqc(Unit.Brass, 1m));
        Assert.Equal(3.048m, GstReturns.InUqc(Unit.RunningFt, 10m));
        Assert.Equal(7m, GstReturns.InUqc(Unit.Kg, 7m));
    }
}
