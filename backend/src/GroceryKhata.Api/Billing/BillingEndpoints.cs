using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Billing;

public sealed record PlanDto(
    string Code, string Name, decimal? PriceMonthly, decimal? PriceYear1, decimal? PriceYear3, decimal? PriceYear5, IReadOnlyList<string> Features,
    int? MaxCustomers, int MaxStaff, int WhatsAppPerMonth, bool IsActive = true, int Shops = 0, int MaxOwners = 1);

public sealed record PaymentDto(
    Guid Id, string PlanCode, PlanTerm Term, decimal Amount, PaymentMethod Method, string? Reference, string? OfferCode, DateTimeOffset PaidAt,
    DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, string InvoiceNo, DateTimeOffset? RefundedAt, string? BillToken = null);

public sealed record ClaimDto(Guid Id, Guid ShopId, string? ShopName, string PlanCode, PlanTerm Term, decimal Amount, string UpiRef, string? OfferCode,
    ClaimStatus Status, DateTimeOffset CreatedAt, string? Note);

/// <summary>The shop's own plan page: where it stands, what it can buy, how to pay, its payments.</summary>
public sealed record SubscriptionView(
    SubscriptionStatus Status, string PlanCode, string PlanName, DateTimeOffset? EndsAt, int? DaysLeft, IReadOnlyList<string> Features,
    int? MaxCustomers, int MaxStaff, int WhatsAppPerMonth, string? CompReason, string? HoldReason, DateTimeOffset? PriceLockUntil,
    IReadOnlyList<PlanDto> Plans, IReadOnlyList<PaymentDto> Payments, ClaimDto? PendingClaim,
    string? PayToUpi, string PayeeName, bool MonthlyAvailable, bool GstRegistered, string ReferralCode, int ReferralBonusDays, string? ShopCode = null,
    string? ChosenPlanCode = null, PlanTerm? ChosenTerm = null, bool OnlinePay = false, ClaimDto? RejectedClaim = null);

public sealed record PlanChoiceRequest(string? PlanCode, PlanTerm? Term);

public sealed record QuoteRequest(string? PlanCode, PlanTerm? Term, string? OfferCode);

public sealed record ClaimRequest(string? PlanCode, PlanTerm? Term, string? OfferCode, string? UpiRef);

public sealed record RecordPaymentRequest(string? PlanCode, PlanTerm? Term, PaymentMethod? Method, string? Reference, string? OfferCode, decimal? Amount);

public sealed record DecideRequest(string? Note);

public sealed record CompRequest(string? PlanCode, int? Months, string? Reason);

public sealed record HoldRequest(string? Reason);

public sealed record MovePlanRequest(string? PlanCode, string? Reason = null);

public sealed record PlanUpdate(
    string? Name, decimal? PriceMonthly, decimal? PriceYear1, decimal? PriceYear3, decimal? PriceYear5, IReadOnlyList<string>? Features,
    int? MaxCustomers, int MaxStaff, int WhatsAppPerMonth, int MaxOwners = 1);

public sealed record OfferRequest(string? Code, string? Title, string? PlanCode, PlanTerm? Term, decimal Price, bool NewShopsOnly, DateTimeOffset? ValidUntil, int? MaxUses);

public sealed record OfferDto(string Code, string Title, string PlanCode, PlanTerm Term, decimal Price, bool NewShopsOnly, DateTimeOffset? ValidUntil, int? MaxUses, int Uses, bool IsActive);

/// <summary>One shop's billing as admins see it.</summary>
public sealed record ShopBillingView(
    SubscriptionStatus Status, string PlanCode, DateTimeOffset? EndsAt, DateTimeOffset TrialEndsAt, DateTimeOffset? PaidUntil,
    string? CompPlanCode, DateTimeOffset? CompUntil, string? CompReason, DateTimeOffset? HoldSince, string? HoldReason,
    IReadOnlyList<PaymentDto> Payments, IReadOnlyList<ClaimDto> Claims);

/// <summary>The billing page's numbers: shops by status, what needs doing, money this month and year.</summary>
public sealed record BillingOverview(
    IReadOnlyDictionary<SubscriptionStatus, int> ByStatus, int TrialsEndingThisWeek, decimal RevenueThisMonth, decimal RevenueThisYear,
    IReadOnlyList<ClaimDto> PendingClaims, IReadOnlyDictionary<string, int>? ByPlan = null);

