using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>What the money went on. The same headings a shop's kharcha book uses.</summary>
public enum ExpenseCategory
{
    Tea,
    Transport,
    Rent,
    Electricity,
    Salary,
    /// <summary>Stock bought (maal kharid). Kept apart: the profit report already counts goods at cost.</summary>
    Purchase,
    Packaging,
    Repair,
    /// <summary>Taken home (owner's drawing). Lowers the cash box, never the shop's profit.</summary>
    Home,
    Other,

    // Money that leaves the cash box but is not a cost of running the shop, so it stays out of profit.

    /// <summary>GST, income tax or other tax paid.</summary>
    Tax,
    /// <summary>A loan instalment (EMI).</summary>
    Loan,
    /// <summary>Something that lasts years: a fridge, a machine, a counter.</summary>
    Equipment,
    /// <summary>Cash put into the bank.</summary>
    Deposit,
    /// <summary>An advance to staff, taken back from their salary later.</summary>
    Advance,

    /// <summary>Interest on a business loan: the cost part of an EMI, so it counts against profit (the principal part is <see cref="Loan"/>).</summary>
    Interest,
}

/// <summary>What "kharcha" means, the same on the kharcha page, the month report and its downloads.</summary>
public static class Kharcha
{
    /// <summary>Headings that are not a running cost: stock (already counted at cost), money taken home, and the out-of-profit headings.</summary>
    public static readonly ExpenseCategory[] OutsideProfit =
        [ExpenseCategory.Purchase, ExpenseCategory.Home, ExpenseCategory.Tax, ExpenseCategory.Loan, ExpenseCategory.Equipment, ExpenseCategory.Deposit, ExpenseCategory.Advance];

    /// <summary>A walk-in refund on a credit note: already counted as a return, so never kharcha.</summary>
    public static bool IsReturnRefund(string? note) => note is not null && (note.StartsWith("Refund CN/", StringComparison.Ordinal) || note.StartsWith("Refund RN/", StringComparison.Ordinal));

    /// <summary>A running cost of the shop, which lowers profit.</summary>
    public static bool IsRunningCost(ExpenseCategory category, string? note) => !OutsideProfit.Contains(category) && !IsReturnRefund(note);
}

/// <summary>
/// One line of the day's kharcha page. Never deleted: a mistake is struck through (who and when), like a pen line in
/// the notebook, so the cash book still adds up and nothing disappears quietly.
/// </summary>
public sealed class Expense : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public decimal Amount { get; set; }

    public ExpenseCategory Category { get; set; }

    /// <summary>The words as written ("chai", "auto to mandi").</summary>
    public string? Note { get; set; }

    public PaymentMode Mode { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset? StruckAt { get; set; }

    public Guid? StruckByUserId { get; set; }

    /// <summary>Spent by a technician on a field trip, from their advance: in the books, but not out of the drawer.</summary>
    public Guid? TripId { get; set; }
}

public sealed record ExpenseRequest(Guid Id, decimal Amount, ExpenseCategory? Category, string? Note, PaymentMode? Mode, DateTimeOffset? OccurredAt);

public sealed record ExpenseDto(
    Guid Id, DateTimeOffset OccurredAt, decimal Amount, ExpenseCategory Category, string? Note, PaymentMode Mode, string? By, DateTimeOffset? StruckAt);

/// <summary>One notebook page: the day's lines, and what went out by cash and by UPI (struck lines don't count).</summary>
/// <summary>One heading's month: how much, how many lines.</summary>
public sealed record ExpenseCategoryTotal(ExpenseCategory Category, decimal Total, int Count);

public sealed record ExpenseDayTotal(DateOnly Date, decimal Total, int Count);

/// <summary>
/// A month's kharcha, rolled up from the daily lines (struck lines left out): totals by cash and UPI, by heading and
/// by day, and every line for the printed report. "Taken home" is shown apart: it lowers the cash box, not the profit.
/// </summary>
public sealed record ExpenseMonthDto(
    string Month, bool Complete, decimal Total, decimal Cash, decimal Upi, decimal TakenHome, decimal ShopKharcha, int Count,
    IReadOnlyList<ExpenseCategoryTotal> ByCategory, IReadOnlyList<ExpenseDayTotal> ByDay, IReadOnlyList<ExpenseDto> Lines,
    decimal? PreviousMonthShopKharcha, decimal OutsideProfit = 0, decimal Refunds = 0);

