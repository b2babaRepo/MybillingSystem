using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One unit of a serialised item (a pump, an inverter, a battery) by its serial number, from the day it comes in to the
/// day it is sold. Companies reject warranty claims when the bill and the serial don't match, so the serial is kept on
/// the bill and the warranty runs from the sale date.
/// </summary>
public sealed class ProductSerial : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid ProductId { get; set; }

    public required string SerialNo { get; set; }

    public SerialStatus Status { get; set; }

    public DateOnly ReceivedOn { get; set; }

    public Guid? BillId { get; set; }

    public Guid? CustomerId { get; set; }

    public DateOnly? SoldOn { get; set; }

    public DateOnly? WarrantyUntil { get; set; }

    /// <summary>The warranty card or online registration was done with the company (many ask within days of sale).</summary>
    public bool WarrantyRegistered { get; set; }
}

public enum SerialStatus
{
    InStock,
    Sold,
}

public sealed record SerialReceiveRequest(Guid ProductId, IReadOnlyList<string>? Serials);

public sealed record SerialDto(
    Guid Id, Guid ProductId, string ProductName, string SerialNo, SerialStatus Status, DateOnly ReceivedOn, Guid? BillId, string? BillNo, string? CustomerName,
    DateOnly? SoldOn, DateOnly? WarrantyUntil, bool InWarranty, bool WarrantyRegistered);

/// <summary>A bill line's unit by serial: the counter picks which unit of a serialised item is going out.</summary>
public sealed record BillSerialRequest(Guid ProductId, string? SerialNo);

public static class Serials
{
    public static IEndpointRouteBuilder MapSerials(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/serials").WithTags("Products").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("/receive", ReceiveAsync);
        group.MapGet("/lookup", LookupAsync);
        group.MapPost("/{id:guid}/registered", RegisteredAsync);
        return app;
    }

