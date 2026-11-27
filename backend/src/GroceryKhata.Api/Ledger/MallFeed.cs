using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A mall's read-only access to one tenant shop's sales (for rent on a share of sales, "ADSR"): an access key that
/// sees sales and nothing else (no costs, no customers), and optionally a nightly push of the day's figures to an
/// address the mall gives. Kept outside row-level security on purpose: the key itself says which shop it reads, so it
/// is looked up before any shop is chosen; every other read then runs as that shop.
/// </summary>
public sealed class MallFeed : Entity
{
    public Guid ShopId { get; set; }

    /// <summary>The mall or ADSR company it is for ("Phoenix Marketcity").</summary>
    public required string Name { get; set; }

    /// <summary>The key's first characters, to tell keys apart in the list; the key itself is kept only as a hash.</summary>
    public required string KeyPrefix { get; set; }

    public required string KeyHash { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Where to send each day's figures after 2 am (HTTPS); null: the mall fetches them itself.</summary>
    public string? PushUrl { get; set; }

    public DateOnly? LastPushedDay { get; set; }

    public string? LastPushError { get; set; }
}

public sealed record MallFeedRequest(string? Name, string? PushUrl);

public sealed record MallFeedDto(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt, DateTimeOffset? LastUsedAt, string? PushUrl, DateOnly? LastPushedDay, string? LastPushError);

/// <summary>A new feed with its key, shown this once.</summary>
public sealed record MallFeedCreated(MallFeedDto Feed, string Key);

/// <summary>
/// One day's sales as a mall reads them. Gross is at regular prices; net sales are after discounts; "ex GST" takes the
/// tax out; returns are credit notes made that day; cancelled bills are counted apart and left out of sales.
/// </summary>
public sealed record MallDay(
    DateOnly Date, int Bills, decimal Gross, decimal Discount, decimal NetSales, decimal Gst, decimal NetSalesExGst,
    int Returns, decimal ReturnsValue, decimal ReturnsExGst, decimal NetAfterReturnsExGst, int Cancelled, decimal CancelledValue,
    decimal Cash, decimal Upi, decimal Card, decimal Bank, decimal OnKhata, decimal Other);

public sealed record MallBill(
    string BillNo, DateTimeOffset At, string? Till, string? MallMachineId, decimal Gross, decimal Discount, decimal Total, decimal Gst, decimal ExGst,
    IReadOnlyList<BillPaymentPart> Payments, decimal OnKhata, DateTimeOffset? CancelledAt, string? CancelReason);

public sealed record MallReturn(string NoteNo, string BillNo, DateTimeOffset At, decimal Total, decimal Gst, string Reason);

public sealed record MallDayDetail(MallDay Day, IReadOnlyList<MallBill> Bills, IReadOnlyList<MallReturn> Returns);

public static class MallFeeds
{
    public const string KeyHeader = "X-Mall-Key";
    public const int MaxDays = 92;

    public static IServiceCollection AddMallFeeds(this IServiceCollection services)
    {
        services.AddHostedService<NightlyMallPush>();
        return services;
    }

    public static IEndpointRouteBuilder MapMallFeeds(this IEndpointRouteBuilder app)
    {
        // The owner's list of feeds.
        var owner = app.MapGroup("/api/v1/mall-feeds").WithTags("Mall").RequireAuthorization(AppPolicies.ShopOwner);
        owner.MapGet("", async (AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
            (await db.MallFeeds.AsNoTracking().Where(f => f.ShopId == tenant.ShopId).OrderBy(f => f.CreatedAt).ToListAsync(ct)).Select(Dto).ToList());
        owner.MapPost("", CreateAsync);
        owner.MapPut("/{id:guid}", UpdateAsync);
        owner.MapPost("/{id:guid}/revoke", async (Guid id, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct) =>
            await db.MallFeeds.Where(f => f.Id == id && f.ShopId == tenant.ShopId && f.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(f => f.RevokedAt, clock.GetUtcNow()), ct) > 0
                ? (IResult)TypedResults.NoContent() : TypedResults.NotFound());

        // What the mall reads with its key: sales only.
        var mall = app.MapGroup("/api/mall/v1").WithTags("Mall").AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit);
        mall.MapGet("/daily", DailyAsync);
        mall.MapGet("/bills", BillsAsync);
        return app;
    }