public sealed record ExpensePageDto(DateOnly Date, decimal Cash, decimal Upi, decimal Total, IReadOnlyList<ExpenseDto> Lines);

public sealed record ExpenseReadRequest(string? Text);

/// <summary>A line read from free text, for the person to check before it is saved.</summary>
public sealed record ExpenseGuess(decimal Amount, ExpenseCategory Category, string? Note, PaymentMode Mode);

/// <summary>
/// The kharcha book: write a line the way you would in the notebook ("chai 40, auto 120, thaili 250 upi"), check what
/// was read, save. Owners and helpers both write; anyone can strike a line within 15 minutes, after that only the owner.
/// </summary>
public static class ExpenseEndpoints
{
    public static readonly TimeSpan StrikeWindow = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapExpenses(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/expenses").WithTags("Expenses").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", PageAsync);
        // The month shows salary and money taken home: the owner's figures, like the month report.
        group.MapGet("/month", MonthAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("", AddAsync);
        group.MapPost("/read", (ExpenseReadRequest request) => TypedResults.Ok(ExpenseReader.Read(request.Text)));
        group.MapPost("/{id:guid}/strike", StrikeAsync);
        return app;
    }

    private static async Task<IResult> PageAsync(string? date, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        DateOnly day;
        if (string.IsNullOrEmpty(date))
        {
            day = IndiaTime.Today(clock);
        }
        else if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Use a date like 2026-10-27."] });
        }

