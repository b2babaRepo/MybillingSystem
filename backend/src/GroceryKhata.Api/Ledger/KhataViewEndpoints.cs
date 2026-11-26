using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Planner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Ledger;

public sealed record KhataLinkDto(string ShopSlug, Guid CustomerId, string Token);

/// <summary>A customer's own copy of their khata at one shop: read-only, the same ledger the shop sees.</summary>
public sealed record KhataViewDto(string ShopName, string? ShopArea, string? ShopPhone, StatementDto Statement, IReadOnlyList<ClaimStatusDto>? Claims = null, string? ShopUpi = null);

/// <summary><see cref="Confirmed"/> false: a khata under this phone that isn't proved theirs yet, so its name and balance stay hidden.</summary>
public sealed record MyKhataDto(Guid ShopId, string ShopName, string? ShopArea, Guid CustomerId, string CustomerName, decimal Balance, bool Confirmed = true);

/// <summary><see cref="Code"/> is null when the customer has already confirmed.</summary>
public sealed record PhoneCodeDto(string? Code, bool Confirmed);

public sealed record ConfirmKhataRequest(string? Code);

/// <summary>
/// Transparency step 2: the customer sees their own khata, so a payment can't vanish without them knowing.
/// Two ways in: a private link the shop shares (no account needed), or the household app, which finds every
/// shop where the person's phone number is a khata customer, and opens it once they enter a code the shop gives them.
/// Both are read-only.
/// </summary>
public static class KhataViewEndpoints
{
    public static IEndpointRouteBuilder MapKhataView(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/customers/{id:guid}/khata-link", GetOrCreateLinkAsync)
            .WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/customers/{id:guid}/khata-link/reset", ResetLinkAsync)
            .WithTags("Khata").RequireAuthorization(AppPolicies.ShopOwner);

        app.MapPost("/api/v1/customers/{id:guid}/phone-code", NewPhoneCodeAsync)
            .WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);

        app.MapGet("/api/v1/khata/{slug}/{customerId:guid}", GetByLinkAsync)
            .WithTags("Khata").AllowAnonymous().RequireRateLimiting(StoreEndpoints.StoreRateLimit);

