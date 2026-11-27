using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Old gold or silver bought from someone without a sale (URD purchase): weighed, tested and valued at the day's
/// buying rate, paid out in cash, UPI or bank. The metal joins the shop's loose metal like old metal taken on a bill.
/// </summary>
public sealed class OldMetalVoucher : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>Voucher number, 1, 2, 3… per shop.</summary>
    public int Number { get; set; }

    public Guid? CustomerId { get; set; }

    public required string SellerName { get; set; }

    public string? SellerPhone { get; set; }

    public string? SellerAddress { get; set; }

    /// <summary>PAN or "FORM 60": needed from ₹2 lakh (Rule 114B, sale or purchase of jewellery).</summary>
    public string? Pan { get; set; }

    public PaymentMode Mode { get; set; }

    public decimal Total { get; set; }

    public Guid ExpenseId { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record OldMetalVoucherRequest(
    Guid Id, Guid? CustomerId, string? SellerName, string? SellerPhone, string? SellerAddress, string? Pan, PaymentMode? Mode, IReadOnlyList<OldMetalRequest>? OldMetal);

public sealed record OldMetalVoucherDto(
    Guid Id, int Number, Guid? CustomerId, string SellerName, string? SellerPhone, string? SellerAddress, string? Pan, PaymentMode Mode, decimal Total,
    IReadOnlyList<OldMetalDto> OldMetal, IReadOnlyList<string> Problems, DateTimeOffset CreatedAt);

public static class OldMetalVoucherEndpoints
{
    /// <summary>Cash paid to one person in a day above ₹10,000 is not allowed as a business expense (Income Tax s.40A(3)).</summary>
    public const decimal CashPaymentLimit = 10_000m;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/old-metal-vouchers", ListAsync);
        group.MapGet("/old-metal-vouchers/{id:guid}", GetAsync);
        group.MapPost("/old-metal-vouchers", BuyAsync);
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static string[] ProblemsFor(PaymentMode mode, decimal total, decimal limit = CashPaymentLimit) =>
        mode == PaymentMode.Cash && total > limit
            ? [$"Paying ₹{total:N0} in cash: cash payments above ₹{limit:N0} to one person in a day are not allowed as a business expense (s.40A(3)). Pay by UPI or bank."]
            : [];

    private static async Task<IResult> BuyAsync(OldMetalVoucherRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.OldMetalVouchers.AsNoTracking().AnyAsync(v => v.Id == request.Id, ct)) return await GetAsync(request.Id, db, ct);
        if (request.OldMetal is not { Count: > 0 and <= 20 }) return Invalid("OldMetal", "Add between 1 and 20 old pieces.");
        var mode = request.Mode ?? PaymentMode.Cash;
        if (mode is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Bank)) return Invalid("Mode", "Pay by cash, UPI or bank.");

        string? name = request.SellerName?.Trim();
        string? phone = request.SellerPhone?.Trim();
        if (request.CustomerId is { } cid)
        {
            var customer = await db.Customers.AsNoTracking().Where(c => c.Id == cid).Select(c => new { c.Name, c.Phone }).SingleOrDefaultAsync(ct);
            if (customer is null) return Invalid("CustomerId", "That customer was not found.");
            name = string.IsNullOrEmpty(name) ? customer.Name : name;
            phone = string.IsNullOrEmpty(phone) ? customer.Phone : phone;
        }

        if (string.IsNullOrEmpty(name) || name.Length > 80) return Invalid("SellerName", "Enter the seller's name.");
        if (phone is { Length: > 20 }) return Invalid("SellerPhone", "Enter a shorter phone number.");
        var address = request.SellerAddress?.Trim();
        if (address is { Length: > 200 }) return Invalid("SellerAddress", "Keep the address under 200 letters.");

        var rates = (await JewelleryEndpoints.CurrentRatesAsync(db, clock, ct)).Current.ToDictionary(r => (r.Metal, r.Purity));
        var (olds, problem) = JewelleryEndpoints.ValueOld(request.OldMetal, rates);
        if (olds is null) return Invalid("OldMetal", problem!);
        var total = olds.Sum(o => o.Value);
        if (total <= 0) return Invalid("OldMetal", "The old metal comes to nothing: check the weight and test reading.");

        var pan = request.Pan?.Trim().ToUpperInvariant();
        if (pan is { Length: > 0 } && !JewelleryEndpoints.PanPattern.IsMatch(pan)) return Invalid("Pan", "Enter a PAN like ABCDE1234F, or FORM 60.");
        var rules = await JewelleryMath.RulesAsync(db, ct);
        if (total >= rules.PanFrom && string.IsNullOrEmpty(pan)) return Invalid("Pan", $"This purchase is ₹{rules.PanFrom:N0} or more: enter the seller's PAN (or FORM 60).");

        var userId = user.UserId()!.Value;
        var number = await db.NextNumberAsync<OldMetalVoucher>(ct);
        var note = $"Old metal bought, voucher {number} from {name}: {string.Join(", ", olds.Select(o => $"{o.Gross:0.000} g {o.Metal} at {o.Reading:0.##}%"))}";
        var expense = new Expense { Amount = total, Category = ExpenseCategory.Purchase, Mode = mode, Note = note.Length > 200 ? note[..200] : note, CreatedByUserId = userId, OccurredAt = clock.GetUtcNow() };
        db.Expenses.Add(expense);
        var voucher = new OldMetalVoucher
        {
            Id = request.Id, Number = number, CustomerId = request.CustomerId, SellerName = name, SellerPhone = string.IsNullOrEmpty(phone) ? null : phone,
            SellerAddress = string.IsNullOrEmpty(address) ? null : address, Pan = string.IsNullOrEmpty(pan) ? null : pan, Mode = mode, Total = total, ExpenseId = expense.Id, CreatedByUserId = userId,
        };
        db.OldMetalVouchers.Add(voucher);
        foreach (var o in olds)
        {
            db.OldMetalPurchases.Add(new OldMetalPurchase { VoucherId = voucher.Id, CustomerId = request.CustomerId, Description = o.Description, Metal = o.Metal, Gross = o.Gross, Reading = o.Reading, Fine = o.Fine, RatePerGram = o.RatePerGram, DeductionPercent = o.DeductionPercent, Value = o.Value });
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(voucher.Id, db, ct);
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var v = await db.OldMetalVouchers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return TypedResults.NotFound();
        var olds = await db.OldMetalPurchases.AsNoTracking().Where(o => o.VoucherId == id).OrderBy(o => o.CreatedAt).ToListAsync(ct);
        return TypedResults.Ok(ToDto(v, olds, (await JewelleryMath.RulesAsync(db, ct)).CashPayoutLimit));
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var vouchers = await db.OldMetalVouchers.AsNoTracking().OrderByDescending(v => v.Number).Take(100).ToListAsync(ct);
        var ids = vouchers.Select(v => v.Id).ToList();
        var olds = (await db.OldMetalPurchases.AsNoTracking().Where(o => o.VoucherId != null && ids.Contains(o.VoucherId.Value)).ToListAsync(ct)).ToLookup(o => o.VoucherId);
        var limit = (await JewelleryMath.RulesAsync(db, ct)).CashPayoutLimit;
        return TypedResults.Ok(vouchers.Select(v => ToDto(v, olds[v.Id].OrderBy(o => o.CreatedAt).ToList(), limit)).ToList());
    }

    private static OldMetalVoucherDto ToDto(OldMetalVoucher v, List<OldMetalPurchase> olds, decimal cashLimit) => new(
        v.Id, v.Number, v.CustomerId, v.SellerName, v.SellerPhone, v.SellerAddress, v.Pan, v.Mode, v.Total,
        olds.Select(o => new OldMetalDto(o.Description, o.Metal, o.Gross, o.Reading, o.Fine, o.RatePerGram, o.DeductionPercent, decimal.Round(o.Fine * o.RatePerGram, 2) - o.Value, o.Value)).ToList(),
        ProblemsFor(v.Mode, v.Total, cashLimit), v.CreatedAt);
}
