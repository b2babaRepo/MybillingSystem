using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>A shop as its online store shows it. <see cref="Phone"/>: the owner's, for "send on WhatsApp".</summary>
public sealed record StoreDto(string Name, string Slug, string? Area, string? Address, string? Phone, IReadOnlyList<StoreItemDto> Items, Guid ShopId = default);

/// <summary><see cref="Available"/>: on the shelf minus what other online orders hold.</summary>
public sealed record StoreItemDto(
    Guid Id, string Name, string? Category, Unit Unit, decimal Price, decimal Available, int? CatalogueItemId = null, DateTimeOffset? ImageAt = null,
    decimal? Mrp = null, decimal? PackSize = null, Unit? PackUnit = null, string? ImageCredit = null,
    decimal? RegularPrice = null, string? OfferName = null);

public static class StoreStock
{
    /// <summary>What an item the shop doesn't count offers online: more than any one order takes.</summary>
    public const decimal Uncounted = 9999m;
}

public sealed record StoreLineRequest(Guid ProductId, decimal Quantity);

public sealed record StoreOrderRequest(
    Guid Id,
    string? Name,
    string? Phone,
    string? Address,
    Fulfilment? Fulfilment,
    string? Slot,
    string? Note,
    IReadOnlyList<StoreLineRequest>? Lines);

/// <summary>Returned once at checkout: <see cref="StatusToken"/> goes in the customer's order-status link.</summary>
public sealed record StoreOrderPlaced(OrderDto Order, string StatusToken);

/// <summary>
/// The shop's public online store at /s/{slug}: catalogue, guest checkout (no account) and a status page reached
/// by a secret link. Anonymous, so every call is rate-limited, and the shop comes only from the slug.
/// </summary>
public sealed record StorePayDto(string Name, string UpiId);

public static class StoreEndpoints
{
    public const string StoreRateLimit = "store";
    public const int MaxLines = 100;

    public static IEndpointRouteBuilder MapStore(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/store/{slug}").WithTags("Online store").AllowAnonymous().RequireRateLimiting(StoreRateLimit);
        group.MapGet("", GetStoreAsync);
        group.MapPost("/orders", PlaceAsync);
        group.MapGet("/orders/{id:guid}", GetOrderAsync);
        group.MapGet("/pay", GetPayAsync);
        return app;
    }

    /// <summary>The shop's name and UPI ID, for the pay page a dues reminder links to. 404 when the shop has no UPI ID.</summary>
    private static async Task<IResult> GetPayAsync(string slug, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (await OpenShopAsync(db, tenant, slug, ct) is not { } shop)
        {
            return TypedResults.NotFound();
        }

        var upi = await db.Shops.AsNoTracking().Where(s => s.Id == shop.Id).Select(s => s.UpiId).SingleOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(upi) ? TypedResults.NotFound() : TypedResults.Ok(new StorePayDto(shop.Name, upi));
    }

    private static async Task<IResult> GetStoreAsync(string slug, AppDbContext db, TenantContext tenant, Billing.Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        if (await OpenShopAsync(db, tenant, slug, ct) is not { } shop || !(await entitlements.ForShopAsync(shop.Id, ct)).Has(Billing.Features.Store))
        {
            return TypedResults.NotFound();
        }

        // Only priced, active items: an item without a price can't be ordered online.
        var items = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.SellingPrice > 0)
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .Select(p => new StoreItemDto(p.Id, p.Name, p.Category, p.Unit, p.SellingPrice, p.OutOn == today ? 0 : p.Uncounted ? StoreStock.Uncounted : p.Stock - p.Reserved, p.CatalogueItemId,
                p.ImageAt ?? (p.MasterItem != null ? p.MasterItem.ImageAt : null), p.Mrp, p.PackSize, p.PackUnit,
                p.ImageAt != null ? p.ImageCredit : p.MasterItem != null && p.MasterItem.ImageAt != null ? p.MasterItem.ImageCredit ?? Catalogue.MasterPhotos.Representative : null))
            .ToListAsync(ct);
        items = items.Select(i => i.CatalogueItemId is null ? i with { CatalogueItemId = Catalogue.CommonCatalogue.Match(i.Name) } : i).ToList();

