using System.Net;
using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace GroceryKhata.Api.Planner;

/// <summary>A customer's phone (browser) that asked for order alerts. The customer's own, not a shop's: no tenant.</summary>
public sealed class CustomerPushDevice : Entity
{
    public Guid UserId { get; set; }

    public required string Endpoint { get; set; }

    public required string P256dh { get; set; }

    public required string Auth { get; set; }

    public string? Language { get; set; }
}

/// <summary>How a shop takes online orders: the delivery charge, free delivery above, minimum order and the time chips.</summary>
public sealed record StoreSettingsDto(decimal? DeliveryFee, decimal? FreeDeliveryAbove, decimal? MinOrder, IReadOnlyList<string> Slots, string? Theme = null);

/// <summary>What the checkout shows before the customer confirms. <see cref="Khata"/>: null when this shop keeps no khata the customer can use.</summary>
public sealed record CheckoutInfoDto(StoreSettingsDto Settings, string? UpiId, KhataCheckoutDto? Khata);

/// <summary><see cref="Allowed"/>: the shop lets this customer order on khata. <see cref="Limit"/>: how high the balance may go (null: no limit).</summary>
public sealed record KhataCheckoutDto(bool Allowed, decimal Balance, decimal? Limit);

public sealed record OnlineKhataRequest(bool Allowed);

public sealed record AddLinesRequest(IReadOnlyList<StoreLineRequest>? Lines);

public sealed record OrderAlertsDto(bool WhatsApp);

/// <summary>
/// Phase 2 of the customer app: the shop's extra order steps (accepted, ready for pickup, out for delivery), the
/// checkout rules a shop sets (charge, minimum, time chips, khata per customer), adding items to a placed order,
/// and telling the customer: a phone notification and, if they asked, WhatsApp from the shop's number.
/// </summary>
public static class OrderUpdates
{
    public const int MaxSlots = 6;

    public static IEndpointRouteBuilder MapOrderUpdates(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1/orders").WithTags("List inbox").RequireAuthorization(AppPolicies.ShopMember);
        shop.MapPost("/{id:guid}/accept", AcceptAsync);
        shop.MapPost("/{id:guid}/ready", ReadyAsync);

        app.MapGet("/api/v1/store-settings", GetSettingsAsync).WithTags("Online store").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPut("/api/v1/store-settings", PutSettingsAsync).WithTags("Online store").RequireAuthorization(AppPolicies.ShopOwner);
        app.MapGet("/api/v1/customers/{id:guid}/online-khata", async (Guid id, AppDbContext db, CancellationToken ct) =>
                await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => (bool?)c.OnlineKhata).SingleOrDefaultAsync(ct) is { } allowed
                    ? Results.Ok(new OnlineKhataRequest(allowed)) : Results.NotFound())
            .WithTags("Online store").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPut("/api/v1/customers/{id:guid}/online-khata", OnlineKhataAsync).WithTags("Online store").RequireAuthorization(AppPolicies.ShopOwner);

        var me = app.MapGroup("/api/v1/me").WithTags("Customer").RequireAuthorization();
        me.MapGet("/checkout/{shopId:guid}", CheckoutAsync);
        me.MapPost("/orders/{id:guid}/add", AddLinesAsync);
        me.MapGet("/push/key", async (AppDbContext db, CancellationToken ct) => Results.Ok(new { key = (await Ledger.StockAlerts.KeysAsync(db, ct)).PublicKey }));
        me.MapPost("/push/subscribe", SubscribeAsync);
        me.MapPost("/push/unsubscribe", async (Ledger.PushSubscribeRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            await db.CustomerPushDevices.Where(d => d.Endpoint == request.Endpoint && d.UserId == user.UserId()).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        me.MapGet("/order-alerts", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
            Results.Ok(new OrderAlertsDto(await db.Users.Where(u => u.Id == user.UserId()).Select(u => u.OrderWhatsApp).SingleOrDefaultAsync(ct))));
        me.MapPut("/order-alerts", async (OrderAlertsDto request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            await db.Users.Where(u => u.Id == user.UserId()).ExecuteUpdateAsync(s => s.SetProperty(u => u.OrderWhatsApp, request.WhatsApp), ct);
            return Results.Ok(request);
        });
        return app;
    }

    // ---- The shop's steps --------------------------------------------------------------------------------------

    /// <summary>The shop has seen the order and will pack it. Only from Placed; a repeat is harmless.</summary>
    private static async Task<IResult> AcceptAsync(Guid id, AppDbContext db, ITenantContext tenant, IServiceProvider services, TimeProvider clock, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id && o.ShopId == tenant.ShopId, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status == OrderStatus.Placed)
        {
            (order.Status, order.AcceptedAt) = (OrderStatus.Accepted, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            await NotifyAsync(services, db, order, "accepted", ct);
        }
        else if (order.Status != OrderStatus.Accepted)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This order is already packed, delivered or cancelled.");
        }

        return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
    }

