using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Agents;

/// <summary>
/// A shop an agent signed up on a visit: the shop, where it is (GPS), photos, the owner, the plan chosen and how it was
/// paid. The owner agrees by reading the agent a code sent to their phone. An admin then approves it (the shop opens
/// under the owner's number) or rejects it; a Billing admin confirms the money.
/// </summary>
public sealed class ShopRegistration : Entity
{
    public Guid AgentId { get; set; }

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Submitted;

    public required string ShopName { get; set; }

    public ShopType Type { get; set; }

    public required string Address { get; set; }

    public required string Market { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>The phone's GPS accuracy in metres.</summary>
    public double? AccuracyMetres { get; set; }

    public required string OwnerName { get; set; }

    /// <summary>E.164. The owner's login once approved.</summary>
    public required string OwnerPhone { get; set; }

    public string? AltPhone { get; set; }

    public string? Gstin { get; set; }

    public required string PlanCode { get; set; }

    public PlanTerm Term { get; set; }

    /// <summary>The plan's price for the term when submitted.</summary>
    public decimal Amount { get; set; }

    public RegistrationPayment PaymentMode { get; set; }

    /// <summary>The UPI reference (UTR) or cash receipt number.</summary>
    public string? PaymentRef { get; set; }

    public RegistrationPaymentStatus PaymentStatus { get; set; }

    /// <summary>When the owner's consent code was checked: they agreed, and the agent was with them.</summary>
    public DateTimeOffset ConsentAt { get; set; }

    public string? RejectReason { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>The shop opened from it.</summary>
    public Guid? ShopId { get; set; }

    public Guid? PaymentId { get; set; }

    public List<RegistrationPhoto> Photos { get; set; } = [];
}

public sealed class RegistrationPhoto : Entity
{
    public Guid RegistrationId { get; set; }

    /// <summary>"front" (shop front with signboard), "inside" or "owner".</summary>
    public required string Kind { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }
}

/// <summary>A code sent to a shop owner's phone during an agent's visit; it only confirms consent, it never signs anyone in.</summary>
public sealed class ConsentCode : Entity
{
    public required string Phone { get; set; }

    public Guid AgentId { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

public enum RegistrationStatus
{
    Submitted,
    Approved,
    Rejected,
}

public enum RegistrationPayment
{
    /// <summary>Paid by UPI to the company's account, with the UTR.</summary>
    Upi,

    /// <summary>Cash handed to the agent, who owes it to the company.</summary>
    Cash,

    /// <summary>Pays later from the app; the shop starts on the free trial.</summary>
    Later,
}

public enum RegistrationPaymentStatus
{
    None,

    /// <summary>The agent says it was paid; not yet checked.</summary>
    Received,

    /// <summary>A billing admin found it in the bank or got the cash; the plan is on.</summary>
    Confirmed,
}

public sealed record ConsentCodeRequest(string? OwnerPhone);

public sealed record RegistrationPhotoRequest(string? Kind, string? DataUrl);

public sealed record ShopRegistrationRequest(
    Guid Id, string? ShopName, ShopType? Type, string? Address, string? Market, double? Latitude, double? Longitude, double? AccuracyMetres,
    string? OwnerName, string? OwnerPhone, string? AltPhone, string? Gstin, string? PlanCode, PlanTerm? Term, RegistrationPayment? PaymentMode,
    string? PaymentRef, string? ConsentCode, IReadOnlyList<RegistrationPhotoRequest>? Photos);

public sealed record RegistrationReasonRequest(string? Reason);

public sealed record RegistrationDto(
    Guid Id, RegistrationStatus Status, string ShopName, ShopType Type, string Address, string Market, double Latitude, double Longitude,
    double? AccuracyMetres, string OwnerName, string OwnerPhone, string? AltPhone, string? Gstin, string PlanCode, PlanTerm Term, decimal Amount,
    RegistrationPayment PaymentMode, string? PaymentRef, RegistrationPaymentStatus PaymentStatus, DateTimeOffset SubmittedAt, DateTimeOffset ConsentAt,
    string? RejectReason, Guid? ShopId, string AgentCode, string AgentName, IReadOnlyList<RegistrationPhotoView> Photos, IReadOnlyList<string> Warnings);

public sealed record RegistrationPhotoView(Guid Id, string Kind);

public static class ShopRegistrationEndpoints
{
    private const int MaxPhotoBytes = 150_000;
    private static readonly string[] PhotoKinds = ["front", "inside", "owner"];
    private static readonly string[] ImageTypes = ["image/webp", "image/jpeg", "image/png"];

    public static IEndpointRouteBuilder MapShopRegistrations(this IEndpointRouteBuilder app)
    {
        var agent = app.MapGroup("/api/v1/agent").WithTags("Agent shop sign-ups").RequireAuthorization();
        agent.MapPost("/consent-code", SendConsentAsync);
        agent.MapPost("/shops", SubmitAsync);
        agent.MapGet("/shops", MineAsync);
        // The plans on sale, with prices, for the agent to offer.
        agent.MapGet("/plans", async (AppDbContext db, CancellationToken ct) =>
            (await db.Plans.AsNoTracking().Where(p => p.IsActive && p.Code != "free").OrderBy(p => p.SortOrder).ToListAsync(ct))
                .Select(p => new PlanDto(p.Code, p.Name, p.PriceMonthly, p.PriceYear1, p.PriceYear3, p.PriceYear5, p.FeatureKeys, p.MaxCustomers, p.MaxStaff, p.WhatsAppPerMonth, true, 0, p.MaxOwners)));

        var admin = app.MapGroup("/api/v1/platform/registrations").WithTags("Agent shop sign-ups").RecordsActivity();
        admin.MapGet("", ListAsync).RequireAuthorization(AppPolicies.PlatformShopReview);
        admin.MapGet("/{id:guid}/photos/{photoId:guid}", PhotoAsync).RequireAuthorization(AppPolicies.PlatformShopReview);
        admin.MapPost("/{id:guid}/approve", ApproveAsync).RequireAuthorization(AppPolicies.PlatformShopReview);
        admin.MapPost("/{id:guid}/reject", RejectAsync).RequireAuthorization(AppPolicies.PlatformShopReview);
        admin.MapPost("/{id:guid}/confirm-payment", ConfirmPaymentAsync).RequireAuthorization(AppPolicies.PlatformBilling);
        return app;
    }

    /// <summary>The signed-in person's agent row, only when approved: blocked or new agents can't sign shops up.</summary>
    private static Task<FieldAgent?> ActiveAgentAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        return db.FieldAgents.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId && a.Status == FieldAgentStatus.Approved, ct);
    }

    private static IResult NotAnAgent() =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only an approved field agent can sign shops up.");

    /// <summary>Sends the owner a 6-digit code; they read it to the agent to agree to the sign-up.</summary>
    private static async Task<IResult> SendConsentAsync(
        ConsentCodeRequest request, ClaimsPrincipal user, AppDbContext db, IServiceProvider services, TimeProvider clock, ILogger<ConsentCode> logger, CancellationToken ct)
    {
        if (await ActiveAgentAsync(user, db, ct) is not { } agent)
        {
            return NotAnAgent();
        }

        if (PhoneNumber.Normalize(request.OwnerPhone) is not { } phone)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["OwnerPhone"] = ["Enter the owner's mobile number."] });
        }