        // The shop's running sale offers: the customer sees the offer price, the regular price struck out, and its name.
        var now = clock.GetUtcNow();
        var running = await Ledger.SaleOffers.RunningAsync(db, now, ct);
        if (running.Count > 0)
        {
            var onSale = await db.Products.AsNoTracking().Where(p => p.IsActive && p.SellingPrice > 0).ToDictionaryAsync(p => p.Id, ct);
            items = items.Select(i => onSale.TryGetValue(i.Id, out var product) && Ledger.SaleOffers.Best(running, product, now) is { } best && best.Price < i.Price
                ? i with { Price = best.Price, RegularPrice = i.Price, OfferName = best.Offer.Name } : i).ToList();
        }

        return TypedResults.Ok(new StoreDto(shop.Name, shop.Slug, shop.Area, shop.Address, shop.OwnerPhone, items, shop.Id));
    }

    /// <summary>
    /// Guest checkout. Prices come from the catalogue, never from the request. Each line reserves its quantity on
    /// the product, in the same transaction as the order. Idempotent on the app's order ID.
    /// </summary>
    private static async Task<IResult> PlaceAsync(string slug, StoreOrderRequest request, AppDbContext db, TenantContext tenant, Billing.Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        if (await OpenShopAsync(db, tenant, slug, ct) is not { } shop || !(await entitlements.ForShopAsync(shop.Id, ct)).Has(Billing.Features.Store))
        {
            return TypedResults.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        var phone = PhoneNumber.Normalize(request.Phone);
        if (request.Id == Guid.Empty)
        {
            errors[nameof(request.Id)] = ["The app must send an ID for every new record."];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        {
            errors[nameof(request.Name)] = ["Enter your name."];
        }

        if (phone is null)
        {
            errors[nameof(request.Phone)] = ["Enter a valid mobile number."];
        }

        var fulfilment = request.Fulfilment ?? Fulfilment.Pickup;
        if (fulfilment == Fulfilment.Delivery && string.IsNullOrWhiteSpace(request.Address))
        {
            errors[nameof(request.Address)] = ["Enter the delivery address."];
        }

        if (request.Address?.Length > 300 || request.Note?.Length > 300 || request.Slot?.Length > 80)
        {
            errors[nameof(request.Note)] = ["Keep the address, time and note short."];
        }

        if (request.Lines is not { Count: > 0 and <= MaxLines })
        {
            errors[nameof(request.Lines)] = [$"Add between 1 and {MaxLines} items."];
        }
        else if (request.Lines.Any(l => !Money.IsValidQuantity(l.Quantity)))
        {
            errors[nameof(request.Lines)] = ["Each quantity must be above 0, with at most three decimals."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // A retry of the same checkout: same order, but the secret link was shown only once, so it is not repeated.
        if (await db.Orders.AsNoTracking().Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == request.Id, ct) is { } existing)
        {
            return existing.CustomerPhone == phone
                ? TypedResults.Conflict(new { title = "This order was already placed. Open it from the link you got." })
                : TypedResults.Conflict();
        }

        var wanted = request.Lines!.GroupBy(l => l.ProductId).Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity))).ToList();
        var ids = wanted.Select(w => w.ProductId).ToList();
        // The tenant filter limits this to the shop from the slug: another shop's product ID simply isn't found.
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive && p.SellingPrice > 0).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != ids.Count)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = ["Some items are no longer sold. Please refresh the store."] });
        }

        // The store screen limits quantities, but only here is it certain. Services have no stock to run out of.
        var today = IndiaTime.Today(clock);
        var unavailable = wanted.Select(w => (Product: products[w.ProductId], w.Quantity))
            .Where(w => !w.Product.IsService && (w.Product.OutOn == today || (!w.Product.Uncounted && w.Product.Stock - w.Product.Reserved < w.Quantity)))
            .Select(w => w.Product.Name).ToList();
        if (unavailable.Count > 0)
        {
            return NotEnough(unavailable);
        }

        // The price the customer saw: a running offer's, when there is one.
        var running = await Ledger.SaleOffers.RunningAsync(db, clock.GetUtcNow(), ct);
        var token = Secrets.NewRefreshToken();
        var lineNo = 0;
        var order = new Order
        {
            Id = request.Id,
            Source = OrderSource.Store,
            CustomerName = request.Name!.Trim(),
            CustomerPhone = phone!,
            ListKind = ListKind.Weekly,
            Fulfilment = fulfilment,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Slot = string.IsNullOrWhiteSpace(request.Slot) ? null : request.Slot.Trim(),
            Payment = OrderPayment.Cash,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            StatusTokenHash = Secrets.Sha256Hex(token),
            Lines = wanted.Select(w =>
            {
                var product = products[w.ProductId];
                var best = Ledger.SaleOffers.Best(running, product, clock.GetUtcNow());
                return new OrderLine
                {
                    LineNo = ++lineNo,
                    ProductId = product.Id,
                    Name = product.Name,
                    Quantity = w.Quantity,
                    Unit = product.Unit,
                    UnitPrice = best?.Price ?? product.SellingPrice,
                    OfferId = best?.Offer.Id,
                    ReservedQuantity = w.Quantity,
                };
            }).ToList(),
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        foreach (var (productId, quantity) in wanted)
        {
            // Checked again as it is taken, so two customers can't both get the last one. Leaving here undoes the whole order.
            var taken = await db.Products.Where(p => p.Id == productId && (p.IsService || p.Uncounted || p.Stock - p.Reserved >= quantity))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Reserved, p => p.Reserved + quantity), ct);
            if (taken == 0)
            {
                return NotEnough([products[productId].Name]);
            }
        }

        await transaction.CommitAsync(ct);
        OrderSignals.Notify(order.ShopId);
        return TypedResults.Created($"/api/v1/store/{slug}/orders/{order.Id}", new StoreOrderPlaced(OrderDto.From(order, shop.Name), token));
    }

    /// <summary>The guest's status page. Needs the token from the link; a wrong or missing one looks like no order.</summary>
    private static IResult NotEnough(IEnumerable<string> names) => TypedResults.ValidationProblem(new Dictionary<string, string[]>
    {
        [nameof(StoreOrderRequest.Lines)] = [$"Not available in that quantity: {string.Join(", ", names)}. Please refresh the store."],
    });

    private static async Task<IResult> GetOrderAsync(string slug, Guid id, string? token, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || await OpenShopAsync(db, tenant, slug, ct) is not { } shop)
        {
            return TypedResults.NotFound();
        }

        var hash = Secrets.Sha256Hex(token);
        var order = await db.Orders.AsNoTracking().Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == id && o.Source == OrderSource.Store && o.StatusTokenHash == hash, ct);
        return order is null ? TypedResults.NotFound() : TypedResults.Ok(OrderDto.From(order, shop.Name));
    }

    internal sealed record OpenShop(string Name, string Slug, string? Area, string? Address, string? OwnerPhone, Guid Id);

    /// <summary>Finds an open shop by slug and works as that shop for the rest of the request (tenant + RLS).</summary>
    internal static async Task<OpenShop?> OpenShopAsync(AppDbContext db, TenantContext tenant, string slug, CancellationToken ct)
    {
        var shop = await (from s in db.Shops.AsNoTracking()
                          where s.Slug == slug.ToLowerInvariant() && s.IsActive
                          select new
                          {
                              s.Id,
                              s.Name,
                              s.Slug,
                              s.Area,
                              s.Address,
                              OwnerPhone = (from m in db.Memberships
                                            join u in db.Users on m.UserId equals u.Id
                                            where m.ShopId == s.Id && m.Role == ShopRole.Owner && m.IsActive
                                            orderby m.CreatedAt
                                            select u.Phone).FirstOrDefault(),
                          })
            .SingleOrDefaultAsync(ct);
        if (shop is null)
        {
            return null;
        }

        tenant.SetShop(shop.Id);
        return new OpenShop(shop.Name, shop.Slug, shop.Area, shop.Address, shop.OwnerPhone, shop.Id);
    }
}