    /// <summary>A packed order goes out: ready at the counter (pickup) or out for delivery. Waits for the customer's OKs first.</summary>
    private static async Task<IResult> ReadyAsync(Guid id, AppDbContext db, ITenantContext tenant, IServiceProvider services, TimeProvider clock, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id && o.ShopId == tenant.ShopId, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        var next = order.Fulfilment == Fulfilment.Pickup ? OrderStatus.ReadyForPickup : OrderStatus.OutForDelivery;
        if (order.Status == next)
        {
            return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
        }

        if (order.Status != OrderStatus.Packed)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Pack the order first.");
        }

        if (order.Lines.Any(l => l.Approval == LineApproval.Pending))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Waiting for the customer to OK the changed items.");
        }

        (order.Status, order.ReadyAt) = (next, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await NotifyAsync(services, db, order, next == OrderStatus.ReadyForPickup ? "ready" : "out", ct);
        return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
    }

    // ---- What the shop sets ------------------------------------------------------------------------------------

    internal static StoreSettingsDto SettingsOf(Shops.Shop s) => new(s.StoreDeliveryFee, s.StoreFreeDeliveryAbove, s.StoreMinOrder, s.StoreSlots, s.StoreTheme);

    /// <summary>The festive looks a store can wear.</summary>
    public static readonly string[] Themes = ["diwali", "holi", "navratri", "eid", "christmas", "independence", "wedding", "pongal"];

    private static async Task<IResult> GetSettingsAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        TypedResults.Ok(SettingsOf(await db.Shops.AsNoTracking().SingleAsync(s => s.Id == tenant.ShopId, ct)));

    private static async Task<IResult> PutSettingsAsync(StoreSettingsDto request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        static bool Bad(decimal? x) => x is { } v && (v < 0 || v > 100_000 || decimal.Round(v, 2) != v);
        var slots = (request.Slots ?? []).Select(x => x?.Trim() ?? "").Where(x => x.Length > 0).Distinct().ToList();
        var errors = new Dictionary<string, string[]>();
        if (Bad(request.DeliveryFee) || Bad(request.FreeDeliveryAbove) || Bad(request.MinOrder))
        {
            errors["DeliveryFee"] = ["Enter amounts in rupees, up to ₹1,00,000."];
        }

        if (slots.Count > MaxSlots || slots.Any(x => x.Length > 40))
        {
            errors["Slots"] = [$"Up to {MaxSlots} times, each under 40 letters."];
        }

        if (request.Theme is { Length: > 0 } theme && !Themes.Contains(theme))
        {
            errors["Theme"] = ["Pick one of the festive looks."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var shop = await db.Shops.SingleAsync(s => s.Id == tenant.ShopId, ct);
        shop.StoreTheme = string.IsNullOrEmpty(request.Theme) ? null : request.Theme;
        static decimal? Zero(decimal? x) => x is > 0 ? x : null;
        (shop.StoreDeliveryFee, shop.StoreFreeDeliveryAbove, shop.StoreMinOrder, shop.StoreSlots) =
            (Zero(request.DeliveryFee), Zero(request.FreeDeliveryAbove), Zero(request.MinOrder), slots.ToArray());
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(SettingsOf(shop));
    }

    private static async Task<IResult> OnlineKhataAsync(Guid id, OnlineKhataRequest request, AppDbContext db, CancellationToken ct)
    {
        var changed = await db.Customers.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.OnlineKhata, request.Allowed), ct);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.Ok(request);
    }

    /// <summary>The delivery charge for an item total: none for pickup, none at or above the free-delivery amount.</summary>
    public static decimal DeliveryFee(StoreSettingsDto settings, Fulfilment fulfilment, decimal itemTotal) =>
        fulfilment == Fulfilment.Delivery && settings.DeliveryFee is > 0 and var fee && !(settings.FreeDeliveryAbove is > 0 and var free && itemTotal >= free) ? fee : 0m;

    // ---- Khata at checkout -------------------------------------------------------------------------------------

    /// <summary>
    /// The signed-in person's khata at the shop the request is working as: only a khata under their SMS-proved phone
    /// that they have confirmed is theirs. Null when there is none.
    /// </summary>
    internal static async Task<Ledger.Customer?> MyKhataAsync(AppDbContext db, Guid? userId, CancellationToken ct)
    {
        if (userId is null)
        {
            return null;
        }

        var phone = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.PhoneVerifiedAt != null).Select(u => u.Phone).SingleOrDefaultAsync(ct);
        return phone is null ? null
            : await db.Customers.AsNoTracking().Where(c => c.Phone == phone && c.PhoneConfirmedAt != null).OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
    }

    /// <summary>Why an order of this amount can't go on khata, or null when it can.</summary>
    internal static string? KhataProblem(Ledger.Customer? khata, decimal amount) =>
        khata is null ? "You have no khata at this shop yet. Ask the shop to open one, or pay at delivery."
        : !khata.OnlineKhata ? "This shop doesn't take app orders on khata for you yet. Pay at delivery or by UPI, or ask the shop."
        : khata.CreditLimit is { } limit && khata.Balance + amount > limit ? $"This would take your khata above the shop's limit of ₹{limit:0.##}."
        : null;

    private static async Task<IResult> CheckoutAsync(Guid shopId, ClaimsPrincipal user, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().SingleOrDefaultAsync(s => s.Id == shopId && s.IsActive, ct);
        if (shop is null)
        {
            return TypedResults.NotFound();
        }

        tenant.SetShop(shopId);
        var khata = await MyKhataAsync(db, user.UserId(), ct);
        return TypedResults.Ok(new CheckoutInfoDto(SettingsOf(shop), string.IsNullOrWhiteSpace(shop.UpiId) ? null : shop.UpiId,
            khata is null ? null : new KhataCheckoutDto(khata.OnlineKhata, khata.Balance, khata.CreditLimit)));
    }

    // ---- Adding to a placed order ------------------------------------------------------------------------------

    /// <summary>
    /// The customer adds items while the shop hasn't packed yet (Placed or Accepted), with no extra charge. Priced
    /// and stock-held exactly as a store checkout; an item already on the order gets the extra quantity.
    /// </summary>
    private static async Task<IResult> AddLinesAsync(Guid id, AddLinesRequest request, ClaimsPrincipal user, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var shopId = await db.Orders.AsNoTracking().Where(o => o.Id == id && o.PlacedByUserId == userId).Select(o => (Guid?)o.ShopId).SingleOrDefaultAsync(ct);
        if (shopId is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Lines is not { Count: > 0 and <= StoreEndpoints.MaxLines } || request.Lines.Any(l => !Money.IsValidQuantity(l.Quantity)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = ["Add at least one item, each with a quantity."] });
        }

        tenant.SetShop(shopId.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == id, ct);
        if (order.Status is not (OrderStatus.Placed or OrderStatus.Accepted))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "The shop has already packed this order. Call the shop, or place a new order.");
        }

        var wanted = request.Lines.GroupBy(l => l.ProductId).Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity))).ToList();
        var ids = wanted.Select(w => w.ProductId).ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive && p.SellingPrice > 0).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != ids.Count)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = ["Some items are no longer sold. Please refresh the shop."] });
        }

        if (order.Lines.Count + wanted.Count > StoreEndpoints.MaxLines)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = [$"An order can have up to {StoreEndpoints.MaxLines} items."] });
        }

        var running = await Ledger.SaleOffers.RunningAsync(db, clock.GetUtcNow(), ct);
        var lineNo = order.Lines.Count == 0 ? 0 : order.Lines.Max(l => l.LineNo);
        var today = IndiaTime.Today(clock);
        foreach (var (productId, quantity) in wanted)
        {
            var product = products[productId];
            var taken = product.OutOn == today ? 0 : await db.Products.Where(p => p.Id == productId && (p.IsService || p.Uncounted || p.Stock - p.Reserved >= quantity))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Reserved, p => p.Reserved + quantity), ct);
            if (taken == 0)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = [$"Not available in that quantity: {product.Name}."] });
            }

            if (order.Lines.FirstOrDefault(l => l.ProductId == productId && !l.Removed && l.PackedQuantity is null) is { } same)
            {
                same.Quantity += quantity;
                same.ReservedQuantity += quantity;
                continue;
            }

            var best = Ledger.SaleOffers.Best(running, product, clock.GetUtcNow());
            var line = new OrderLine
            {
                ShopId = order.ShopId,
                PlacedByUserId = userId,
                OrderId = order.Id,
                LineNo = ++lineNo,
                ProductId = product.Id,
                Name = product.Name,
                Quantity = quantity,
                Unit = product.Unit,
                UnitPrice = best?.Price ?? product.SellingPrice,
                OfferId = best?.Offer.Id,
                ReservedQuantity = quantity,
            };
            order.Lines.Add(line);
            // Its ID is already set, so it must be marked new or saving would look for it to update.
            db.Add(line);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        OrderSignals.Notify(order.ShopId);
        return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
    }

    // ---- Telling the customer ----------------------------------------------------------------------------------

    private static async Task<IResult> SubscribeAsync(Ledger.PushSubscribeRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (request.Endpoint is not { Length: > 10 and <= 1000 } endpoint || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || request.P256dh is not { Length: > 10 and <= 200 } || request.Auth is not { Length: > 5 and <= 100 })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Endpoint"] = ["This phone's notification details are not valid."] });
        }

        var userId = user.UserId()!.Value;
        var language = request.Language is "hi" or "gu" ? request.Language : "en";
        if (await db.CustomerPushDevices.FirstOrDefaultAsync(d => d.Endpoint == endpoint, ct) is { } known)
        {
            // The same phone, perhaps now signed in as someone else in the family: alerts follow who is signed in.
            (known.UserId, known.Language) = (userId, language);
        }
        else
        {
            db.CustomerPushDevices.Add(new CustomerPushDevice { UserId = userId, Endpoint = endpoint, P256dh = request.P256dh, Auth = request.Auth, Language = language });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Subscribed twice at once: the other one stands.
        }

        return Results.NoContent();
    }

    /// <summary>The alert for one step, in the phone's language: title and body.</summary>
    public static (string Title, string Body) Message(string lang, string kind, string shop, string code)
    {
        var hi = lang == "hi";
        var gu = lang == "gu";
        return kind switch
        {
            "accepted" => (hi ? $"{shop} ने ऑर्डर ले लिया" : gu ? $"{shop}એ ઓર્ડર લીધો" : $"{shop} accepted your order",
                hi ? $"ऑर्डर {code} पैक हो रहा है।" : gu ? $"ઓર્ડર {code} પૅક થઈ રહ્યો છે." : $"Order {code} is being packed."),
            "updated" => (hi ? $"{shop} ने आपका ऑर्डर बदला" : gu ? $"{shop}એ તમારો ઓર્ડર બદલ્યો" : $"{shop} updated your order",
                hi ? "बदले सामान को हाँ या ना कहें।" : gu ? "બદલાયેલી વસ્તુને હા કે ના કહો." : "Please OK or leave out the changed items."),
            "packed" => (hi ? $"ऑर्डर {code} पैक हो गया" : gu ? $"ઓર્ડર {code} પૅક થયો" : $"Order {code} is packed",
                hi ? $"{shop} पर तैयार।" : gu ? $"{shop} પર તૈયાર." : $"Ready at {shop} soon."),
            "ready" => (hi ? $"ऑर्डर {code} लेने के लिए तैयार" : gu ? $"ઓર્ડર {code} લેવા માટે તૈયાર" : $"Order {code} is ready for pickup",
                hi ? $"{shop} से ले जाएँ।" : gu ? $"{shop}થી લઈ જાઓ." : $"Collect it from {shop}."),
            "out" => (hi ? $"ऑर्डर {code} रास्ते में है" : gu ? $"ઓર્ડર {code} રસ્તામાં છે" : $"Order {code} is out for delivery",
                hi ? $"{shop} से निकल गया।" : gu ? $"{shop}થી નીકળી ગયો." : $"On its way from {shop}."),
            "delivered" => (hi ? $"ऑर्डर {code} मिल गया" : gu ? $"ઓર્ડર {code} મળી ગયો" : $"Order {code} delivered",
                hi ? "बिल ऐप में देखें।" : gu ? "બિલ ઍપમાં જુઓ." : "See the bill in the app."),
            _ => (hi ? $"ऑर्डर {code} रद्द" : gu ? $"ઓર્ડર {code} રદ" : $"Order {code} cancelled",
                hi ? $"{shop} ने ऑर्डर रद्द किया।" : gu ? $"{shop}એ ઓર્ડર રદ કર્યો." : $"{shop} cancelled the order."),
        };
    }

    private static string WhatsAppStep(string kind) => kind switch
    {
        "accepted" => "accepted by the shop",
        "updated" => "changed by the shop. Please OK the changes in the app",
        "packed" => "packed",
        "ready" => "ready for pickup",
        "out" => "out for delivery",
        "delivered" => "delivered",
        _ => "cancelled",
    };

    /// <summary>
    /// Tells the customer who placed the order: a notification on each phone they asked on, and WhatsApp from the
    /// shop's number if they opted in and the shop has connected WhatsApp. Never fails the shop's step.
    /// </summary>
    internal static async Task NotifyAsync(IServiceProvider services, AppDbContext db, Order order, string kind, CancellationToken ct)
    {
        if (order.PlacedByUserId is not { } userId)
        {
            return;
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("OrderUpdates");
        try
        {
            var shop = await db.Shops.AsNoTracking().Where(s => s.Id == order.ShopId).Select(s => s.Name).SingleAsync(ct);
            var code = $"#{order.Id.ToString("N")[^4..].ToUpperInvariant()}";
            var devices = await db.CustomerPushDevices.Where(d => d.UserId == userId).ToListAsync(ct);
            if (devices.Count > 0)
            {
                var keys = await Ledger.StockAlerts.KeysAsync(db, ct);
                var vapid = new VapidDetails("mailto:support@groceryKhata.app", keys.PublicKey, keys.PrivateKey);
                using var client = new WebPushClient();
                foreach (var device in devices)
                {
                    var (title, body) = Message(device.Language ?? "en", kind, shop, code);
                    var payload = JsonSerializer.Serialize(new { title, body, url = $"/#/orders/{order.Id}" });
                    try
                    {
                        await client.SendNotificationAsync(new PushSubscription(device.Endpoint, device.P256dh, device.Auth), payload, vapid, ct);
                    }
                    catch (WebPushException e) when (e.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                    {
                        db.CustomerPushDevices.Remove(device);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        logger.LogWarning(e, "Order alert not delivered to a device");
                    }
                }

                await db.SaveChangesAsync(ct);
            }

            var wants = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.OrderWhatsApp, u.FullName }).SingleOrDefaultAsync(ct);
            if (wants is { OrderWhatsApp: true })
            {
                await WhatsAppShopMessages.SendOrderUpdateAsync(db, services, order.CustomerPhone, [wants.FullName, code, shop, WhatsAppStep(kind)], $"orders/{order.Id}", ct);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Order update for {Order} not sent", order.Id);
        }
    }
}