        var me = app.MapGroup("/api/v1/me/khata").WithTags("Khata").RequireAuthorization();
        me.MapGet("", GetMineAsync);
        me.MapGet("/{shopId:guid}/{customerId:guid}", GetMineAtShopAsync);
        me.MapPost("/{shopId:guid}/{customerId:guid}/confirm", ConfirmAsync).RequireRateLimiting(StoreEndpoints.StoreRateLimit);
        return app;
    }

    /// <summary>How long a code from the shop works.</summary>
    public static readonly TimeSpan CodeLife = TimeSpan.FromHours(24);

    /// <summary>Wrong tries before a code stops: with a million codes, guessing can't get anywhere.</summary>
    public const int MaxCodeTries = 5;

    private static string HashCode(Guid customerId, string code) => Secrets.Sha256Hex($"{customerId:N}:{code}");

    /// <summary>A code for the shop to tell the customer face to face or on a call. Never sent to the typed number: that number is what's being checked.</summary>
    private static async Task<IResult> NewPhoneCodeAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        if (customer.Phone is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["This customer has no phone number."] });
        }

        if (customer.PhoneConfirmedAt is not null)
        {
            return TypedResults.Ok(new PhoneCodeDto(null, true));
        }

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        (customer.ConfirmCodeHash, customer.ConfirmCodeAt, customer.ConfirmCodeTries) = (HashCode(customer.Id, code), clock.GetUtcNow(), 0);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new PhoneCodeDto(code, false));
    }

    /// <summary>The person enters the shop's code: this khata is theirs from now on. Every failure looks the same from outside.</summary>
    private static async Task<IResult> ConfirmAsync(
        Guid shopId, Guid customerId, ConfirmKhataRequest request, ClaimsPrincipal user, IServiceScopeFactory scopes, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var phone = await VerifiedPhoneAsync(user, db, ct);
        await using var scope = scopes.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.SetShop(shopId);
        tenant.SetUser(user.UserId()!.Value);
        var shopDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = await shopDb.Customers.SingleOrDefaultAsync(c => c.Id == customerId && c.Phone == phone, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        if (customer.PhoneConfirmedAt is not null)
        {
            return TypedResults.NoContent();
        }

        var live = customer.ConfirmCodeHash is not null && customer.ConfirmCodeAt is { } made && made + CodeLife > clock.GetUtcNow();
        if (live && request.Code?.Trim() is { Length: 6 } code && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(customer.ConfirmCodeHash!), System.Text.Encoding.UTF8.GetBytes(HashCode(customer.Id, code))))
        {
            (customer.PhoneConfirmedAt, customer.ConfirmCodeHash, customer.ConfirmCodeAt) = (clock.GetUtcNow(), null, null);
            await shopDb.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        }

        if (live && ++customer.ConfirmCodeTries >= MaxCodeTries)
        {
            (customer.ConfirmCodeHash, customer.ConfirmCodeAt) = (null, null);
        }

        await shopDb.SaveChangesAsync(ct);
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Code"] = ["That code didn't work. Ask the shop for a new one."] });
    }

    /// <summary>How long a khata link works. A forwarded or old link stops on its own.</summary>
    public static readonly TimeSpan LinkLife = TimeSpan.FromDays(90);

    /// <summary>Sharing again in the link's last week makes a fresh one, so the customer never gets one about to stop.</summary>
    private static readonly TimeSpan RenewWithin = TimeSpan.FromDays(7);

    /// <summary>The customer's link: the same one while it works, so links already sent keep working.</summary>
    private static async Task<IResult> GetOrCreateLinkAsync(
        Guid id, AppDbContext db, ITenantContext tenant, IOptions<Billing.BillingOptions> billing, TimeProvider clock, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        var token = CurrentLinkToken(customer, billing.Value.BillLinkKey, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var slug = await db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.Slug).SingleAsync(ct);
        return TypedResults.Ok(new KhataLinkDto(slug, customer.Id, token));
    }

    /// <summary>A new link for a lost or forwarded one: the old link stops working at once.</summary>
    private static async Task<IResult> ResetLinkAsync(
        Guid id, AppDbContext db, ITenantContext tenant, IOptions<Billing.BillingOptions> billing, TimeProvider clock, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        var token = NewLinkToken(customer, billing.Value.BillLinkKey, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        var slug = await db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.Slug).SingleAsync(ct);
        return TypedResults.Ok(new KhataLinkDto(slug, customer.Id, token));
    }

    /// <summary>
    /// What <see cref="Customer.KhataToken"/> holds for a link made before links were signed: a hash of its token, put
    /// there by the KhataLinksSigned migration. Such a link still opens until it expires, but its token can't be sent again.
    /// </summary>
    private const string LegacyPrefix = "h:";

    /// <summary>
    /// The token in a khata link: signed with Billing:BillLinkKey from the customer and the random value kept in
    /// <see cref="Customer.KhataToken"/>. The database alone can't open anyone's khata, and a new random value
    /// (a reset, or a new phone number) stops the old link.
    /// </summary>
    internal static string LinkToken(string key, Guid customerId, string nonce) =>
        System.Buffers.Text.Base64Url.EncodeToString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes($"khata:{customerId:N}:{nonce}")));

    /// <summary>The hash a pre-signing token was stored as (matches the migration's SQL).</summary>
    internal static string LegacyHash(string token) => LegacyPrefix + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>A fresh link for this customer, ending the one before. Save the customer after.</summary>
    internal static string NewLinkToken(Customer customer, string key, DateTimeOffset now)
    {
        (customer.KhataToken, customer.KhataTokenAt) = (Secrets.NewRefreshToken(), now);
        return LinkToken(key, customer.Id, customer.KhataToken);
    }

    /// <summary>
    /// The customer's link token: the same one while it works; a fresh one when there is none, it is in its last week
    /// or over, or it was made before links were signed. Save the customer after.
    /// </summary>
    internal static string CurrentLinkToken(Customer customer, string key, DateTimeOffset now) =>
        customer.KhataToken is { } nonce && !nonce.StartsWith(LegacyPrefix, StringComparison.Ordinal)
            && customer.KhataTokenAt is { } made && made + LinkLife - RenewWithin > now
            ? LinkToken(key, customer.Id, nonce)
            : NewLinkToken(customer, key, now);

    private static bool LinkMatches(string key, Guid customerId, string stored, string t) =>
        stored.StartsWith(LegacyPrefix, StringComparison.Ordinal)
            ? CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(LegacyHash(t)))
            : CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(LinkToken(key, customerId, stored)), Encoding.UTF8.GetBytes(t));

    /// <summary>The page behind the link. A wrong or missing token looks exactly like no such khata.</summary>
    private static async Task<IResult> GetByLinkAsync(
        string slug, Guid customerId, string? t, AppDbContext db, TenantContext tenant, IOptions<Billing.BillingOptions> billing, LedgerService ledger, TimeProvider clock, CancellationToken ct)
    {
        if (await OpenByLinkAsync(slug, customerId, t, db, tenant, billing, ct) is not { } shop)
        {
            return TypedResults.NotFound();
        }

        var statement = await ledger.GetStatementAsync(customerId, null, ct);
        return statement is null ? TypedResults.NotFound() : TypedResults.Ok(new KhataViewDto(shop.Name, shop.Area, shop.OwnerPhone, statement, await Receipts.ClaimsForAsync(db, customerId, clock, ct)));
    }

    /// <summary>
    /// Checks a khata link and works as its shop for the rest of the request. Null for a wrong or missing token,
    /// an unknown customer or a closed shop: all look the same from outside.
    /// </summary>
    internal static async Task<StoreEndpoints.OpenShop?> OpenByLinkAsync(
        string slug, Guid customerId, string? t, AppDbContext db, TenantContext tenant, IOptions<Billing.BillingOptions> billing, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(t) || await StoreEndpoints.OpenShopAsync(db, tenant, slug, ct) is not { } shop)
        {
            return null;
        }

        var link = await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => new { c.KhataToken, c.KhataTokenAt }).SingleOrDefaultAsync(ct);
        var stored = link is { KhataTokenAt: { } made } && made + LinkLife > DateTimeOffset.UtcNow ? link.KhataToken : null;
        return stored is not null && LinkMatches(billing.Value.BillLinkKey, customerId, stored, t) ? shop : null;
    }

    /// <summary>
    /// The signed-in person's phone, once an SMS code has proved it is theirs; otherwise a value no khata has. A password
    /// account can be opened with anyone's number, so it never sees the khatas kept against that number.
    /// </summary>
    private static async Task<string> VerifiedPhoneAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == user.UserId() && u.PhoneVerifiedAt != null).Select(u => u.Phone).SingleOrDefaultAsync(ct) ?? "unverified";

    /// <summary>
    /// A scope working as <paramref name="shopId"/>, or null unless the customer there has the signed-in person's
    /// phone and has confirmed the khata is theirs. The caller disposes it.
    /// </summary>
    internal static async Task<AsyncServiceScope?> OpenMineAsync(
        Guid shopId, Guid customerId, ClaimsPrincipal user, IServiceScopeFactory scopes, AppDbContext db, CancellationToken ct)
    {
        var phone = await VerifiedPhoneAsync(user, db, ct);
        var scope = scopes.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.SetShop(shopId);
        tenant.SetUser(user.UserId()!.Value);
        var shopDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await shopDb.Shops.AnyAsync(s => s.Id == shopId && s.IsActive, ct) && await shopDb.Customers.AnyAsync(c => c.Id == customerId && c.Phone == phone && c.PhoneConfirmedAt != null, ct))
        {
            return scope;
        }

        await scope.DisposeAsync();
        return null;
    }

    /// <summary>
    /// Every shop where this person's phone is a khata customer. Read past the shop filter on purpose, limited to
    /// the person's own phone; row-level security allows the same through its own_khata_read policy.
    /// </summary>
    private static async Task<IReadOnlyList<MyKhataDto>> GetMineAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var phone = await VerifiedPhoneAsync(user, db, ct);
        return await (from c in db.Customers.IgnoreQueryFilters().AsNoTracking()
                      join s in db.Shops.AsNoTracking() on c.ShopId equals s.Id
                      where c.Phone == phone && s.IsActive
                      orderby c.Balance descending, s.Name
                      select new MyKhataDto(s.Id, s.Name, s.Area, c.Id, c.PhoneConfirmedAt != null ? c.Name : "", c.PhoneConfirmedAt != null ? c.Balance : 0m, c.PhoneConfirmedAt != null))
            .ToListAsync(ct);
    }

    /// <summary>One shop's khata for the signed-in person: read as that shop, after checking the phone is theirs.</summary>
    private static async Task<IResult> GetMineAtShopAsync(
        Guid shopId, Guid customerId, string? month, ClaimsPrincipal user, IServiceScopeFactory scopes, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var phone = await VerifiedPhoneAsync(user, db, ct);

        // A scope of its own: this request may already be working as another shop (a shopkeeper's own khata elsewhere).
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var tenant = sp.GetRequiredService<TenantContext>();
        tenant.SetShop(shopId);
        tenant.SetUser(user.UserId()!.Value);
        var shopDb = sp.GetRequiredService<AppDbContext>();
        var shop = await shopDb.Shops.AsNoTracking().Where(s => s.Id == shopId && s.IsActive).Select(s => new { s.Name, s.Area, s.UpiId }).SingleOrDefaultAsync(ct);
        if (shop is null || !await shopDb.Customers.AnyAsync(c => c.Id == customerId && c.Phone == phone && c.PhoneConfirmedAt != null, ct))
        {
            return TypedResults.NotFound();
        }

        var statement = await sp.GetRequiredService<LedgerService>().GetStatementAsync(customerId, IndiaTime.ParseMonth(month), ct);
        return statement is null ? TypedResults.NotFound() : TypedResults.Ok(new KhataViewDto(shop.Name, shop.Area, null, statement, await Receipts.ClaimsForAsync(shopDb, customerId, clock, ct),
            string.IsNullOrWhiteSpace(shop.UpiId) ? null : shop.UpiId));
    }
}