    /// <summary>Serial numbers as printed: trimmed, upper-case, no inner spaces.</summary>
    public static string Clean(string serial) => string.Concat(serial.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private static async Task<List<SerialDto>> ToDtosAsync(AppDbContext db, IQueryable<ProductSerial> query, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var rows = await query.AsNoTracking().OrderBy(s => s.Status).ThenByDescending(s => s.ReceivedOn).ThenBy(s => s.SerialNo).Take(500).ToListAsync(ct);
        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var billIds = rows.Where(r => r.BillId != null).Select(r => r.BillId!.Value).ToList();
        var bills = await db.Bills.AsNoTracking().Where(b => billIds.Contains(b.Id)).Select(b => new { b.Id, b.Prefix, b.FinancialYear, b.Number }).ToDictionaryAsync(b => b.Id, b => Ledger.Bill.No(b.Prefix, b.FinancialYear, b.Number), ct);
        var customerIds = rows.Where(r => r.CustomerId != null).Select(r => r.CustomerId!.Value).ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return rows.Select(s => new SerialDto(s.Id, s.ProductId, products.GetValueOrDefault(s.ProductId) ?? "", s.SerialNo, s.Status, s.ReceivedOn, s.BillId,
            s.BillId is { } b ? bills.GetValueOrDefault(b) : null, s.CustomerId is { } c ? customers.GetValueOrDefault(c) : null, s.SoldOn, s.WarrantyUntil,
            s.WarrantyUntil is { } until && until >= today, s.WarrantyRegistered)).ToList();
    }

    /// <summary>A product's units (or every unit), in stock first.</summary>
    private static async Task<IReadOnlyList<SerialDto>> ListAsync(Guid? productId, string? status, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var query = db.ProductSerials.AsQueryable();
        if (productId is { } p) query = query.Where(s => s.ProductId == p);
        if (FromQuery<SerialStatus>(status) is { } st) query = query.Where(s => s.Status == st);
        return await ToDtosAsync(db, query, clock, ct);
    }

    /// <summary>
    /// Serial numbers of units that came in (from the company's bill, or scanned off the boxes). Doesn't move stock: the
    /// purchase bill or stock entry does that. A serial already known for this item is skipped.
    /// </summary>
    private static async Task<IResult> ReceiveAsync(SerialReceiveRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, ct);
        if (product is null) return TypedResults.NotFound();
        if (!product.TracksSerials) return Invalid("ProductId", "Turn on serial numbers for this item first.");
        var serials = (request.Serials ?? []).Select(Clean).Where(s => s.Length > 0).Distinct().ToList();
        if (serials.Count is 0 or > 500) return Invalid("Serials", "Enter 1 to 500 serial numbers.");
        if (serials.Any(s => s.Length > 40)) return Invalid("Serials", "A serial number is at most 40 letters and digits.");
        var known = (await db.ProductSerials.Where(s => s.ProductId == request.ProductId && serials.Contains(s.SerialNo)).Select(s => s.SerialNo).ToListAsync(ct)).ToHashSet();
        var today = IndiaTime.Today(clock);
        db.ProductSerials.AddRange(serials.Where(s => !known.Contains(s)).Select(s => new ProductSerial { ProductId = request.ProductId, SerialNo = s, Status = SerialStatus.InStock, ReceivedOn = today }));
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new { added = serials.Count - known.Count, skipped = known.Count });
    }

    /// <summary>Any unit by its serial: what it is, who bought it on which bill, and whether it's still in warranty.</summary>
    private static async Task<IResult> LookupAsync(string? serial, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var s = Clean(serial ?? "");
        if (s.Length is 0 or > 40) return Invalid("serial", "Enter the serial number.");
        var found = await ToDtosAsync(db, db.ProductSerials.Where(x => x.SerialNo == s), clock, ct);
        return found.Count == 0 ? TypedResults.NotFound() : TypedResults.Ok(found);
    }

    private static async Task<IResult> RegisteredAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var s = await db.ProductSerials.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return TypedResults.NotFound();
        if (s.Status != SerialStatus.Sold) return Invalid("Status", "Register the warranty after the unit is sold.");
        s.WarrantyRegistered = true;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, db.ProductSerials.Where(x => x.Id == id), clock, ct)).Single());
    }

    /// <summary>
    /// Checks a bill's serials before it is saved: every serialised line needs one in-stock serial per unit (whole
    /// units only). Returns an error, or nothing.
    /// </summary>
    public static async Task<string?> CheckAsync(AppDbContext db, IReadOnlyList<BillLine> lines, IReadOnlyDictionary<Guid, Product> products, IReadOnlyList<BillSerialRequest>? serials, CancellationToken ct)
    {
        var picked = (serials ?? []).Where(s => !string.IsNullOrWhiteSpace(s.SerialNo)).Select(s => (s.ProductId, Serial: Clean(s.SerialNo!))).ToList();
        if (picked.Select(p => p.Serial).Distinct().Count() != picked.Count)
        {
            return "The same serial number is on the bill twice.";
        }

        foreach (var g in lines.Where(l => l.ProductId is { } id && products.TryGetValue(id, out var p) && p.TracksSerials).GroupBy(l => l.ProductId!.Value))
        {
            var quantity = g.Sum(l => l.Quantity);
            var name = products[g.Key].Name;
            if (decimal.Truncate(quantity) != quantity) return $"{name}: sold by the unit, so the quantity must be whole.";
            var mine = picked.Where(p => p.ProductId == g.Key).Select(p => p.Serial).ToList();
            if (mine.Count != quantity) return $"{name}: pick {quantity:0} serial number(s), one for each unit going out.";
            var inStock = await db.ProductSerials.CountAsync(s => s.ProductId == g.Key && s.Status == SerialStatus.InStock && mine.Contains(s.SerialNo), ct);
            if (inStock != mine.Count) return $"{name}: a serial number isn't in stock (already sold, or not received).";
        }

        return picked.Any(p => !lines.Any(l => l.ProductId == p.ProductId)) ? "A serial number is for an item that isn't on the bill." : null;
    }

    /// <summary>Marks the bill's serials sold (warranty from today) and writes them on their lines. In the bill's transaction.</summary>
    public static async Task SellAsync(AppDbContext db, Guid billId, Guid? customerId, IReadOnlyList<BillLine> lines, IReadOnlyDictionary<Guid, Product> products, IReadOnlyList<BillSerialRequest>? serials, DateOnly today, CancellationToken ct)
    {
        var picked = (serials ?? []).Where(s => !string.IsNullOrWhiteSpace(s.SerialNo)).Select(s => (s.ProductId, Serial: Clean(s.SerialNo!))).ToList();
        if (picked.Count == 0)
        {
            return;
        }

        var all = picked.Select(p => p.Serial).ToList();
        var units = await db.ProductSerials.Where(s => all.Contains(s.SerialNo) && s.Status == SerialStatus.InStock).ToListAsync(ct);
        foreach (var g in picked.GroupBy(p => p.ProductId))
        {
            var months = products[g.Key].WarrantyMonths;
            var until = months is { } m ? today.AddMonths(m).AddDays(-1) : (DateOnly?)null;
            foreach (var u in units.Where(u => u.ProductId == g.Key && g.Any(p => p.Serial == u.SerialNo)))
            {
                (u.Status, u.BillId, u.CustomerId, u.SoldOn, u.WarrantyUntil) = (SerialStatus.Sold, billId, customerId, today, until);
            }

            var line = lines.First(l => l.ProductId == g.Key);
            var label = string.Join(", ", g.Select(p => p.Serial)) + (until is { } w ? $" · warranty till {w:dd MMM yyyy}" : "");
            line.Batch = (line.Batch is null ? $"S/N {label}" : $"{line.Batch}; S/N {label}") is var text && text.Length > 200 ? text[..199] + "…" : text;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>A cancelled bill's units go back in stock, their warranty cleared.</summary>
    public static Task RestoreAsync(AppDbContext db, Guid billId, CancellationToken ct) =>
        db.ProductSerials.Where(s => s.BillId == billId).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.Status, SerialStatus.InStock).SetProperty(s => s.BillId, (Guid?)null).SetProperty(s => s.CustomerId, (Guid?)null)
            .SetProperty(s => s.SoldOn, (DateOnly?)null).SetProperty(s => s.WarrantyUntil, (DateOnly?)null).SetProperty(s => s.WarrantyRegistered, false), ct);
}