public static class BillingEndpoints
{
    public static IServiceCollection AddBilling(this IServiceCollection services)
    {
        services.AddOptions<BillingOptions>().BindConfiguration(BillingOptions.SectionName);
        services.AddScoped<Entitlements>();
        services.AddScoped<BillingService>();
        services.AddHostedService<BillingSeeder>();
        services.AddSingleton<SubscriptionWatch>();
        services.AddHostedService(sp => sp.GetRequiredService<SubscriptionWatch>());
        return services;
    }

    public static IEndpointRouteBuilder MapBilling(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1/subscription").WithTags("Subscription").RequireAuthorization(AppPolicies.ShopMember);
        shop.MapGet("", ViewAsync);
        shop.MapPost("/quote", QuoteAsync);
        shop.MapPost("/claims", ClaimAsync).RequireAuthorization(AppPolicies.ShopOwner);
        shop.MapPost("/choice", ChooseAsync).RequireAuthorization(AppPolicies.ShopOwner);

        var admin = app.MapGroup("/api/v1/platform").WithTags("Platform billing").RequireAuthorization(AppPolicies.PlatformAdmin).RecordsActivity();
        admin.MapGet("/billing", OverviewAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapGet("/shops/{id:guid}/billing", ShopBillingAsync);
        admin.MapPost("/shops/{id:guid}/payments", RecordAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/claims/{id:guid}/confirm", ConfirmAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/claims/{id:guid}/reject", RejectAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/payments/{id:guid}/refund", RefundAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/shops/{id:guid}/complimentary", CompAsync).RequireAuthorization(AppPolicies.PlatformApprover);
        admin.MapDelete("/shops/{id:guid}/complimentary", EndCompAsync).RequireAuthorization(AppPolicies.PlatformApprover);
        admin.MapPost("/shops/{id:guid}/hold", HoldAsync).RequireAuthorization(AppPolicies.PlatformApprover);
        admin.MapPost("/shops/{id:guid}/release", ReleaseAsync).RequireAuthorization(AppPolicies.PlatformApprover);
        admin.MapGet("/plans", (AppDbContext db, TimeProvider clock, CancellationToken ct) => AdminPlansAsync(db, clock, ct));
        admin.MapPost("/plans", AddPlanAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPut("/plans/{code}", UpdatePlanAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/plans/{code}/active", PlanActiveAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/shops/{id:guid}/plan", MovePlanAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapGet("/offers", OffersAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/offers", AddOfferAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        admin.MapPost("/offers/{code}/active", OfferActiveAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        return app;
    }

    // ---- The shop's side ----

    private static async Task<SubscriptionView> ViewAsync(
        AppDbContext db, ITenantContext tenant, Entitlements entitlements, TimeProvider clock, IOptions<BillingOptions> options, CancellationToken ct)
    {
        var o = options.Value;
        var e = await entitlements.ForShopAsync(tenant.ShopId, ct);
        var s = await entitlements.FindOrNewAsync(tenant.ShopId, ct);
        var now = clock.GetUtcNow();
        var payments = await db.SubscriptionPayments.AsNoTracking().Where(p => p.ShopId == tenant.ShopId).OrderByDescending(p => p.PaidAt)
            .Select(ToDto).ToListAsync(ct);
        payments = Invoices.WithTokens(payments, o);
        var claim = await db.PaymentClaims.AsNoTracking().Where(c => c.ShopId == tenant.ShopId && c.Status == ClaimStatus.Pending)
            .OrderByDescending(c => c.CreatedAt).Select(c => new ClaimDto(c.Id, c.ShopId, null, c.PlanCode, c.Term, c.Amount, c.UpiRef, c.OfferCode, c.Status, c.CreatedAt, c.Note))
            .FirstOrDefaultAsync(ct);
        // The last claim the team turned down, if nothing was sent or paid since: the owner sees why.
        var rejected = await db.PaymentClaims.AsNoTracking().Where(c => c.ShopId == tenant.ShopId && c.Status == ClaimStatus.Rejected)
            .OrderByDescending(c => c.CreatedAt).Select(c => new ClaimDto(c.Id, c.ShopId, null, c.PlanCode, c.Term, c.Amount, c.UpiRef, c.OfferCode, c.Status, c.CreatedAt, c.Note))
            .FirstOrDefaultAsync(ct);
        if (rejected is not null && (claim is not null || payments.Any(p => p.PaidAt > rejected.CreatedAt)))
        {
            rejected = null;
        }

        var shopRow = await db.Shops.AsNoTracking().Where(x => x.Id == tenant.ShopId).Select(x => new { x.Slug, x.Number }).SingleAsync(ct);
        var (slug, number) = (shopRow.Slug, shopRow.Number);
        return new SubscriptionView(
            e.Status, e.PlanCode, e.PlanName, e.EndsAt, e.EndsAt is { } end ? Math.Max(0, (int)Math.Ceiling((end - now).TotalDays)) : null,
            e.Features.Order().ToList(), e.MaxCustomers, e.MaxStaff, e.WhatsAppPerMonth, s.CompPlanCode is null ? null : s.CompReason, s.HoldReason,
            s.PriceLockUntil > now ? s.PriceLockUntil : null, await PlansAsync(db, ct), payments, claim,
            string.IsNullOrWhiteSpace(o.UpiId) ? null : o.UpiId, o.PayeeName, o.AutoPayEnabled, o.GstRegistered, slug, o.ReferralBonusDays, GroceryKhata.Api.Shops.Shop.FormatCode(number), s.ChosenPlanCode, s.ChosenTerm, RazorpayCheckout.IsOn(o), rejected);
    }

    private static async Task<IResult> QuoteAsync(QuoteRequest request, ITenantContext tenant, BillingService billing, CancellationToken ct) =>
        request.PlanCode is null || request.Term is null
            ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["PlanCode"] = ["Choose a plan and period."] })
            : TypedResults.Ok(await billing.QuoteAsync(tenant.ShopId, request.PlanCode, request.Term.Value, request.OfferCode, ct));

    /// <summary>"I paid by UPI": recorded for a billing admin to check. Monthly needs AutoPay, so it isn't claimed this way.</summary>
    private static async Task<IResult> ClaimAsync(
        ClaimRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, BillingService billing, CancellationToken ct)
    {
        var upiRef = request.UpiRef?.Trim().ToUpperInvariant();
        if (request.PlanCode is null || request.Term is null or PlanTerm.Monthly || upiRef is not { Length: >= 6 and <= 30 } || !upiRef.All(char.IsAsciiLetterOrDigit))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["UpiRef"] = ["Choose a yearly plan and enter the UPI reference (UTR, 12 digits) from your payment app."] });
        }

