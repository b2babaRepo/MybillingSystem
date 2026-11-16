using GroceryKhata.Api.Catalogue;

namespace GroceryKhata.Api.Ledger;

/// <summary>One HSN row for GSTR-1 Table 12: HSN, rate and unit code, net of the month's credit notes.</summary>
public sealed record HsnRow(string Section, string Hsn, decimal? Rate, string Uqc, decimal Quantity, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst);

/// <summary>
/// The month's tax as GSTR-3B (or CMP-08 for a composition shop) would show it: tax on sales less credit notes, input
/// tax that can be claimed, and what is left to pay by head after using the credit in the order the law sets.
/// </summary>
public sealed record TaxPayableDto(
    decimal OutputTaxable, decimal OutputCgst, decimal OutputSgst, decimal OutputIgst,
    decimal InputCgst, decimal InputSgst, decimal InputIgst,
    decimal PayCgst, decimal PaySgst, decimal PayIgst,
    decimal CarryCgst, decimal CarrySgst, decimal CarryIgst,
    decimal? CompositionTurnover = null, decimal? CompositionTax = null);

public static class GstReturns
{
    /// <summary>
    /// The GST unit code (UQC) for an item's unit, as GSTR-1 asks. Only codes on the portal's UQC master
    /// (einvoice1.gst.gov.in, Master Codes) are accepted; a unit with none of its own, like a jar or a strip, goes as OTH.
    /// </summary>
    public static string Uqc(Unit unit) => unit switch
    {
        Unit.Kg => "KGS",
        Unit.G => "GMS",
        Unit.Tonne => "TON",
        Unit.Litre => "LTR",
        Unit.Ml => "MLT",
        Unit.Metre => "MTR",
        Unit.Dozen => "DOZ",
        Unit.Box => "BOX",
        Unit.Bag => "BAG",
        Unit.Packet => "PAC",
        Unit.Pair => "PRS",
        Unit.Piece => "PCS",
        Unit.Cft or Unit.Brass => "CBM",
        Unit.SqFt => "SQF",
        Unit.RunningFt => "MTR",
        Unit.Quintal => "QTL",
        Unit.Carton => "CTN",
        Unit.Bottle => "BTL",
        Unit.Set => "SET",
        _ => "OTH",
    };

    /// <summary>
    /// The quantity in the UQC's own measure: GSTR-1 has no cubic or running feet, so they go as cubic metres and metres,
    /// converted, never as the same number under a bigger unit.
    /// </summary>
    public static decimal InUqc(Unit unit, decimal quantity) => unit switch
    {
        Unit.Cft => decimal.Round(quantity * 0.0283168m, 3),
        Unit.Brass => decimal.Round(quantity * 2.83168m, 3),
        Unit.RunningFt => decimal.Round(quantity * 0.3048m, 3),
        _ => quantity,
    };

    /// <summary>A line on a sale or a credit note, reduced to what the HSN table and the tax summary need.</summary>
    internal sealed record TaxLine(bool B2b, string? Hsn, decimal? Rate, Unit Unit, decimal Quantity, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst);

    /// <summary>GSTR-1 Table 12 since 2025: B2B and B2C apart, by HSN, rate and unit; credit notes taken off.</summary>
    internal static List<HsnRow> Hsn(IEnumerable<TaxLine> sales, IEnumerable<TaxLine> returns) =>
        sales.Concat(returns.Select(r => r with { Quantity = -r.Quantity, Taxable = -r.Taxable, Cgst = -r.Cgst, Sgst = -r.Sgst, Igst = -r.Igst }))
            .GroupBy(l => (l.B2b, Hsn: l.Hsn ?? "", l.Rate, Uqc: Uqc(l.Unit)))
            .Select(g => new HsnRow(g.Key.B2b ? "B2B" : "B2C", g.Key.Hsn.Length == 0 ? "No HSN" : g.Key.Hsn, g.Key.Rate, g.Key.Uqc,
                g.Sum(l => InUqc(l.Unit, l.Quantity)), g.Sum(l => l.Taxable), g.Sum(l => l.Cgst), g.Sum(l => l.Sgst), g.Sum(l => l.Igst)))
            .OrderBy(r => r.Section).ThenBy(r => r.Hsn).ThenBy(r => r.Rate).ToList();

    /// <summary>
    /// Tax to pay for the month. Input IGST is used first against IGST, then CGST, then SGST; input CGST against CGST
    /// then IGST; input SGST against SGST then IGST (sections 49 and 49A). What can't be used is carried forward.
    /// A composition shop pays 1% of its turnover (0.5% each), which is what a trader in goods pays; others need the CA.
    /// </summary>
    internal static TaxPayableDto Payable(bool composition, decimal turnover, IReadOnlyCollection<TaxLine> sales, IReadOnlyCollection<TaxLine> returns,
        (decimal Cgst, decimal Sgst, decimal Igst) input)
    {
        if (composition)
        {
            var tax = decimal.Round(turnover * 0.01m, 2);
            var half = decimal.Round(tax / 2, 2);
            return new TaxPayableDto(turnover, 0, 0, 0, 0, 0, 0, half, tax - half, 0, 0, 0, 0, turnover, tax);
        }

        decimal Net(Func<TaxLine, decimal> f) => sales.Sum(f) - returns.Sum(f);
        var (oc, os, oi) = (Net(l => l.Cgst), Net(l => l.Sgst), Net(l => l.Igst));
        var (ic, isg, ii) = input;

        decimal Use(ref decimal credit, ref decimal due)
        {
            var used = Math.Min(Math.Max(0, credit), Math.Max(0, due));
            credit -= used;
            due -= used;
            return used;
        }

        var (dueC, dueS, dueI) = (oc, os, oi);
        var (credI, credC, credS) = (ii, ic, isg);
        Use(ref credI, ref dueI);
        Use(ref credI, ref dueC);
        Use(ref credI, ref dueS);
        Use(ref credC, ref dueC);
        Use(ref credC, ref dueI);
        Use(ref credS, ref dueS);
        Use(ref credS, ref dueI);
        return new TaxPayableDto(Net(l => l.Taxable), oc, os, oi, ic, isg, ii,
            Math.Max(0, dueC), Math.Max(0, dueS), Math.Max(0, dueI), Math.Max(0, credC), Math.Max(0, credS), Math.Max(0, credI));
    }
}
