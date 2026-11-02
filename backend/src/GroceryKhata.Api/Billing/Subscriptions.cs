using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Billing;

/// <summary>
/// A plan shops can be on. Prices per term (null: that term isn't sold), the features it opens and its limits.
/// Super admins edit plans; a shop's payment records the price it paid, so editing a plan never changes past bills.
/// </summary>
public sealed class Plan : Entity
{
    /// <summary>"free", "basic", "silver", "pro" (shown as Gold), or one an admin added.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public int SortOrder { get; set; }

    public decimal? PriceMonthly { get; set; }

    public decimal? PriceYear1 { get; set; }

    public decimal? PriceYear3 { get; set; }

    /// <summary>The 5-year price: billed one year at a time (a fifth each year), the rate locked for five years.</summary>
    public decimal? PriceYear5 { get; set; }

    /// <summary>Feature keys this plan opens (see <see cref="Features"/>).</summary>
    public string[] FeatureKeys { get; set; } = [];

    /// <summary>Null: no limit.</summary>
    public int? MaxCustomers { get; set; }

    /// <summary>Helper logins (each with a PIN).</summary>
    public int MaxStaff { get; set; }

    /// <summary>Owners: people with full access, signing in with their number.</summary>
    public int MaxOwners { get; set; } = 1;

    public int WhatsAppPerMonth { get; set; }

    public bool IsActive { get; set; } = true;

    public decimal? PriceFor(PlanTerm term) => term switch
    {
        PlanTerm.Monthly => PriceMonthly,
        PlanTerm.Year1 => PriceYear1,
        PlanTerm.Year3 => PriceYear3,
        PlanTerm.Year5 => PriceYear5 is { } five ? decimal.Round(five / 5, 0) : null,
        _ => null,
    };
}

public enum PlanTerm
{
    Monthly,
    Year1,
    Year3,
    /// <summary>A five-year price lock, paid a year at a time.</summary>
    Year5,
}

/// <summary>Feature keys plans open. Anything not here is in every plan, including Free.</summary>
public static class Features
{
    public const string Reports = "reports";
    public const string Store = "store";
    public const string WhatsApp = "whatsapp";
    public const string Staff = "staff";
    public const string Ai = "ai";
    public const string Trade = "trade";
    public const string BillPhoto = "bill_photo";
    public const string Gst = "gst";
    /// <summary>Tally files and the GST summary for the CA.</summary>
    public const string GstExport = "gst_export";

    public static readonly string[] All = [Reports, Store, WhatsApp, Staff, Ai, Trade, BillPhoto, Gst, GstExport];
}

/// <summary>
/// A shop's subscription: dates, not a stored status, so it is always right without a nightly job (see
/// <see cref="Entitlements"/>). One row per shop, made when first needed; until then the shop is on its free trial.
/// </summary>
public sealed class Subscription : Entity
{
    public Guid ShopId { get; set; }

    /// <summary>The plan paid for ("basic", "pro"); null until the first payment.</summary>
    public string? PlanCode { get; set; }

    public PlanTerm? Term { get; set; }

    public DateTimeOffset TrialEndsAt { get; set; }

    /// <summary>The plan and term the owner or agent picked, not yet paid: My plan opens on it. Cleared by the payment.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string? ChosenPlanCode { get; set; }

    public PlanTerm? ChosenTerm { get; set; }

    /// <summary>Why an admin last moved this shop's plan: a move takes no money, so it must say why.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? MoveReason { get; set; }

    /// <summary>Paid up to this moment. Null: never paid.</summary>
    public DateTimeOffset? PaidUntil { get; set; }

    /// <summary>The 5-year rate holds until then.</summary>
    public DateTimeOffset? PriceLockUntil { get; set; }

    public decimal? LockedYearlyPrice { get; set; }

    /// <summary>Complimentary (free) access given by an admin: this plan until <see cref="CompUntil"/> (null: for life).</summary>
    public string? CompPlanCode { get; set; }

    public DateTimeOffset? CompUntil { get; set; }

    public string? CompReason { get; set; }

    public Guid? CompByUserId { get; set; }

    /// <summary>On hold: Free features only (the shop's own data and its customers' khata stay open), with the reason.</summary>
    public DateTimeOffset? HoldSince { get; set; }

    public string? HoldReason { get; set; }

    /// <summary>The shop that brought this one (both get a free month at this shop's first payment).</summary>
    public Guid? ReferredByShopId { get; set; }

    public bool ReferralRewarded { get; set; }
}

public enum PaymentMethod
{
    Upi,
    Cash,
    Gateway,
    Bank,
}

