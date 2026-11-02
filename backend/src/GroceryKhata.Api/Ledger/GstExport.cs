using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>Something the CA would trip on, with how many bills or lines it touches.</summary>
public sealed record GstCheck(string Code, int Count);

/// <summary>One month of GST for the owner: what goes to the CA, and what to fix first.</summary>
public sealed record GstMonthDto(
    string Month, bool Composition, int Bills, int Cancelled, string? FirstBillNo, string? LastBillNo,
    decimal Sales, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal B2bSales, IReadOnlyList<GstCheck> Checks, bool CanDownload,
    int CreditNotes = 0, decimal CreditNoteValue = 0, decimal CreditNoteTax = 0);

/// <summary>
/// "GST &amp; Tally": a month's bills as Tally import files (parties first, then sales) and a summary sheet, for the
/// shop's CA. Tally builds GSTR-1, GSTR-3B and the HSN summary from them. Totals are open to every owner; the download is
/// a paid feature. A working aid: the CA checks it and files.
/// </summary>
public static class GstExport
{
    private static readonly Dictionary<string, string> States = new()
    {
        ["01"] = "Jammu and Kashmir", ["02"] = "Himachal Pradesh", ["03"] = "Punjab", ["04"] = "Chandigarh", ["05"] = "Uttarakhand",
        ["06"] = "Haryana", ["07"] = "Delhi", ["08"] = "Rajasthan", ["09"] = "Uttar Pradesh", ["10"] = "Bihar", ["11"] = "Sikkim",
        ["12"] = "Arunachal Pradesh", ["13"] = "Nagaland", ["14"] = "Manipur", ["15"] = "Mizoram", ["16"] = "Tripura", ["17"] = "Meghalaya",
        ["18"] = "Assam", ["19"] = "West Bengal", ["20"] = "Jharkhand", ["21"] = "Odisha", ["22"] = "Chhattisgarh", ["23"] = "Madhya Pradesh",
        ["24"] = "Gujarat", ["26"] = "Dadra and Nagar Haveli and Daman and Diu", ["27"] = "Maharashtra", ["29"] = "Karnataka", ["30"] = "Goa",
        ["31"] = "Lakshadweep", ["32"] = "Kerala", ["33"] = "Tamil Nadu", ["34"] = "Puducherry", ["35"] = "Andaman and Nicobar Islands",
        ["36"] = "Telangana", ["37"] = "Andhra Pradesh", ["38"] = "Ladakh",
    };

    public static IEndpointRouteBuilder MapGstExport(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/gst").WithTags("Reports").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("/month", MonthAsync);
        group.MapGet("/export.zip", ExportAsync).RequireFeature(Features.GstExport);
        return app;
    }

