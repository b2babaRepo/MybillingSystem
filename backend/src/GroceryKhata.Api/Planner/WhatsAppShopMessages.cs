using System.Globalization;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Planner;

public enum WaDirection
{
    In,
    Out,
}

/// <summary>
/// One WhatsApp message between the shop's own number and a customer: sent bills, reminders and statements, replies,
/// and what customers wrote in. <see cref="Status"/> follows Meta: sent, delivered, read, failed; "received" for incoming.
/// </summary>
public sealed class ShopWhatsAppMessage : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The other side, as WhatsApp gives it: country code and number, digits only ("919876543210").</summary>
    public required string Phone { get; set; }

    public Guid? CustomerId { get; set; }

    public WaDirection Direction { get; set; }

    /// <summary>bill, reminder, statement, reply, test, or text (incoming).</summary>
    public required string Kind { get; set; }

    /// <summary>What it said, as the shop would read it.</summary>
    public required string Body { get; set; }

    public string? MetaMessageId { get; set; }

    public required string Status { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>The bill a "bill" message was about.</summary>
    public Guid? BillId { get; set; }
}

/// <summary>One of the shop's message templates, as Meta knows it.</summary>
public sealed record WaTemplateStatus(string Key, string Name, string Status, string? Reason);

public sealed record WaTemplatesDto(bool CanCreate, IReadOnlyList<WaTemplateStatus> Templates);

/// <summary>Send a template: a bill (BillId), a payment reminder or a khata statement (CustomerId).</summary>
public sealed record WaSendRequest(string? Kind, Guid? BillId, Guid? CustomerId);

public sealed record WaMessageDto(Guid Id, WaDirection Direction, string Kind, string Body, string Status, string? Error, DateTimeOffset At);

/// <summary>One conversation in the inbox: the customer (when the number is on the khata), the last word, and whether a reply is free now.</summary>
public sealed record WaConversationDto(string Phone, Guid? CustomerId, string? Name, string LastBody, WaDirection LastDirection, DateTimeOffset LastAt, int Waiting, bool CanReplyFreely);

public sealed record WaThreadDto(string Phone, Guid? CustomerId, string? Name, bool CanReplyFreely, DateTimeOffset? WindowEndsAt, IReadOnlyList<WaMessageDto> Messages);

public sealed record WaReplyRequest(string? Text);

/// <summary>A template Apna Kaarobar asks Meta to approve for each shop.</summary>
public sealed record WaTemplateDef(string Key, string Name, string Body, string[] Examples, string? ButtonText);

/// <summary>
/// Bills, payment reminders and khata statements sent from the shop's own WhatsApp number through Meta's Cloud API,
/// with the shop's approved templates, and an inbox for what customers write back. A free reply is allowed for 24 hours
/// after a customer's last message (Meta's rule); outside that only a template can start the talk again.
/// </summary>
public static class WhatsAppShopMessages
{
    public const string Protection = "WhatsApp.ShopToken";

    public static readonly TimeSpan ReplyWindow = TimeSpan.FromHours(24);

    /// <summary>The three templates: utility messages, English, each with a button to the bill or khata link.</summary>
    public static readonly WaTemplateDef[] Templates =
    [
        new("bill", "gk_bill_v1",
            "Namaste {{1}}, thank you for shopping at {{2}}. Your bill {{3}} is ₹{{4}}. Amount due on it: ₹{{5}}.",
            ["Ramesh", "Sharma Kirana", "2026-27/0042", "1,250.00", "0.00"], "View bill"),
        new("reminder", "gk_payment_reminder_v1",
            "Namaste {{1}}, ₹{{2}} is due on your khata at {{3}}. Please pay when convenient. UPI: {{4}}. Thank you!",
            ["Ramesh", "2,400.00", "Sharma Kirana", "sharmakirana@okaxis"], "View khata"),
        new("statement", "gk_khata_statement_v1",
            "Namaste {{1}}, here is your khata with {{2}}. Balance today: ₹{{3}}. Tap below to see every entry.",
            ["Ramesh", "Sharma Kirana", "2,400.00"], "View khata"),
        new("order", "gk_order_update_v1",
            "Namaste {{1}}, your order {{2}} at {{3}} is now {{4}}.",
            ["Ramesh", "#4F2A", "Sharma Kirana", "out for delivery"], "View order"),
    ];

    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    private static string Rupees(decimal amount) => amount.ToString("N2", India);

