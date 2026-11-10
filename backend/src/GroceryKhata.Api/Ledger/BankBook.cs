using System.Globalization;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

public enum BankBookSource
{
    /// <summary>The UPI or card part of a bill paid at the counter.</summary>
    Bill,

    /// <summary>A bill cancelled later: its UPI or card money goes back (negative).</summary>
    BillCancelled,

    /// <summary>A khata payment by UPI or card, or a cheque on the day it cleared.</summary>
    KhataPayment,

    /// <summary>A khata payment taken back (negative).</summary>
    Reversal,

    /// <summary>Kharcha or a supplier paid by UPI or bank (negative).</summary>
    Expense,

    /// <summary>Cash taken from the galla and put in the bank: out of the cash book, into this one (a contra entry).</summary>
    Deposit,

    /// <summary>Cash drawn from the bank into the galla (negative here, in the cash book).</summary>
    Withdrawal,
}

/// <summary>One line of the bank book, with the balance after it.</summary>
public sealed record BankBookLine(DateTimeOffset At, BankBookSource Source, string Who, PaymentMode Mode, decimal Amount, string? Reference, decimal Balance, Guid? BillId = null, string? Account = null);

/// <summary>Money in by way, for the period.</summary>
public sealed record BankBookIn(decimal Upi, decimal Card, decimal Cheque, decimal Deposits);

/// <summary>
/// The bank book for a period: the shop's one bank account, where UPI, card and cleared cheques land and from which
/// UPI and bank payments go. Opening and closing balances, so the owner can tick it against the passbook.
/// </summary>
public sealed record BankBookDto(
    DateOnly From, DateOnly To, decimal Opening, decimal In, decimal Out, decimal Closing, BankBookIn InBy, IReadOnlyList<BankBookLine> Lines,
    decimal? StartingBalance, DateOnly? StartingDate, string Account = BankSetup.Main, IReadOnlyList<BankAccountView>? Accounts = null);

public sealed record BankOpeningRequest(decimal? Amount, DateOnly? Date, string? Account = null);

/// <summary>One of the shop's bank accounts, with the passbook balance its book starts from.</summary>
public sealed record BankAccount(string Id, string Name, decimal? Opening = null, DateOnly? OpeningDate = null);

public sealed record BankAccountView(string Id, string Name);

/// <summary>
/// The shop's bank accounts (one to three) and which one each kind of money uses: UPI settles to one, the card machine
/// often to another; cheques are paid into one; bank payments and cash deposits go from or to one.
/// </summary>
public sealed record BankSetup(IReadOnlyList<BankAccount> Accounts, string Upi, string Card, string Cheque, string Out, string Deposit)
{
    public const string Main = "main";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static BankSetup Read(string? json, decimal? opening, DateOnly? openingDate) =>
        (json is null ? null : JsonSerializer.Deserialize<BankSetup>(json, JsonOptions))
        ?? new BankSetup([new BankAccount(Main, "Bank", opening, openingDate)], Main, Main, Main, Main, Main);

    public string Write() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>The account a line of money belongs to.</summary>
    public string AccountOf(BankBookLine l) => l.Account is { } named && Accounts.Any(a => a.Id == named) ? named : l.Source switch
    {
        BankBookSource.Deposit or BankBookSource.Withdrawal => Deposit,
        BankBookSource.Expense => l.Mode == PaymentMode.Upi ? Upi : Out,
        _ => l.Mode switch { PaymentMode.Card => Card, PaymentMode.Cheque => Cheque, _ => Upi },
    };
}

public static class BankBook
{
    public static IEndpointRouteBuilder MapBankBook(this IEndpointRouteBuilder app)
    {
        // The bank balance is the owner's business.
        var group = app.MapGroup("/api/v1/bank-book").WithTags("Khata").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", GetAsync);
        group.MapPut("/opening", SetOpeningAsync);
        group.MapGet("/accounts", (AppDbContext db, ITenantContext tenant, CancellationToken ct) => SetupAsync(db, tenant, ct));
        group.MapPut("/accounts", SetAccountsAsync);
        return app;
    }