    /// <summary>The GSTIN check digit (the 15th character), as the GST portal works it out.</summary>
    public static bool IsValidGstin(string? gstin)
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        if (gstin is not { Length: 15 } || !States.ContainsKey(gstin[..2]) || gstin.Any(c => chars.IndexOf(c) < 0))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 14; i++)
        {
            var product = chars.IndexOf(gstin[i]) * (i % 2 == 0 ? 1 : 2);
            sum += (product / 36) + (product % 36);
        }

        return chars[(36 - (sum % 36)) % 36] == gstin[14];
    }

    private sealed record MonthData(List<Bill> Bills, List<Bill> Cancelled, Dictionary<Guid, (string Name, string? Gstin)> Customers, bool Composition, string? ShopGstin, int Returns, List<CreditNote> Notes);

    private static async Task<MonthData> LoadAsync(AppDbContext db, ITenantContext tenant, (int Year, int Month) period, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Month(period.Year, period.Month);
        var all = await db.Bills.AsNoTracking().Include(b => b.Lines).Where(b => b.OccurredAt >= start && b.OccurredAt < end)
            .OrderBy(b => b.FinancialYear).ThenBy(b => b.Number).ToListAsync(ct);
        var notes = await db.CreditNotes.AsNoTracking().Include(n => n.Lines).Where(n => n.OccurredAt >= start && n.OccurredAt < end).OrderBy(n => n.FinancialYear).ThenBy(n => n.Number).ToListAsync(ct);
        var ids = all.Where(b => b.CustomerId != null).Select(b => b.CustomerId!.Value).Concat(notes.Where(n => n.CustomerId != null).Select(n => n.CustomerId!.Value)).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.Name, c.Gstin), ct);
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.GstComposition, s.Gstin }).SingleAsync(ct);
        // Returns from order disputes have no credit note: they still need one from the CA.
        var returns = await db.LedgerEntries.AsNoTracking().CountAsync(e => e.Kind == LedgerEntryKind.Return && e.OccurredAt >= start && e.OccurredAt < end && (e.Note == null || !e.Note.StartsWith("CN/")), ct);
        return new MonthData(all.Where(b => b.CancelledAt == null).ToList(), all.Where(b => b.CancelledAt != null).ToList(), customers, shop.GstComposition, shop.Gstin, returns, notes);
    }

    private static (decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst) Tax(Bill b) =>
        b.Gstin is null || b.BillOfSupply
            ? (b.Total, 0, 0, 0)
            : b.Lines.Select(LineTax).Aggregate((Taxable: 0m, Cgst: 0m, Sgst: 0m, Igst: 0m), (t, l) => (t.Taxable + l.Taxable, t.Cgst + l.Cgst, t.Sgst + l.Sgst, t.Igst + l.Igst));

    /// <summary>A line's tax as kept when billed; lines from before tax was kept are worked out from their rate (within the state).</summary>
    private static (decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst) LineTax(BillLine l)
    {
        if (l.TaxableValue is { } kept)
        {
            return (kept, l.Cgst ?? 0, l.Sgst ?? 0, l.Igst ?? 0);
        }

        if (l.GstRate is not { } rate)
        {
            return (l.Amount, 0, 0, 0);
        }

        var taxable = decimal.Round(l.Amount / (1 + (rate / 100)), 2);
        var half = decimal.Round((l.Amount - taxable) / 2, 2);
        return (taxable, half, l.Amount - taxable - half, 0);
    }

    private static async Task<IResult> MonthAsync(string? month, AppDbContext db, ITenantContext tenant, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        if (IndiaTime.ParseMonth(month ?? IndiaTime.Today(clock).ToString("yyyy-MM", CultureInfo.InvariantCulture)) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var d = await LoadAsync(db, tenant, period, ct);
        var lines = d.Bills.Where(b => b.Gstin != null && !b.BillOfSupply).SelectMany(b => b.Lines).ToList();
        var checks = new List<GstCheck>
        {
            new("noGstin", d.Bills.Count(b => b.Gstin is null)),
            new("noRate", lines.Count(l => l.GstRate is null)),
            new("noHsn", lines.Count(l => string.IsNullOrEmpty(l.HsnCode))),
            new("oldTax", lines.Count(l => l.GstRate is not null && l.TaxableValue is null)),
            new("badBuyerGstin", d.Bills.Count(b => b.BuyerGstin is not null && !IsValidGstin(b.BuyerGstin))),
            new("returns", d.Returns),
        }.Where(c => c.Count > 0).ToList();
        var taxes = d.Bills.Select(Tax).ToList();
        return TypedResults.Ok(new GstMonthDto(
            $"{period.Year:D4}-{period.Month:D2}", d.Composition, d.Bills.Count, d.Cancelled.Count,
            d.Bills.Concat(d.Cancelled).OrderBy(b => b.FinancialYear).ThenBy(b => b.Number).FirstOrDefault()?.BillNo,
            d.Bills.Concat(d.Cancelled).OrderBy(b => b.FinancialYear).ThenBy(b => b.Number).LastOrDefault()?.BillNo,
            d.Bills.Sum(b => b.Total), taxes.Sum(x => x.Taxable), taxes.Sum(x => x.Cgst), taxes.Sum(x => x.Sgst), taxes.Sum(x => x.Igst),
            d.Bills.Where(b => b.BuyerGstin is not null && IsValidGstin(b.BuyerGstin)).Sum(b => b.Total), checks,
            (await entitlements.ForShopAsync(tenant.ShopId, ct)).Has(Features.GstExport),
            d.Notes.Count, d.Notes.Sum(n => n.Total), d.Notes.Sum(n => n.Lines.Sum(l => l.Cgst + l.Sgst + l.Igst))));
    }

    private static async Task<IResult> ExportAsync(string? month, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (IndiaTime.ParseMonth(month ?? IndiaTime.Today(clock).ToString("yyyy-MM", CultureInfo.InvariantCulture)) is not { } period)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var d = await LoadAsync(db, tenant, period, ct);
        var name = $"{period.Year:D4}-{period.Month:D2}";
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "1-masters.xml", Masters(d));
            Add(archive, "2-vouchers.xml", Vouchers(d));
            Add(archive, $"summary-{name}.csv", Summary(d));
            Add(archive, "READ-ME.txt", """
                Grocery Khata: a month of sales for your CA.
                In TallyPrime: Import > Masters > 1-masters.xml first, then Import > Vouchers > 2-vouchers.xml.
                Ledger names can be renamed in Tally after import. Cancelled bills are listed in the summary, not imported.
                Bills made before the shop had GST on its plan carry no GSTIN and are marked in the summary.
                This is a working aid: check it before filing.
                """);
        }

        return TypedResults.File(zip.ToArray(), "application/zip", $"gst-tally-{name}.zip");
    }

    private static void Add(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static string X(string? text) => SecurityElement.Escape(text ?? "") ?? "";

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Party(Bill b, MonthData d) => b.CustomerId is { } id && d.Customers.TryGetValue(id, out var c) ? c.Name : "Cash";

    private static string StateOf(string? gstin, string? fallback) =>
        gstin is { Length: >= 2 } && States.TryGetValue(gstin[..2], out var state) ? state : fallback is { Length: >= 2 } && States.TryGetValue(fallback[..2], out var own) ? own : "";

    private static string Envelope(string request, string body) =>
        $"<ENVELOPE><HEADER><TALLYREQUEST>Import Data</TALLYREQUEST></HEADER><BODY><IMPORTDATA><REQUESTDESC><REPORTNAME>{request}</REPORTNAME></REQUESTDESC><REQUESTDATA>\n{body}</REQUESTDATA></IMPORTDATA></BODY></ENVELOPE>\n";

    /// <summary>Ledgers the vouchers use: sales, the tax heads, round off, cash, and one party per customer billed.</summary>
    private static string Masters(MonthData d)
    {
        var body = new StringBuilder();
        void Ledger(string name, string parent, string extra = "") =>
            body.Append($"<TALLYMESSAGE><LEDGER NAME=\"{X(name)}\" ACTION=\"Create\"><NAME>{X(name)}</NAME><PARENT>{X(parent)}</PARENT>{extra}</LEDGER></TALLYMESSAGE>\n");
        Ledger("Sales", "Sales Accounts");
        foreach (var head in new[] { "CGST", "SGST", "IGST" })
        {
            Ledger($"Output {head}", "Duties & Taxes", $"<TAXTYPE>GST</TAXTYPE><GSTDUTYHEAD>{(head == "SGST" ? "State Tax" : head == "CGST" ? "Central Tax" : "Integrated Tax")}</GSTDUTYHEAD>");
        }

        Ledger("Round Off", "Indirect Expenses");
        Ledger("Cash", "Cash-in-Hand");
        foreach (var (_, c) in d.Customers.OrderBy(x => x.Value.Name))
        {
            var gst = c.Gstin is not null && IsValidGstin(c.Gstin) ? $"<PARTYGSTIN>{c.Gstin}</PARTYGSTIN><GSTREGISTRATIONTYPE>Regular</GSTREGISTRATIONTYPE>" : "<GSTREGISTRATIONTYPE>Unregistered/Consumer</GSTREGISTRATIONTYPE>";
            Ledger(c.Name, "Sundry Debtors", $"{gst}<LEDSTATENAME>{X(StateOf(c.Gstin, d.ShopGstin))}</LEDSTATENAME><ISBILLWISEON>Yes</ISBILLWISEON>");
        }

        return Envelope("All Masters", body.ToString());
    }

    /// <summary>One sales voucher per bill: the party debited with the total, sales and tax credited, round off to balance.</summary>
    private static string Vouchers(MonthData d)
    {
        var body = new StringBuilder();
        foreach (var b in d.Bills)
        {
            var (taxable, cgst, sgst, igst) = Tax(b);
            var party = Party(b, d);
            var date = b.OccurredAt.ToOffset(IndiaTime.Offset).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var credits = new List<(string Ledger, decimal Amount)> { ("Sales", taxable), ("Output CGST", cgst), ("Output SGST", sgst), ("Output IGST", igst) };
            var roundOff = b.Total - credits.Sum(x => x.Amount);
            body.Append($"<TALLYMESSAGE><VOUCHER VCHTYPE=\"Sales\" ACTION=\"Create\"><GUID>gk-{b.Id}</GUID><DATE>{date}</DATE><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME>")
                .Append($"<VOUCHERNUMBER>{X(b.BillNo)}</VOUCHERNUMBER><PARTYLEDGERNAME>{X(party)}</PARTYLEDGERNAME>")
                .Append(b.BuyerGstin is { } g ? $"<PARTYGSTIN>{g}</PARTYGSTIN>" : "")
                .Append($"<PLACEOFSUPPLY>{X(StateOf(b.BuyerGstin, b.Gstin ?? d.ShopGstin))}</PLACEOFSUPPLY><PERSISTEDVIEW>Accounting Voucher View</PERSISTEDVIEW>")
                .Append($"<ALLLEDGERENTRIES.LIST><LEDGERNAME>{X(party)}</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-{Amount(b.Total)}</AMOUNT></ALLLEDGERENTRIES.LIST>");
            foreach (var (ledger, amount) in credits.Where(x => x.Amount != 0))
            {
                body.Append($"<ALLLEDGERENTRIES.LIST><LEDGERNAME>{ledger}</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>{Amount(amount)}</AMOUNT></ALLLEDGERENTRIES.LIST>");
            }

            if (roundOff != 0)
            {
                body.Append($"<ALLLEDGERENTRIES.LIST><LEDGERNAME>Round Off</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>{Amount(roundOff)}</AMOUNT></ALLLEDGERENTRIES.LIST>");
            }

            body.Append("</VOUCHER></TALLYMESSAGE>\n");
        }

        // Credit notes: the sale turned round. The party is credited; sales and tax are debited.
        foreach (var n in d.Notes)
        {
            var party = n.CustomerId is { } id && d.Customers.TryGetValue(id, out var c) ? c.Name : "Cash";
            var debits = new List<(string Ledger, decimal Amount)> { ("Sales", n.Lines.Sum(l => l.TaxableValue)), ("Output CGST", n.Lines.Sum(l => l.Cgst)), ("Output SGST", n.Lines.Sum(l => l.Sgst)), ("Output IGST", n.Lines.Sum(l => l.Igst)) };
            var roundOff = n.Total - debits.Sum(x => x.Amount);
            body.Append($"<TALLYMESSAGE><VOUCHER VCHTYPE=\"Credit Note\" ACTION=\"Create\"><GUID>gk-{n.Id}</GUID><DATE>{n.OccurredAt.ToOffset(IndiaTime.Offset):yyyyMMdd}</DATE><VOUCHERTYPENAME>Credit Note</VOUCHERTYPENAME>")
                .Append($"<VOUCHERNUMBER>{X(n.NoteNo)}</VOUCHERNUMBER><PARTYLEDGERNAME>{X(party)}</PARTYLEDGERNAME><NARRATION>{X(n.Reason)}</NARRATION><PERSISTEDVIEW>Accounting Voucher View</PERSISTEDVIEW>")
                .Append($"<ALLLEDGERENTRIES.LIST><LEDGERNAME>{X(party)}</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>{Amount(n.Total)}</AMOUNT></ALLLEDGERENTRIES.LIST>");
            foreach (var (ledger, amount) in debits.Where(x => x.Amount != 0))
            {
                body.Append($"<ALLLEDGERENTRIES.LIST><LEDGERNAME>{ledger}</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-{Amount(amount)}</AMOUNT></ALLLEDGERENTRIES.LIST>");
            }

            if (roundOff != 0)
            {
                body.Append($"<ALLLEDGERENTRIES.LIST><LEDGERNAME>Round Off</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-{Amount(roundOff)}</AMOUNT></ALLLEDGERENTRIES.LIST>");
            }

            body.Append("</VOUCHER></TALLYMESSAGE>\n");
        }

        return Envelope("Vouchers", body.ToString());
    }

    /// <summary>The sheet for the CA: every bill, then GST by rate, then by HSN. Opens in Excel.</summary>
    private static string Summary(MonthData d)
    {
        static string C(string? text) => text is null ? "" : text.Contains(',') || text.Contains('"') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
        var csv = new StringBuilder("Bill no,Date,Party,Buyer GSTIN,Type,Status,Total,Taxable,CGST,SGST,IGST\n");
        foreach (var b in d.Bills.Concat(d.Cancelled).OrderBy(b => b.FinancialYear).ThenBy(b => b.Number))
        {
            var (taxable, cgst, sgst, igst) = Tax(b);
            var type = b.Gstin is null ? "No GSTIN (before GST plan)" : b.BillOfSupply ? "Bill of supply" : b.BuyerGstin is not null && IsValidGstin(b.BuyerGstin) ? "B2B" : "B2C";
            csv.AppendJoin(',', b.BillNo, b.OccurredAt.ToOffset(IndiaTime.Offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), C(Party(b, d)), b.BuyerGstin ?? "", type,
                b.CancelledAt is null ? "Active" : "Cancelled", Amount(b.Total), Amount(taxable), Amount(cgst), Amount(sgst), Amount(igst)).Append('\n');
        }

        var taxed = d.Bills.Where(b => b.Gstin != null && !b.BillOfSupply).SelectMany(b => b.Lines).ToList();
        csv.Append("\nGST rate,Taxable,CGST,SGST,IGST\n");
        foreach (var g in taxed.GroupBy(l => l.GstRate).OrderBy(g => g.Key))
        {
            csv.AppendJoin(',', g.Key is { } r ? $"{r:0.##}%" : "Rate not set", Amount(g.Sum(l => LineTax(l).Taxable)), Amount(g.Sum(l => LineTax(l).Cgst)), Amount(g.Sum(l => LineTax(l).Sgst)), Amount(g.Sum(l => LineTax(l).Igst))).Append('\n');
        }

        csv.Append("\nCredit note,Date,Against bill,Party,Reason,Total,Taxable,CGST,SGST,IGST\n");
        foreach (var n in d.Notes)
        {
            var bill = d.Bills.Concat(d.Cancelled).FirstOrDefault(b => b.Id == n.BillId)?.BillNo ?? "(earlier month)";
            var party = n.CustomerId is { } id && d.Customers.TryGetValue(id, out var cust) ? cust.Name : "Cash";
            csv.AppendJoin(',', n.NoteNo, n.OccurredAt.ToOffset(IndiaTime.Offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), bill, C(party), C(n.Reason),
                Amount(n.Total), Amount(n.Lines.Sum(l => l.TaxableValue)), Amount(n.Lines.Sum(l => l.Cgst)), Amount(n.Lines.Sum(l => l.Sgst)), Amount(n.Lines.Sum(l => l.Igst))).Append('\n');
        }

        csv.Append("\nHSN,GST rate,Quantity,Taxable,Total tax\n");
        foreach (var g in taxed.GroupBy(l => (l.HsnCode, l.GstRate)).OrderBy(g => g.Key.HsnCode))
        {
            csv.AppendJoin(',', g.Key.HsnCode ?? "No HSN", g.Key.GstRate is { } r ? $"{r:0.##}%" : "", g.Sum(l => l.Quantity).ToString(CultureInfo.InvariantCulture),
                Amount(g.Sum(l => LineTax(l).Taxable)), Amount(g.Sum(l => { var t = LineTax(l); return t.Cgst + t.Sgst + t.Igst; }))).Append('\n');
        }

        return csv.ToString();
    }
}
