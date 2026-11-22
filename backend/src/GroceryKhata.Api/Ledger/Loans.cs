using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>Where a loan's money went when it was taken: the drawer, the bank, or straight to a seller (a machine bought on loan).</summary>
public enum LoanReceivedInto
{
    Cash,
    Bank,
    Direct,
}

/// <summary>
/// A business loan, kept apart from the day's money: how much was taken, at what yearly interest, and when. What is
/// still owed is the amount less the principal part of each EMI paid. It shows on the balance sheet as a loan.
/// </summary>
public sealed class Loan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Lender { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Yearly interest, in percent (12 = 12% a year). The EMI's interest part is worked out monthly on what is still owed.</summary>
    public decimal RatePercent { get; set; }

    public DateOnly StartedOn { get; set; }

    /// <summary>The usual instalment, to fill in each month. Null: the shop types it each time.</summary>
    public decimal? Emi { get; set; }

    public LoanReceivedInto ReceivedInto { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset? StruckAt { get; set; }
}

/// <summary>
/// One instalment: <see cref="Principal"/> lowers the loan and stays out of profit; <see cref="Interest"/> is a cost of
/// the business. Each is written to the kharcha book (categories Loan and Interest), so the cash or bank book drops by
/// the whole EMI.
/// </summary>
public sealed class LoanPayment : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid LoanId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public decimal Principal { get; set; }

    public decimal Interest { get; set; }

    public PaymentMode Mode { get; set; }

    public Guid? PrincipalExpenseId { get; set; }

    public Guid? InterestExpenseId { get; set; }

    public DateTimeOffset? StruckAt { get; set; }
}

public sealed record LoanRequest(Guid Id, string? Lender, decimal Amount, decimal RatePercent, DateOnly? StartedOn, decimal? Emi, LoanReceivedInto? ReceivedInto, string? Note);

/// <summary><see cref="Interest"/> null: worked out on what is still owed (monthly, at the loan's rate).</summary>
public sealed record LoanPaymentRequest(Guid Id, decimal Amount, decimal? Interest, PaymentMode? Mode, DateTimeOffset? OccurredAt);

public sealed record LoanDto(
    Guid Id, string Lender, decimal Amount, decimal RatePercent, DateOnly StartedOn, decimal? Emi, LoanReceivedInto ReceivedInto, string? Note,
    decimal PrincipalPaid, decimal InterestPaid, decimal Outstanding, int Payments, DateTimeOffset? LastPaidAt, decimal NextInterest);

public sealed record LoanPaymentDto(Guid Id, DateTimeOffset OccurredAt, decimal Amount, decimal Principal, decimal Interest, PaymentMode Mode, decimal OutstandingAfter);

public sealed record LoanStatementDto(LoanDto Loan, IReadOnlyList<LoanPaymentDto> Payments);