    private static MallFeedDto Dto(MallFeed f) => new(f.Id, f.Name, f.KeyPrefix, f.CreatedAt, f.RevokedAt, f.LastUsedAt, f.PushUrl, f.LastPushedDay, f.LastPushError);

    private static string Hash(string key) => Secrets.Sha256Hex(key);

    private static string? PushProblem(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null
        : Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && url.Length <= 300 ? null
        : "The address to send to must start with https:// (or leave it empty).";

    private static async Task<IResult> CreateAsync(MallFeedRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (name is not { Length: >= 2 and <= 80 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Name the mall or the company that collects the sales."] });
        }

        if (PushProblem(request.PushUrl) is { } problem)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["PushUrl"] = [problem] });
        }

        if (await db.MallFeeds.CountAsync(f => f.ShopId == tenant.ShopId && f.RevokedAt == null, ct) >= 5)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Up to 5 mall feeds at a time: stop one first."] });
        }

        var key = "gkm_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var feed = new MallFeed { ShopId = tenant.ShopId, Name = name, KeyPrefix = key[..10], KeyHash = Hash(key), PushUrl = string.IsNullOrWhiteSpace(request.PushUrl) ? null : request.PushUrl.Trim() };
        db.MallFeeds.Add(feed);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new MallFeedCreated(Dto(feed), key));
    }

    private static async Task<IResult> UpdateAsync(Guid id, MallFeedRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var feed = await db.MallFeeds.SingleOrDefaultAsync(f => f.Id == id && f.ShopId == tenant.ShopId, ct);
        if (feed is null)
        {
            return TypedResults.NotFound();
        }

        if (PushProblem(request.PushUrl) is { } problem)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["PushUrl"] = [problem] });
        }

        feed.Name = request.Name?.Trim() is { Length: >= 2 and <= 80 } name ? name : feed.Name;
        feed.PushUrl = string.IsNullOrWhiteSpace(request.PushUrl) ? null : request.PushUrl.Trim();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Dto(feed));
    }

    /// <summary>The feed the request's key opens, with the request then working as that shop; null: no such live key.</summary>
    private static async Task<MallFeed?> OpenAsync(HttpContext http, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var key = http.Request.Headers[KeyHeader].ToString();
        if (string.IsNullOrEmpty(key) && http.Request.Headers.Authorization.ToString() is { } auth && auth.StartsWith("Bearer gkm_", StringComparison.Ordinal))
        {
            key = auth["Bearer ".Length..];
        }

        if (!key.StartsWith("gkm_", StringComparison.Ordinal) || key.Length > 100)
        {
            return null;
        }

        var hash = Hash(key);
        var feed = await db.MallFeeds.SingleOrDefaultAsync(f => f.KeyHash == hash && f.RevokedAt == null, ct);
        if (feed is null || !await db.Shops.AsNoTracking().AnyAsync(s => s.Id == feed.ShopId && s.IsActive, ct))
        {
            return null;
        }

        feed.LastUsedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        tenant.SetShop(feed.ShopId);
        return feed;
    }

    private static IResult NoKey() =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Mall key needed", detail: $"Send the shop's mall key in the {KeyHeader} header.");

    private static (DateOnly From, DateOnly To)? Range(string? from, string? to, TimeProvider clock)
    {
        var yesterday = IndiaTime.Today(clock).AddDays(-1);
        DateOnly Parse(string? text, DateOnly fallback) =>
            DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : fallback;
        var start = Parse(from, yesterday);
        var end = Parse(to, start);
        return end < start || end.DayNumber - start.DayNumber >= MaxDays ? null : (start, end);
    }

    /// <summary>Day by day (India time), from and to inclusive (yyyy-MM-dd; both default to yesterday; up to 92 days). format=csv for a file.</summary>
    private static async Task<IResult> DailyAsync(string? from, string? to, string? format, HttpContext http, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (await OpenAsync(http, db, tenant, clock, ct) is null)
        {
            return NoKey();
        }

        if (Range(from, to, clock) is not { } range)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["from"] = [$"Dates as yyyy-MM-dd, from before to, at most {MaxDays} days."] });
        }

        var days = new List<MallDay>();
        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            days.Add((await DayAsync(db, day, ct)).Day);
        }

        return format == "csv" ? TypedResults.Text(DaysCsv(days), "text/csv") : TypedResults.Ok(days);
    }

    /// <summary>One day's bills, returns and totals (date as yyyy-MM-dd, default yesterday). format=csv for the bills as a file.</summary>
    private static async Task<IResult> BillsAsync(string? date, string? format, HttpContext http, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (await OpenAsync(http, db, tenant, clock, ct) is null)
        {
            return NoKey();
        }

        if (Range(date, date, clock) is not { } range)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["date"] = ["Give the date as yyyy-MM-dd."] });
        }

        var detail = await DayAsync(db, range.From, ct);
        return format == "csv" ? TypedResults.Text(BillsCsv(detail.Bills), "text/csv") : TypedResults.Ok(detail);
    }

    /// <summary>One shop day (India time), worked out from the bills and credit notes as they stand.</summary>
    public static async Task<MallDayDetail> DayAsync(AppDbContext db, DateOnly day, CancellationToken ct)
    {
        var (start, end) = IndiaTime.Day(day);
        var made = await db.Bills.AsNoTracking().Include(b => b.Lines).Where(b => b.OccurredAt >= start && b.OccurredAt < end).OrderBy(b => b.OccurredAt).ToListAsync(ct);
        var tillIds = made.Where(b => b.TerminalId != null).Select(b => b.TerminalId!.Value).Distinct().ToList();
        var tills = await db.Terminals.AsNoTracking().Where(t => tillIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        static decimal Gst(Bill b) => b.Lines.Sum(l => (l.Cgst ?? 0) + (l.Sgst ?? 0) + (l.Igst ?? 0));
        static decimal Gross(Bill b) => b.Lines.Sum(l => Money.LineAmount(l.Quantity, l.ListPrice is { } list && list > l.UnitPrice ? list : l.UnitPrice));

        var bills = made.Select(b =>
        {
            var till = b.TerminalId is { } id ? tills.GetValueOrDefault(id) : null;
            var gross = Gross(b);
            return new MallBill(b.BillNo, b.OccurredAt, till?.Code ?? b.Counter, till?.MallMachineId, gross, gross - b.Total, b.Total, Gst(b), b.Total - Gst(b),
                b.Parts(), b.Total - b.PaidAmount - b.ExchangeCredit, b.CancelledAt, b.CancelReason);
        }).ToList();

        var notes = await (from n in db.CreditNotes.AsNoTracking().Include(n => n.Lines)
                           join b in db.Bills.AsNoTracking() on n.BillId equals b.Id
                           where n.OccurredAt >= start && n.OccurredAt < end
                           orderby n.OccurredAt
                           select new { n, b.Prefix, b.FinancialYear, b.Number }).ToListAsync(ct);
        var returns = notes.Select(x => new MallReturn(x.n.NoteNo, Bill.No(x.Prefix, x.FinancialYear, x.Number), x.n.OccurredAt, x.n.Total,
            x.n.Lines.Sum(l => l.Cgst + l.Sgst + l.Igst), x.n.Reason)).ToList();

        var live = made.Where(b => b.CancelledAt == null).ToList();
        var parts = live.SelectMany(b => b.Parts()).ToList();
        decimal Paid(PaymentMode mode) => parts.Where(p => p.Mode == mode).Sum(p => p.Amount);
        var net = live.Sum(b => b.Total);
        var gst = live.Sum(Gst);
        var returnsValue = returns.Sum(r => r.Total);
        var returnsExGst = returns.Sum(r => r.Total - r.Gst);
        var cash = Paid(PaymentMode.Cash);
        var upi = Paid(PaymentMode.Upi);
        var card = Paid(PaymentMode.Card);
        var bank = Paid(PaymentMode.Bank);
        var onKhata = live.Sum(b => b.Total - b.PaidAmount - b.ExchangeCredit);
        var cancelled = made.Where(b => b.CancelledAt != null).ToList();
        var dayTotals = new MallDay(day, live.Count, live.Sum(Gross), live.Sum(Gross) - net, net, gst, net - gst,
            returns.Count, returnsValue, returnsExGst, net - gst - returnsExGst, cancelled.Count, cancelled.Sum(b => b.Total),
            cash, upi, card, bank, onKhata, net - cash - upi - card - bank - onKhata);
        return new MallDayDetail(dayTotals, bills, returns);
    }

    private static string Csv(IEnumerable<IEnumerable<object?>> rows) =>
        string.Join("\r\n", rows.Select(r => string.Join(",", r.Select(v => v switch
        {
            null => "",
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            DateTimeOffset t => t.ToOffset(IndiaTime.Offset).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            // Quoted only when it must be (a comma, quote or line break in it).
            _ when v.ToString()!.IndexOfAny([',', '"', '\r', '\n']) >= 0 => $"\"{v.ToString()!.Replace("\"", "\"\"")}\"",
            _ => v.ToString(),
        })))) + "\r\n";

    public static string DaysCsv(IEnumerable<MallDay> days) => Csv(
        new[] { new object?[] { "date", "bills", "gross", "discount", "net_sales", "gst", "net_sales_ex_gst", "returns", "returns_value", "returns_ex_gst", "net_after_returns_ex_gst", "cancelled", "cancelled_value", "cash", "upi", "card", "bank", "on_khata", "other" } }
            .Concat(days.Select(d => new object?[] { d.Date, d.Bills, d.Gross, d.Discount, d.NetSales, d.Gst, d.NetSalesExGst, d.Returns, d.ReturnsValue, d.ReturnsExGst, d.NetAfterReturnsExGst, d.Cancelled, d.CancelledValue, d.Cash, d.Upi, d.Card, d.Bank, d.OnKhata, d.Other })));

    public static string BillsCsv(IEnumerable<MallBill> bills) => Csv(
        new[] { new object?[] { "bill_no", "time", "till", "mall_machine_id", "gross", "discount", "total", "gst", "ex_gst", "cash", "upi", "card", "bank", "on_khata", "cancelled_at", "cancel_reason" } }
            .Concat(bills.Select(b => new object?[] { b.BillNo, b.At, b.Till, b.MallMachineId, b.Gross, b.Discount, b.Total, b.Gst, b.ExGst,
                b.Payments.Where(p => p.Mode == PaymentMode.Cash).Sum(p => p.Amount), b.Payments.Where(p => p.Mode == PaymentMode.Upi).Sum(p => p.Amount),
                b.Payments.Where(p => p.Mode == PaymentMode.Card).Sum(p => p.Amount), b.Payments.Where(p => p.Mode == PaymentMode.Bank).Sum(p => p.Amount),
                b.OnKhata, b.CancelledAt, b.CancelReason })));
}

