using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

// Phase 2 of the jewellery counter: girvi (loans against pledged jewellery), gold savings schemes, customer orders and
// repairs, and pieces sent out on approval. Money moves through books the shop already keeps: girvi in its own cash
// book lines, scheme instalments and order advances as khata payments (an advance on the customer's khata that the
// final bill uses up).

/// <summary>A loan against pledged jewellery (girvi): what was pledged, how much was lent, and the interest terms.</summary>
public sealed class GirviLoan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>What was pledged, as written on the pawn ticket.</summary>
    public required string Items { get; set; }

    public required string Metal { get; set; }

    public decimal Gross { get; set; }

    /// <summary>Test reading in percent.</summary>
    public decimal Reading { get; set; }

    public decimal Fine { get; set; }

    /// <summary>Value of the pledge at the day's buying rate when lent.</summary>
    public decimal PledgeValue { get; set; }

    public decimal Principal { get; set; }

    /// <summary>Interest per month, in percent (1.5 = 18% a year).</summary>
    public decimal RatePerMonth { get; set; }

    /// <summary>True: interest counted by the day (30 days a month). False: every month started counts in full.</summary>
    public bool DayWise { get; set; }

    /// <summary>True: interest not paid by each anniversary of the loan is added to the principal.</summary>
    public bool CompoundYearly { get; set; }

    public DateOnly PledgedOn { get; set; }

    public DateOnly DueOn { get; set; }

    public PaymentMode Mode { get; set; }

    /// <summary>Aadhaar last four, voter ID or other proof seen, as the shop writes it.</summary>
    public string? Kyc { get; set; }

    public string? Note { get; set; }

    public DateOnly? ClosedOn { get; set; }

    public Guid CreatedByUserId { get; set; }
}

/// <summary>Money paid back on a girvi loan: interest first, then principal.</summary>
public sealed class GirviPayment : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid LoanId { get; set; }

    public DateOnly PaidOn { get; set; }

    public decimal Amount { get; set; }

    public decimal InterestPart { get; set; }

    public decimal PrincipalPart { get; set; }

    public PaymentMode Mode { get; set; }

    public DateTimeOffset At { get; set; }
}

/// <summary>A savings scheme the shop runs: so many monthly instalments, then a bonus off the making charges.</summary>
public sealed class SchemePlan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public int Months { get; set; }

    public decimal Instalment { get; set; }

    /// <summary>Taken off the making charges when the member buys jewellery after paying every instalment.</summary>
    public decimal Bonus { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// "amount": the member saves rupees. "grams": each instalment buys gold of <see cref="Purity"/> at that day's rate,
    /// and the member gets those grams at the end (valued at the day they buy, never less than they paid).
    /// </summary>
    public string Kind { get; set; } = "amount";

    /// <summary>Gold purity a gram scheme saves in (916 for 22K).</summary>
    public int? Purity { get; set; }
}

/// <summary>A customer enrolled in a scheme. Instalments are khata payments, so the money waits on their khata as an advance.</summary>
public sealed class SchemeMember : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid PlanId { get; set; }

    public int Number { get; set; }

    public Guid CustomerId { get; set; }

    public DateOnly StartedOn { get; set; }

    public Guid? RedeemedBillId { get; set; }

    public DateOnly? ClosedOn { get; set; }
}

public sealed class SchemeInstalment : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid MemberId { get; set; }

    public Guid EntryId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly PaidOn { get; set; }

    /// <summary>Gram schemes: the gold this instalment bought, and the selling rate per gram that day.</summary>
    public decimal? Grams { get; set; }

    public decimal? RatePerGram { get; set; }
}

/// <summary>A made-to-order piece or a repair taken in: what, how heavy, by when, and advances paid.</summary>
public sealed class JewelleryOrder : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    /// <summary>order or repair.</summary>
    public required string Kind { get; set; }

    public Guid CustomerId { get; set; }

    public required string Description { get; set; }

    public string? Metal { get; set; }

    public int? Purity { get; set; }

    /// <summary>Order: the approximate weight wanted. Repair: the weight taken in.</summary>
    public decimal? Weight { get; set; }

    /// <summary>Repair: the weight handed back.</summary>
    public decimal? WeightOut { get; set; }

    /// <summary>The rate fixed for the customer at booking (per gram); null: the rate on delivery.</summary>
    public decimal? FixedRate { get; set; }

    /// <summary>Repair charge (GST 18%).</summary>
    public decimal? Charge { get; set; }

    public DateOnly DueOn { get; set; }

    /// <summary>booked, ready, delivered or cancelled.</summary>
    public required string Status { get; set; }

    public decimal Advance { get; set; }

    public Guid? BillId { get; set; }

    public string? Note { get; set; }
}

/// <summary>Pieces sent home with a customer to choose from (approval, "memo"): they come back, or get billed.</summary>
public sealed class ApprovalMemo : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public int Number { get; set; }

    public Guid? CustomerId { get; set; }

    public required string Party { get; set; }

    public DateOnly DueBack { get; set; }

    public DateOnly? ClosedOn { get; set; }
}

public sealed class ApprovalItem : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid MemoId { get; set; }

    public Guid PieceId { get; set; }

    public decimal GrossOut { get; set; }

    public decimal? GrossBack { get; set; }

    public DateTimeOffset? ReturnedAt { get; set; }

    public Guid? SoldBillId { get; set; }
}

// ---------- requests and views ----------