        if (services.GetService<ISmsSender>() is not { } sender)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "SMS isn't available right now.");
        }

        var now = clock.GetUtcNow();
        var recent = await db.ConsentCodes.Where(c => c.Phone == phone && c.CreatedAt > now.AddMinutes(-15)).Select(c => c.CreatedAt).ToListAsync(ct);
        if (recent.Count >= 3 || recent.Any(t => t > now.AddSeconds(-30)))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Please wait before sending another code.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await db.ConsentCodes.Where(c => c.Phone == phone && c.UsedAt == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), ct);
        db.ConsentCodes.Add(new ConsentCode { Phone = phone, AgentId = agent.Id, CodeHash = Hash(phone, code), ExpiresAt = now.AddMinutes(10) });
        await db.SaveChangesAsync(ct);
        try
        {
            await sender.SendOtpAsync(phone, code, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "SMS provider refused a consent code");
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "The SMS couldn't be sent. Try again in a minute.");
        }

        return TypedResults.Ok(new { validMinutes = 10 });
    }

    private static async Task<IResult> SubmitAsync(
        ShopRegistrationRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await ActiveAgentAsync(user, db, ct) is not { } agent)
        {
            return NotAnAgent();
        }

        // Sent again (offline retry): the first one stands.
        if (await db.ShopRegistrations.AsNoTracking().AnyAsync(r => r.Id == request.Id && r.AgentId == agent.Id, ct))
        {
            return TypedResults.Ok((await ViewsAsync(db, db.ShopRegistrations.Where(r => r.Id == request.Id), ct)).Single());
        }

        var errors = new Dictionary<string, string[]>();
        var phone = PhoneNumber.Normalize(request.OwnerPhone);
        var alt = string.IsNullOrWhiteSpace(request.AltPhone) ? null : PhoneNumber.Normalize(request.AltPhone);
        if (request.Id == Guid.Empty) errors["Id"] = ["The app must send an ID."];
        if (request.ShopName?.Trim() is not { Length: >= 2 and <= 120 }) errors["ShopName"] = ["Enter the shop's name, as on the board."];
        if (request.Type is null) errors["Type"] = ["Choose the type of shop."];
        if (request.Address?.Trim() is not { Length: >= 5 and <= 300 }) errors["Address"] = ["Enter the shop's address."];
        if (request.Market?.Trim() is not { Length: >= 2 and <= 120 }) errors["Market"] = ["Enter the market."];
        if (request.Latitude is not (>= 6 and <= 38) || request.Longitude is not (>= 68 and <= 98)) errors["Latitude"] = ["Take the shop's location (GPS) at the shop."];
        if (request.OwnerName?.Trim() is not { Length: >= 2 and <= 120 }) errors["OwnerName"] = ["Enter the owner's name."];
        if (phone is null) errors["OwnerPhone"] = ["Enter the owner's mobile number."];
        if (!string.IsNullOrWhiteSpace(request.AltPhone) && alt is null) errors["AltPhone"] = ["Check the other number."];
        var gstin = string.IsNullOrWhiteSpace(request.Gstin) ? null : request.Gstin.Trim().ToUpperInvariant();
        if (gstin is not null && (gstin.Length != 15 || !gstin.All(char.IsAsciiLetterOrDigit))) errors["Gstin"] = ["A GSTIN has 15 letters and digits."];
        if (request.Term is null || request.PaymentMode is null) errors["PlanCode"] = ["Choose the plan, period and how it's paid."];
        var paymentRef = string.IsNullOrWhiteSpace(request.PaymentRef) ? null : request.PaymentRef.Trim().ToUpperInvariant();
        if (request.PaymentMode == RegistrationPayment.Upi && paymentRef is not { Length: >= 6 and <= 30 }) errors["PaymentRef"] = ["Enter the UPI reference (UTR) from the payment."];

        var photos = new List<RegistrationPhoto>();
        foreach (var p in request.Photos ?? [])
        {
            var parts = p.DataUrl?.Split(',', 2);
            var type = parts is [var head, _] && head.StartsWith("data:", StringComparison.Ordinal) && head.EndsWith(";base64", StringComparison.Ordinal) ? head[5..^7] : null;
            byte[]? data = null;
            try
            {
                data = type is not null && ImageTypes.Contains(type) ? Convert.FromBase64String(parts![1]) : null;
            }
            catch (FormatException)
            {
            }

            if (!PhotoKinds.Contains(p.Kind) || data is not { Length: > 0 and <= MaxPhotoBytes } || photos.Any(x => x.Kind == p.Kind))
            {
                errors["Photos"] = ["Send up to three photos (front, inside, owner), each under 150 KB."];
                break;
            }

            photos.Add(new RegistrationPhoto { Kind = p.Kind!, ContentType = type!, Data = data });
        }

        if (!photos.Any(p => p.Kind == "front"))
        {
            errors["Photos"] = ["Take a photo of the shop front with its board."];
        }

        var plan = request.PlanCode is null ? null : await db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.Code == request.PlanCode && p.IsActive && p.Code != "free", ct);
        var amount = request.Term is { } term ? plan?.PriceFor(term) : null;
        if (amount is null) errors["PlanCode"] = ["Choose a plan and a period it is sold for."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // The owner's code: proves they agreed and the agent was with them.
        var now = clock.GetUtcNow();
        var consent = await db.ConsentCodes.Where(c => c.Phone == phone && c.UsedAt == null && c.ExpiresAt > now).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        var code = request.ConsentCode?.Trim();
        if (consent is null || consent.Attempts >= 5 || code is not { Length: 6 }
            || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(consent.CodeHash), Convert.FromHexString(Hash(phone!, code))))
        {
            if (consent is not null)
            {
                consent.Attempts++;
                await db.SaveChangesAsync(ct);
            }

            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ConsentCode"] = ["The owner's code is wrong or expired. Send a new one."] });
        }

        consent.UsedAt = now;
        var registration = new ShopRegistration
        {
            Id = request.Id, AgentId = agent.Id, ShopName = request.ShopName!.Trim(), Type = request.Type!.Value, Address = request.Address!.Trim(),
            Market = request.Market!.Trim(), Latitude = request.Latitude!.Value, Longitude = request.Longitude!.Value, AccuracyMetres = request.AccuracyMetres,
            OwnerName = request.OwnerName!.Trim(), OwnerPhone = phone!, AltPhone = alt, Gstin = gstin, PlanCode = plan!.Code, Term = request.Term!.Value,
            Amount = amount!.Value, PaymentMode = request.PaymentMode!.Value, PaymentRef = request.PaymentMode == RegistrationPayment.Later ? null : paymentRef,
            PaymentStatus = request.PaymentMode == RegistrationPayment.Later ? RegistrationPaymentStatus.None : RegistrationPaymentStatus.Received,
            ConsentAt = now, Photos = photos,
        };
        db.ShopRegistrations.Add(registration);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ViewsAsync(db, db.ShopRegistrations.Where(r => r.Id == registration.Id), ct)).Single());
    }

    private static async Task<IResult> MineAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var agent = await db.FieldAgents.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId, ct);
        return agent is null
            ? NotAnAgent()
            : TypedResults.Ok(await ViewsAsync(db, db.ShopRegistrations.Where(r => r.AgentId == agent.Id).OrderByDescending(r => r.CreatedAt).Take(300), ct, warnings: false));
    }

    private static async Task<IReadOnlyList<RegistrationDto>> ListAsync(string? status, AppDbContext db, CancellationToken ct)
    {
        var query = db.ShopRegistrations.AsQueryable();
        if (Enum.TryParse<RegistrationStatus>(status, ignoreCase: true, out var s))
        {
            query = query.Where(r => r.Status == s);
        }
        else if (status == "payment")
        {
            query = query.Where(r => r.Status == RegistrationStatus.Approved && r.PaymentStatus == RegistrationPaymentStatus.Received);
        }

        return await ViewsAsync(db, query.OrderByDescending(r => r.CreatedAt).Take(200), ct);
    }

    private static async Task<IResult> PhotoAsync(Guid id, Guid photoId, AppDbContext db, CancellationToken ct) =>
        await db.RegistrationPhotos.AsNoTracking().Where(p => p.Id == photoId && p.RegistrationId == id).Select(p => new { p.Data, p.ContentType }).SingleOrDefaultAsync(ct) is { } photo
            ? TypedResults.File(photo.Data, photo.ContentType)
            : TypedResults.NotFound();

    /// <summary>
    /// Opens the shop under the owner's number (a new account if the number has none: they sign in with an SMS code,
    /// and their number is already proved by the consent code). The shop starts approved, on the usual trial until the
    /// payment is confirmed.
    /// </summary>
    private static async Task<IResult> ApproveAsync(
        Guid id, ClaimsPrincipal admin, AppDbContext db, IPasswordHasher<User> hasher, TimeProvider clock, CancellationToken ct)
    {
        var r = await db.ShopRegistrations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
        {
            return TypedResults.NotFound();
        }

        if (r.Status != RegistrationStatus.Submitted)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Already reviewed.");
        }

        var now = clock.GetUtcNow();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Phone == r.OwnerPhone, ct);
        if (user is null)
        {
            user = new User { Phone = r.OwnerPhone, FullName = r.OwnerName, PhoneVerifiedAt = r.ConsentAt };
            // Nobody knows this password: the owner signs in with an SMS code and can set one later.
            user.PasswordHash = hasher.HashPassword(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));
            db.Users.Add(user);
        }

        var shop = new Shop { Name = r.ShopName, Slug = AuthService.NewSlug(r.ShopName), Area = r.Market, Type = r.Type, ApprovedAt = now };
        db.Shops.Add(shop);
        db.Memberships.Add(new Membership { UserId = user.Id, ShopId = shop.Id, Role = ShopRole.Owner });
        (r.Status, r.ShopId, r.ReviewedAt, r.ReviewedByUserId) = (RegistrationStatus.Approved, shop.Id, now, admin.UserId());
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ViewsAsync(db, db.ShopRegistrations.Where(x => x.Id == id), ct)).Single());
    }

    private static async Task<IResult> RejectAsync(Guid id, RegistrationReasonRequest request, ClaimsPrincipal admin, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (reason is not { Length: >= 3 and <= 200 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Reason"] = ["Say why (the agent sees it)."] });
        }

        var r = await db.ShopRegistrations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
        {
            return TypedResults.NotFound();
        }

        if (r.Status != RegistrationStatus.Submitted)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Already reviewed.");
        }

        (r.Status, r.RejectReason, r.ReviewedAt, r.ReviewedByUserId) = (RegistrationStatus.Rejected, reason, clock.GetUtcNow(), admin.UserId());
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ViewsAsync(db, db.ShopRegistrations.Where(x => x.Id == id), ct)).Single());
    }

    /// <summary>The money the agent reported is in (bank or cash handed over): recorded as the shop's payment, its plan starts.</summary>
    private static async Task<IResult> ConfirmPaymentAsync(Guid id, ClaimsPrincipal admin, AppDbContext db, BillingService billing, CancellationToken ct)
    {
        var r = await db.ShopRegistrations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
        {
            return TypedResults.NotFound();
        }

        if (r is not { Status: RegistrationStatus.Approved, PaymentStatus: RegistrationPaymentStatus.Received, ShopId: { } shopId })
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Approve the shop first; only a payment the agent reported can be confirmed.");
        }

        var quote = await billing.QuoteAsync(shopId, r.PlanCode, r.Term, null, ct);
        if (quote.Error is not null)
        {
            quote = new Quote(r.PlanCode, r.Term, r.Amount, null, null);
        }

        var payment = await billing.RecordAsync(shopId, quote with { Amount = r.Amount },
            r.PaymentMode == RegistrationPayment.Upi ? PaymentMethod.Upi : PaymentMethod.Cash, r.PaymentRef, admin.UserId(), ct);
        (r.PaymentStatus, r.PaymentId) = (RegistrationPaymentStatus.Confirmed, payment.Id);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ViewsAsync(db, db.ShopRegistrations.Where(x => x.Id == id), ct)).Single());
    }

    /// <summary>
    /// Registrations as shown, with warnings for the reviewer: the owner's number already has a shop or another
    /// sign-up, another sign-up within 30 metres, a rough GPS fix, or a UPI reference used before.
    /// </summary>
    private static async Task<IReadOnlyList<RegistrationDto>> ViewsAsync(AppDbContext db, IQueryable<ShopRegistration> query, CancellationToken ct, bool warnings = true)
    {
        var rows = await (from r in query.AsNoTracking()
                          join a in db.FieldAgents.AsNoTracking() on r.AgentId equals a.Id
                          select new { R = r, a.Code, a.FullName, Photos = r.Photos.Select(p => new RegistrationPhotoView(p.Id, p.Kind)).ToList() })
            .ToListAsync(ct);
        var phones = rows.Select(x => x.R.OwnerPhone).Distinct().ToList();
        var ids = rows.Select(x => x.R.Id).ToList();
        var others = warnings
            ? await db.ShopRegistrations.AsNoTracking().Where(r => r.Status != RegistrationStatus.Rejected)
                .Select(r => new { r.Id, r.OwnerPhone, r.Latitude, r.Longitude, r.PaymentRef, r.ShopName }).ToListAsync(ct)
            : [];
        var owners = warnings
            ? await (from u in db.Users.AsNoTracking()
                     where phones.Contains(u.Phone)
                     join m in db.Memberships.AsNoTracking() on u.Id equals m.UserId
                     where m.Role == ShopRole.Owner
                     select u.Phone).ToListAsync(ct)
            : [];

        return rows.Select(x =>
        {
            var r = x.R;
            var w = new List<string>();
            if (warnings)
            {
                if (owners.Contains(r.OwnerPhone) && r.Status == RegistrationStatus.Submitted) w.Add("This owner's number already has a shop.");
                if (others.Any(o => o.Id != r.Id && o.OwnerPhone == r.OwnerPhone)) w.Add("Another sign-up uses this owner's number.");
                if (others.FirstOrDefault(o => o.Id != r.Id && Metres(o.Latitude, o.Longitude, r.Latitude, r.Longitude) < 30) is { } near)
                    w.Add($"Another sign-up is within 30 m: {near.ShopName}.");
                if (r.AccuracyMetres is > 100) w.Add($"Rough location (±{r.AccuracyMetres:0} m).");
                if (r.PaymentRef is { } pr && others.Any(o => o.Id != r.Id && o.PaymentRef == pr)) w.Add("This payment reference was used on another sign-up.");
            }

            return new RegistrationDto(r.Id, r.Status, r.ShopName, r.Type, r.Address, r.Market, r.Latitude, r.Longitude, r.AccuracyMetres, r.OwnerName,
                r.OwnerPhone, r.AltPhone, r.Gstin, r.PlanCode, r.Term, r.Amount, r.PaymentMode, r.PaymentRef, r.PaymentStatus, r.CreatedAt, r.ConsentAt,
                r.RejectReason, r.ShopId, x.Code ?? "", x.FullName, x.Photos, w);
        }).ToList();
    }

    /// <summary>Distance on the ground between two GPS points (haversine).</summary>
    public static double Metres(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lon2 - lon1) / 2), 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private static string Hash(string phone, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"consent:{phone}:{code}")));
}