/// <summary>Money received for a subscription, with its own GST invoice number. Refunded payments stay, marked.</summary>
public sealed class SubscriptionPayment : Entity
{
    public Guid ShopId { get; set; }

    public required string PlanCode { get; set; }

    public PlanTerm Term { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>UPI reference (UTR), receipt number or gateway ID.</summary>
    public string? Reference { get; set; }

    public string? OfferCode { get; set; }

    public DateTimeOffset PaidAt { get; set; }

    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }

    public required string InvoiceNo { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public DateTimeOffset? RefundedAt { get; set; }
}

public enum ClaimStatus
{
    Pending,
    Confirmed,
    Rejected,
}

/// <summary>
/// "I paid by UPI": the shop pays the platform's UPI ID and sends the reference. A billing admin checks the bank
/// statement and confirms (which records the payment) or rejects it. Nothing changes until confirmed.
/// </summary>
public sealed class PaymentClaim : Entity
{
    public Guid ShopId { get; set; }

    public required string PlanCode { get; set; }

    public PlanTerm Term { get; set; }

    public decimal Amount { get; set; }

    public required string UpiRef { get; set; }

    public string? OfferCode { get; set; }

    public ClaimStatus Status { get; set; }

    public Guid ClaimedByUserId { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public string? Note { get; set; }
}

/// <summary>An offer code: a special price for a plan and term ("WELCOME299": Basic's first year at ₹299, new shops only).</summary>
public sealed class Offer : Entity
{
    public required string Code { get; set; }

    public required string Title { get; set; }

    public required string PlanCode { get; set; }

    public PlanTerm Term { get; set; }

    public decimal Price { get; set; }

    /// <summary>Only for shops that have never paid.</summary>
    public bool NewShopsOnly { get; set; }

    public DateTimeOffset? ValidUntil { get; set; }

    public int? MaxUses { get; set; }

    public int Uses { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Where a shop stands right now, worked out from its dates.</summary>
public enum SubscriptionStatus
{
    /// <summary>The first 30 days: Pro, free.</summary>
    Trial,
    Active,
    /// <summary>Up to 7 days after the paid period: everything still works, reminders go out.</summary>
    Grace,
    Complimentary,
    OnHold,
    /// <summary>Not paying: Free plan features (which include all of the shop's own data).</summary>
    Free,
}

public sealed record Entitlement(
    SubscriptionStatus Status, string PlanCode, string PlanName, IReadOnlySet<string> Features, int? MaxCustomers, int MaxStaff, int WhatsAppPerMonth,
    DateTimeOffset? EndsAt, int MaxOwners = 1)
{
    public bool Has(string feature) => Features.Contains(feature);
}

/// <summary>The "Billing" config section: where shops pay, and GST on our invoices.</summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Razorpay keys. Empty: online payment stays off and shops pay by UPI reference.</summary>
    public string RazorpayKeyId { get; set; } = string.Empty;

    public string RazorpayKeySecret { get; set; } = string.Empty;

    public string RazorpayWebhookSecret { get; set; } = string.Empty;

    /// <summary>The platform's UPI ID that shops pay ("groceryk@okicici"). Empty: the shop's plan screen asks them to call.</summary>
    public string UpiId { get; set; } = string.Empty;

    public string PayeeName { get; set; } = "Grocery Khata";

    /// <summary>Key for plan bill links sent on WhatsApp. Set a long random value in production; changing it breaks old links.</summary>
    public string BillLinkKey { get; set; } = "dev-only-bill-link-key-change-me";

    /// <summary>Once registered for GST, invoices show 18% inside the price.</summary>
    public bool GstRegistered { get; set; }

    public string? Gstin { get; set; }

    /// <summary>Monthly plans need UPI AutoPay (a payment gateway); hidden from shops until it is set up.</summary>
    public bool AutoPayEnabled { get; set; }

    public int TrialDays { get; set; } = 30;

    public int GraceDays { get; set; } = 7;

    /// <summary>The longest free (complimentary) period an approver may give; longer, or for life, needs a super admin.</summary>
    public int ApproverCompMonths { get; set; } = 3;

    public int RefundDays { get; set; } = 7;

    public int ReferralBonusDays { get; set; } = 30;
}

/// <summary>Works out what a shop may use, from its subscription's dates and the plans.</summary>
public sealed class Entitlements(AppDbContext db, TimeProvider clock, Microsoft.Extensions.Options.IOptions<BillingOptions> options)
{
    public BillingOptions Options => options.Value;

