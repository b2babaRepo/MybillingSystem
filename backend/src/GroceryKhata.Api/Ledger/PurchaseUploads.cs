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

/// <summary>
/// A supplier's bill photo waiting to become a purchase: sent from the computer, or from a phone through a QR link that
/// needs no sign-in. Nothing is read or booked on upload: the owner opens it, the AI drafts the lines, the owner checks
/// them and saves the purchase as usual (stock, supplier khata, GST). Then it is marked used.
/// </summary>
public sealed class PurchaseUpload : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    /// <summary>The photo or PDF as a data URL (JPEG, PNG, WebP or PDF), at most <see cref="OrderDrafts.MaxImageBytes"/>.</summary>
    public required string Image { get; set; }

    /// <summary>"computer" or "phone".</summary>
    public required string Source { get; set; }

    /// <summary>The supplier bill (purchase) it became. Null: still waiting.</summary>
    public Guid? PurchaseId { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

public sealed record PurchaseUploadRequest(string ImageDataUrl);

public sealed record PurchaseUploadDto(Guid Id, string Source, DateTimeOffset CreatedAt, DateTimeOffset? UsedAt, Guid? PurchaseId);

/// <summary>The link a phone opens from the QR code: the shop, when it stops working, and its signature.</summary>
/// <summary><see cref="SentTo"/>: the last 4 digits of the owner's WhatsApp number the link went to; null when it wasn't sent.</summary>
public sealed record PurchaseUploadLinkDto(string Slug, long Expires, string Token, string? SentTo = null);

/// <summary>Where the phone page is (the app's address as the computer sees it), so the WhatsApp message can carry the whole link.</summary>
public sealed record PurchaseUploadLinkRequest(string? AppUrl);

public sealed record PurchaseUploadUsedRequest(Guid? PurchaseId);

public static class PurchaseUploads
{
    /// <summary>How long a QR link works.</summary>
    public static readonly TimeSpan LinkLife = TimeSpan.FromMinutes(30);

    /// <summary>At most this many photos wait at once, so an open link can't fill the shop's storage.</summary>
    public const int MaxWaiting = 50;

    public static IEndpointRouteBuilder MapPurchaseUploads(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/suppliers/uploads").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("", (PurchaseUploadRequest request, AppDbContext db, CancellationToken ct) => AddAsync(request, "computer", db, ct))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2_200_000));
        group.MapPost("/{id:guid}/used", UsedAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/link", MakeLink);
        app.MapPost("/api/v1/upload/{slug}", PhoneAsync).WithTags("Suppliers").AllowAnonymous().RequireRateLimiting(StoreEndpoints.StoreRateLimit)
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2_200_000));
        return app;
    }

    /// <summary>"upload:" keeps these signatures apart from bill links signed with the same key.</summary>
    public static string Token(string key, string slug, long expires) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes($"upload:{slug}:{expires}")))[..32].ToLowerInvariant();

    /// <summary>
    /// Makes the QR link and, like Vyapar, sends it to the owner's own WhatsApp so the phone can open it from there.
    /// Sending needs the WhatsApp Business number (Whatsapp:PhoneNumberId) and an approved template
    /// (Whatsapp:UploadLinkTemplate, its body's {{1}} is the link). Without them, or if WhatsApp refuses, the QR still works.
    /// </summary>
    private static async Task<IResult> MakeLink(
        PurchaseUploadLinkRequest? request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, IOptions<Billing.BillingOptions> options, TimeProvider clock,
        IConfiguration configuration, IServiceProvider services, ILogger<PurchaseUpload> logger, CancellationToken ct)
    {
        var slug = db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.Slug).Single();
        var expires = clock.GetUtcNow().Add(LinkLife).ToUnixTimeSeconds();
        var token = Token(options.Value.BillLinkKey, slug, expires);
        string? sentTo = null;
        var numberId = configuration["Whatsapp:PhoneNumberId"];
        var template = configuration["Whatsapp:UploadLinkTemplate"];
        if (services.GetService<IWhatsAppClient>() is { } whatsapp && !string.IsNullOrWhiteSpace(numberId) && !string.IsNullOrWhiteSpace(template)
            && Uri.TryCreate(request?.AppUrl, UriKind.Absolute, out var app) && app.Scheme is "https" or "http"
            && user.UserId() is { } userId && await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Phone).SingleOrDefaultAsync(ct) is { Length: >= 10 } phone)
        {
            var link = $"{app.GetLeftPart(UriPartial.Path)}#/up/{slug}?e={expires}&t={token}";
            try
            {
                await whatsapp.SendTemplateAsync(numberId, phone.TrimStart('+'), template, configuration["Whatsapp:UploadLinkLanguage"] ?? "en", [link], ct);
                sentTo = phone[^4..];
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Upload link could not be sent on WhatsApp");
            }
        }

        return TypedResults.Ok(new PurchaseUploadLinkDto(slug, expires, token, sentTo));
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.PurchaseUploads.AsNoTracking().OrderByDescending(u => u.CreatedAt).Take(100)
            .Select(u => new PurchaseUploadDto(u.Id, u.Source, u.CreatedAt, u.UsedAt, u.PurchaseId)).ToListAsync(ct));

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.PurchaseUploads.AsNoTracking().Where(u => u.Id == id).Select(u => u.Image).SingleOrDefaultAsync(ct) is { } image
            ? TypedResults.Ok(new PurchaseUploadRequest(image)) : TypedResults.NotFound();

    private static async Task<IResult> AddAsync(PurchaseUploadRequest request, string source, AppDbContext db, CancellationToken ct)
    {
        if (OrderDrafts.ParseImageOrPdf(request.ImageDataUrl).MediaType is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ImageDataUrl"] = ["Send a JPEG, PNG or WebP photo, or a PDF, under 1.5 MB."] });
        }

        if (await db.PurchaseUploads.CountAsync(u => u.UsedAt == null, ct) >= MaxWaiting)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: $"{MaxWaiting} bills are already waiting.", detail: "Make purchases from them, or delete some, then upload more.");
        }

        var upload = new PurchaseUpload { Image = request.ImageDataUrl, Source = source };
        db.PurchaseUploads.Add(upload);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/suppliers/uploads/{upload.Id}", new PurchaseUploadDto(upload.Id, upload.Source, upload.CreatedAt, null, null));
    }

    /// <summary>From the phone: a wrong or old link looks exactly like no such shop.</summary>
    private static async Task<IResult> PhoneAsync(
        string slug, long e, string? t, PurchaseUploadRequest request, AppDbContext db, TenantContext tenant, IOptions<Billing.BillingOptions> options, TimeProvider clock, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(t) || e < clock.GetUtcNow().ToUnixTimeSeconds()
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(t), Encoding.UTF8.GetBytes(Token(options.Value.BillLinkKey, slug.ToLowerInvariant(), e)))
            || await StoreEndpoints.OpenShopAsync(db, tenant, slug, ct) is null)
        {
            return TypedResults.NotFound();
        }

        return await AddAsync(request, "phone", db, ct);
    }

    private static async Task<IResult> UsedAsync(Guid id, PurchaseUploadUsedRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var upload = await db.PurchaseUploads.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (upload is null)
        {
            return TypedResults.NotFound();
        }

        upload.UsedAt ??= clock.GetUtcNow();
        upload.PurchaseId ??= request.PurchaseId;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var upload = await db.PurchaseUploads.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (upload is null)
        {
            return TypedResults.NotFound();
        }

        db.PurchaseUploads.Remove(upload);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