        var quote = await billing.QuoteAsync(tenant.ShopId, request.PlanCode, request.Term.Value, request.OfferCode, ct);
        if (quote.Error is { } error)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["OfferCode"] = [error] });
        }

        if (await db.PaymentClaims.AnyAsync(c => c.ShopId == tenant.ShopId && c.Status == ClaimStatus.Pending, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Your last payment is still being checked.");
        }

        if (await db.PaymentClaims.AnyAsync(c => c.UpiRef == upiRef && c.Status != ClaimStatus.Rejected, ct)
            || await db.SubscriptionPayments.AnyAsync(p => p.Reference == upiRef, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This UPI reference is already sent.");
        }

        var claim = new PaymentClaim
        {
            ShopId = tenant.ShopId, PlanCode = quote.PlanCode, Term = quote.Term, Amount = quote.Amount, UpiRef = upiRef, OfferCode = quote.OfferCode,
            ClaimedByUserId = user.UserId()!.Value,
        };
        db.PaymentClaims.Add(claim);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new ClaimDto(claim.Id, claim.ShopId, null, claim.PlanCode, claim.Term, claim.Amount, claim.UpiRef, claim.OfferCode, claim.Status, claim.CreatedAt, null));
    }

    /// <summary>Remembers the plan picked, so a shop that pays later comes back to it. Opens nothing.</summary>
    private static async Task<IResult> ChooseAsync(PlanChoiceRequest request, AppDbContext db, ITenantContext tenant, Entitlements entitlements, CancellationToken ct)
    {
        if (request.PlanCode is null or "free" || request.Term is null || !await db.Plans.AnyAsync(p => p.Code == request.PlanCode, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["PlanCode"] = ["Choose a plan and period."] });
        }

        var subscription = await entitlements.FindOrNewAsync(tenant.ShopId, ct, track: true);
        (subscription.ChosenPlanCode, subscription.ChosenTerm) = (request.PlanCode, request.Term);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // ---- Admins ----

    private static async Task<BillingOverview> OverviewAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var statuses = (await ShopPlansAsync(db, clock, ct)).Values.ToList();
        var india = now.ToOffset(TimeSpan.FromHours(5.5));
        var monthStart = new DateTimeOffset(india.Year, india.Month, 1, 0, 0, 0, TimeSpan.FromHours(5.5)).ToUniversalTime();
        var yearStart = new DateTimeOffset(india.Month >= 4 ? india.Year : india.Year - 1, 4, 1, 0, 0, 0, TimeSpan.FromHours(5.5)).ToUniversalTime();
        var paid = db.SubscriptionPayments.AsNoTracking().Where(p => p.RefundedAt == null);
        return new BillingOverview(
            Enum.GetValues<SubscriptionStatus>().ToDictionary(s => s, s => statuses.Count(x => x.Status == s)),
            statuses.Count(x => x.Status == SubscriptionStatus.Trial && x.EndsAt < now.AddDays(7)),
            await paid.Where(p => p.PaidAt >= monthStart).SumAsync(p => (decimal?)p.Amount, ct) ?? 0,
            await paid.Where(p => p.PaidAt >= yearStart).SumAsync(p => (decimal?)p.Amount, ct) ?? 0,
            await ClaimsAsync(db, null, ClaimStatus.Pending, ct),
            // Trials run on Gold but count apart, as the shop list's plan filter does.
            statuses.GroupBy(x => x.Status == SubscriptionStatus.Trial ? "trial" : x.PlanCode).ToDictionary(g => g.Key, g => g.Count()));
    }

    private static async Task<IResult> ShopBillingAsync(Guid id, AppDbContext db, Entitlements entitlements, IOptions<BillingOptions> options, CancellationToken ct)
    {
        if (!await db.Shops.AnyAsync(s => s.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        var e = await entitlements.ForShopAsync(id, ct);
        var s = await entitlements.FindOrNewAsync(id, ct);
        var payments = Invoices.WithTokens(await db.SubscriptionPayments.AsNoTracking().Where(p => p.ShopId == id).OrderByDescending(p => p.PaidAt).Select(ToDto).ToListAsync(ct), options.Value);
        return TypedResults.Ok(new ShopBillingView(e.Status, e.PlanCode, e.EndsAt, s.TrialEndsAt, s.PaidUntil, s.CompPlanCode, s.CompUntil, s.CompReason,
            s.HoldSince, s.HoldReason, payments, await ClaimsAsync(db, id, null, ct)));
    }

    /// <summary>Cash or UPI collected by the team, or a bank transfer: recorded at the plan's price (or a set amount).</summary>
    private static async Task<IResult> RecordAsync(Guid id, RecordPaymentRequest request, ClaimsPrincipal admin, AppDbContext db, BillingService billing, IOptions<BillingOptions> options, CancellationToken ct)
    {
        if (request.PlanCode is null || request.Term is null || request.Method is null || !await db.Shops.AnyAsync(s => s.Id == id, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["PlanCode"] = ["Choose the shop, plan, period and how it was paid."] });
        }

        var quote = await billing.QuoteAsync(id, request.PlanCode, request.Term.Value, request.OfferCode, ct);
        if (quote.Error is { } error)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["OfferCode"] = [error] });
        }

        if (request.Amount is { } amount)
        {
            if (amount <= 0 || amount > Money.MaxAmount)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Amount"] = ["Enter the amount received."] });
            }

            quote = quote with { Amount = amount };
        }

        var payment = await billing.RecordAsync(id, quote, request.Method.Value, request.Reference?.Trim(), admin.UserId(), ct);
        return TypedResults.Ok(Invoices.WithTokens([ToDto.Compile()(payment)], options.Value)[0]);
    }

    private static async Task<IResult> ConfirmAsync(Guid id, ClaimsPrincipal admin, AppDbContext db, BillingService billing, CancellationToken ct)
    {
        // One update decides who confirms, so two admins (or a double tap) can't add two periods for one payment.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var decidedBy = admin.UserId();
        var won = await db.PaymentClaims.Where(c => c.Id == id && c.Status == ClaimStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ClaimStatus.Confirmed).SetProperty(c => c.DecidedByUserId, decidedBy), ct);
        if (won == 0)
        {
            return TypedResults.NotFound();
        }

        var claim = await db.PaymentClaims.AsNoTracking().SingleAsync(c => c.Id == id, ct);
        var payment = await billing.RecordAsync(claim.ShopId, new Quote(claim.PlanCode, claim.Term, claim.Amount, claim.OfferCode, null), PaymentMethod.Upi, claim.UpiRef, decidedBy, ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(ToDto.Compile()(payment));
    }

    private static async Task<IResult> RejectAsync(Guid id, DecideRequest request, ClaimsPrincipal admin, AppDbContext db, CancellationToken ct)
    {
        var claim = await db.PaymentClaims.SingleOrDefaultAsync(c => c.Id == id && c.Status == ClaimStatus.Pending, ct);
        if (claim is null)
        {
            return TypedResults.NotFound();
        }

        claim.Status = ClaimStatus.Rejected;
        claim.DecidedByUserId = admin.UserId();
        claim.Note = string.IsNullOrWhiteSpace(request.Note) ? "Payment not found in the bank statement." : request.Note.Trim()[..Math.Min(request.Note.Trim().Length, 200)];
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RefundAsync(Guid id, BillingService billing, CancellationToken ct) =>
        await billing.RefundAsync(id, ct) is { } error
            ? TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: error)
            : TypedResults.NoContent();

    /// <summary>Free access to a plan, with a reason: an approver up to a few months; longer, or for life, a super admin.</summary>
    private static async Task<IResult> CompAsync(
        Guid id, CompRequest request, ClaimsPrincipal admin, AppDbContext db, Entitlements entitlements, TimeProvider clock, IOptions<BillingOptions> options,
        ILogger<Subscription> logger, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (!await IsPaidPlanAsync(db, request.PlanCode, ct) || request.Months is <= 0 or > 120 || reason is not { Length: >= 3 and <= 200 }
            || !await db.Shops.AnyAsync(s => s.Id == id, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Choose a plan, how many months (empty: for life) and why."] });
        }

        var role = await db.PlatformAdmins.Where(a => a.UserId == admin.UserId()).Select(a => a.Role).SingleAsync(ct);
        if (role != AdminRole.Super && (request.Months is null || request.Months > options.Value.ApproverCompMonths))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: $"Free access longer than {options.Value.ApproverCompMonths} months, or for life, needs a super admin.");
        }

        var s = await entitlements.FindOrNewAsync(id, ct, track: true);
        s.CompPlanCode = request.PlanCode;
        s.CompUntil = request.Months is { } months ? clock.GetUtcNow().AddMonths(months) : null;
        s.CompReason = reason;
        s.CompByUserId = admin.UserId();
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Admin {AdminId} gave shop {ShopId} free {Plan} for {Months} months: {Reason}", admin.UserId(), id, request.PlanCode, request.Months, reason);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> EndCompAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        await db.Subscriptions.Where(s => s.ShopId == id).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.CompPlanCode, (string?)null).SetProperty(s => s.CompUntil, (DateTimeOffset?)null).SetProperty(s => s.CompReason, (string?)null), ct);
        return TypedResults.NoContent();
    }

    /// <summary>Holds a shop's plan: Free features only until released. Its own data and its customers' khata stay open.</summary>
    private static async Task<IResult> HoldAsync(Guid id, HoldRequest request, ClaimsPrincipal admin, AppDbContext db, Entitlements entitlements, TimeProvider clock,
        ILogger<Subscription> logger, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (reason is not { Length: >= 3 and <= 200 } || !await db.Shops.AnyAsync(s => s.Id == id, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Say why the plan is on hold (the shop sees it)."] });
        }

        var s = await entitlements.FindOrNewAsync(id, ct, track: true);
        s.HoldSince = clock.GetUtcNow();
        s.HoldReason = reason;
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Admin {AdminId} put shop {ShopId} on hold: {Reason}", admin.UserId(), id, reason);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ReleaseAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        await db.Subscriptions.Where(s => s.ShopId == id).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.HoldSince, (DateTimeOffset?)null).SetProperty(s => s.HoldReason, (string?)null), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> UpdatePlanAsync(string code, PlanUpdate request, AppDbContext db, ILogger<Plan> logger, ClaimsPrincipal admin, CancellationToken ct)
    {
        var plan = await db.Plans.SingleOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        decimal?[] prices = [request.PriceMonthly, request.PriceYear1, request.PriceYear3, request.PriceYear5];
        if (string.IsNullOrWhiteSpace(request.Name) || prices.Any(p => p is < 0 or > 1_000_000) || request.MaxStaff is < 0 or > 100
            || request.WhatsAppPerMonth is < 0 or > 100_000 || request.MaxCustomers is < 1 || request.Features?.Any(f => !Features.All.Contains(f)) == true)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Check the name, prices, limits and features."] });
        }

        plan.Name = request.Name.Trim();
        (plan.PriceMonthly, plan.PriceYear1, plan.PriceYear3, plan.PriceYear5) = (request.PriceMonthly, request.PriceYear1, request.PriceYear3, request.PriceYear5);
        plan.FeatureKeys = request.Features?.Distinct().ToArray() ?? plan.FeatureKeys;
        (plan.MaxCustomers, plan.MaxStaff, plan.WhatsAppPerMonth, plan.MaxOwners) = (request.MaxCustomers, request.MaxStaff, request.WhatsAppPerMonth, Math.Clamp(request.MaxOwners, 1, 20));
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Admin {AdminId} changed plan {Plan}", admin.UserId(), code);
        return TypedResults.Ok(await PlansAsync(db, ct, includeInactive: true));
    }

    /// <summary>A new plan on sale, after the others ("Platinum"). Its code comes from the name.</summary>
    private static async Task<IResult> AddPlanAsync(PlanUpdate request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        var code = new string((name ?? "").ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        if (name is not { Length: >= 2 and <= 30 } || code.Length < 2 || !ValidPlan(request))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Give the plan a name (letters and digits), a 1-year price and check the limits."] });
        }

        if (await db.Plans.AnyAsync(p => p.Code == code, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A plan with this name already exists.");
        }

        db.Plans.Add(new Plan
        {
            Code = code, Name = name, SortOrder = (await db.Plans.MaxAsync(p => (int?)p.SortOrder, ct) ?? 0) + 1,
            PriceMonthly = request.PriceMonthly, PriceYear1 = request.PriceYear1, PriceYear3 = request.PriceYear3, PriceYear5 = request.PriceYear5,
            FeatureKeys = request.Features?.Distinct().ToArray() ?? [], MaxCustomers = request.MaxCustomers, MaxStaff = request.MaxStaff, WhatsAppPerMonth = request.WhatsAppPerMonth,
            MaxOwners = Math.Clamp(request.MaxOwners, 1, 20),
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await AdminPlansAsync(db, clock, ct));
    }

    /// <summary>
    /// Takes a plan off sale, or back on. Shops already on it keep it until their period ends. Free can't be turned
    /// off, nor the last plan on sale.
    /// </summary>
    private static async Task<IResult> PlanActiveAsync(string code, bool active, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var plan = await db.Plans.SingleOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null)
        {
            return TypedResults.NotFound();
        }

        if (!active && (plan.Code == "free" || !await db.Plans.AnyAsync(p => p.IsActive && p.Code != "free" && p.Code != code, ct)))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Keep Free and at least one paid plan on sale.");
        }

        plan.IsActive = active;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await AdminPlansAsync(db, clock, ct));
    }

    /// <summary>
    /// Moves a shop to another plan for the time it has: a paid shop keeps its paid-until date, a shop on free access
    /// keeps its end date. A shop on trial or Free has nothing to move: record a payment or give free access instead.
    /// </summary>
    private static async Task<IResult> MovePlanAsync(Guid id, MovePlanRequest request, AppDbContext db, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        if (!await IsPaidPlanAsync(db, request.PlanCode, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["PlanCode"] = ["Choose a plan that is on sale."] });
        }

        var reason = request.Reason?.Trim();
        if (reason is not { Length: >= 3 and <= 200 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Say why: a move takes no payment."] });
        }

        if (!await db.Shops.AnyAsync(s => s.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        var s = await entitlements.FindOrNewAsync(id, ct, track: true);
        var now = clock.GetUtcNow();
        var inGrace = (await entitlements.ForShopAsync(id, ct)).Status == SubscriptionStatus.Grace;
        if (s.CompPlanCode is not null && (s.CompUntil is null || s.CompUntil > now))
        {
            s.CompPlanCode = request.PlanCode;
        }
        else if (s.PaidUntil > now || inGrace)
        {
            s.PlanCode = request.PlanCode;
        }
        else
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This shop has no paid or free period to move. Record a payment or give free access.");
        }

        s.MoveReason = reason;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static Task<bool> IsPaidPlanAsync(AppDbContext db, string? code, CancellationToken ct) =>
        code is null || code == "free" ? Task.FromResult(false) : db.Plans.AnyAsync(p => p.Code == code && p.IsActive, ct);

    private static bool ValidPlan(PlanUpdate r)
    {
        decimal?[] prices = [r.PriceMonthly, r.PriceYear1, r.PriceYear3, r.PriceYear5];
        return r.PriceYear1 is > 0 && !prices.Any(p => p is < 0 or > 1_000_000) && r.MaxStaff is >= 0 and <= 100
            && r.WhatsAppPerMonth is >= 0 and <= 100_000 && r.MaxCustomers is null or >= 1 && r.Features?.All(f => Features.All.Contains(f)) != false;
    }

    /// <summary>Every plan, on sale or not, with how many shops are on it today (shops on trial aren't counted).</summary>
    private static async Task<IReadOnlyList<PlanDto>> AdminPlansAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var counts = await ShopPlansAsync(db, clock, ct);
        return (await PlansAsync(db, ct, includeInactive: true))
            .Select(p => p with { Shops = counts.Values.Count(e => e.PlanCode == p.Code && e.Status != SubscriptionStatus.Trial) }).ToList();
    }

    /// <summary>Each active shop's plan today (trial, paid, free access…), worked out from the dates.</summary>
    public static async Task<Dictionary<Guid, Entitlement>> ShopPlansAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var plans = await db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Code, ct);
        var shops = await db.Shops.AsNoTracking().Where(s => s.IsActive).Select(s => new { s.Id, s.CreatedAt }).ToListAsync(ct);
        var subs = await db.Subscriptions.AsNoTracking().ToDictionaryAsync(s => s.ShopId, ct);
        return shops.ToDictionary(s => s.Id, s => Entitlements.Compute(
            subs.GetValueOrDefault(s.Id) ?? new Subscription { ShopId = s.Id, TrialEndsAt = s.CreatedAt.AddDays(30) }, plans, now, 7));
    }

    private static async Task<IReadOnlyList<OfferDto>> OffersAsync(AppDbContext db, CancellationToken ct) =>
        await db.Offers.AsNoTracking().OrderByDescending(o => o.IsActive).ThenBy(o => o.Code)
            .Select(o => new OfferDto(o.Code, o.Title, o.PlanCode, o.Term, o.Price, o.NewShopsOnly, o.ValidUntil, o.MaxUses, o.Uses, o.IsActive)).ToListAsync(ct);

    private static async Task<IResult> AddOfferAsync(OfferRequest request, AppDbContext db, CancellationToken ct)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        if (code is not { Length: >= 4 and <= 20 } || !code.All(char.IsAsciiLetterOrDigit) || string.IsNullOrWhiteSpace(request.Title)
            || !await IsPaidPlanAsync(db, request.PlanCode, ct) || request.Term is null || request.Price is < 0 or > 1_000_000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Code"] = ["Enter a code (4-20 letters/digits), a title, the plan, period and price."] });
        }

        if (await db.Offers.AnyAsync(o => o.Code == code, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "That code is already used.");
        }

        db.Offers.Add(new Offer
        {
            Code = code, Title = request.Title.Trim(), PlanCode = request.PlanCode!, Term = request.Term.Value, Price = request.Price, NewShopsOnly = request.NewShopsOnly,
            ValidUntil = request.ValidUntil, MaxUses = request.MaxUses,
        });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await OffersAsync(db, ct));
    }

    private static async Task<IResult> OfferActiveAsync(string code, bool active, AppDbContext db, CancellationToken ct)
    {
        var changed = await db.Offers.Where(o => o.Code == code.ToUpperInvariant()).ExecuteUpdateAsync(u => u.SetProperty(o => o.IsActive, active), ct);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.Ok(await OffersAsync(db, ct));
    }

    private static async Task<IReadOnlyList<PlanDto>> PlansAsync(AppDbContext db, CancellationToken ct, bool includeInactive = false) =>
        (await db.Plans.AsNoTracking().Where(p => includeInactive || p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(ct))
        .Select(p => new PlanDto(p.Code, p.Name, p.PriceMonthly, p.PriceYear1, p.PriceYear3, p.PriceYear5, p.FeatureKeys, p.MaxCustomers, p.MaxStaff, p.WhatsAppPerMonth, p.IsActive, 0, p.MaxOwners))
        .ToList();

    private static async Task<IReadOnlyList<ClaimDto>> ClaimsAsync(AppDbContext db, Guid? shopId, ClaimStatus? status, CancellationToken ct) =>
        await (from c in db.PaymentClaims.AsNoTracking()
               join s in db.Shops.AsNoTracking() on c.ShopId equals s.Id
               where (shopId == null || c.ShopId == shopId) && (status == null || c.Status == status)
               orderby c.CreatedAt descending
               select new ClaimDto(c.Id, c.ShopId, s.Name, c.PlanCode, c.Term, c.Amount, c.UpiRef, c.OfferCode, c.Status, c.CreatedAt, c.Note))
            .Take(100).ToListAsync(ct);

    private static readonly System.Linq.Expressions.Expression<Func<SubscriptionPayment, PaymentDto>> ToDto = p =>
        new PaymentDto(p.Id, p.PlanCode, p.Term, p.Amount, p.Method, p.Reference, p.OfferCode, p.PaidAt, p.PeriodStart, p.PeriodEnd, p.InvoiceNo, p.RefundedAt);
}

