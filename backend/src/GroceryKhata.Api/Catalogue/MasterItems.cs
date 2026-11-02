using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using GroceryKhata.Api.Shops;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One item on the platform's master list for a trade ("kirana", "hardware"): what a shop of that trade picks its items
/// from. Shared by every shop, owned by none (no row-level security). An item sold in two trades has a row in each.
/// </summary>
public sealed class MasterItem
{
    public int Id { get; set; }

    /// <summary>The trade as the app writes it ("building_materials").</summary>
    public required string Trade { get; set; }

    public required string Category { get; set; }

    /// <summary>The name shops see; <see cref="NameHi"/> is the Hindi one when there is one.</summary>
    public required string Name { get; set; }

    public string? NameHi { get; set; }

    public Unit Unit { get; set; }

    /// <summary>Null when not checked: the shop sets it. A CA should confirm rates before they are filled in.</summary>
    public decimal? GstRate { get; set; }

    public string? HsnCode { get; set; }

    /// <summary>The grocery common-list item it stands for, so households' lists still find it.</summary>
    public int? CatalogueItemId { get; set; }

    /// <summary>False: no longer offered to new picks. Shops that took it keep their copy. Never deleted.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record MasterItemDto(int Id, string Trade, string Category, string Name, string? NameHi, Unit Unit, decimal? GstRate, string? HsnCode, bool IsActive);

public sealed record MasterItemRequest(string? Trade, string? Category, string? Name, string? NameHi, Unit? Unit, decimal? GstRate, string? HsnCode, bool? IsActive = null);

public sealed record PickRequest(IReadOnlyList<int>? Ids);

public static class MasterItems
{
    public static IEndpointRouteBuilder MapMasterItems(this IEndpointRouteBuilder app)
    {
        // Shops: the lists of their own trades, and picking from them.
        app.MapGet("/api/v1/master-items", ShopListAsync).WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/products/from-master", PickAsync).WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);

        // Platform admins look after the lists.
        var admin = app.MapGroup("/api/v1/platform/master-items").WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin).RecordsActivity();
        admin.MapGet("", AdminListAsync);
        admin.MapPost("", AddAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        admin.MapPut("/{id:int}", ChangeAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        return app;
    }

    /// <summary>
    /// Fills an empty master list from the lists the app was built with: the grocery common list and each trade's
    /// starter items. Runs at start-up; does nothing once the list has rows (admins own it from then on).
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.MasterItems.AnyAsync(ct))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        db.MasterItems.AddRange(CommonCatalogue.Items.Select(i => new MasterItem
        {
            Trade = "kirana", Category = i.Category, Name = i.NameEn, NameHi = i.Name, Unit = i.Unit, CatalogueItemId = i.Id, UpdatedAt = now,
        }));
        foreach (var type in Enum.GetValues<ShopType>().Where(t => t != ShopType.Kirana))
        {
            db.MasterItems.AddRange(StarterItems.For(type).Select(r => new MasterItem
            {
                Trade = ShopTypes.Name(type), Category = r.Category ?? "Other", Name = r.Name!, Unit = r.Unit ?? Unit.Piece, GstRate = r.GstRate, HsnCode = r.HsnCode, UpdatedAt = now,
            }));
        }

        await db.SaveChangesAsync(ct);
    }

    private static MasterItemDto ToDto(MasterItem i) => new(i.Id, i.Trade, i.Category, i.Name, i.NameHi, i.Unit, i.GstRate, i.HsnCode, i.IsActive);

