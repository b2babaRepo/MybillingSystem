using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// The household side: a person's monthly and weekly lists, finding a shop, and sending a list to it.
/// Works with a plain user token (no shop); everything here is scoped to the signed-in person.
/// </summary>
public static class PlannerEndpoints
{
    public static IEndpointRouteBuilder MapPlanner(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/v1/me").WithTags("Household planner").RequireAuthorization();
        me.MapGet("/lists", GetListsAsync);
        me.MapPut("/lists/{kind}/items/{id:guid}", UpsertItemAsync);
        me.MapDelete("/lists/{kind}/items/{id:guid}", DeleteItemAsync);
        me.MapGet("/orders", GetMyOrdersAsync);
        me.MapPost("/orders", PlaceOrderAsync);
        me.MapPost("/orders/{id:guid}/lines/{lineId:guid}/approve", ApproveLineAsync);

        app.MapGet("/api/v1/shops", SearchShopsAsync).WithTags("Household planner").RequireAuthorization();
        return app;
    }

    private static async Task<IReadOnlyList<ListDto>> GetListsAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var lists = await db.ShoppingLists.AsNoTracking().Include(l => l.Items).Where(l => l.UserId == userId).ToListAsync(ct);
        return Enum.GetValues<ListKind>().Select(kind =>
        {
            var items = lists.SingleOrDefault(l => l.Kind == kind)?.Items ?? [];
            return new ListDto(kind, items.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
                .Select(i => new ListItemDto(i.Id, i.Name, i.Quantity, i.Unit, i.Checked, i.Note, i.CatalogueItemId, i.AiConfidence)).ToList());
        }).ToList();
    }

    private static async Task<IResult> UpsertItemAsync(
        string kind, Guid id, UpsertItemRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!TryParseKind(kind, out var listKind) || id == Guid.Empty)
        {
            return TypedResults.NotFound();
        }

        if (request.Validate() is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var userId = user.UserId()!.Value;
        var item = await db.ShoppingListItems.SingleOrDefaultAsync(i => i.Id == id, ct);
        var list = await EnsureListAsync(db, userId, listKind, ct);
        if (item is not null && item.ListId != list.Id)
        {
            // Someone else's item, or one on the other list: never move or reveal it.
            return TypedResults.NotFound();
        }

        if (item is null)
        {
            item = new ShoppingListItem { Id = id, ListId = list.Id, Name = request.Name!.Trim() };
            db.ShoppingListItems.Add(item);
        }

        item.Name = request.Name!.Trim();
        item.CatalogueItemId = request.CatalogueItemId;
        item.Quantity = request.Quantity;
        item.Unit = request.Unit ?? Catalogue.Unit.Piece;
        item.Checked = request.Checked;
        item.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        item.AiConfidence = request.AiConfidence;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // Two first edits raced to create the list or the item; the other one won, and that's the same result.
            return TypedResults.Conflict();
        }

        return TypedResults.Ok(new ListItemDto(item.Id, item.Name, item.Quantity, item.Unit, item.Checked, item.Note, item.CatalogueItemId, item.AiConfidence));
    }

    private static async Task<IResult> DeleteItemAsync(string kind, Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!TryParseKind(kind, out var listKind))
        {
            return TypedResults.NotFound();
        }

        var userId = user.UserId()!.Value;
        // Deleting what is already gone is fine: an offline delete may arrive twice.
        await db.ShoppingListItems
            .Where(i => i.Id == id && db.ShoppingLists.Any(l => l.Id == i.ListId && l.UserId == userId && l.Kind == listKind))
            .ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IReadOnlyList<ShopDirectoryDto>> SearchShopsAsync(
        string? search, string? type, double? lat, double? lng, AppDbContext db, Microsoft.Extensions.Options.IOptions<Shops.PlatformOptions> platform, CancellationToken ct)
    {
        var hold = platform.Value.HoldNewShops;
        var query = db.Shops.AsNoTracking().Where(s => s.IsActive && (!hold || s.ApprovedAt != null));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s => EF.Functions.ILike(s.Name, pattern) || (s.Area != null && EF.Functions.ILike(s.Area, pattern))
                || (s.City != null && EF.Functions.ILike(s.City, pattern)) || (s.Village != null && EF.Functions.ILike(s.Village, pattern))
                || db.Markets.Any(m => m.Id == s.MarketId && EF.Functions.ILike(m.Name, pattern)));
        }

        // "building_materials", as the app writes it.
        if (Enum.TryParse<Shops.ShopType>(type?.Replace("_", ""), ignoreCase: true, out var wanted))
        {
            var name = Shops.ShopTypes.Name(wanted);
            query = query.Where(s => s.Type == wanted || s.ExtraTypes.Contains(name));
        }

        // Near me: shops with a location within 5 km, nearest first. The point is used for this search only, never stored.
        if (lat is >= -90 and <= 90 && lng is >= -180 and <= 180)
        {
            const double radiusKm = 5;
            var (dLat, dLng) = (radiusKm / 111.0, radiusKm / (111.0 * Math.Cos(lat.Value * Math.PI / 180)));
            var near = await query.Where(s => s.Latitude != null && s.Longitude != null
                    && s.Latitude > lat - dLat && s.Latitude < lat + dLat && s.Longitude > lng - dLng && s.Longitude < lng + dLng)
                .Select(s => new { s.Id, s.Name, s.Area, s.Address, s.Type, Lat = s.Latitude!.Value, Lng = s.Longitude!.Value }).Take(500).ToListAsync(ct);
            return near.Select(s => new ShopDirectoryDto(s.Id, s.Name, s.Area, s.Address, s.Type, Math.Round(DistanceKm(lat.Value, lng!.Value, s.Lat, s.Lng), 1)))
                .Where(s => s.DistanceKm <= radiusKm).OrderBy(s => s.DistanceKm).Take(50).ToList();
        }

        return await query.OrderBy(s => s.Area).ThenBy(s => s.Name).Take(50)
            .Select(s => new ShopDirectoryDto(s.Id, s.Name, s.Area, s.Address, s.Type, null))
            .ToListAsync(ct);
    }

    /// <summary>Straight-line distance on the earth (haversine).</summary>
    private static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        double Rad(double degrees) => degrees * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) + (Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lng2 - lng1) / 2), 2));
        return 6371 * 2 * Math.Asin(Math.Sqrt(a));
    }

    private static async Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.PlacedByUserId == userId)
            .OrderByDescending(o => o.CreatedAt).Take(30)
            .ToListAsync(ct);
        return await WithShopNamesAsync(db, orders, ct);
    }

    /// <summary>
    /// Copies the household's list into an order at the chosen shop. Idempotent on the app's ID. The list keeps
    /// its items (next month starts from it); only the ticks are cleared.
    /// </summary>
    private static async Task<IResult> PlaceOrderAsync(
        PlaceOrderRequest request, ClaimsPrincipal user, AppDbContext db, TenantContext tenant, Microsoft.Extensions.Options.IOptions<Shops.PlatformOptions> platform, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        if (await db.Orders.AsNoTracking().Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == request.Id && o.PlacedByUserId == userId, ct) is { } existing)
        {
            return TypedResults.Ok((await WithShopNamesAsync(db, [existing], ct))[0]);
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty)
        {
            errors[nameof(request.Id)] = ["The app must send an ID for every new record."];
        }

        if (request.Kind is null)
        {
            errors[nameof(request.Kind)] = ["Choose the monthly or weekly list."];
        }

        if (request.Slot?.Length > 80 || request.Note?.Length > 300)
        {
            errors[nameof(request.Note)] = ["Keep the time and note short."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (tenant.ShopId != Guid.Empty && tenant.ShopId != request.ShopId)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ShopId)] = ["Switch to your household lists to order from another shop."],
            });
        }

        var hold = platform.Value.HoldNewShops;
        var shop = await db.Shops.AsNoTracking().SingleOrDefaultAsync(s => s.Id == request.ShopId && s.IsActive && (!hold || s.ApprovedAt != null), ct);
        if (shop is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.ShopId)] = ["Shop not found."] });
        }

        var list = await db.ShoppingLists.Include(l => l.Items).SingleOrDefaultAsync(l => l.UserId == userId && l.Kind == request.Kind, ct);
        if (list is not { Items.Count: > 0 })
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Items"] = ["Add something to the list first."] });
        }

        var person = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);

        // The order belongs to the shop: work as that shop for this insert, so the stamp and row-level security accept it.
        tenant.SetShop(shop.Id);
        // Lines picked from the common catalogue (or named like it) arrive matched to this shop's item and price,
        // so packing starts filled in and delivery takes the stock. Anything unmatched the shop prices by hand.
        var products = await CatalogueEndpoints.ShopProductsAsync(db, ct);
        var shopItems = CatalogueEndpoints.MatchToCatalogue(products).ToDictionary(m => m.CatalogueItemId, m => m.Product);
        var choices = (request.Choices ?? []).GroupBy(c => c.ListItemId).ToDictionary(g => g.Key, g => g.Last());
        var lineNo = 0;
        var order = new Order
        {
            Id = request.Id,
            PlacedByUserId = userId,
            CustomerName = person.FullName,
            CustomerPhone = person.Phone,
            ListKind = request.Kind!.Value,
            Fulfilment = request.Fulfilment ?? Fulfilment.Pickup,
            Slot = string.IsNullOrWhiteSpace(request.Slot) ? null : request.Slot.Trim(),
            Payment = request.Payment ?? OrderPayment.Cash,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            // A copy of the words: changing or deleting the saved address later doesn't change this order.
            Address = (request.Fulfilment ?? Fulfilment.Pickup) == Fulfilment.Delivery && !string.IsNullOrWhiteSpace(request.Address) ? request.Address.Trim()[..Math.Min(request.Address.Trim().Length, 300)] : null,
            Lines = list.Items.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
                .Select(i => OrderLines.FromListItem(i, choices.GetValueOrDefault(i.Id), products, shopItems, userId, ++lineNo)).ToList(),
        };
        db.Orders.Add(order);
        foreach (var item in list.Items)
        {
            item.Checked = false;
        }

        try
        {
            await db.SaveChangesAsync(ct);
            OrderSignals.Notify(order.ShopId);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            return TypedResults.Conflict();
        }

        return TypedResults.Created($"/api/v1/me/orders/{order.Id}", OrderDto.From(order, shop.Name));
    }

    /// <summary>
    /// The household's answer to a changed line: OK (it stays on the bill) or no (it's left out and the total drops).
    /// Once nothing waits, the shop can deliver.
    /// </summary>
    private static async Task<IResult> ApproveLineAsync(
        Guid id, Guid lineId, ApproveLineRequest request, ClaimsPrincipal user, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var shopId = await db.Orders.AsNoTracking().Where(o => o.Id == id && o.PlacedByUserId == userId).Select(o => (Guid?)o.ShopId).SingleOrDefaultAsync(ct);
        if (shopId is null)
        {
            return TypedResults.NotFound();
        }

        // The order is the shop's: change it as that shop, so the stamp and row-level security accept it.
        tenant.SetShop(shopId.Value);
        var order = await db.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == id, ct);
        var line = order.Lines.SingleOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return TypedResults.NotFound();
        }

        if (line.Approval == LineApproval.Pending && order.Status == OrderStatus.Packed)
        {
            line.Approval = request.Approve ? LineApproval.Approved : LineApproval.Declined;
            if (!request.Approve)
            {
                line.Removed = true;
            }

            if (order.Lines.All(l => l.Removed))
            {
                order.Status = OrderStatus.Cancelled; // nothing left to deliver
            }
            else
            {
                order.FinalAmount = OrderEndpoints.FinalAmount(order);
            }

            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok((await WithShopNamesAsync(db, [order], ct))[0]);
    }

    internal static async Task<IReadOnlyList<OrderDto>> WithShopNamesAsync(AppDbContext db, IReadOnlyList<Order> orders, CancellationToken ct)
    {
        var shopIds = orders.Select(o => o.ShopId).Distinct().ToList();
        var names = await db.Shops.AsNoTracking().Where(s => shopIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return orders.Select(o => OrderDto.From(o, names.GetValueOrDefault(o.ShopId, ""))).ToList();
    }

    private static async Task<ShoppingList> EnsureListAsync(AppDbContext db, Guid userId, ListKind kind, CancellationToken ct)
    {
        var list = await db.ShoppingLists.SingleOrDefaultAsync(l => l.UserId == userId && l.Kind == kind, ct);
        if (list is null)
        {
            list = new ShoppingList { UserId = userId, Kind = kind };
            db.ShoppingLists.Add(list);
        }

        return list;
    }

    private static bool TryParseKind(string value, out ListKind kind) =>
        Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);
}
