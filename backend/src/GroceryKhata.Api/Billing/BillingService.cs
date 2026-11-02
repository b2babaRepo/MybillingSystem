using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Billing;

/// <summary>The price for a plan and term: the plan's own, a locked 5-year rate, or an offer's.</summary>
public sealed record Quote(string PlanCode, PlanTerm Term, decimal Amount, string? OfferCode, string? Error);

/// <summary>Prices, payments, refunds and the referral reward. Used by the shop's plan screen and the admin screens.</summary>
public sealed class BillingService(AppDbContext db, Entitlements entitlements, TimeProvider clock, ILogger<BillingService> logger)
{
    public async Task<Quote> QuoteAsync(Guid shopId, string planCode, PlanTerm term, string? offerCode, CancellationToken ct)
    {
        var plan = await db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.Code == planCode && p.IsActive, ct);
        if (plan is null || plan.Code == "free" || plan.PriceFor(term) is not { } price)
        {
            return new Quote(planCode, term, 0, null, "This plan isn't sold for that period.");
        }

        var subscription = await entitlements.FindOrNewAsync(shopId, ct);
        var now = clock.GetUtcNow();
        if (term == PlanTerm.Year5 && subscription.PriceLockUntil > now && subscription.PlanCode == planCode && subscription.LockedYearlyPrice is { } locked)
        {
            price = locked;
        }

        if (string.IsNullOrWhiteSpace(offerCode))
        {
            return new Quote(planCode, term, price, null, null);
        }