    public async Task<Entitlement> ForShopAsync(Guid shopId, CancellationToken ct)
    {
        var subscription = await FindOrNewAsync(shopId, ct);
        var plans = await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Code, ct);
        return Compute(subscription, plans, clock.GetUtcNow(), options.Value.GraceDays);
    }

    /// <summary>The shop's row, or an unsaved one starting its trial from the day the shop opened.</summary>
    public async Task<Subscription> FindOrNewAsync(Guid shopId, CancellationToken ct, bool track = false)
    {
        var query = track ? db.Subscriptions : db.Subscriptions.AsNoTracking();
        if (await query.SingleOrDefaultAsync(s => s.ShopId == shopId, ct) is { } existing)
        {
            return existing;
        }

        var opened = await db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => s.CreatedAt).SingleAsync(ct);
        var subscription = new Subscription { ShopId = shopId, TrialEndsAt = opened.AddDays(options.Value.TrialDays) };
        if (track)
        {
            db.Subscriptions.Add(subscription);
        }

        return subscription;
    }

    public static Entitlement Compute(Subscription s, IReadOnlyDictionary<string, Plan> plans, DateTimeOffset now, int graceDays)
    {
        Entitlement As(SubscriptionStatus status, string code, DateTimeOffset? endsAt)
        {
            var plan = plans.GetValueOrDefault(code) ?? plans.GetValueOrDefault("free");
            return new Entitlement(status, plan?.Code ?? "free", plan?.Name ?? "Free", (plan?.FeatureKeys ?? []).ToHashSet(),
                plan?.MaxCustomers, plan?.MaxStaff ?? 0, plan?.WhatsAppPerMonth ?? 0, endsAt, Math.Max(1, plan?.MaxOwners ?? 1));
        }

        if (s.HoldSince is not null)
        {
            return As(SubscriptionStatus.OnHold, "free", null);
        }

        if (s.CompPlanCode is { } comp && (s.CompUntil is null || s.CompUntil > now))
        {
            return As(SubscriptionStatus.Complimentary, comp, s.CompUntil);
        }

        if (s.PaidUntil is { } paid && s.PlanCode is { } planCode)
        {
            if (paid > now)
            {
                return As(SubscriptionStatus.Active, planCode, paid);
            }

            if (paid.AddDays(graceDays) > now)
            {
                return As(SubscriptionStatus.Grace, planCode, paid.AddDays(graceDays));
            }
        }

        // The trial opens Gold, so a new shop sees everything it could buy.
        return s.TrialEndsAt > now ? As(SubscriptionStatus.Trial, "pro", s.TrialEndsAt) : As(SubscriptionStatus.Free, "free", null);
    }

    /// <summary>The default plans (prices in rupees), added when the table is empty. Super admins change them later.</summary>
    public static IEnumerable<Plan> DefaultPlans() =>
    [
        new() { Code = "free", Name = "Free", SortOrder = 0, MaxCustomers = 100, MaxStaff = 0, MaxOwners = 1, WhatsAppPerMonth = 0 },
        new()
        {
            Code = "basic", Name = "Basic", SortOrder = 1, PriceMonthly = 59, PriceYear1 = 499, PriceYear3 = 1099, PriceYear5 = 1699,
            FeatureKeys = [Features.Reports, Features.Store, Features.WhatsApp, Features.Staff], MaxStaff = 1, MaxOwners = 1, WhatsAppPerMonth = 100,
        },
        Silver(),
        new()
        {
            Code = "pro", Name = "Gold", SortOrder = 3, PriceMonthly = 199, PriceYear1 = 1999, PriceYear3 = 4499, PriceYear5 = 6999,
            FeatureKeys = Features.All, MaxStaff = 10, MaxOwners = 3, WhatsAppPerMonth = 500,
        },
    ];

    /// <summary>Between Basic and Gold: helpers and bill photos, without AI ordering or trade features.</summary>
    public static Plan Silver() => new()
    {
        Code = "silver", Name = "Silver", SortOrder = 2, PriceMonthly = 99, PriceYear1 = 999, PriceYear3 = 2299, PriceYear5 = 3499,
        FeatureKeys = [Features.Reports, Features.Store, Features.WhatsApp, Features.Staff, Features.BillPhoto], MaxStaff = 3, MaxOwners = 2, WhatsAppPerMonth = 300,
    };

    public static Offer DefaultOffer() => new()
    {
        Code = "WELCOME299", Title = "First year of Basic at ₹299", PlanCode = "basic", Term = PlanTerm.Year1, Price = 299, NewShopsOnly = true,
    };
}
