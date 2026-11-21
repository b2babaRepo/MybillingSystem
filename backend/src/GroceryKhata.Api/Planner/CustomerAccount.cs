using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// A signed-in customer's cart at one shop, kept on the server so it is the same on every phone and survives a
/// reinstall. Lines are item ids and quantities only: names, prices and stock are read live from the shop.
/// </summary>
public sealed class CustomerCart : Entity
{
    public Guid UserId { get; set; }

    public Guid ShopId { get; set; }

    /// <summary>JSON: [{ productId, quantity }].</summary>
    public string Lines { get; set; } = "[]";
}

/// <summary>A shop the customer keeps on their list, to order from again without searching.</summary>
public sealed class SavedShop : Entity
{
    public Guid UserId { get; set; }

    public Guid ShopId { get; set; }
}

public sealed record CartLine(Guid ProductId, decimal Quantity);

public sealed record CartRequest(IReadOnlyList<CartLine>? Lines);

/// <summary>A cart line as it stands now at the shop: missing items (removed by the shop) are left out.</summary>
public sealed record CartItemDto(Guid ProductId, string Name, Catalogue.Unit Unit, decimal Price, decimal Quantity, decimal Available, DateTimeOffset? ImageAt);

public sealed record CartDto(Guid ShopId, string ShopName, string ShopSlug, IReadOnlyList<CartItemDto> Items, decimal Total, DateTimeOffset UpdatedAt);

public sealed record SavedShopDto(Guid Id, string Name, string Slug, string? Area, string? Address, Shops.ShopType Type, long Number, DateTimeOffset SavedAt);

public static class CustomerAccount
{
    private const int MaxLines = StoreEndpoints.MaxLines;
    private const int MaxSaved = 50;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapCustomerAccount(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/v1/me").WithTags("Customer").RequireAuthorization();
        me.MapGet("/carts", CartsAsync);
        me.MapPut("/carts/{shopId:guid}", PutCartAsync);
        me.MapGet("/shops", SavedAsync);
        me.MapPut("/shops/{shopId:guid}", SaveAsync);
        me.MapDelete("/shops/{shopId:guid}", UnsaveAsync);
        me.MapPost("/orders/{id:guid}/cancel", CancelAsync);
        return app;
    }

    private static List<CartLine> LinesOf(CustomerCart c) => JsonSerializer.Deserialize<List<CartLine>>(c.Lines, Json) ?? [];

    /// <summary>Every shop's cart, newest first, each priced from the shop's items as they are now.</summary>
    private static async Task<IReadOnlyList<CartDto>> CartsAsync(ClaimsPrincipal user, AppDbContext db, TenantContext tenant, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var carts = await db.CustomerCarts.AsNoTracking().Where(c => c.UserId == userId).OrderByDescending(c => c.UpdatedAt).ToListAsync(ct);
        var shopIds = carts.Select(c => c.ShopId).ToList();
        var shops = await db.Shops.AsNoTracking().Where(s => shopIds.Contains(s.Id) && s.IsActive).ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Slug }, ct);
        var result = new List<CartDto>();
        foreach (var cart in carts.Where(c => shops.ContainsKey(c.ShopId)))
        {
            var lines = LinesOf(cart);
            var ids = lines.Select(l => l.ProductId).ToList();
            // Read as that shop: its items are behind row security.
            tenant.SetShop(cart.ShopId);
            var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive && p.SellingPrice > 0)
                .Select(p => new { p.Id, p.Name, p.Unit, p.SellingPrice, Available = p.Uncounted ? 9999 : p.Stock - p.Reserved, p.ImageAt }).ToDictionaryAsync(p => p.Id, ct);
            var items = lines.Where(l => products.ContainsKey(l.ProductId)).Select(l =>
            {
                var p = products[l.ProductId];
                return new CartItemDto(p.Id, p.Name, p.Unit, p.SellingPrice, l.Quantity, p.Available, p.ImageAt);
            }).ToList();
            if (items.Count > 0)
            {
                var shop = shops[cart.ShopId];
                result.Add(new CartDto(cart.ShopId, shop.Name, shop.Slug, items, items.Sum(i => Money.LineAmount(i.Quantity, i.Price)), cart.UpdatedAt));
            }
        }

        return result;
    }

    /// <summary>Replaces the cart at one shop (the app sends the whole cart). An empty cart is removed.</summary>
    private static async Task<IResult> PutCartAsync(Guid shopId, CartRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var lines = (request.Lines ?? []).Where(l => l.Quantity > 0).ToList();
        if (lines.Count > MaxLines || lines.Select(l => l.ProductId).Distinct().Count() != lines.Count || lines.Any(l => !Money.IsValidQuantity(l.Quantity)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = [$"Up to {MaxLines} different items, each once, with a quantity."] });
        }

        if (!await db.Shops.AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        var userId = user.UserId()!.Value;
        var cart = await db.CustomerCarts.SingleOrDefaultAsync(c => c.UserId == userId && c.ShopId == shopId, ct);
        if (lines.Count == 0)
        {
            if (cart is not null)
            {
                db.CustomerCarts.Remove(cart);
            }
        }
        else
        {
            if (cart is null)
            {
                db.CustomerCarts.Add(cart = new CustomerCart { UserId = userId, ShopId = shopId });
            }

            cart.Lines = JsonSerializer.Serialize(lines, Json);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IReadOnlyList<SavedShopDto>> SavedAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        return await (from x in db.SavedShops.AsNoTracking()
                      join s in db.Shops.AsNoTracking() on x.ShopId equals s.Id
                      where x.UserId == userId && s.IsActive
                      orderby x.CreatedAt descending
                      select new SavedShopDto(s.Id, s.Name, s.Slug, s.Area, s.Address, s.Type, s.Number, x.CreatedAt)).ToListAsync(ct);
    }

    private static async Task<IResult> SaveAsync(Guid shopId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        if (!await db.Shops.AnyAsync(s => s.Id == shopId && s.IsActive, ct))
        {
            return TypedResults.NotFound();
        }

        if (await db.SavedShops.AnyAsync(x => x.UserId == userId && x.ShopId == shopId, ct))
        {
            return TypedResults.NoContent();
        }

        if (await db.SavedShops.CountAsync(x => x.UserId == userId, ct) >= MaxSaved)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ShopId"] = [$"Keep up to {MaxSaved} shops."] });
        }

        db.SavedShops.Add(new SavedShop { UserId = userId, ShopId = shopId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Saved twice at once: the other one stands.
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> UnsaveAsync(Guid shopId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        await db.SavedShops.Where(x => x.UserId == user.UserId() && x.ShopId == shopId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// The customer cancels their own order while the shop hasn't packed it yet. Stock it was holding goes back. Once
    /// packed, the customer asks the shop instead (it may already be on its way).
    /// </summary>
    private static async Task<IResult> CancelAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var shopId = await db.Orders.AsNoTracking().Where(o => o.Id == id && o.PlacedByUserId == userId).Select(o => (Guid?)o.ShopId).SingleOrDefaultAsync(ct);
        if (shopId is null)
        {
            return TypedResults.NotFound();
        }

        // Changing the order (and the stock it held) is done as that shop.
        tenant.SetShop(shopId.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == id, ct);
        if (order.Status == OrderStatus.Cancelled)
        {
            return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
        }

        if (order.Status != OrderStatus.Placed)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "The shop has already packed this order. Call the shop to change or cancel it.");
        }

        (order.Status, order.CancelledAt) = (OrderStatus.Cancelled, clock.GetUtcNow());
        await OrderEndpoints.ReleaseReservationsAsync(db, order, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
    }
}