        var code = offerCode.Trim().ToUpperInvariant();
        var offer = await db.Offers.AsNoTracking().SingleOrDefaultAsync(o => o.Code == code && o.IsActive, ct);
        // A refunded first payment still used the first-payment offer.
        var everPaid = await db.SubscriptionPayments.AnyAsync(p => p.ShopId == shopId, ct);
        var error = offer switch
        {
            null => "This offer code isn't valid.",
            _ when offer.PlanCode != planCode || offer.Term != term => $"This offer is for {offer.Title}.",
            _ when offer.ValidUntil < now => "This offer has ended.",
            _ when offer.MaxUses <= offer.Uses => "This offer has been fully used.",
            _ when offer.NewShopsOnly && everPaid => "This offer is for a shop's first payment.",
            _ => null,
        };
        return error is null ? new Quote(planCode, term, Math.Min(price, offer!.Price), code, null) : new Quote(planCode, term, price, null, error);
    }

    /// <summary>
    /// Records money received and moves the paid-until date on: from today, or from the end of the current paid period
    /// when renewing early (nothing is lost). The first payment of a referred shop gives both shops a free month.
    /// </summary>
    public async Task<SubscriptionPayment> RecordAsync(
        Guid shopId, Quote quote, PaymentMethod method, string? reference, Guid? recordedBy, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var subscription = await entitlements.FindOrNewAsync(shopId, ct, track: true);
        // Paying early loses nothing: the paid time starts after what's left of the plan or the trial.
        var start = new[] { subscription.PaidUntil ?? now, subscription.TrialEndsAt, now }.Max();
        var end = quote.Term switch
        {
            PlanTerm.Monthly => start.AddMonths(1),
            PlanTerm.Year3 => start.AddYears(3),
            _ => start.AddYears(1), // a year, and each year of a 5-year lock
        };

        if (quote.Term == PlanTerm.Year5 && !(subscription.PriceLockUntil > now))
        {
            subscription.PriceLockUntil = now.AddYears(5);
            subscription.LockedYearlyPrice = quote.Amount;
        }

        var firstPayment = !await db.SubscriptionPayments.AnyAsync(p => p.ShopId == shopId, ct);
        subscription.PlanCode = quote.PlanCode;
        subscription.Term = quote.Term;
        subscription.PaidUntil = end;
        (subscription.ChosenPlanCode, subscription.ChosenTerm) = (null, null);
        var payment = new SubscriptionPayment
        {
            ShopId = shopId, PlanCode = quote.PlanCode, Term = quote.Term, Amount = quote.Amount, Method = method, Reference = reference,
            OfferCode = quote.OfferCode, PaidAt = now, PeriodStart = start, PeriodEnd = end, RecordedByUserId = recordedBy,
            InvoiceNo = await NextInvoiceNoAsync(now, ct),
        };
        db.SubscriptionPayments.Add(payment);
        if (quote.OfferCode is { } code)
        {
            await db.Offers.Where(o => o.Code == code).ExecuteUpdateAsync(s => s.SetProperty(o => o.Uses, o => o.Uses + 1), ct);
        }

        if (firstPayment && subscription.ReferredByShopId is { } referrer && !subscription.ReferralRewarded)
        {
            subscription.ReferralRewarded = true;
            subscription.PaidUntil = subscription.PaidUntil.Value.AddDays(entitlements.Options.ReferralBonusDays);
            await GiveBonusAsync(referrer, now, ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Shop {ShopId} paid {Amount} for {Plan} {Term} ({Method})", shopId, quote.Amount, quote.PlanCode, quote.Term, method);
        return payment;
    }

    /// <summary>
    /// Refunds a 1- or 3-year payment within the refund window: money back, and the paid period it bought is taken off.
    /// Monthly payments aren't refunded. Returns an error to show, or null.
    /// </summary>
    public async Task<string?> RefundAsync(Guid paymentId, CancellationToken ct)
    {
        var payment = await db.SubscriptionPayments.SingleOrDefaultAsync(p => p.Id == paymentId, ct);
        var now = clock.GetUtcNow();
        if (payment is null || payment.RefundedAt is not null)
        {
            return "Payment not found, or already refunded.";
        }

        if (payment.Term == PlanTerm.Monthly)
        {
            return "Monthly payments aren't refunded.";
        }

        if (payment.PaidAt.AddDays(entitlements.Options.RefundDays) < now)
        {
            return $"Refunds are within {entitlements.Options.RefundDays} days of paying.";
        }

        payment.RefundedAt = now;
        var subscription = await db.Subscriptions.SingleAsync(s => s.ShopId == payment.ShopId, ct);
        subscription.PaidUntil = subscription.PaidUntil - (payment.PeriodEnd - payment.PeriodStart);
        // The first payment earned both shops a referral month: a refund takes it back from both.
        var first = await db.SubscriptionPayments.Where(p => p.ShopId == payment.ShopId).OrderBy(p => p.PaidAt).Select(p => p.Id).FirstAsync(ct);
        if (first == payment.Id && subscription.ReferralRewarded && subscription.ReferredByShopId is { } referrer)
        {
            var days = entitlements.Options.ReferralBonusDays;
            subscription.PaidUntil = subscription.PaidUntil?.AddDays(-days);
            subscription.ReferralRewarded = false;
            await TakeBonusAsync(referrer, days, now, ct);
        }

        // A payment made during the trial started after it: taking it back leaves the trial, not paid days.
        if (subscription.PaidUntil <= subscription.TrialEndsAt && subscription.TrialEndsAt > now)
        {
            subscription.PaidUntil = null;
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>A free month for the shop that brought a new paying shop: on its paid period, else as a free Basic month.</summary>
    private async Task GiveBonusAsync(Guid shopId, DateTimeOffset now, CancellationToken ct)
    {
        var subscription = await entitlements.FindOrNewAsync(shopId, ct, track: true);
        var days = entitlements.Options.ReferralBonusDays;
        if (subscription.PaidUntil > now)
        {
            subscription.PaidUntil = subscription.PaidUntil.Value.AddDays(days);
        }
        else
        {
            subscription.CompPlanCode ??= "basic";
            subscription.CompUntil = (subscription.CompUntil > now ? subscription.CompUntil.Value : now).AddDays(days);
            subscription.CompReason ??= "Referral reward";
        }
    }

    /// <summary>Takes back a referral month: from the paid period it was added to, else from the free month given.</summary>
    private async Task TakeBonusAsync(Guid shopId, int days, DateTimeOffset now, CancellationToken ct)
    {
        var subscription = await entitlements.FindOrNewAsync(shopId, ct, track: true);
        if (subscription.PaidUntil > now)
        {
            subscription.PaidUntil = subscription.PaidUntil.Value.AddDays(-days);
        }
        else if (subscription.CompReason == "Referral reward" && subscription.CompUntil is { } until)
        {
            subscription.CompUntil = until.AddDays(-days);
        }
    }

    /// <summary>GK/2026-27/0001: numbered within India's financial year (April to March).</summary>
    private async Task<string> NextInvoiceNoAsync(DateTimeOffset now, CancellationToken ct)
    {
        var india = now.ToOffset(TimeSpan.FromHours(5.5));
        var startYear = india.Month >= 4 ? india.Year : india.Year - 1;
        var prefix = $"GK/{startYear}-{(startYear + 1) % 100:D2}/";
        var count = await db.SubscriptionPayments.CountAsync(p => p.InvoiceNo.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }
}
