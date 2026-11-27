using System.Security.Claims;
using System.Security.Cryptography;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A gift voucher the shop sells (₹500, ₹1,000…): its money is taken the day it is sold, and it pays for bills later
/// as a way of paying (payment mode Voucher, its code as the reference) until its balance is used or it expires.
/// Cancelling a bill paid with it puts the amount back on it.
/// </summary>
public sealed class GiftVoucher : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>GV + 10 characters, printed on the voucher and typed or scanned at the counter.</summary>
    public required string Code { get; set; }

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public DateTimeOffset SoldAt { get; set; }

    /// <summary>How the buyer paid for it (cash, UPI or card): its money is counted that day.</summary>
    public PaymentMode SoldMode { get; set; }

    public DateOnly ExpiresOn { get; set; }

    public string? BuyerName { get; set; }

    public Guid SoldByUserId { get; set; }

    /// <summary>Cancelled unused (its money given back that day).</summary>
    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed record SellVoucherRequest(Guid Id, decimal Amount, PaymentMode Mode, int Months = 12, string? BuyerName = null);

public sealed record GiftVoucherDto(Guid Id, string Code, decimal Amount, decimal Balance, DateTimeOffset SoldAt, PaymentMode SoldMode, DateOnly ExpiresOn, string? BuyerName, DateTimeOffset? CancelledAt);

public static class GiftVouchers
{
    // No 0/O or 1/I: read out loud and typed without mistakes.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static IEndpointRouteBuilder MapGiftVouchers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/gift-vouchers").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", async (AppDbContext db, CancellationToken ct) =>
            (await db.GiftVouchers.AsNoTracking().OrderByDescending(v => v.SoldAt).Take(200).ToListAsync(ct)).Select(Dto).ToList());
        group.MapGet("/{code}", async (string code, AppDbContext db, CancellationToken ct) =>
            await db.GiftVouchers.AsNoTracking().SingleOrDefaultAsync(v => v.Code == Clean(code), ct) is { } v ? TypedResults.Ok(Dto(v)) : (IResult)TypedResults.NotFound());
        group.MapPost("", SellAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    public static GiftVoucherDto Dto(GiftVoucher v) => new(v.Id, v.Code, v.Amount, v.Balance, v.SoldAt, v.SoldMode, v.ExpiresOn, v.BuyerName, v.CancelledAt);

    /// <summary>The code as kept: capitals, no spaces or dashes.</summary>
    public static string Clean(string? code) => new((code ?? "").Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static async Task<IResult> SellAsync(SellVoucherRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.GiftVouchers.AsNoTracking().SingleOrDefaultAsync(v => v.Id == request.Id, ct) is { } same)
        {
            return TypedResults.Ok(Dto(same));
        }

        if (request.Id == Guid.Empty || !Money.IsValidAmount(request.Amount) || request.Amount > 100_000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = ["A voucher is from ₹1 to ₹1 lakh."] });
        }

        if (request.Mode is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Card) || request.Months is < 1 or > 36 || request.BuyerName?.Trim().Length > 80)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Mode"] = ["Paid by cash, UPI or card; valid for 1 to 36 months."] });
        }

        var now = clock.GetUtcNow();
        var voucher = new GiftVoucher
        {
            Id = request.Id, Code = "GV" + new string(RandomNumberGenerator.GetItems<char>(Alphabet, 10)), Amount = request.Amount, Balance = request.Amount,
            SoldAt = now, SoldMode = request.Mode, ExpiresOn = IndiaTime.Today(clock).AddMonths(request.Months),
            BuyerName = string.IsNullOrWhiteSpace(request.BuyerName) ? null : request.BuyerName.Trim(), SoldByUserId = user.UserId()!.Value,
        };
        db.GiftVouchers.Add(voucher);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Dto(voucher));
    }

    /// <summary>Cancels a voucher nothing was spent from: its money goes back to the buyer today.</summary>
    private static async Task<IResult> CancelAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var voucher = await db.GiftVouchers.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (voucher is null)
        {
            return TypedResults.NotFound();
        }

        if (voucher.CancelledAt is null && voucher.Balance != voucher.Amount)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Part of this voucher was already spent: it can't be cancelled.");
        }

        voucher.CancelledAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Dto(voucher));
    }

    /// <summary>
    /// Takes a bill's voucher parts off their vouchers, inside the bill's own transaction: each must be live, unexpired
    /// and hold enough (one statement each, so two tills can't spend the same balance). Null: all taken; else the problem.
    /// </summary>
    public static async Task<string?> SpendAsync(AppDbContext db, IEnumerable<BillPaymentPart> parts, DateOnly today, CancellationToken ct)
    {
        foreach (var part in parts.Where(p => p.Mode == PaymentMode.Voucher))
        {
            var code = Clean(part.Reference);
            var taken = await db.GiftVouchers.Where(v => v.Code == code && v.CancelledAt == null && v.ExpiresOn >= today && v.Balance >= part.Amount)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Balance, v => v.Balance - part.Amount), ct);
            if (taken == 0)
            {
                return $"Gift voucher {code} isn't valid here, has expired, or holds less than ₹{part.Amount:0.##}.";
            }
        }

        return null;
    }

    /// <summary>A cancelled bill's voucher payments go back on their vouchers.</summary>
    public static async Task RefundAsync(AppDbContext db, IEnumerable<BillPaymentPart> parts, CancellationToken ct)
    {
        foreach (var part in parts.Where(p => p.Mode == PaymentMode.Voucher))
        {
            var code = Clean(part.Reference);
            await db.GiftVouchers.Where(v => v.Code == code).ExecuteUpdateAsync(s => s.SetProperty(v => v.Balance, v => v.Balance + part.Amount), ct);
        }
    }
}
