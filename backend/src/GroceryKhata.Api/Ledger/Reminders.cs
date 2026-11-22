using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// One change to a customer's agreement to get reminders: who recorded it, when, for which number and in what words.
/// Rows are only added, so the shop can show what it did. The current state is <see cref="Customer.ReminderConsentAt"/>.
/// </summary>
public sealed class CustomerConsent : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public bool Given { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(16)]
    public string? Phone { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public required string Wording { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>One reminder sent. <see cref="Customer.LastRemindedAt"/> is only the latest; this is the history.</summary>
public sealed class ReminderLog : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>What they owed when reminded.</summary>
    public decimal Amount { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset SentAt { get; set; }
}

public sealed record ConsentRequest(bool Given);

/// <summary><see cref="Hold"/>: why not today (noPhone, noConsent, moneyWaiting, recent, tooNew); null means ready to send.</summary>
public sealed record ReminderItem(Guid CustomerId, string Name, string? Phone, decimal Balance, int OldestDays, DateTimeOffset? LastRemindedAt, string? Hold);

public sealed record ReminderPlan(IReadOnlyList<ReminderItem> Ready, IReadOnlyList<ReminderItem> Held);

/// <summary>
/// Who to remind today. The shop presses send for each one until WhatsApp's own approval is in; the rules that keep
/// reminders polite and lawful live here so automatic sending can use the same ones later.
/// </summary>
public static class Reminders
{
    /// <summary>The words the shop agrees it put to the customer. Kept with each consent, so a later change doesn't rewrite old ones.</summary>
    public const string Wording = "Customer agreed to get payment reminders from this shop on WhatsApp.";

    /// <summary>At most one reminder a week: more than that loses the customer. A starting point, to be set from what pilot shops find.</summary>
    public const int GapDays = 7;

    /// <summary>A fresh bill isn't chased: dues start counting after this many days.</summary>
    public const int MinAgeDays = 3;

    public static IEndpointRouteBuilder MapReminders(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("/reminders/today", TodayAsync);
        group.MapPut("/customers/{id:guid}/reminder-consent", SetConsentAsync);
        return app;
    }

    /// <summary>Why this customer shouldn't be reminded today, or null. Consent and money waiting come first: no schedule overrides them.</summary>
    public static string? Hold(string? phone, bool consent, bool moneyWaiting, int oldestDays, DateTimeOffset? lastReminded, DateTimeOffset now) =>
        phone is null ? "noPhone"
        : !consent ? "noConsent"
        : moneyWaiting ? "moneyWaiting"
        : lastReminded is { } last && now - last < TimeSpan.FromDays(GapDays) ? "recent"
        : oldestDays < MinAgeDays ? "tooNew"
        : null;

    private static async Task<ReminderPlan> TodayAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var dues = await ReportEndpoints.DuesAsync(db, clock, ct);
        var consented = (await db.Customers.AsNoTracking().Where(c => c.ReminderConsentAt != null).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var now = clock.GetUtcNow();
        var items = dues.Customers.Select(d => new ReminderItem(d.CustomerId, d.Name, d.Phone, d.Balance, d.OldestDays, d.LastRemindedAt,
            Hold(d.Phone, consented.Contains(d.CustomerId), d.MoneyWaiting, d.OldestDays, d.LastRemindedAt, now))).ToList();
        return new ReminderPlan(items.Where(i => i.Hold is null).ToList(), items.Where(i => i.Hold is not null).ToList());
    }

    /// <summary>The shop records what the customer told it: yes, or stop. Only a customer with a number can agree.</summary>
    private static async Task<IResult> SetConsentAsync(Guid id, ConsentRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Given && customer.Phone is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["Add the customer's number first."] });
        }

        var now = clock.GetUtcNow();
        customer.ReminderConsentAt = request.Given ? now : null;
        db.CustomerConsents.Add(new CustomerConsent { CustomerId = id, Given = request.Given, Phone = customer.Phone, Wording = Wording, UserId = user.UserId()!.Value, RecordedAt = now });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
