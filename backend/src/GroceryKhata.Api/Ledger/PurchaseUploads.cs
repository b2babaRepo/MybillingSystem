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

    /// <summary>The photo as a data URL (JPEG, PNG or WebP), at most <see cref="OrderDrafts.MaxImageBytes"/>.</summary>
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
public sealed record PurchaseUploadLinkDto(string Slug, long Expires, string Token);

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

    private static IResult MakeLink(AppDbContext db, ITenantContext tenant, IOptions<Billing.BillingOptions> options, TimeProvider clock)
    {
        var slug = db.Shops.Where(s => s.Id == tenant.ShopId).Select(s => s.Slug).Single();
        var expires = clock.GetUtcNow().Add(LinkLife).ToUnixTimeSeconds();
        return TypedResults.Ok(new PurchaseUploadLinkDto(slug, expires, Token(options.Value.BillLinkKey, slug, expires)));
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.PurchaseUploads.AsNoTracking().OrderByDescending(u => u.CreatedAt).Take(100)
            .Select(u => new PurchaseUploadDto(u.Id, u.Source, u.CreatedAt, u.UsedAt, u.PurchaseId)).ToListAsync(ct));

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.PurchaseUploads.AsNoTracking().Where(u => u.Id == id).Select(u => u.Image).SingleOrDefaultAsync(ct) is { } image
            ? TypedResults.Ok(new PurchaseUploadRequest(image)) : TypedResults.NotFound();

    private static async Task<IResult> AddAsync(PurchaseUploadRequest request, string source, AppDbContext db, CancellationToken ct)
    {
        if (OrderDrafts.ParseImage(request.ImageDataUrl).MediaType is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ImageDataUrl"] = ["Send a JPEG, PNG or WebP photo under 1.5 MB."] });
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