        return TypedResults.Ok(await LoadPageAsync(db, day, ct));
    }

    /// <summary>The month's kharcha ("2026-10"; empty: this month). Complete once the month is over.</summary>
    private static async Task<IResult> MonthAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var chosen = string.IsNullOrEmpty(month) ? (today.Year, today.Month) : IndiaTime.ParseMonth(month);
        if (chosen is not { } m)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
        }

        var (start, end) = IndiaTime.Month(m.Year, m.Month);
        var lines = await (from e in db.Expenses.AsNoTracking()
                           where e.OccurredAt >= start && e.OccurredAt < end && e.StruckAt == null
                           join u in db.Users.AsNoTracking() on e.CreatedByUserId equals u.Id into users
                           from u in users.DefaultIfEmpty()
                           orderby e.OccurredAt, e.CreatedAt
                           select new ExpenseDto(e.Id, e.OccurredAt, e.Amount, e.Category, e.Note, e.Mode, u == null ? null : u.FullName, e.StruckAt))
            .ToListAsync(ct);
        var home = lines.Where(l => l.Category == ExpenseCategory.Home).Sum(l => l.Amount);
        var total = lines.Sum(l => l.Amount);
        var byCategory = lines.GroupBy(l => l.Category).Select(g => new ExpenseCategoryTotal(g.Key, g.Sum(l => l.Amount), g.Count()))
            .OrderByDescending(c => c.Total).ToList();
        var byDay = lines.GroupBy(l => DateOnly.FromDateTime(l.OccurredAt.ToOffset(IndiaTime.Offset).DateTime))
            .Select(g => new ExpenseDayTotal(g.Key, g.Sum(l => l.Amount), g.Count())).OrderBy(d => d.Date).ToList();

        // Shop kharcha is running costs only, as in the month report: stock paid for, money taken home, the
        // out-of-profit headings and credit-note refunds are shown apart.
        var running = lines.Where(l => Kharcha.IsRunningCost(l.Category, l.Note)).Sum(l => l.Amount);
        var refunds = lines.Where(l => Kharcha.IsReturnRefund(l.Note)).Sum(l => l.Amount);

        // Last month's shop kharcha, to compare.
        var (prevStart, prevEnd) = IndiaTime.Month(m.Month == 1 ? m.Year - 1 : m.Year, m.Month == 1 ? 12 : m.Month - 1);
        // Summed in the database, on the same rule as Kharcha.IsRunningCost.
        var previousMonth = db.Expenses.AsNoTracking().Where(e => e.OccurredAt >= prevStart && e.OccurredAt < prevEnd && e.StruckAt == null);
        decimal? previous = !await previousMonth.AnyAsync(ct) ? null : await previousMonth
            .Where(e => !Kharcha.OutsideProfit.Contains(e.Category) && (e.Note == null || !(e.Note.StartsWith("Refund CN/") || e.Note.StartsWith("Refund RN/"))))
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        return TypedResults.Ok(new ExpenseMonthDto(
            $"{m.Year:D4}-{m.Month:D2}", end <= clock.GetUtcNow(), total,
            lines.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount), lines.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount),
            home, running, lines.Count, byCategory, byDay, lines, previous, total - home - running - refunds, refunds));
    }

    internal static async Task<ExpensePageDto> LoadPageAsync(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Day(day);
        var lines = await (from e in db.Expenses.AsNoTracking()
                           // A refund on a credit note isn't kharcha: it shows in the cash book as a refund, not on this page.
                           where e.OccurredAt >= start && e.OccurredAt < end && (e.Note == null || !(e.Note.StartsWith("Refund CN/") || e.Note.StartsWith("Refund RN/")))
                           join u in db.Users.AsNoTracking() on e.CreatedByUserId equals u.Id into users
                           from u in users.DefaultIfEmpty()
                           orderby e.OccurredAt, e.CreatedAt
                           select new ExpenseDto(e.Id, e.OccurredAt, e.Amount, e.Category, e.Note, e.Mode, u == null ? null : u.FullName, e.StruckAt))
            .ToListAsync(ct);
        var live = lines.Where(l => l.StruckAt is null).ToList();
        var cash = live.Where(l => l.Mode == PaymentMode.Cash).Sum(l => l.Amount);
        var upi = live.Where(l => l.Mode == PaymentMode.Upi).Sum(l => l.Amount);
        return new ExpensePageDto(day, cash, upi, cash + upi, lines);
    }

    /// <summary>Saves one line. Safe to send twice (the phone's ID makes it the same line), so an offline retry is harmless.</summary>
    private static async Task<IResult> AddAsync(ExpenseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty)
        {
            errors[nameof(request.Id)] = ["The app must send an ID for every new record."];
        }

        if (request.Amount <= 0 || request.Amount > Money.MaxAmount || decimal.Round(request.Amount, 2) != request.Amount)
        {
            errors[nameof(request.Amount)] = ["Enter the amount spent."];
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > 120)
        {
            errors[nameof(request.Note)] = ["Keep the note short (120 letters)."];
        }

        if (request.Mode is not (null or PaymentMode.Cash or PaymentMode.Upi))
        {
            errors[nameof(request.Mode)] = ["Cash or UPI."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await db.Expenses.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok(ToDto(existing));
        }

        var now = clock.GetUtcNow();
        var expense = new Expense
        {
            Id = request.Id,
            OccurredAt = request.OccurredAt is { } at && at <= now ? at.ToUniversalTime() : now,
            Amount = request.Amount,
            Category = request.Category ?? ExpenseReader.Guess(note ?? "").Category,
            Note = note,
            Mode = request.Mode ?? PaymentMode.Cash,
            CreatedByUserId = user.UserId()!.Value,
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/expenses/{expense.Id}", ToDto(expense));
    }

    private static async Task<IResult> StrikeAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null)
        {
            return TypedResults.NotFound();
        }

        if (Kharcha.IsReturnRefund(expense.Note))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This is a refund on a credit note, not kharcha: it can't be struck out here.");
        }

        var now = clock.GetUtcNow();
        var isOwner = user.FindFirst(AppClaims.Role)?.Value == RoleNames.ToName(ShopRole.Owner);
        if (expense.StruckAt is null)
        {
            if (!isOwner && now - expense.CreatedAt > StrikeWindow)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only the owner can strike an older line.");
            }

            expense.StruckAt = now;
            expense.StruckByUserId = user.UserId();
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(ToDto(expense));
    }

    private static ExpenseDto ToDto(Expense e) => new(e.Id, e.OccurredAt, e.Amount, e.Category, e.Note, e.Mode, null, e.StruckAt);
}