    public static IEndpointRouteBuilder MapWhatsAppShopMessages(this IEndpointRouteBuilder app)
    {
        var biz = app.MapGroup("/api/v1/whatsapp/business").WithTags("WhatsApp").RequireAuthorization(AppPolicies.ShopOwner);
        biz.MapGet("/templates", TemplatesAsync);
        biz.MapPost("/templates", CreateTemplatesAsync);
        app.MapPost("/api/v1/whatsapp/business/send", SendAsync).WithTags("WhatsApp").RequireAuthorization(AppPolicies.ShopMember);
        // Whether to show "Send from shop WhatsApp" to anyone at the counter, helpers included.
        app.MapGet("/api/v1/whatsapp/business/on", async (AppDbContext db, CancellationToken ct) => TypedResults.Ok(new { connected = await db.ShopWhatsApps.AnyAsync(ct) }))
            .WithTags("WhatsApp").RequireAuthorization(AppPolicies.ShopMember);
        var inbox = app.MapGroup("/api/v1/whatsapp/inbox").WithTags("WhatsApp").RequireAuthorization(AppPolicies.ShopMember);
        inbox.MapGet("", InboxAsync);
        inbox.MapGet("/{phone}", ThreadAsync);
        inbox.MapPost("/{phone}/reply", ReplyAsync);
        inbox.MapPost("/{phone}/last-bill", LastBillAsync);
        return app;
    }

    private sealed record Connection(ShopWhatsApp Row, string Token, IMetaSignup Meta);

    private static async Task<Connection?> ConnectionAsync(AppDbContext db, IServiceProvider services, IDataProtectionProvider protection, CancellationToken ct)
    {
        if (services.GetService<IMetaSignup>() is not { } meta || await db.ShopWhatsApps.AsNoTracking().SingleOrDefaultAsync(ct) is not { } row)
        {
            return null;
        }

        return new Connection(row, protection.CreateProtector(Protection).Unprotect(row.AccessToken), meta);
    }

    private static IResult NotConnected() =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Connect the shop's WhatsApp Business number first.", detail: "Settings → Your WhatsApp Business number.");

    private static async Task<IResult> TemplatesAsync(AppDbContext db, IServiceProvider services, IDataProtectionProvider protection, IConfiguration configuration, CancellationToken ct)
    {
        if (await ConnectionAsync(db, services, protection, ct) is not { } c)
        {
            return NotConnected();
        }

        var known = await c.Meta.TemplatesAsync(c.Row.WabaId, c.Token, ct);
        var list = Templates.Select(d => known.FirstOrDefault(k => k.Name == d.Name) is { } k ? k with { Key = d.Key } : new WaTemplateStatus(d.Key, d.Name, "MISSING", null)).ToList();
        return TypedResults.Ok(new WaTemplatesDto(!string.IsNullOrWhiteSpace(configuration["Whatsapp:PublicAppUrl"]), list));
    }

