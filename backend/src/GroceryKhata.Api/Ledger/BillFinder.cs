using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>One bill in the search: enough to recognise it before opening it to cancel, return or exchange.</summary>
public sealed record FoundBill(Guid Id, string BillNo, DateTimeOffset OccurredAt, decimal Total, string? CustomerName, string? CustomerPhone,
    int Lines, string Items, bool Cancelled);

/// <summary>
/// Find an old bill when a customer comes back: by bill number, customer name or mobile, or an item on the bill,
/// within a date range. Newest first, at most 50.
/// </summary>
public static class BillFinder
{
    private static readonly TimeSpan India = TimeSpan.FromHours(5.5);

    public static IEndpointRouteBuilder MapBillFinder(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/bills/find", FindAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    private static async Task<IReadOnlyList<FoundBill>> FindAsync(string? q, DateOnly? from, DateOnly? to, AppDbContext db, CancellationToken ct)
    {
        var bills = db.Bills.AsNoTracking().AsQueryable();
        if (from is { } f)
        {
            var start = new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), India).ToUniversalTime();
            bills = bills.Where(b => b.OccurredAt >= start);
        }

        if (to is { } t)
        {
            var end = new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), India).ToUniversalTime();
            bills = bills.Where(b => b.OccurredAt < end);
        }

        var text = q?.Trim() ?? "";
        if (text.Length > 0)
        {
            // "0042", "42" or "2026-27/0042": the bill number. Digits also match a mobile number.
            var slash = text.LastIndexOf('/');
            var numberPart = slash >= 0 ? text[(slash + 1)..] : text;
            int? number = int.TryParse(numberPart, out var n) && n > 0 ? n : null;
            var year = slash > 0 ? text[..slash] : null;
            var like = $"%{text.Replace("%", "").Replace("_", "")}%";
            bills = bills.Where(b =>
                (number != null && b.Number == number && (year == null || b.FinancialYear == year))
                || db.Customers.Any(c => c.Id == b.CustomerId && (EF.Functions.ILike(c.Name, like) || (c.Phone != null && EF.Functions.ILike(c.Phone, like))))
                || b.Lines.Any(l => EF.Functions.ILike(l.Name, like)));
        }

        var found = await bills.OrderByDescending(b => b.OccurredAt).Take(50)
            .Select(b => new
            {
                b.Id, b.FinancialYear, b.Number, b.OccurredAt, b.Total, Cancelled = b.CancelledAt != null,
                Customer = db.Customers.Where(c => c.Id == b.CustomerId).Select(c => new { c.Name, c.Phone }).FirstOrDefault(),
                Lines = b.Lines.Count,
                Names = b.Lines.OrderBy(l => l.LineNo).Select(l => l.Name).Take(3).ToList(),
            }).ToListAsync(ct);
        return found.Select(b => new FoundBill(b.Id, $"{b.FinancialYear}/{b.Number:D4}", b.OccurredAt, b.Total, b.Customer?.Name, b.Customer?.Phone,
            b.Lines, string.Join(", ", b.Names) + (b.Lines > 3 ? $" +{b.Lines - 3}" : ""), b.Cancelled)).ToList();
    }
}