/// <summary>
/// Reads a notebook line like "chai 40, auto 120, thaili 250 upi" or "किराया 5000" into lines with an amount, a
/// heading, and cash or UPI. Rules only (fast, works with no AI key): a new line starts at a comma, "aur", or when a
/// second amount or a second thing appears. The person checks every line before it is saved.
/// </summary>
public static partial class ExpenseReader
{
    private static readonly (ExpenseCategory Category, string[] Words)[] Headings =
    [
        (ExpenseCategory.Tea, ["chai", "chay", "tea", "nashta", "naashta", "snacks", "pani", "चाय", "नाश्ता", "पानी"]),
        (ExpenseCategory.Transport, ["auto", "tempo", "bhada", "bhaada", "rickshaw", "riksha", "petrol", "diesel", "transport", "gaadi", "gadi", "ऑटो", "टेम्पो", "भाड़ा", "भाडा", "पेट्रोल", "गाड़ी"]),
        (ExpenseCategory.Rent, ["kiraya", "kiraaya", "rent", "किराया"]),
        (ExpenseCategory.Electricity, ["bijli", "light", "electricity", "बिजली"]),
        (ExpenseCategory.Salary, ["salary", "tankhwah", "tankha", "pagar", "pagaar", "majdoori", "mazdoori", "staff", "वेतन", "तनख्वाह", "पगार", "मजदूरी"]),
        (ExpenseCategory.Purchase, ["maal", "mal", "purchase", "mandi", "wholesale", "thok", "माल", "मंडी"]),
        (ExpenseCategory.Packaging, ["thaili", "thaila", "polythene", "packing", "carrybag", "थैली", "पन्नी"]),
        (ExpenseCategory.Repair, ["repair", "marammat", "mistri", "मरम्मत", "मिस्त्री"]),
        (ExpenseCategory.Home, ["ghar", "home", "personal", "घर"]),
        (ExpenseCategory.Tax, ["gst", "tax", "incometax", "टैक्स"]),
        (ExpenseCategory.Loan, ["emi", "loan", "kist", "kisht", "qist", "लोन", "किस्त", "क़िस्त"]),
        (ExpenseCategory.Equipment, ["fridge", "freezer", "machine", "equipment", "counter", "inverter", "फ्रिज", "मशीन"]),
        (ExpenseCategory.Deposit, ["deposit", "jama", "jamaa", "जमा"]),
        (ExpenseCategory.Advance, ["advance", "एडवांस"]),
    ];

    /// <summary>
    /// Words before a meter number ("bijli 3200 reading 14520"): that number is the dial, not rupees. Only "reading":
    /// "meter" and "unit" are also measures ("rassi 10 meter 200"), where the number after them is the rupees.
    /// </summary>
    private static readonly string[] ReadingWords = ["reading", "रीडिंग"];

    /// <summary>Measures that follow a quantity ("10 meter", "2 kg"): that number is how much, not rupees.</summary>
    private static readonly string[] MeasureWords =
        ["meter", "metre", "mtr", "m", "kg", "kilo", "gram", "gm", "litre", "liter", "ltr", "l", "piece", "pieces", "pc", "pcs", "dozen", "packet", "packets", "bag", "bags", "box", "feet", "ft", "मीटर", "किलो", "लीटर", "दर्जन", "पैकेट"];

    private static readonly string[] UpiWords = ["upi", "online", "gpay", "phonepe", "paytm", "bank", "यूपीआई", "ऑनलाइन"];