    /// <summary>Asks Meta to approve the templates this shop hasn't got yet. Approval usually takes minutes, sometimes a day.</summary>
    private static async Task<IResult> CreateTemplatesAsync(AppDbContext db, IServiceProvider services, IDataProtectionProvider protection, IConfiguration configuration, CancellationToken ct)
    {
        if (await ConnectionAsync(db, services, protection, ct) is not { } c)
        {
            return NotConnected();
        }

        if (configuration["Whatsapp:PublicAppUrl"] is not { Length: > 0 } baseUrl || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var site) || site.Scheme != "https")
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The app's public https address isn't set on this server (Whatsapp:PublicAppUrl).");
        }

        var known = await c.Meta.TemplatesAsync(c.Row.WabaId, c.Token, ct);
        var errors = new List<string>();
        foreach (var d in Templates.Where(d => known.All(k => k.Name != d.Name)))
        {
            try
            {
                await c.Meta.CreateTemplateAsync(c.Row.WabaId, c.Token, d, $"{site.GetLeftPart(UriPartial.Path).TrimEnd('/')}/#/", ct);
            }
            catch (HttpRequestException e)
            {
                errors.Add($"{d.Name}: {e.Message}");
            }
        }

        return errors.Count == 0 ? await TemplatesAsync(db, services, protection, configuration, ct)
            : TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Meta did not accept every template.", detail: string.Join(" · ", errors));
    }

    /// <summary>The customer's WhatsApp number from the khata phone ("+919876543210" → "919876543210").</summary>
    private static string? WaPhone(string? phone) => PhoneNumber.Normalize(phone)?.TrimStart('+');

    private static async Task<IResult> SendAsync(
        WaSendRequest request, AppDbContext db, ITenantContext tenant, IServiceProvider services, IDataProtectionProvider protection,
        IOptions<Billing.BillingOptions> billing, TimeProvider clock, CancellationToken ct)
    {
        if (await ConnectionAsync(db, services, protection, ct) is not { } c)
        {
            return NotConnected();
        }

        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.Name, s.Slug, s.UpiId }).SingleAsync(ct);
        Customer? customer;
        string[] parameters;
        string link;
        Guid? billId = null;
        var def = Templates.FirstOrDefault(t => t.Key == request.Kind);
        switch (def?.Key)
        {
            case "bill":
                var bill = await db.Bills.AsNoTracking().SingleOrDefaultAsync(b => b.Id == request.BillId, ct);
                if (bill?.CustomerId is null)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["BillId"] = ["This bill has no customer to send it to."] });
                }

                customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bill.CustomerId, ct);
                parameters = [customer?.Name ?? "", shop.Name, bill.BillNo, Rupees(bill.Total), Rupees(Math.Max(0, bill.Total - bill.PaidAmount))];
                link = $"b/{shop.Slug}/{bill.Id}?t={BillLinks.Token(billing.Value.BillLinkKey, bill.Id)}";
                billId = bill.Id;
                break;
            case "reminder" or "statement":
                customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == request.CustomerId, ct);
                if (customer is null)
                {
                    return TypedResults.NotFound();
                }

                // The khata link: the same one "Send khata link" gives, made now if the customer has none.
                if (customer.KhataToken is null)
                {
                    (customer.KhataToken, customer.KhataTokenAt) = (Secrets.NewRefreshToken(), clock.GetUtcNow());
                    await db.SaveChangesAsync(ct);
                }

                parameters = def.Key == "reminder"
                    ? [customer.Name, Rupees(Math.Max(0, customer.Balance)), shop.Name, shop.UpiId ?? "ask at the shop"]
                    : [customer.Name, shop.Name, Rupees(customer.Balance)];
                link = $"k/{shop.Slug}/{customer.Id}?t={customer.KhataToken}";
                break;
            default:
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Kind"] = ["Send a bill, reminder or statement."] });
        }

        if (WaPhone(customer?.Phone) is not { } to)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["Add this customer's mobile number first."] });
        }

        var body = parameters.Select((p, i) => (p, i)).Aggregate(def.Body, (text, x) => text.Replace($"{{{{{x.i + 1}}}}}", x.p));
        return await DeliverAsync(c, db, to, customer!.Id, def.Key, body, billId, clock, () => c.Meta.SendTemplateAsync(c.Row.PhoneNumberId, c.Token, to, def.Name, "en", parameters, link, ct), ct);
    }

    /// <summary>
    /// An order step to a customer who asked for WhatsApp updates, from the shop's own number. Quietly does nothing
    /// when the shop hasn't connected WhatsApp; a failure is logged with the message like any other.
    /// </summary>
    internal static async Task SendOrderUpdateAsync(AppDbContext db, IServiceProvider services, string phone, string[] parameters, string link, CancellationToken ct)
    {
        if (await ConnectionAsync(db, services, services.GetRequiredService<IDataProtectionProvider>(), ct) is not { } c || WaPhone(phone) is not { } to)
        {
            return;
        }

        var def = Templates.Single(t => t.Key == "order");
        var body = parameters.Select((p, i) => (p, i)).Aggregate(def.Body, (text, x) => text.Replace($"{{{{{x.i + 1}}}}}", x.p));
        var customerId = await db.Customers.AsNoTracking().Where(x => x.Phone == phone).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        await DeliverAsync(c, db, to, customerId, def.Key, body, null, services.GetRequiredService<TimeProvider>(),
            () => c.Meta.SendTemplateAsync(c.Row.PhoneNumberId, c.Token, to, def.Name, "en", parameters, link, ct), ct);
    }

    /// <summary>Sends, logs the message (sent or failed, with Meta's reason) and answers with the logged message.</summary>
    private static async Task<IResult> DeliverAsync(
        Connection c, AppDbContext db, string to, Guid? customerId, string kind, string body, Guid? billId, TimeProvider clock, Func<Task<string>> send, CancellationToken ct)
    {
        var row = new ShopWhatsAppMessage { Phone = to, CustomerId = customerId, Direction = WaDirection.Out, Kind = kind, Body = body, Status = "sent", At = clock.GetUtcNow(), BillId = billId };
        try
        {
            row.MetaMessageId = await send();
        }
        catch (HttpRequestException e)
        {
            (row.Status, row.Error) = ("failed", e.Message.Length > 300 ? e.Message[..300] : e.Message);
        }

        db.ShopWhatsAppMessages.Add(row);
        await db.SaveChangesAsync(ct);
        var dto = new WaMessageDto(row.Id, row.Direction, row.Kind, row.Body, row.Status, row.Error, row.At);
        return row.Status == "failed"
            ? TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "WhatsApp did not send it.", detail: row.Error)
            : TypedResults.Ok(dto);
    }

    private static async Task<Dictionary<string, (Guid Id, string Name)>> CustomersByPhoneAsync(AppDbContext db, IEnumerable<string> phones, CancellationToken ct)
    {
        var wanted = phones.Select(p => "+" + p).ToList();
        return (await db.Customers.AsNoTracking().Where(c => c.Phone != null && wanted.Contains(c.Phone)).Select(c => new { c.Id, c.Name, c.Phone }).ToListAsync(ct))
            .GroupBy(c => c.Phone!.TrimStart('+')).ToDictionary(g => g.Key, g => (g.First().Id, g.First().Name));
    }

    private static async Task<IResult> InboxAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var recent = await db.ShopWhatsAppMessages.AsNoTracking().OrderByDescending(m => m.At).Take(2000).ToListAsync(ct);
        var names = await CustomersByPhoneAsync(db, recent.Select(m => m.Phone).Distinct(), ct);
        var now = clock.GetUtcNow();
        var list = recent.GroupBy(m => m.Phone).Select(g =>
        {
            var last = g.First();
            var lastIn = g.FirstOrDefault(m => m.Direction == WaDirection.In)?.At;
            // Waiting: what came in after the shop last wrote, by when each reached the server (WhatsApp's own times are whole seconds).
            var lastOut = g.Where(m => m.Direction == WaDirection.Out).Max(m => (DateTimeOffset?)m.CreatedAt);
            var waiting = g.Count(m => m.Direction == WaDirection.In && (lastOut is null || m.CreatedAt > lastOut));
            var who = names.TryGetValue(g.Key, out var n) ? n : default;
            return new WaConversationDto(g.Key, who.Id == Guid.Empty ? null : who.Id, who.Name, last.Body, last.Direction, last.At, waiting, lastIn is { } li && now - li < ReplyWindow);
        }).OrderByDescending(x => x.Waiting > 0).ThenByDescending(x => x.LastAt).ToList();
        return TypedResults.Ok(list);
    }

    private static async Task<IResult> ThreadAsync(string phone, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var messages = await db.ShopWhatsAppMessages.AsNoTracking().Where(m => m.Phone == phone).OrderBy(m => m.At).ToListAsync(ct);
        var names = await CustomersByPhoneAsync(db, [phone], ct);
        var lastIn = messages.LastOrDefault(m => m.Direction == WaDirection.In)?.At;
        var ends = lastIn?.Add(ReplyWindow);
        var who = names.TryGetValue(phone, out var n) ? n : default;
        return TypedResults.Ok(new WaThreadDto(phone, who.Id == Guid.Empty ? null : who.Id, who.Name, ends > clock.GetUtcNow(), ends,
            messages.Select(m => new WaMessageDto(m.Id, m.Direction, m.Kind, m.Body, m.Status, m.Error, m.At)).ToList()));
    }

    /// <summary>A typed reply: only while the 24-hour window from the customer's last message is open.</summary>
    private static async Task<IResult> ReplyAsync(string phone, WaReplyRequest request, AppDbContext db, IServiceProvider services, IDataProtectionProvider protection, TimeProvider clock, CancellationToken ct)
    {
        if (await ConnectionAsync(db, services, protection, ct) is not { } c)
        {
            return NotConnected();
        }

        var text = request.Text?.Trim();
        if (text is not { Length: > 0 and <= 4000 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Text"] = ["Type a reply (up to 4,000 characters)."] });
        }

        var lastIn = await db.ShopWhatsAppMessages.Where(m => m.Phone == phone && m.Direction == WaDirection.In).MaxAsync(m => (DateTimeOffset?)m.At, ct);
        if (lastIn is null || clock.GetUtcNow() - lastIn > ReplyWindow)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "WhatsApp allows a typed reply only within 24 hours of the customer's last message.",
                detail: "Send a bill, reminder or statement instead: those use the shop's approved templates.");
        }

        var customer = (await CustomersByPhoneAsync(db, [phone], ct)).GetValueOrDefault(phone);
        return await DeliverAsync(c, db, phone, customer.Id == Guid.Empty ? null : customer.Id, "reply", text, null, clock, () => c.Meta.SendTextAsync(c.Row.PhoneNumberId, c.Token, phone, text, ct), ct);
    }

    /// <summary>"Send my last bill": the customer's latest bill, as the bill template.</summary>
    private static async Task<IResult> LastBillAsync(
        string phone, AppDbContext db, ITenantContext tenant, IServiceProvider services, IDataProtectionProvider protection, IOptions<Billing.BillingOptions> billing, TimeProvider clock, CancellationToken ct)
    {
        var customer = (await CustomersByPhoneAsync(db, [phone], ct)).GetValueOrDefault(phone);
        var bill = customer.Id == Guid.Empty ? null
            : await db.Bills.AsNoTracking().Where(b => b.CustomerId == customer.Id && b.CancelledAt == null).OrderByDescending(b => b.OccurredAt).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        return bill is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "No bill for this number on the khata.")
            : await SendAsync(new WaSendRequest("bill", bill, null), db, tenant, services, protection, billing, clock, ct);
    }

    /// <summary>
    /// The webhook's part for shops' numbers: customers' messages go into that shop's inbox, and Meta's delivery
    /// reports (sent, delivered, read, failed) update the shop's sent messages. Runs as the shop.
    /// </summary>
    public static async Task TakeAsync(JsonElement root, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        if (!root.TryGetProperty("entry", out var entries))
        {
            return;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            foreach (var change in entry.GetProperty("changes").EnumerateArray())
            {
                var value = change.GetProperty("value");
                if (!value.TryGetProperty("metadata", out var meta) || meta.GetProperty("phone_number_id").GetString() is not { } numberId)
                {
                    continue;
                }

                Guid? shopId;
                await using (var lookup = scopes.CreateAsyncScope())
                {
                    shopId = await lookup.ServiceProvider.GetRequiredService<AppDbContext>().WhatsAppNumbers.AsNoTracking()
                        .Where(n => n.PhoneNumberId == numberId).Select(n => (Guid?)n.ShopId).SingleOrDefaultAsync(ct);
                }

                if (shopId is null)
                {
                    continue;
                }

                // A fresh scope set to the shop before its database is first touched, so row-level security sees the shop.
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(shopId.Value);
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (value.TryGetProperty("messages", out var messages))
                {
                    var phones = messages.EnumerateArray().Select(m => m.GetProperty("from").GetString()!).Distinct().ToList();
                    var names = await CustomersByPhoneAsync(db, phones, ct);
                    foreach (var m in messages.EnumerateArray())
                    {
                        var id = m.TryGetProperty("id", out var i) ? i.GetString() : null;
                        if (id is not null && await db.ShopWhatsAppMessages.AnyAsync(x => x.MetaMessageId == id, ct))
                        {
                            continue; // Meta resends: taken once
                        }

                        var from = m.GetProperty("from").GetString()!;
                        var who = names.GetValueOrDefault(from);
                        db.ShopWhatsAppMessages.Add(new ShopWhatsAppMessage
                        {
                            Phone = from, CustomerId = who.Id == Guid.Empty ? null : who.Id, Direction = WaDirection.In, Kind = "text", Body = Describe(m),
                            MetaMessageId = id, Status = "received",
                            At = m.TryGetProperty("timestamp", out var ts) && long.TryParse(ts.GetString(), out var unix) ? DateTimeOffset.FromUnixTimeSeconds(unix) : clock.GetUtcNow(),
                        });
                    }
                }

                if (value.TryGetProperty("statuses", out var statuses))
                {
                    foreach (var s in statuses.EnumerateArray())
                    {
                        var id = s.GetProperty("id").GetString();
                        var status = s.GetProperty("status").GetString();
                        var row = await db.ShopWhatsAppMessages.SingleOrDefaultAsync(x => x.MetaMessageId == id, ct);
                        // Statuses can arrive out of order: never step back from read to delivered.
                        if (row is null || status is null || Rank(status) <= Rank(row.Status))
                        {
                            continue;
                        }

                        row.Status = status;
                        if (status == "failed" && s.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                        {
                            row.Error = errors[0].TryGetProperty("title", out var title) ? title.GetString() : "failed";
                        }
                    }
                }

                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static int Rank(string status) => status switch { "sent" => 1, "delivered" => 2, "read" => 3, "failed" => 4, _ => 0 };

    /// <summary>An incoming message as a line of text: its words, or what it is ("[photo]", "[voice note]").</summary>
    private static string Describe(JsonElement m)
    {
        var type = m.TryGetProperty("type", out var t) ? t.GetString() : null;
        string? Text(string kind, string field) => m.TryGetProperty(kind, out var k) && k.TryGetProperty(field, out var f) ? f.GetString() : null;
        var text = type switch
        {
            "text" => Text("text", "body"),
            "button" => Text("button", "text"),
            "image" => "[photo] " + (Text("image", "caption") ?? ""),
            "document" => "[document] " + (Text("document", "filename") ?? ""),
            "audio" => "[voice note]",
            "location" => "[location]",
            _ => $"[{type}]",
        };
        text = (text ?? "").Trim();
        return text.Length > 2000 ? text[..2000] : text.Length == 0 ? "[message]" : text;
    }
}