public static class Loans
{
    public static IEndpointRouteBuilder MapLoans(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/loans").WithTags("Money").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapPost("", AddAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/{id:guid}/strike", StrikeLoanAsync);
        group.MapPost("/{id:guid}/payments", PayAsync);
        group.MapPost("/{id:guid}/payments/{paymentId:guid}/strike", StrikePaymentAsync);
        return app;
    }

    /// <summary>What all the shop's loans still owe at the end of a day: for the balance sheet.</summary>
    public static async Task<decimal> OutstandingAsync(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        var end = IndiaTime.Day(day).End;
        var taken = await db.Loans.AsNoTracking().Where(l => l.StruckAt == null && l.StartedOn <= day).SumAsync(l => (decimal?)l.Amount, ct) ?? 0;
        var repaid = await (from p in db.LoanPayments.AsNoTracking()
                            join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                            where p.StruckAt == null && l.StruckAt == null && p.OccurredAt < end
                            select (decimal?)p.Principal).SumAsync(ct) ?? 0;
        return taken - repaid;
    }

    /// <summary>One month's interest on what is still owed, at the yearly rate.</summary>
    public static decimal MonthInterest(decimal outstanding, decimal ratePercent) => outstanding <= 0 ? 0 : decimal.Round(outstanding * ratePercent / 1200, 2);

    private static async Task<List<LoanDto>> LoadAsync(AppDbContext db, Guid? only, CancellationToken ct)
    {
        var loans = await db.Loans.AsNoTracking().Where(l => l.StruckAt == null && (only == null || l.Id == only)).OrderBy(l => l.StartedOn).ToListAsync(ct);
        var ids = loans.Select(l => l.Id).ToList();
        var paid = (await db.LoanPayments.AsNoTracking().Where(p => p.StruckAt == null && ids.Contains(p.LoanId))
            .GroupBy(p => p.LoanId).Select(g => new { g.Key, Principal = g.Sum(p => p.Principal), Interest = g.Sum(p => p.Interest), Count = g.Count(), Last = g.Max(p => p.OccurredAt) })
            .ToListAsync(ct)).ToDictionary(x => x.Key);
        return loans.Select(l =>
        {
            var p = paid.GetValueOrDefault(l.Id);
            var outstanding = l.Amount - (p?.Principal ?? 0);
            return new LoanDto(l.Id, l.Lender, l.Amount, l.RatePercent, l.StartedOn, l.Emi, l.ReceivedInto, l.Note,
                p?.Principal ?? 0, p?.Interest ?? 0, outstanding, p?.Count ?? 0, p?.Last, MonthInterest(outstanding, l.RatePercent));
        }).ToList();
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct) => TypedResults.Ok(await LoadAsync(db, null, ct));

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        if ((await LoadAsync(db, id, ct)).SingleOrDefault() is not { } loan)
        {
            return TypedResults.NotFound();
        }

        var payments = await db.LoanPayments.AsNoTracking().Where(p => p.LoanId == id && p.StruckAt == null).OrderBy(p => p.OccurredAt).ToListAsync(ct);
        var owed = loan.Amount;
        var rows = payments.Select(p => { owed -= p.Principal; return new LoanPaymentDto(p.Id, p.OccurredAt, p.Principal + p.Interest, p.Principal, p.Interest, p.Mode, owed); }).ToList();
        return TypedResults.Ok(new LoanStatementDto(loan, rows));
    }

    private static async Task<IResult> AddAsync(LoanRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var lender = request.Lender?.Trim();
        if (lender is not { Length: >= 2 and <= 80 }) errors["Lender"] = ["Enter who the loan is from (a bank or a person)."];
        if (request.Amount <= 0 || request.Amount > Money.MaxAmount) errors["Amount"] = ["Enter the loan amount."];
        if (request.RatePercent is < 0 or > 60) errors["RatePercent"] = ["Enter the yearly interest, 0 to 60%."];
        if (request.Emi is <= 0) errors["Emi"] = ["Leave the EMI empty or enter an amount above zero."];
        var today = IndiaTime.Today(clock);
        var started = request.StartedOn ?? today;
        if (started > today) errors["StartedOn"] = ["The loan can't start in the future."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await db.Loans.AnyAsync(l => l.Id == request.Id, ct))
        {
            return TypedResults.Ok((await LoadAsync(db, request.Id, ct)).SingleOrDefault());
        }

        var into = request.ReceivedInto ?? LoanReceivedInto.Cash;
        db.Loans.Add(new Loan { Id = request.Id, Lender = lender!, Amount = request.Amount, RatePercent = request.RatePercent, StartedOn = started, Emi = request.Emi, ReceivedInto = into, Note = request.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 200)] : null });
        // The money arriving: into the drawer, or into the bank (through the drawer's to-bank move, so the bank book shows it).
        if (into != LoanReceivedInto.Direct)
        {
            var at = started == today ? clock.GetUtcNow() : IndiaTime.Day(started).Start.AddHours(10);
            var by = user.UserId()!.Value;
            db.CashMoves.Add(new CashMove { Kind = CashMoveKind.Added, Amount = request.Amount, Note = $"Loan from {lender}", OccurredAt = at, CreatedByUserId = by, LoanId = request.Id });
            if (into == LoanReceivedInto.Bank)
            {
                db.CashMoves.Add(new CashMove { Kind = CashMoveKind.ToBank, Amount = request.Amount, Note = $"Loan from {lender}", OccurredAt = at, CreatedByUserId = by, LoanId = request.Id });
            }
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/loans/{request.Id}", (await LoadAsync(db, request.Id, ct)).Single());
    }

    private static async Task<IResult> PayAsync(Guid id, LoanPaymentRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if ((await LoadAsync(db, id, ct)).SingleOrDefault() is not { } loan)
        {
            return TypedResults.NotFound();
        }

        if (await db.LoanPayments.AnyAsync(p => p.Id == request.Id, ct))
        {
            return TypedResults.Ok(loan);
        }

        var interest = request.Interest ?? Math.Min(request.Amount, loan.NextInterest);
        var principal = request.Amount - interest;
        var errors = new Dictionary<string, string[]>();
        if (request.Amount <= 0 || request.Amount > Money.MaxAmount) errors["Amount"] = ["Enter the amount paid."];
        else if (interest < 0 || interest > request.Amount) errors["Interest"] = ["The interest part must be between 0 and the amount paid."];
        else if (principal > loan.Outstanding + 0.01m) errors["Amount"] = [$"Only {loan.Outstanding:0.##} is still owed on this loan."];
        var mode = request.Mode ?? PaymentMode.Cash;
        if (mode is not (PaymentMode.Cash or PaymentMode.Upi or PaymentMode.Bank)) errors["Mode"] = ["Pay by cash, UPI or bank."];
        var now = clock.GetUtcNow();
        var at = request.OccurredAt ?? now;
        if (at > now.AddMinutes(5)) errors["OccurredAt"] = ["The payment can't be in the future."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var by = user.UserId()!.Value;
        Expense? Kharcha(decimal amount, ExpenseCategory category, string note) => amount <= 0 ? null
            : new Expense { OccurredAt = at, Amount = amount, Category = category, Note = note, Mode = mode, CreatedByUserId = by };
        var principalExpense = Kharcha(principal, ExpenseCategory.Loan, $"EMI {loan.Lender}: principal");
        var interestExpense = Kharcha(interest, ExpenseCategory.Interest, $"EMI {loan.Lender}: interest");
        foreach (var e in new[] { principalExpense, interestExpense }.OfType<Expense>())
        {
            db.Expenses.Add(e);
        }

        db.LoanPayments.Add(new LoanPayment { Id = request.Id, LoanId = id, OccurredAt = at, Principal = principal, Interest = interest, Mode = mode, PrincipalExpenseId = principalExpense?.Id, InterestExpenseId = interestExpense?.Id });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await LoadAsync(db, id, ct)).Single());
    }

    /// <summary>An EMI entered by mistake: struck, with its two kharcha lines, so the loan and the books go back.</summary>
    private static async Task<IResult> StrikePaymentAsync(Guid id, Guid paymentId, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var payment = await db.LoanPayments.SingleOrDefaultAsync(p => p.Id == paymentId && p.LoanId == id, ct);
        if (payment is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow();
        var by = user.UserId()!.Value;
        payment.StruckAt ??= now;
        foreach (var e in await db.Expenses.Where(e => (e.Id == payment.PrincipalExpenseId || e.Id == payment.InterestExpenseId) && e.StruckAt == null).ToListAsync(ct))
        {
            e.StruckAt = now;
            e.StruckByUserId = by;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>A loan entered by mistake (no EMI paid yet): struck, with the money it brought in.</summary>
    private static async Task<IResult> StrikeLoanAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var loan = await db.Loans.SingleOrDefaultAsync(l => l.Id == id, ct);
        if (loan is null)
        {
            return TypedResults.NotFound();
        }

        if (await db.LoanPayments.AnyAsync(p => p.LoanId == id && p.StruckAt == null, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "EMIs are recorded on this loan.", detail: "Remove its EMIs first.");
        }

        var now = clock.GetUtcNow();
        var by = user.UserId()!.Value;
        loan.StruckAt ??= now;
        foreach (var m in await db.CashMoves.Where(m => m.LoanId == id && m.StruckAt == null).ToListAsync(ct))
        {
            m.StruckAt = now;
            m.StruckByUserId = by;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