    private static async Task<BankSetup> SetupAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.BankAccounts, s.BankOpeningBalance, s.BankOpeningDate }).SingleAsync(ct);
        return BankSetup.Read(shop.BankAccounts, shop.BankOpeningBalance, shop.BankOpeningDate);
    }

    private static async Task<IResult> GetAsync(string? from, string? to, string? account, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        DateOnly start = new(today.Year, today.Month, 1), end = today;
        if ((from is not null && !DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
            || (to is not null && !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["from"] = ["Use dates like 2026-11-01."] });
        }

        if (end < start || end.DayNumber - start.DayNumber > 366)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["to"] = ["Pick up to a year, ending on or after the start."] });
        }

        var setup = await SetupAsync(db, tenant, ct);
        var book = account is null ? setup.Accounts[0] : setup.Accounts.FirstOrDefault(a => a.Id == account);
        if (book is null)
        {
            return TypedResults.NotFound();
        }

        async Task<List<BankBookLine>> Mine(DateOnly a, DateOnly b) => (await MovementsAsync(db, a, b, ct)).Where(l => setup.AccountOf(l) == book.Id).ToList();

        // The book starts at the passbook balance the owner gave (or 0 at the shop's first day); everything between that
        // and the period comes into the period's opening.
        var startingDate = book.OpeningDate ?? new DateOnly(2000, 1, 1);
        var bookStart = start < startingDate ? startingDate : start;
        var before = bookStart > startingDate ? await Mine(startingDate, bookStart.AddDays(-1)) : [];
        var opening = (book.Opening ?? 0) + before.Sum(l => l.Amount);
        var lines = end < bookStart ? [] : await Mine(bookStart, end);
        var running = opening;
        var withBalance = lines.Select(l => l with { Balance = running += l.Amount }).ToList();
        var inLines = lines.Where(l => l.Amount > 0).ToList();
        decimal By(Func<BankBookLine, bool> which) => inLines.Where(which).Sum(l => l.Amount);
        var inBy = new BankBookIn(
            By(l => l.Mode == PaymentMode.Upi && l.Source != BankBookSource.Deposit), By(l => l.Mode == PaymentMode.Card),
            By(l => l.Mode == PaymentMode.Cheque), By(l => l.Source == BankBookSource.Deposit));
        var moneyIn = inLines.Sum(l => l.Amount);
        var moneyOut = -lines.Where(l => l.Amount < 0).Sum(l => l.Amount);
        return TypedResults.Ok(new BankBookDto(start, end, opening, moneyIn, moneyOut, opening + moneyIn - moneyOut, inBy, withBalance, book.Opening, book.OpeningDate,
            book.Id, setup.Accounts.Select(a => new BankAccountView(a.Id, a.Name)).ToList()));
    }

    private static async Task<IResult> SetOpeningAsync(BankOpeningRequest request, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (request.Amount is { } amount && (decimal.Round(amount, 2) != amount || Math.Abs(amount) > 100_000_000_000m))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["amount"] = ["Enter the passbook balance in rupees, at most two decimals."] });
        }

        if (request.Amount is not null && (request.Date is null || request.Date > IndiaTime.Today(clock)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Pick the passbook date (today or earlier)."] });
        }

        var setup = await SetupAsync(db, tenant, ct);
        var id = request.Account ?? setup.Accounts[0].Id;
        if (setup.Accounts.All(a => a.Id != id))
        {
            return TypedResults.NotFound();
        }

        // The balance is at the start of that date: the book adds that day's own money on top.
        var updated = setup with { Accounts = setup.Accounts.Select(a => a.Id == id ? a with { Opening = request.Amount, OpeningDate = request.Amount == null ? null : request.Date } : a).ToList() };
        await db.Shops.Where(s => s.Id == tenant.ShopId).ExecuteUpdateAsync(s => s.SetProperty(x => x.BankAccounts, updated.Write()), ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Names the accounts and says which kind of money uses which. An account in the list without a known id is new; a
    /// kind can point at a new one by its place in the list ("#1"). A removed account's money goes to whichever account
    /// its kind now uses.
    /// </summary>
    private static async Task<IResult> SetAccountsAsync(BankSetup request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var accounts = (request.Accounts ?? []).Select(a => a with { Name = a.Name?.Trim() ?? "" }).ToList();
        if (accounts.Count is < 1 or > 3 || accounts.Any(a => a.Name.Length is < 1 or > 40)
            || accounts.Select(a => a.Name.ToLowerInvariant()).Distinct().Count() != accounts.Count)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["accounts"] = ["Name one to three accounts, each different and under 40 characters."] });
        }

        var current = await SetupAsync(db, tenant, ct);
        var kept = accounts.Select(a => current.Accounts.FirstOrDefault(c => c.Id == a.Id) is { } old
            ? old with { Name = a.Name }
            : new BankAccount(Guid.CreateVersion7().ToString("N")[..12], a.Name)).ToList();
        string Resolve(string? id) =>
            id is not null && id.StartsWith('#') && int.TryParse(id[1..], out var i) && i >= 0 && i < kept.Count ? kept[i].Id
            : kept.Any(k => k.Id == id) ? id! : kept[0].Id;
        var setup = new BankSetup(kept, Resolve(request.Upi), Resolve(request.Card), Resolve(request.Cheque), Resolve(request.Out), Resolve(request.Deposit));
        await db.Shops.Where(s => s.Id == tenant.ShopId).ExecuteUpdateAsync(s => s.SetProperty(x => x.BankAccounts, setup.Write()), ct);
        return TypedResults.Ok(setup);
    }

    /// <summary>Every rupee that reached or left the bank from <paramref name="from"/> to <paramref name="to"/>, oldest first.</summary>
    internal static async Task<List<BankBookLine>> MovementsAsync(AppDbContext db, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var (start, _) = IndiaTime.Day(from);
        var (_, end) = IndiaTime.Day(to);
        var bank = new[] { PaymentMode.Upi, PaymentMode.Card };
        var lines = new List<BankBookLine>();

        // Bills: their UPI and card parts on the day they were made; taken back on the day one was cancelled.
        var bills = await (from b in db.Bills.AsNoTracking()
                           where ((b.OccurredAt >= start && b.OccurredAt < end) || (b.CancelledAt >= start && b.CancelledAt < end))
                               && (b.Payments != null || (b.PaymentMode != null && bank.Contains(b.PaymentMode.Value)))
                           join c in db.Customers.AsNoTracking() on b.CustomerId equals c.Id into customers
                           from c in customers.DefaultIfEmpty()
                           select new { b.Id, b.BillNo, b.OccurredAt, b.PaidAmount, b.PaymentMode, b.Payments, b.CancelledAt, Name = c == null ? null : c.Name }).ToListAsync(ct);
        foreach (var b in bills)
        {
            foreach (var p in BillPayments.Parts(b.PaymentMode, b.PaidAmount, b.Payments).Where(p => bank.Contains(p.Mode)))
            {
                if (b.OccurredAt >= start && b.OccurredAt < end)
                {
                    lines.Add(new BankBookLine(b.OccurredAt, BankBookSource.Bill, b.Name ?? "", p.Mode, p.Amount, b.BillNo, 0, b.Id));
                }

                if (b.CancelledAt >= start && b.CancelledAt < end)
                {
                    lines.Add(new BankBookLine(b.CancelledAt.Value, BankBookSource.BillCancelled, b.Name ?? "", p.Mode, -p.Amount, b.BillNo, 0, b.Id));
                }
            }
        }

        // Khata payments by UPI or card, and cleared cheques on the day they cleared (a bounced one never reached the bank).
        var khata = await (from e in db.LedgerEntries.AsNoTracking()
                           where e.BillId == null && e.TripId == null
                               && (((e.PaymentMode == PaymentMode.Upi || e.PaymentMode == PaymentMode.Card) && e.OccurredAt >= start && e.OccurredAt < end)
                                   || (e.PaymentMode == PaymentMode.Cheque && e.ChequeStatus == ChequeStatus.Cleared
                                       && (e.ReversesEntryId == null ? e.ChequeSettledAt >= start && e.ChequeSettledAt < end : e.OccurredAt >= start && e.OccurredAt < end)))
                           join c in db.Customers.AsNoTracking() on e.CustomerId equals c.Id
                           select new { e.Kind, e.Amount, e.PaymentMode, e.OccurredAt, e.ChequeSettledAt, e.ReversesEntryId, e.UpiRef, e.ChequeNo, c.Name }).ToListAsync(ct);
        lines.AddRange(khata.Select(e => new BankBookLine(
            e.PaymentMode == PaymentMode.Cheque && e.ReversesEntryId == null ? e.ChequeSettledAt!.Value : e.OccurredAt,
            e.Kind == LedgerEntryKind.Reversal ? BankBookSource.Reversal : BankBookSource.KhataPayment, e.Name, e.PaymentMode!.Value, -e.Amount,
            e.UpiRef ?? e.ChequeNo, 0)));

        // Money out by UPI or bank; and cash deposited from the galla, which comes in here.
        var spent = await db.Expenses.AsNoTracking()
            .Where(x => x.OccurredAt >= start && x.OccurredAt < end && x.StruckAt == null
                && (x.Mode == PaymentMode.Upi || x.Mode == PaymentMode.Bank || (x.Category == ExpenseCategory.Deposit && x.Mode == PaymentMode.Cash)))
            .Select(x => new { x.OccurredAt, x.Amount, x.Mode, x.Category, x.Note }).ToListAsync(ct);
        lines.AddRange(spent.Select(x => x.Category == ExpenseCategory.Deposit && x.Mode == PaymentMode.Cash
            ? new BankBookLine(x.OccurredAt, BankBookSource.Deposit, x.Note ?? "", PaymentMode.Cash, x.Amount, null, 0)
            : new BankBookLine(x.OccurredAt, BankBookSource.Expense, x.Note ?? x.Category.ToString(), x.Mode, -x.Amount, null, 0)));

        // Cash moved between the drawer and the bank by hand.
        lines.AddRange((await db.CashMoves.AsNoTracking()
            .Where(m => m.OccurredAt >= start && m.OccurredAt < end && m.StruckAt == null && (m.Kind == CashMoveKind.ToBank || m.Kind == CashMoveKind.FromBank))
            .ToListAsync(ct))
            .Select(m => m.Kind == CashMoveKind.ToBank
                ? new BankBookLine(m.OccurredAt, BankBookSource.Deposit, m.Note ?? "", PaymentMode.Cash, m.Amount, null, 0, null, m.BankAccount)
                : new BankBookLine(m.OccurredAt, BankBookSource.Withdrawal, m.Note ?? "", PaymentMode.Cash, -m.Amount, null, 0, null, m.BankAccount)));

        return lines.OrderBy(l => l.At).ToList();
    }
}