    private static readonly Dictionary<string, decimal> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bees"] = 20, ["बीस"] = 20, ["tees"] = 30, ["तीस"] = 30, ["chalis"] = 40, ["chaalis"] = 40, ["चालीस"] = 40,
        ["pachas"] = 50, ["pachaas"] = 50, ["पचास"] = 50, ["saath"] = 60, ["साठ"] = 60, ["sattar"] = 70, ["सत्तर"] = 70,
        ["assi"] = 80, ["अस्सी"] = 80, ["nabbe"] = 90, ["नब्बे"] = 90,
    };

    private static readonly Dictionary<string, decimal> Multipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sau"] = 100, ["सौ"] = 100, ["hazar"] = 1000, ["hazaar"] = 1000, ["हजार"] = 1000, ["हज़ार"] = 1000,
    };

    public static IReadOnlyList<ExpenseGuess> Read(string? text)
    {
        var result = new List<ExpenseGuess>();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
        {
            return result;
        }

        foreach (var part in Separator().Split(text))
        {
            decimal? amount = null;
            var words = new List<string>();
            var upi = false;
            var amountFirst = false; // "300 mandi tempo": words after a leading amount all belong to it
            var dialNext = false; // the number after "reading" stays in the note

            void Flush()
            {
                if (amount is > 0)
                {
                    var note = words.Count == 0 ? null : string.Join(' ', words);
                    var category = Guess(note ?? "").Category;
                    // "bank jama 5000": cash taken from the box to the bank, so the mode is cash even though it says bank.
                    result.Add(new ExpenseGuess(amount.Value, category, note, upi && category != ExpenseCategory.Deposit ? PaymentMode.Upi : PaymentMode.Cash));
                }

                amount = null;
                words = [];
                upi = false;
                amountFirst = false;
            }

            var tokens = part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (var at = 0; at < tokens.Length; at++)
            {
                var raw = tokens[at];
                var token = raw.Trim('₹', '.', ':', '-', '/', '=').ToLowerInvariant();
                if (token is "rs" or "rupaye" or "rupees" or "rupay" or "रुपये" or "रुपए" or "रु" or "")
                {
                    continue;
                }

                if (UpiWords.Contains(token))
                {
                    upi = true;
                    continue;
                }

                if (ReadingWords.Contains(token))
                {
                    dialNext = true;
                    words.Add(raw.Trim('₹', ',', '.'));
                    continue;
                }

                // "rassi 10 meter 200": a number before a measure is how much was bought, not rupees, so it stays in the note.
                var measured = at + 1 < tokens.Length && MeasureWords.Contains(tokens[at + 1].Trim('.', ',').ToLowerInvariant());
                if ((dialNext || measured) && ParseNumber(token) is not null)
                {
                    dialNext = false;
                    words.Add(raw.Trim('₹', ',', '.'));
                    continue;
                }

                if (ParseNumber(token) is { } number)
                {
                    if (Multipliers.TryGetValue(token, out var times) && amount is { } before)
                    {
                        amount = before * times; // "do sau" = 200
                    }
                    else if (amount is not null)
                    {
                        Flush(); // a second amount: a new line ("40 120" or "chai 40 auto 120")
                        amount = number;
                        amountFirst = true;
                    }
                    else
                    {
                        amount = number;
                        amountFirst = words.Count == 0;
                    }

                    continue;
                }

                if (amount is not null && words.Count > 0 && !amountFirst)
                {
                    Flush(); // "chai 40 auto": a new thing after the amount starts the next line
                }

                words.Add(raw.Trim('₹', ',', '.'));
            }

            Flush();
        }

        return result;
    }

    /// <summary>The heading for some words; Other when nothing matches.</summary>
    public static ExpenseGuess Guess(string note)
    {
        var lower = note.ToLowerInvariant();
        foreach (var (category, words) in Headings)
        {
            if (words.Any(w => Regex.IsMatch(lower, $@"(^|[^\p{{L}}]){Regex.Escape(w)}([^\p{{L}}]|$)")))
            {
                return new ExpenseGuess(0, category, note, PaymentMode.Cash);
            }
        }

        return new ExpenseGuess(0, ExpenseCategory.Other, note, PaymentMode.Cash);
    }

    private static decimal? ParseNumber(string token)
    {
        if (decimal.TryParse(token.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (NumberWords.TryGetValue(token, out var tens))
        {
            return tens;
        }

        if (Multipliers.TryGetValue(token, out var times))
        {
            return times; // "sau" alone = 100; after a number it multiplies (handled by the caller)
        }

        return Planner.GroceryWords.Numbers.TryGetValue(token, out var small) ? small : null;
    }

    [GeneratedRegex(@"\s*(?:,|;|\n|\+|\baur\b|और)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex Separator();
}