public sealed record GirviRequest(Guid CustomerId, string Items, string Metal, decimal Gross, decimal Reading, decimal Principal, decimal RatePerMonth, bool? DayWise,
    int? Months, PaymentMode? Mode, string? Kyc, string? Note, string? PledgedOn, bool? CompoundYearly = null);
public sealed record GirviTopUpRequest(decimal Amount, PaymentMode? Mode, string? On);
public sealed record GirviPayRequest(decimal Amount, PaymentMode? Mode, string? PaidOn);
public sealed record GirviPaymentDto(DateOnly PaidOn, decimal Amount, decimal InterestPart, decimal PrincipalPart, PaymentMode Mode);
public sealed record GirviDto(
    Guid Id, int Number, Guid CustomerId, string Customer, string? Phone, string Items, string Metal, decimal Gross, decimal Reading, decimal Fine, decimal PledgeValue,
    decimal Principal, decimal RatePerMonth, bool DayWise, DateOnly PledgedOn, DateOnly DueOn, string? Kyc, string? Note, DateOnly? ClosedOn,
    decimal PrincipalLeft, decimal InterestDue, decimal ToClose, bool Overdue, IReadOnlyList<GirviPaymentDto> Payments, bool CompoundYearly = false, decimal Capitalised = 0);

public sealed record SchemePlanRequest(string Name, int Months, decimal Instalment, decimal? Bonus, string? Kind = null, int? Purity = null);
public sealed record SchemePlanDto(Guid Id, string Name, int Months, decimal Instalment, decimal Bonus, bool IsActive, int Members, string Kind = "amount", int? Purity = null);
public sealed record SchemeJoinRequest(Guid PlanId, Guid CustomerId);
public sealed record SchemePayRequest(Guid EntryId, decimal? Amount, PaymentMode? Mode);
public sealed record SchemeMemberDto(
    Guid Id, int Number, Guid PlanId, string Plan, Guid CustomerId, string Customer, string? Phone, DateOnly StartedOn, int Months, decimal Instalment, decimal Bonus,
    int Paid, decimal PaidTotal, bool Complete, Guid? RedeemedBillId, DateOnly? ClosedOn, DateOnly NextDue, string Kind = "amount", int? Purity = null, decimal Grams = 0);

public sealed record JewelleryOrderRequest(string Kind, Guid CustomerId, string Description, string? Metal, int? Purity, decimal? Weight, decimal? FixedRate, decimal? Charge, string DueOn, string? Note);
public sealed record JewelleryAdvanceRequest(Guid EntryId, decimal Amount, PaymentMode? Mode);
public sealed record JewelleryOrderStatusRequest(string Status, decimal? WeightOut, decimal? Charge);
public sealed record RepairDeliverRequest(Guid BillId, decimal? WeightOut, decimal Charge, decimal MoneyPaid, PaymentMode? Mode);
public sealed record JewelleryOrderDto(
    Guid Id, int Number, string Kind, Guid CustomerId, string Customer, string? Phone, string Description, string? Metal, int? Purity, decimal? Weight, decimal? WeightOut,
    decimal? FixedRate, decimal? Charge, DateOnly DueOn, string Status, decimal Advance, Guid? BillId, string? Note, bool Late, DateTimeOffset CreatedAt);

public sealed record ApprovalRequest(Guid? CustomerId, string? Party, IReadOnlyList<Guid> PieceIds, int? Days);
public sealed record ApprovalReturnRequest(Guid PieceId, decimal GrossBack);
public sealed record ApprovalItemDto(Guid PieceId, int TagNo, string Name, decimal GrossOut, decimal? GrossBack, bool Returned, bool Sold, bool WeightDiffers);
public sealed record ApprovalDto(Guid Id, int Number, Guid? CustomerId, string Party, DateTimeOffset OutAt, DateOnly DueBack, DateOnly? ClosedOn, bool Late, IReadOnlyList<ApprovalItemDto> Items);