/// <summary>After 2 am India time, sends each mall that gave an address the previous day's figures (retrying hourly until it lands).</summary>
public sealed class NightlyMallPush(IServiceScopeFactory scopes, IHttpClientFactory http, TimeProvider clock, ILogger<NightlyMallPush> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Mall push round failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), clock, stoppingToken);
        }
    }

    /// <summary>Sends every feed whose yesterday hasn't gone yet. Returns how many were sent.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var india = clock.GetUtcNow().ToOffset(IndiaTime.Offset);
        if (india.Hour < 2)
        {
            return 0;
        }

        var yesterday = DateOnly.FromDateTime(india.DateTime).AddDays(-1);
        List<(Guid Id, Guid ShopId)> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            due = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().MallFeeds.AsNoTracking()
                .Where(f => f.RevokedAt == null && f.PushUrl != null && (f.LastPushedDay == null || f.LastPushedDay < yesterday))
                .Select(f => new { f.Id, f.ShopId }).ToListAsync(ct)).Select(f => (f.Id, f.ShopId)).ToList();
        }

        var sent = 0;
        foreach (var (id, shopId) in due)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(shopId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var feed = await db.MallFeeds.SingleAsync(f => f.Id == id, ct);
            try
            {
                var detail = await MallFeeds.DayAsync(db, yesterday, ct);
                var shop = await db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => new { s.Name, s.Gstin }).SingleAsync(ct);
                using var client = http.CreateClient("mall-push");
                client.Timeout = TimeSpan.FromSeconds(30);
                using var response = await client.PostAsJsonAsync(feed.PushUrl, new { shop = shop.Name, gstin = shop.Gstin, keyPrefix = feed.KeyPrefix, detail.Day, detail.Bills, detail.Returns }, ct);
                response.EnsureSuccessStatusCode();
                (feed.LastPushedDay, feed.LastPushError) = (yesterday, null);
                sent++;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                feed.LastPushError = exception.Message.Length > 300 ? exception.Message[..300] : exception.Message;
                logger.LogWarning(exception, "Mall push to feed {FeedId} failed", id);
            }

            await db.SaveChangesAsync(ct);
        }

        return sent;
    }
}