    /// <summary>A trade's active items, or every trade the shop works in.</summary>
    private static async Task<IReadOnlyList<MasterItemDto>> ShopListAsync(string? trade, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.Type, s.ExtraTypes }).SingleAsync(ct);
        var trades = shop.ExtraTypes.Append(ShopTypes.Name(shop.Type)).ToList();
        if (trade is not null)
        {
            trades = trades.Where(t => t == trade).ToList();
        }

        return await db.MasterItems.AsNoTracking().Where(i => i.IsActive && trades.Contains(i.Trade))
            .OrderBy(i => i.Trade).ThenBy(i => i.Category).ThenBy(i => i.Name).Select(i => ToDto(i)).ToListAsync(ct);
    }

    /// <summary>Adds the picked items to the shop, with no price yet (set on the rates screen or the first bill). Items it has are skipped.</summary>
    private static async Task<IResult> PickAsync(PickRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, StockService stock, CancellationToken ct)
    {
        var ids = (request.Ids ?? []).Distinct().ToList();
        if (ids.Count is 0 or > 500)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Ids"] = ["Choose up to 500 items."] });
        }

        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.Type, s.ExtraTypes }).SingleAsync(ct);
        var trades = shop.ExtraTypes.Append(ShopTypes.Name(shop.Type)).ToList();
        var items = await db.MasterItems.AsNoTracking().Where(i => ids.Contains(i.Id) && i.IsActive && trades.Contains(i.Trade)).ToListAsync(ct);
        if (items.Count == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Ids"] = ["These items aren't on your trades' lists."] });
        }

        var rows = items.Select(i => new ImportRow(i.NameHi ?? i.Name, i.Unit, 0, null, i.Category, null, null, i.CatalogueItemId, GstRate: i.GstRate, HsnCode: i.HsnCode)).ToList();
        var (result, errors) = await stock.ImportAsync(new ImportRequest(rows), user.UserId()!.Value, ct);
        return errors is null ? TypedResults.Ok(result) : TypedResults.ValidationProblem(errors);
    }

    private static async Task<IReadOnlyList<MasterItemDto>> AdminListAsync(string? trade, string? q, AppDbContext db, CancellationToken ct)
    {
        var query = db.MasterItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(trade))
        {
            query = query.Where(i => i.Trade == trade);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(i => EF.Functions.ILike(i.Name, pattern) || (i.NameHi != null && EF.Functions.ILike(i.NameHi, pattern)) || EF.Functions.ILike(i.Category, pattern));
        }

        return await query.OrderBy(i => i.Trade).ThenBy(i => i.Category).ThenBy(i => i.Name).Take(1000).Select(i => ToDto(i)).ToListAsync(ct);
    }

    private static Dictionary<string, string[]> Check(MasterItemRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        if (r.Trade is null || !Enum.TryParse<ShopType>(r.Trade.Replace("_", ""), ignoreCase: true, out _)) errors["Trade"] = ["Choose the trade."];
        if (r.Name?.Trim() is not { Length: >= 2 and <= 120 }) errors["Name"] = ["Enter the item's name."];
        if (r.Category?.Trim() is not { Length: >= 2 and <= 60 }) errors["Category"] = ["Enter its category."];
        if (r.Unit is null) errors["Unit"] = ["Choose the unit."];
        if (r.GstRate is { } rate && rate is not (0m or 0.25m or 3m or 5m or 12m or 18m or 28m or 40m)) errors["GstRate"] = ["Choose a GST rate: 0, 5, 18 or 40%."];
        if (!string.IsNullOrWhiteSpace(r.HsnCode) && (r.HsnCode.Trim().Length is < 4 or > 8 || !r.HsnCode.Trim().All(char.IsAsciiDigit))) errors["HsnCode"] = ["An HSN or SAC code is 4 to 8 digits."];
        return errors;
    }

    private static void Apply(MasterItem i, MasterItemRequest r)
    {
        i.Trade = ShopTypes.Name(Enum.Parse<ShopType>(r.Trade!.Replace("_", ""), ignoreCase: true));
        (i.Category, i.Name, i.NameHi, i.Unit) = (r.Category!.Trim(), r.Name!.Trim(), string.IsNullOrWhiteSpace(r.NameHi) ? null : r.NameHi.Trim(), r.Unit!.Value);
        (i.GstRate, i.HsnCode, i.IsActive, i.UpdatedAt) = (r.GstRate, string.IsNullOrWhiteSpace(r.HsnCode) ? null : r.HsnCode.Trim(), r.IsActive ?? i.IsActive, DateTimeOffset.UtcNow);
    }

    private static async Task<IResult> AddAsync(MasterItemRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Check(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var item = new MasterItem { Trade = "", Category = "", Name = "" };
        Apply(item, request);
        if (await db.MasterItems.AnyAsync(i => i.Trade == item.Trade && i.Name.ToLower() == item.Name.ToLower(), ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This trade already has an item with that name.");
        }

        db.MasterItems.Add(item);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(item));
    }

    private static async Task<IResult> ChangeAsync(int id, MasterItemRequest request, AppDbContext db, CancellationToken ct)
    {
        var item = await db.MasterItems.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        if (Check(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        Apply(item, request);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(item));
    }
}