/// <summary>Adds the default plans and the welcome offer on an empty database.</summary>
public sealed class BillingSeeder(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Plans.AnyAsync(cancellationToken))
        {
            db.Plans.AddRange(Entitlements.DefaultPlans());
            db.Offers.Add(Entitlements.DefaultOffer());
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Plans from before Basic/Silver/Gold: add Silver between Basic and Pro, and Pro becomes Gold.
        if (!await db.Plans.AnyAsync(p => p.Code == "silver", cancellationToken))
        {
            db.Plans.Add(Entitlements.Silver());
            await db.Plans.Where(p => p.Code == "pro").ExecuteUpdateAsync(u => u
                .SetProperty(p => p.SortOrder, 3).SetProperty(p => p.Name, p => p.Name == "Pro" ? "Gold" : p.Name), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Plan limits on endpoints: a feature the shop's plan must include, answered with 402 and the plan that has it.</summary>
public static class PlanGate
{
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, string feature)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;
            var tenant = services.GetRequiredService<ITenantContext>();
            if (!tenant.HasShop)
            {
                return await next(context);
            }

            var entitlement = await services.GetRequiredService<Entitlements>().ForShopAsync(tenant.ShopId, context.HttpContext.RequestAborted);
            return entitlement.Has(feature) ? await next(context) : await UpgradeAsync(services, feature);
        });

    /// <summary>402 with the cheapest plan that opens the feature, for the app to show "needs Basic".</summary>
    public static async Task<IResult> UpgradeAsync(IServiceProvider services, string feature)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var plan = await db.Plans.AsNoTracking().Where(p => p.IsActive && p.FeatureKeys.Contains(feature)).OrderBy(p => p.SortOrder).Select(p => p.Name).FirstOrDefaultAsync();
        return TypedResults.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: $"This needs the {plan ?? "Pro"} plan.",
            detail: "Open My plan to upgrade.", extensions: new Dictionary<string, object?> { ["feature"] = feature, ["plan"] = plan });
    }

    /// <summary>The plan's limit on customers (Free: 100).</summary>
    public static async ValueTask<object?> CustomerLimitAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var tenant = services.GetRequiredService<ITenantContext>();
        var entitlement = await services.GetRequiredService<Entitlements>().ForShopAsync(tenant.ShopId, context.HttpContext.RequestAborted);
        if (entitlement.MaxCustomers is { } max && await services.GetRequiredService<AppDbContext>().Customers.CountAsync(context.HttpContext.RequestAborted) >= max)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: $"The {entitlement.PlanName} plan keeps up to {max} customers.",
                detail: "Upgrade to Basic for unlimited customers. Your existing customers and their khata stay as they are.");
        }

        return await next(context);
    }
}