/// <summary>Girvi interest: simple, per month, on the principal still owed; counted by the day or by every month started.</summary>
public static class GirviMath
{
    /// <summary>
    /// Interest owed from <paramref name="from"/> to <paramref name="to"/> on a principal that changed on payment days.
    /// Day-wise: every day is a 30th of a month. Month-wise: the loan's life counts as the months started (at least one;
    /// 1 Jan to 1 Feb is one month, to 2 Feb two), shared over the stretches between payments by their days.
    /// </summary>
    public static decimal Interest(decimal principal, decimal ratePerMonth, bool dayWise, DateOnly from, DateOnly to, IReadOnlyList<(DateOnly On, decimal PrincipalPaid)> payments)
    {
        if (to < from) return 0;
        var stretches = new List<(decimal Principal, int Days)>();
        var left = principal;
        var at = from;
        foreach (var (on, paid) in payments.Where(p => p.On >= from && p.On <= to).OrderBy(p => p.On))
        {
            stretches.Add((left, on.DayNumber - at.DayNumber));
            left -= paid;
            at = on;
        }

        stretches.Add((left, to.DayNumber - at.DayNumber));
        var totalDays = to.DayNumber - from.DayNumber;
        decimal total;
        if (dayWise)
        {
            total = stretches.Sum(s => s.Principal * ratePerMonth / 100m * s.Days / 30m);
        }
        else
        {
            var months = Math.Max(1, MonthsStarted(from, to));
            total = totalDays == 0
                ? left * ratePerMonth / 100m * months
                : stretches.Sum(s => s.Principal * ratePerMonth / 100m * months * s.Days / totalDays);
        }

        return decimal.Round(total, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Interest with yearly compounding: at each anniversary of the loan, interest earned and not yet paid is added to
    /// the principal (the usual girvi practice). Returns the interest earned that was not added, and the amount added.
    /// Payments carry the interest and principal they paid; a top-up is a negative principal payment.
    /// </summary>
    public static (decimal Interest, decimal Capitalised) Compound(decimal principal, decimal ratePerMonth, bool dayWise, DateOnly from, DateOnly to,
        IReadOnlyList<(DateOnly On, decimal InterestPaid, decimal PrincipalPaid)> payments)
    {
        if (to < from) return (0, 0);
        decimal interest = 0, capitalised = 0, basePrincipal = principal;
        var start = from;
        while (true)
        {
            var anniversary = start.AddYears(1);
            var end = anniversary < to ? anniversary : to;
            var inSegment = payments.Where(p => p.On >= start && p.On < end || (end == to && p.On == to)).Select(p => (p.On, p.PrincipalPaid)).ToList();
            interest += Interest(basePrincipal, ratePerMonth, dayWise, start, end, inSegment);
            basePrincipal -= inSegment.Sum(p => p.PrincipalPaid);
            if (end == to) break;
            var unpaid = interest - payments.Where(p => p.On < end).Sum(p => p.InterestPaid);
            if (unpaid > 0)
            {
                basePrincipal += unpaid;
                capitalised += unpaid;
                interest -= unpaid;
            }

            start = end;
        }

        return (interest, capitalised);
    }

    /// <summary>Months started from <paramref name="from"/> to <paramref name="to"/>: 1 Jan to 1 Feb is 1, to 2 Feb is 2.</summary>
    public static int MonthsStarted(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
        if (from.AddMonths(months) > to) months--;
        if (from.AddMonths(months) < to) months++;
        return Math.Max(0, months);
    }
}

public static class JewelleryBookEndpoints
{
    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static DateOnly? Day(string? text, TimeProvider clock) =>
        string.IsNullOrEmpty(text) ? IndiaTime.Today(clock) : DateOnly.TryParseExact(text, "yyyy-MM-dd", out var d) ? d : null;

    public static IEndpointRouteBuilder MapJewelleryBook(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/jewellery").WithTags("Jewellery").RequireAuthorization(AppPolicies.ShopMember);
        g.MapGet("/girvi", GirviListAsync);
        g.MapPost("/girvi", GirviAddAsync);
        g.MapPost("/girvi/{id:guid}/payments", GirviPayAsync);
        g.MapPost("/girvi/{id:guid}/top-up", GirviTopUpAsync);
        g.MapGet("/schemes", PlansAsync);
        g.MapPost("/schemes", AddPlanAsync);
        g.MapGet("/schemes/members", MembersAsync);
        g.MapPost("/schemes/members", JoinAsync);
        g.MapPost("/schemes/members/{id:guid}/instalments", PayInstalmentAsync);
        g.MapGet("/orders", OrdersAsync);
        g.MapPost("/orders", AddOrderAsync);
        g.MapPost("/orders/{id:guid}/advance", OrderAdvanceAsync);
        g.MapPost("/orders/{id:guid}/status", OrderStatusAsync);
        g.MapPost("/orders/{id:guid}/deliver-repair", DeliverRepairAsync);
        g.MapGet("/approvals", ApprovalsAsync);
        g.MapPost("/approvals", SendApprovalAsync);
        g.MapPost("/approvals/{id:guid}/return", ReturnApprovalAsync);
        return app;
    }

    // ---------------- girvi ----------------

    private static async Task<List<GirviDto>> GirviViewsAsync(AppDbContext db, TimeProvider clock, Func<IQueryable<GirviLoan>, IQueryable<GirviLoan>> filter, CancellationToken ct)
    {
        var loans = await filter(db.GirviLoans.AsNoTracking()).OrderByDescending(l => l.Number).Take(300).ToListAsync(ct);
        var ids = loans.Select(l => l.Id).ToList();
        var pays = (await db.GirviPayments.AsNoTracking().Where(p => ids.Contains(p.LoanId)).OrderBy(p => p.At).ToListAsync(ct)).ToLookup(p => p.LoanId);
        var custIds = loans.Select(l => l.CustomerId).Distinct().ToList();
        var people = await db.Customers.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        var today = IndiaTime.Today(clock);
        return loans.Select(l =>
        {
            var ps = pays[l.Id].ToList();
            var (interest, left, capitalised) = l.ClosedOn is null ? Owed(l, ps, today) : (0, 0, 0);
            var who = people.GetValueOrDefault(l.CustomerId);
            return new GirviDto(l.Id, l.Number, l.CustomerId, who?.Name ?? "", who?.Phone, l.Items, l.Metal, l.Gross, l.Reading, l.Fine, l.PledgeValue, l.Principal, l.RatePerMonth,
                l.DayWise, l.PledgedOn, l.DueOn, l.Kyc, l.Note, l.ClosedOn, left, interest, left + interest, l.ClosedOn is null && today > l.DueOn,
                ps.Select(p => new GirviPaymentDto(p.PaidOn, p.Amount, p.InterestPart, p.PrincipalPart, p.Mode)).ToList(), l.CompoundYearly, capitalised);
        }).ToList();
    }

    /// <summary>
    /// What is owed on a loan on <paramref name="on"/>: interest not yet paid, principal left (with top-ups and any
    /// interest added on anniversaries of a compounding loan), and the interest so added.
    /// </summary>
    public static (decimal InterestDue, decimal PrincipalLeft, decimal Capitalised) Owed(GirviLoan l, IReadOnlyList<GirviPayment> ps, DateOnly on)
    {
        var principalPaid = ps.Sum(p => p.PrincipalPart);
        var interestPaid = ps.Sum(p => p.InterestPart);
        if (l.CompoundYearly)
        {
            // Earned = added to principal + interest paid before each anniversary + the rest; so what is still owed is the
            // interest not added, less all interest paid.
            var (interest, capitalised) = GirviMath.Compound(l.Principal, l.RatePerMonth, l.DayWise, l.PledgedOn, on, ps.Select(p => (p.PaidOn, p.InterestPart, p.PrincipalPart)).ToList());
            return (Math.Max(0, interest - interestPaid), l.Principal + capitalised - principalPaid, capitalised);
        }

        var simple = GirviMath.Interest(l.Principal, l.RatePerMonth, l.DayWise, l.PledgedOn, on, ps.Select(p => (p.PaidOn, p.PrincipalPart)).ToList());
        return (Math.Max(0, simple - interestPaid), l.Principal - principalPaid, 0);
    }

    private static async Task<IResult> GirviListAsync(string? status, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await GirviViewsAsync(db, clock, q => status == "closed" ? q.Where(l => l.ClosedOn != null) : status == "all" ? q : q.Where(l => l.ClosedOn == null), ct));

    private static async Task<IResult> GirviAddAsync(GirviRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the owner gives a girvi loan.");
        if (!await db.Customers.AnyAsync(c => c.Id == r.CustomerId, ct)) return Invalid("CustomerId", "Choose the customer.");
        if (string.IsNullOrWhiteSpace(r.Items) || r.Items.Trim().Length > 200) return Invalid("Items", "Write what was pledged.");
        if (r.Metal is not ("gold" or "silver") || r.Gross is <= 0 or > 10_000 || r.Reading is <= 0 or > 100) return Invalid("Gross", "Enter the metal, weight and test reading.");
        if (r.Principal is <= 0 || r.Principal > Money.MaxAmount) return Invalid("Principal", "Enter the amount lent.");
        if (r.RatePerMonth is < 0 or > 10) return Invalid("RatePerMonth", "Interest is a monthly percent from 0 to 10.");
        if (r.Mode is { } m && m is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Bank)) return Invalid("Mode", "Lend by cash, UPI or bank.");
        if (Day(r.PledgedOn, clock) is not { } on || on > IndiaTime.Today(clock)) return Invalid("PledgedOn", "Enter the date lent.");
        var fine = JewelleryMath.Grams(r.Gross * r.Reading / 100m);
        var rates = (await JewelleryEndpoints.CurrentRatesAsync(db, clock, ct)).Current;
        var pure = rates.FirstOrDefault(x => x.Metal == r.Metal && x.Purity == 999);
        var value = pure is null ? 0 : decimal.Round(fine * (pure.BuyPerGram ?? pure.SellPerGram), 2);
        var number = (await db.GirviLoans.MaxAsync(l => (int?)l.Number, ct) ?? 0) + 1;
        var loan = new GirviLoan
        {
            Number = number, CustomerId = r.CustomerId, Items = r.Items.Trim(), Metal = r.Metal, Gross = JewelleryMath.Grams(r.Gross), Reading = r.Reading, Fine = fine, PledgeValue = value,
            Principal = decimal.Round(r.Principal, 2), RatePerMonth = r.RatePerMonth, DayWise = r.DayWise ?? false, PledgedOn = on, DueOn = on.AddMonths(Math.Clamp(r.Months ?? 12, 1, 60)), CompoundYearly = r.CompoundYearly ?? false,
            Mode = r.Mode ?? PaymentMode.Cash, Kyc = string.IsNullOrWhiteSpace(r.Kyc) ? null : r.Kyc.Trim()[..Math.Min(60, r.Kyc.Trim().Length)],
            Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim()[..Math.Min(200, r.Note.Trim().Length)], CreatedByUserId = user.UserId()!.Value,
        };
        db.GirviLoans.Add(loan);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await GirviViewsAsync(db, clock, q => q.Where(l => l.Id == loan.Id), ct)).Single());
    }

    /// <summary>A payment pays the interest due first, then principal. Paying everything owed closes the loan (the pledge goes back).</summary>
    private static async Task<IResult> GirviPayAsync(Guid id, GirviPayRequest r, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.GirviLoans.SingleOrDefaultAsync(l => l.Id == id, ct) is not { } loan) return TypedResults.NotFound();
        if (loan.ClosedOn is not null) return Invalid("Amount", "This girvi is already closed.");
        if (r.Amount <= 0 || r.Amount > Money.MaxAmount) return Invalid("Amount", "Enter the amount paid.");
        if (Day(r.PaidOn, clock) is not { } on || on < loan.PledgedOn || on > IndiaTime.Today(clock)) return Invalid("PaidOn", "Enter the date paid.");
        var past = await db.GirviPayments.AsNoTracking().Where(p => p.LoanId == id).ToListAsync(ct);
        var (interestDue, principalLeft, _) = Owed(loan, past, on);
        if (r.Amount > interestDue + principalLeft) return Invalid("Amount", $"Only ₹{interestDue + principalLeft:N0} is owed on this girvi.");
        var interestPart = Math.Min(r.Amount, interestDue);
        var payment = new GirviPayment { LoanId = id, PaidOn = on, Amount = r.Amount, InterestPart = interestPart, PrincipalPart = r.Amount - interestPart, Mode = r.Mode ?? PaymentMode.Cash, At = clock.GetUtcNow() };
        db.GirviPayments.Add(payment);
        if (payment.PrincipalPart >= principalLeft) loan.ClosedOn = on;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await GirviViewsAsync(db, clock, q => q.Where(l => l.Id == id), ct)).Single());
    }

    /// <summary>
    /// More money lent on the same pledge (a top-up). Kept as a payment with a negative principal part, so interest on
    /// it runs from its own date and the loan's history stays in one place. Owner only; the pledge must still cover it.
    /// </summary>
    private static async Task<IResult> GirviTopUpAsync(Guid id, GirviTopUpRequest r, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the owner lends more on a girvi.");
        if (await db.GirviLoans.SingleOrDefaultAsync(l => l.Id == id, ct) is not { } loan) return TypedResults.NotFound();
        if (loan.ClosedOn is not null) return Invalid("Amount", "This girvi is already closed.");
        if (r.Amount <= 0 || r.Amount > Money.MaxAmount) return Invalid("Amount", "Enter the amount lent.");
        if (r.Mode is { } m && m is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Bank)) return Invalid("Mode", "Lend by cash, UPI or bank.");
        if (Day(r.On, clock) is not { } on || on < loan.PledgedOn || on > IndiaTime.Today(clock)) return Invalid("On", "Enter the date lent.");
        var past = await db.GirviPayments.AsNoTracking().Where(p => p.LoanId == id).ToListAsync(ct);
        var (interestDue, principalLeft, _) = Owed(loan, past, on);
        if (loan.PledgeValue > 0 && principalLeft + interestDue + r.Amount > loan.PledgeValue)
            return Invalid("Amount", $"The pledge is worth ₹{loan.PledgeValue:N0}: at most ₹{Math.Max(0, loan.PledgeValue - principalLeft - interestDue):N0} more can be lent on it.");
        db.GirviPayments.Add(new GirviPayment { LoanId = id, PaidOn = on, Amount = -r.Amount, InterestPart = 0, PrincipalPart = -r.Amount, Mode = r.Mode ?? PaymentMode.Cash, At = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await GirviViewsAsync(db, clock, q => q.Where(l => l.Id == id), ct)).Single());
    }

    // ---------------- schemes ----------------

    private static async Task<IResult> PlansAsync(AppDbContext db, CancellationToken ct)
    {
        var counts = await db.SchemeMembers.AsNoTracking().Where(m => m.ClosedOn == null).GroupBy(m => m.PlanId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var plans = await db.SchemePlans.AsNoTracking().OrderBy(p => p.CreatedAt).ToListAsync(ct);
        return TypedResults.Ok(plans.Select(p => new SchemePlanDto(p.Id, p.Name, p.Months, p.Instalment, p.Bonus, p.IsActive, counts.GetValueOrDefault(p.Id), p.Kind, p.Purity)));
    }

    private static async Task<IResult> AddPlanAsync(SchemePlanRequest r, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!LedgerEndpoints.IsOwner(user)) return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the owner starts a scheme.");
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 60) return Invalid("Name", "Name the scheme.");
        if (r.Months is < 2 or > 36 || r.Instalment is <= 0 or > 10_000_000 || r.Bonus is < 0 or > 10_000_000) return Invalid("Months", "Enter 2–36 months, the monthly instalment and the bonus.");
        var kind = r.Kind ?? "amount";
        if (kind is not ("amount" or "grams")) return Invalid("Kind", "A scheme saves rupees or grams of gold.");
        if (kind == "grams" && (r.Purity is not { } p || !JewelleryMath.ValidPurity("gold", p))) return Invalid("Purity", "Choose the gold purity the scheme saves in.");
        var plan = new SchemePlan { Name = r.Name.Trim(), Months = r.Months, Instalment = decimal.Round(r.Instalment, 2), Bonus = decimal.Round(r.Bonus ?? 0, 2), Kind = kind, Purity = kind == "grams" ? r.Purity : null };
        db.SchemePlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new SchemePlanDto(plan.Id, plan.Name, plan.Months, plan.Instalment, plan.Bonus, true, 0, plan.Kind, plan.Purity));
    }

    internal static async Task<List<SchemeMemberDto>> MemberViewsAsync(AppDbContext db, Func<IQueryable<SchemeMember>, IQueryable<SchemeMember>> filter, CancellationToken ct)
    {
        var members = await filter(db.SchemeMembers.AsNoTracking()).OrderByDescending(m => m.Number).Take(500).ToListAsync(ct);
        var ids = members.Select(m => m.Id).ToList();
        var paid = (await db.SchemeInstalments.AsNoTracking().Where(i => ids.Contains(i.MemberId)).ToListAsync(ct)).ToLookup(i => i.MemberId);
        var plans = await db.SchemePlans.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var custIds = members.Select(m => m.CustomerId).Distinct().ToList();
        var people = await db.Customers.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        return members.Select(m =>
        {
            var plan = plans[m.PlanId];
            var count = paid[m.Id].Count();
            var who = people.GetValueOrDefault(m.CustomerId);
            return new SchemeMemberDto(m.Id, m.Number, m.PlanId, plan.Name, m.CustomerId, who?.Name ?? "", who?.Phone, m.StartedOn, plan.Months, plan.Instalment, plan.Bonus,
                count, paid[m.Id].Sum(i => i.Amount), count >= plan.Months, m.RedeemedBillId, m.ClosedOn, m.StartedOn.AddMonths(Math.Min(count, plan.Months)),
                plan.Kind, plan.Purity, paid[m.Id].Sum(i => i.Grams ?? 0));
        }).ToList();
    }

    private static async Task<IResult> MembersAsync(string? status, AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await MemberViewsAsync(db, q => status == "closed" ? q.Where(m => m.ClosedOn != null) : status == "all" ? q : q.Where(m => m.ClosedOn == null), ct));

    private static async Task<IResult> JoinAsync(SchemeJoinRequest r, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.SchemePlans.AnyAsync(p => p.Id == r.PlanId && p.IsActive, ct)) return Invalid("PlanId", "Choose a running scheme.");
        if (!await db.Customers.AnyAsync(c => c.Id == r.CustomerId, ct)) return Invalid("CustomerId", "Choose the customer.");
        var member = new SchemeMember { PlanId = r.PlanId, CustomerId = r.CustomerId, StartedOn = IndiaTime.Today(clock), Number = (await db.SchemeMembers.MaxAsync(m => (int?)m.Number, ct) ?? 0) + 1 };
        db.SchemeMembers.Add(member);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await MemberViewsAsync(db, q => q.Where(m => m.Id == member.Id), ct)).Single());
    }

    /// <summary>An instalment is a khata payment by the member (an advance the final bill uses), and is counted on the scheme.</summary>
    private static async Task<IResult> PayInstalmentAsync(Guid id, SchemePayRequest r, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.SchemeInstalments.AsNoTracking().AnyAsync(i => i.EntryId == r.EntryId, ct)) return TypedResults.Ok((await MemberViewsAsync(db, q => q.Where(m => m.Id == id), ct)).Single());
        if (await db.SchemeMembers.SingleOrDefaultAsync(m => m.Id == id, ct) is not { } member) return TypedResults.NotFound();
        if (member.ClosedOn is not null) return Invalid("Amount", "This membership is closed.");
        var plan = await db.SchemePlans.SingleAsync(p => p.Id == member.PlanId, ct);
        if (await db.SchemeInstalments.CountAsync(i => i.MemberId == id, ct) >= plan.Months) return Invalid("Amount", "Every instalment is paid. The member can buy jewellery now.");
        var amount = r.Amount ?? plan.Instalment;
        if (amount <= 0 || amount > Money.MaxAmount) return Invalid("Amount", "Enter the instalment.");
        // A gram scheme turns the instalment into gold at the day's selling rate for its purity.
        decimal? grams = null, rateUsed = null;
        if (plan.Kind == "grams")
        {
            var rates = (await JewelleryEndpoints.CurrentRatesAsync(db, clock, ct)).Current;
            var rate = rates.FirstOrDefault(x => x.Metal == "gold" && x.Purity == plan.Purity)?.SellPerGram ?? 0;
            if (rate <= 0) return Invalid("Amount", "Set today's gold rate first: a gram scheme turns each instalment into gold at the day's rate.");
            (grams, rateUsed) = (decimal.Round(amount / rate, 3), rate);
        }
        var entry = await ledger.AddKhataEntryAsync(new KhataEntryRequest(r.EntryId, member.CustomerId, KhataEntryType.Payment, amount, r.Mode ?? PaymentMode.Cash, $"Scheme {plan.Name} #{member.Number}", null),
            user.UserId()!.Value, ct, LedgerEndpoints.IsOwner(user));
        if (entry.Value is null) return entry.ToHttp("");
        db.SchemeInstalments.Add(new SchemeInstalment { MemberId = id, EntryId = r.EntryId, Amount = amount, PaidOn = IndiaTime.Today(clock), Grams = grams, RatePerGram = rateUsed });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await MemberViewsAsync(db, q => q.Where(m => m.Id == id), ct)).Single());
    }

    // ---------------- orders and repairs ----------------

    private static async Task<List<JewelleryOrderDto>> OrderViewsAsync(AppDbContext db, TimeProvider clock, Func<IQueryable<JewelleryOrder>, IQueryable<JewelleryOrder>> filter, CancellationToken ct)
    {
        var orders = await filter(db.JewelleryOrders.AsNoTracking()).OrderBy(o => o.DueOn).ThenBy(o => o.Number).Take(500).ToListAsync(ct);
        var custIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var people = await db.Customers.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Phone }, ct);
        var today = IndiaTime.Today(clock);
        return orders.Select(o =>
        {
            var who = people.GetValueOrDefault(o.CustomerId);
            return new JewelleryOrderDto(o.Id, o.Number, o.Kind, o.CustomerId, who?.Name ?? "", who?.Phone, o.Description, o.Metal, o.Purity, o.Weight, o.WeightOut, o.FixedRate, o.Charge,
                o.DueOn, o.Status, o.Advance, o.BillId, o.Note, o.Status is "booked" && today > o.DueOn, o.CreatedAt);
        }).ToList();
    }

    private static async Task<IResult> OrdersAsync(string? status, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await OrderViewsAsync(db, clock, q => status == "done" ? q.Where(o => o.Status == "delivered" || o.Status == "cancelled") : status == "all" ? q : q.Where(o => o.Status == "booked" || o.Status == "ready"), ct));

    private static async Task<IResult> AddOrderAsync(JewelleryOrderRequest r, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (r.Kind is not ("order" or "repair")) return Invalid("Kind", "An order or a repair.");
        if (!await db.Customers.AnyAsync(c => c.Id == r.CustomerId, ct)) return Invalid("CustomerId", "Choose the customer.");
        if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Trim().Length > 200) return Invalid("Description", "Describe the piece or the repair.");
        if (r.Metal is not null && r.Metal is not ("gold" or "silver")) return Invalid("Metal", "Gold or silver.");
        if (r.Weight is < 0 or > 10_000 || r.FixedRate is < 0 or > 1_000_000 || r.Charge is < 0 or > 10_000_000) return Invalid("Weight", "Enter weights and amounts of zero or more.");
        if (Day(r.DueOn, clock) is not { } due || due < IndiaTime.Today(clock)) return Invalid("DueOn", "Enter the delivery date.");
        var order = new JewelleryOrder
        {
            Number = (await db.JewelleryOrders.MaxAsync(o => (int?)o.Number, ct) ?? 0) + 1, Kind = r.Kind, CustomerId = r.CustomerId, Description = r.Description.Trim(), Metal = r.Metal,
            Purity = r.Purity, Weight = r.Weight is { } w ? JewelleryMath.Grams(w) : null, FixedRate = r.FixedRate, Charge = r.Charge, DueOn = due, Status = "booked",
            Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim()[..Math.Min(200, r.Note.Trim().Length)],
        };
        db.JewelleryOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await OrderViewsAsync(db, clock, q => q.Where(o => o.Id == order.Id), ct)).Single());
    }

    /// <summary>An advance is a khata payment by the customer; the final bill uses it up.</summary>
    private static async Task<IResult> OrderAdvanceAsync(Guid id, JewelleryAdvanceRequest r, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.JewelleryOrders.SingleOrDefaultAsync(o => o.Id == id, ct) is not { } order) return TypedResults.NotFound();
        if (order.Status is "delivered" or "cancelled") return Invalid("Amount", "This order is closed.");
        if (r.Amount <= 0 || r.Amount > Money.MaxAmount) return Invalid("Amount", "Enter the advance.");
        var entry = await ledger.AddKhataEntryAsync(new KhataEntryRequest(r.EntryId, order.CustomerId, KhataEntryType.Payment, r.Amount, r.Mode ?? PaymentMode.Cash,
            $"Advance on {(order.Kind == "repair" ? "repair" : "order")} #{order.Number}", null), user.UserId()!.Value, ct, LedgerEndpoints.IsOwner(user));
        if (entry.Value is null) return entry.ToHttp("");
        order.Advance += r.Amount;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await OrderViewsAsync(db, clock, q => q.Where(o => o.Id == id), ct)).Single());
    }

    private static async Task<IResult> OrderStatusAsync(Guid id, JewelleryOrderStatusRequest r, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.JewelleryOrders.SingleOrDefaultAsync(o => o.Id == id, ct) is not { } order) return TypedResults.NotFound();
        if (r.Status is not ("booked" or "ready" or "delivered" or "cancelled")) return Invalid("Status", "Booked, ready, delivered or cancelled.");
        if (order.Status is "delivered" or "cancelled") return Invalid("Status", "This order is closed.");
        order.Status = r.Status;
        if (r.WeightOut is { } w) order.WeightOut = JewelleryMath.Grams(w);
        if (r.Charge is { } c) order.Charge = c;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await OrderViewsAsync(db, clock, q => q.Where(o => o.Id == id), ct)).Single());
    }

    /// <summary>Hands a repair back on a bill for its charge (GST 18%, a service); any advance on the khata pays towards it.</summary>
    private static async Task<IResult> DeliverRepairAsync(Guid id, RepairDeliverRequest r, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await db.JewelleryOrders.SingleOrDefaultAsync(o => o.Id == id, ct) is not { } order) return TypedResults.NotFound();
        if (order.Kind != "repair") return Invalid("Kind", "Orders are billed on the jewellery bill.");
        if (order.Status is "delivered" or "cancelled") return order.BillId == r.BillId ? TypedResults.Ok((await OrderViewsAsync(db, clock, q => q.Where(o => o.Id == id), ct)).Single()) : Invalid("Status", "This repair is closed.");
        if (r.Charge <= 0 || r.Charge > Money.MaxAmount) return Invalid("Charge", "Enter the repair charge.");
        var total = decimal.Round(r.Charge * 1.18m, 0, MidpointRounding.AwayFromZero);
        var result = await ledger.CreateBillAsync(new BillRequest(r.BillId, order.CustomerId, [new BillLineRequest(null, $"Repair #{order.Number}: {order.Description}", 1, Unit.Piece, total)],
            Math.Min(r.MoneyPaid, total), r.Mode ?? PaymentMode.Cash, null), user.UserId()!.Value, ct, isOwner: LedgerEndpoints.IsOwner(user), taxSnapshot: [(18m, "998729")]);
        if (result.Value is not { } bill) return result.ToHttp("");
        (order.Status, order.BillId, order.Charge) = ("delivered", bill.Id, r.Charge);
        if (r.WeightOut is { } w) order.WeightOut = JewelleryMath.Grams(w);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await OrderViewsAsync(db, clock, q => q.Where(o => o.Id == id), ct)).Single());
    }

    // ---------------- approval memos ----------------

    private static async Task<List<ApprovalDto>> ApprovalViewsAsync(AppDbContext db, TimeProvider clock, Func<IQueryable<ApprovalMemo>, IQueryable<ApprovalMemo>> filter, CancellationToken ct)
    {
        var memos = await filter(db.ApprovalMemos.AsNoTracking()).OrderByDescending(m => m.Number).Take(200).ToListAsync(ct);
        var ids = memos.Select(m => m.Id).ToList();
        var items = (await (from i in db.ApprovalItems.AsNoTracking()
                            where ids.Contains(i.MemoId)
                            join p in db.JewelleryPieces.AsNoTracking() on i.PieceId equals p.Id
                            select new { i, p.TagNo, p.Name }).ToListAsync(ct)).ToLookup(x => x.i.MemoId);
        var today = IndiaTime.Today(clock);
        return memos.Select(m => new ApprovalDto(m.Id, m.Number, m.CustomerId, m.Party, m.CreatedAt, m.DueBack, m.ClosedOn, m.ClosedOn is null && today > m.DueBack,
            items[m.Id].Select(x => new ApprovalItemDto(x.i.PieceId, x.TagNo, x.Name, x.i.GrossOut, x.i.GrossBack, x.i.ReturnedAt is not null, x.i.SoldBillId is not null,
                x.i.GrossBack is { } back && Math.Abs(back - x.i.GrossOut) > 0.005m)).ToList())).ToList();
    }

    private static async Task<IResult> ApprovalsAsync(string? status, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        TypedResults.Ok(await ApprovalViewsAsync(db, clock, q => status == "closed" ? q.Where(m => m.ClosedOn != null) : status == "all" ? q : q.Where(m => m.ClosedOn == null), ct));

    private static async Task<IResult> SendApprovalAsync(ApprovalRequest r, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (r.PieceIds is not { Count: > 0 and <= 50 }) return Invalid("PieceIds", "Choose the pieces sent.");
        string party;
        if (r.CustomerId is { } cid)
        {
            if (await db.Customers.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Name).SingleOrDefaultAsync(ct) is not { } name) return Invalid("CustomerId", "Choose the customer.");
            party = name;
        }
        else if (!string.IsNullOrWhiteSpace(r.Party) && r.Party.Trim().Length <= 80) party = r.Party.Trim();
        else return Invalid("Party", "Who took the pieces?");
        var pieces = await db.JewelleryPieces.Where(p => r.PieceIds.Contains(p.Id)).ToListAsync(ct);
        if (pieces.Count != r.PieceIds.Distinct().Count()) return Invalid("PieceIds", "A piece is not in stock.");
        var busy = await db.ApprovalItems.Where(i => r.PieceIds.Contains(i.PieceId) && i.ReturnedAt == null && i.SoldBillId == null).Select(i => i.PieceId).ToListAsync(ct);
        if (pieces.Any(p => p.SoldBillId is not null) || busy.Count > 0) return Invalid("PieceIds", "A piece is sold or already out on approval.");
        var memo = new ApprovalMemo { Number = (await db.ApprovalMemos.MaxAsync(m => (int?)m.Number, ct) ?? 0) + 1, CustomerId = r.CustomerId, Party = party, DueBack = IndiaTime.Today(clock).AddDays(Math.Clamp(r.Days ?? 3, 1, 60)) };
        db.ApprovalMemos.Add(memo);
        foreach (var p in pieces) db.ApprovalItems.Add(new ApprovalItem { MemoId = memo.Id, PieceId = p.Id, GrossOut = p.Gross });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ApprovalViewsAsync(db, clock, q => q.Where(m => m.Id == memo.Id), ct)).Single());
    }

    /// <summary>A piece comes back: it is weighed again (a different weight is flagged) and is back in stock. The memo closes when nothing is left out.</summary>
    private static async Task<IResult> ReturnApprovalAsync(Guid id, ApprovalReturnRequest r, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.ApprovalMemos.SingleOrDefaultAsync(m => m.Id == id, ct) is not { } memo) return TypedResults.NotFound();
        if (await db.ApprovalItems.SingleOrDefaultAsync(i => i.MemoId == id && i.PieceId == r.PieceId, ct) is not { } item) return TypedResults.NotFound();
        if (item.ReturnedAt is not null || item.SoldBillId is not null) return Invalid("PieceId", "This piece is already back or sold.");
        if (r.GrossBack <= 0 || r.GrossBack > 10_000) return Invalid("GrossBack", "Weigh the piece and enter its weight.");
        (item.GrossBack, item.ReturnedAt) = (JewelleryMath.Grams(r.GrossBack), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await CloseIfDoneAsync(db, memo, clock, ct);
        return TypedResults.Ok((await ApprovalViewsAsync(db, clock, q => q.Where(m => m.Id == id), ct)).Single());
    }

    internal static async Task CloseIfDoneAsync(AppDbContext db, ApprovalMemo memo, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.ApprovalItems.AnyAsync(i => i.MemoId == memo.Id && i.ReturnedAt == null && i.SoldBillId == null, ct))
        {
            memo.ClosedOn = IndiaTime.Today(clock);
            await db.SaveChangesAsync(ct);
        }
    }
}
