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

    /// <summary>The sizes it comes in ("500 g", "1 kg"): a shop that picks it gets one item per size, sold by the packet. Empty: one loose item.</summary>
    public List<string> PackSizes { get; set; } = [];

    /// <summary>When the platform team last set its photo (<see cref="MasterItemImage"/>); null: none yet.</summary>
    public DateTimeOffset? ImageAt { get; set; }

    /// <summary>The photo's credit line, when its source needs one; shops otherwise show "Representative image".</summary>
    public string? ImageCredit { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When an admin added it; null for the list the app started with. Shops see it as new for a while after.</summary>
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>
/// An item a shop asks to have on its trade's master list. Admins approve it (it becomes a master item) or turn it
/// down. Read across shops by admins, so it carries the shop's name and has no row-level security.
/// </summary>
public sealed class MasterSuggestion
{
    public int Id { get; set; }

    public Guid ShopId { get; set; }

    public required string ShopName { get; set; }

    public required string Trade { get; set; }

    public required string Category { get; set; }

    public required string Name { get; set; }

    public Unit Unit { get; set; }

    public string? Note { get; set; }

    public SuggestionStatus Status { get; set; }

    /// <summary>The master item it became, once approved.</summary>
    public int? MasterItemId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }
}

public enum SuggestionStatus
{
    Pending,
    Approved,
    Rejected,
}

public sealed record SuggestRequest(string? Trade, string? Category, string? Name, Unit? Unit, string? Note);

public sealed record SuggestionDto(int Id, string ShopName, string Trade, string Category, string Name, Unit Unit, string? Note, SuggestionStatus Status, int? MasterItemId, DateTimeOffset CreatedAt);

/// <summary>The master list's GST rate or HSN for an item the shop has differs from the shop's own.</summary>
public sealed record MasterChangeDto(Guid ProductId, string ProductName, decimal? GstFrom, decimal? GstTo, string? HsnFrom, string? HsnTo);

/// <summary>What the master list offers a shop: corrections to its items, and items added to its trades lately that it doesn't have.</summary>
public sealed record MasterUpdatesDto(IReadOnlyList<MasterChangeDto> Changes, IReadOnlyList<MasterItemDto> NewItems);

public sealed record ApplyChangesRequest(IReadOnlyList<Guid>? ProductIds);

public sealed record MasterItemDto(int Id, string Trade, string Category, string Name, string? NameHi, Unit Unit, decimal? GstRate, string? HsnCode, bool IsActive, IReadOnlyList<string> PackSizes,
    DateTimeOffset? ImageAt = null, string? ImageCredit = null);

public sealed record MasterItemRequest(string? Trade, string? Category, string? Name, string? NameHi, Unit? Unit, decimal? GstRate, string? HsnCode, bool? IsActive = null, IReadOnlyList<string>? PackSizes = null);

public sealed record PickRequest(IReadOnlyList<int>? Ids);

/// <summary>One line per item: name, Hindi name, category, unit, GST %, HSN, pack sizes split by "|". Existing names in the trade are updated.</summary>
public sealed record MasterImportRequest(string? Trade, string? Text);

public sealed record MasterImportResult(int Added, int Updated, IReadOnlyList<string> Problems);

public static class MasterItems
{
    public static IEndpointRouteBuilder MapMasterItems(this IEndpointRouteBuilder app)
    {
        // Shops: the lists of their own trades, and picking from them.
        app.MapGet("/api/v1/master-items", ShopListAsync).WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/products/from-master", PickAsync).WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);
        app.MapGet("/api/v1/master-items/updates", UpdatesAsync).WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/master-items/updates/apply", ApplyChangesAsync).WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);
        app.MapPost("/api/v1/master-items/suggest", SuggestAsync).WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);

        // Platform admins look after the lists.
        var admin = app.MapGroup("/api/v1/platform/master-items").WithTags("Platform").RequireAuthorization(AppPolicies.PlatformAdmin).RecordsActivity();
        admin.MapGet("", AdminListAsync);
        admin.MapPost("", AddAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        admin.MapPut("/{id:int}", ChangeAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        admin.MapPost("/import", ImportAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        admin.MapGet("/suggestions", SuggestionsAsync);
        admin.MapPost("/suggestions/{id:int}/approve", ApproveAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        admin.MapPost("/suggestions/{id:int}/reject", RejectAsync).RequireAuthorization(AppPolicies.PlatformOpenShop);
        return app;
    }

    /// <summary>
    /// Fills the master list from the lists the app was built with: the grocery common list and each trade's starter
    /// items. Runs at start-up and fills only trades with no rows yet, so a trade added later gets its list while
    /// trades admins already look after are left alone.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var filled = (await db.MasterItems.Select(i => i.Trade).Distinct().ToListAsync(ct)).ToHashSet();
        var now = DateTimeOffset.UtcNow;
        if (!filled.Contains("kirana"))
        {
            db.MasterItems.AddRange(CommonCatalogue.Items.Select(i => new MasterItem
            {
                Trade = "kirana", Category = i.Category, Name = i.NameEn, NameHi = i.Name, Unit = i.Unit, CatalogueItemId = i.Id, UpdatedAt = now,
            }));
        }

        foreach (var type in Enum.GetValues<ShopType>().Where(t => t != ShopType.Kirana && !filled.Contains(ShopTypes.Name(t))))
        {
            db.MasterItems.AddRange(StarterItems.For(type).Select(r => new MasterItem
            {
                Trade = ShopTypes.Name(type), Category = r.Category ?? "Other", Name = r.Name!, Unit = r.Unit ?? Unit.Piece, GstRate = r.GstRate, HsnCode = r.HsnCode, UpdatedAt = now,
            }));
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Adds a trade's long list (a CSV built into the app: name, Hindi name, category, unit, GST, HSN, pack sizes) to the master list.
    /// Only names not already there are added, so it is safe at every start-up and never undoes an admin's edits or
    /// removals. GST is left empty where the research couldn't confirm it: the shop or a CA sets it.
    /// </summary>
    public static async Task SeedListsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assembly = typeof(MasterItems).Assembly;
        foreach (var (resource, trade) in new[] { ("medical-master.csv", ShopTypes.Name(ShopType.Medical)), ("kirana-master.csv", ShopTypes.Name(ShopType.Kirana)),
            ("paint-master.csv", ShopTypes.Name(ShopType.Paint)), ("hardware-master.csv", ShopTypes.Name(ShopType.Hardware)),
            ("beauty-master.csv", ShopTypes.Name(ShopType.Beauty)) })
        {
            var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resource, StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                continue;
            }

            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var existing = (await db.MasterItems.Where(i => i.Trade == trade).ToListAsync(ct)).GroupBy(i => i.Name.ToLower()).ToDictionary(g => g.Key, g => g.First());
            var have = existing.Keys.ToHashSet();
            var listed = new Dictionary<string, string>();
            var now = DateTimeOffset.UtcNow;
            var first = true;
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                if (first)
                {
                    first = false; // header
                    continue;
                }

                var f = SplitCsv(line);
                if (f.Length < 6 || f[0].Trim() is not { Length: >= 2 and <= 120 } item)
                {
                    continue;
                }

                listed[item.ToLower()] = f[2];
                if (!have.Add(item.ToLower()))
                {
                    continue;
                }

                db.MasterItems.Add(new MasterItem
                {
                    Trade = trade, Name = item, NameHi = f[1].Length > 0 ? f[1] : null, Category = f[2].Length > 0 ? f[2][..Math.Min(60, f[2].Length)] : "Other",
                    Unit = Enum.TryParse<Unit>(f[3], ignoreCase: true, out var unit) ? unit : Unit.Piece,
                    GstRate = decimal.TryParse(f[4], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var gst) ? gst : null,
                    HsnCode = f[5].Length > 0 ? f[5] : null, UpdatedAt = now,
                    // An optional seventh column: the sizes it comes in ("100 g|500 g"), one shop item per size when picked.
                    PackSizes = f.Length > 6 ? Sizes(f[6].Split('|')) : [],
                });
            }

            // The list's categories were once finer (143 for medical, now about 24 groups). A row still in an old
            // category moves to its group; one an admin has put in a current group is left as it is.
            var groups = listed.Values.ToHashSet();
            foreach (var (key, row) in existing.Where(e => !groups.Contains(e.Value.Category)))
            {
                var group = listed.GetValueOrDefault(key) ?? StarterGroups.GetValueOrDefault(row.Category);
                if (group is not null)
                {
                    (row.Category, row.UpdatedAt) = (group, now);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Starter items' old categories (medical, hardware, paint), and the groups they now belong to.</summary>
    private static readonly Dictionary<string, string> StarterGroups = new()
    {
        // Hardware and paint starter items, folded into the long lists' groups.
        ["Hinges"] = "Door and furniture fittings", ["Fittings"] = "Door and furniture fittings", ["Tools"] = "Hand tools", ["Plumbing"] = "Pipes and fittings",
        ["Screws & nails"] = "Fasteners", ["Bolts"] = "Fasteners", ["Adhesives"] = "Adhesives and tapes",
        ["Interior"] = "Interior emulsion", ["Exterior"] = "Exterior emulsion", ["Enamel"] = "Enamel and metal paint", ["Primer & putty"] = "Primer",
        ["Antibiotics"] = "Antibiotics & infections", ["Antibiotics (Schedule H)"] = "Antibiotics & infections", ["Baby care"] = "Baby & mother care",
        ["Cold & allergy"] = "Cold, cough & allergy", ["Devices"] = "Diagnostic devices", ["First aid"] = "First aid & wound care",
        ["Stomach"] = "Stomach & digestion", ["Vitamins"] = "Vitamins & supplements", ["Diabetes & BP"] = "Heart & BP",
    };

    private static MasterItemDto ToDto(MasterItem i) => new(i.Id, i.Trade, i.Category, i.Name, i.NameHi, i.Unit, i.GstRate, i.HsnCode, i.IsActive, i.PackSizes, i.ImageAt, i.ImageCredit);

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

        return await AddToShopAsync(ids, user.UserId()!.Value, db, tenant, stock, ct);
    }

    /// <summary>Gives a shop just opened by an admin every item on its trade's list, as a working start (the owner removes what it doesn't sell).</summary>
    public static async Task AddTradeListAsync(IServiceScopeFactory scopes, Guid shopId, Guid userId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetShop(shopId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trade = await db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => s.Type).SingleAsync(ct);
        var name = ShopTypes.Name(trade);
        var ids = await db.MasterItems.AsNoTracking().Where(i => i.IsActive && i.Trade == name).Select(i => i.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await AddToShopAsync(ids, userId, db, scope.ServiceProvider.GetRequiredService<ITenantContext>(), scope.ServiceProvider.GetRequiredService<StockService>(), ct);
        }
    }

    private static async Task<IResult> AddToShopAsync(List<int> ids, Guid userId, AppDbContext db, ITenantContext tenant, StockService stock, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.Type, s.ExtraTypes }).SingleAsync(ct);
        var trades = shop.ExtraTypes.Append(ShopTypes.Name(shop.Type)).ToList();
        var items = await db.MasterItems.AsNoTracking().Where(i => ids.Contains(i.Id) && i.IsActive && trades.Contains(i.Trade)).ToListAsync(ct);
        if (items.Count == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Ids"] = ["These items aren't on your trades' lists."] });
        }

        var rows = items.SelectMany(i => i.PackSizes.Count == 0
            ? [new ImportRow(i.NameHi ?? i.Name, i.Unit, 0, null, i.Category, null, null, i.CatalogueItemId, GstRate: i.GstRate, HsnCode: i.HsnCode, MasterItemId: i.Id)]
            : i.PackSizes.Select(size => new ImportRow($"{i.NameHi ?? i.Name} {size}", PackUnit(i.Unit), 0, null, i.Category, null, null, i.CatalogueItemId, GstRate: i.GstRate, HsnCode: i.HsnCode, MasterItemId: i.Id)))
            .ToList();
        var (result, errors) = await stock.ImportAsync(new ImportRequest(rows), userId, ct);
        return errors is null ? TypedResults.Ok(result) : TypedResults.ValidationProblem(errors);
    }

    /// <summary>Adds or updates many items of one trade from pasted CSV lines. Bad lines are reported and skipped.</summary>
    private static async Task<IResult> ImportAsync(MasterImportRequest request, AppDbContext db, CancellationToken ct)
    {
        var lines = (request.Text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count is 0 or > 5000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Text"] = ["Paste 1 to 5,000 lines."] });
        }

        var (added, updated, problems) = (0, 0, new List<string>());
        var inPaste = new Dictionary<string, MasterItem>();
        foreach (var (line, index) in lines.Select((l, i) => (l, i + 1)))
        {
            var f = SplitCsv(line);
            if (index == 1 && f[0].Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                continue; // a header row
            }

            string? At(int i) => f.Length > i && f[i].Length > 0 ? f[i] : null;
            var row = new MasterItemRequest(request.Trade, At(2), At(0), At(1),
                At(3) is { } u && u.All(char.IsAsciiDigit) ? null : Enum.TryParse<Unit>(At(3) ?? "piece", ignoreCase: true, out var unit) ? unit : null,
                decimal.TryParse(At(4)?.TrimEnd('%'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var gst) ? gst : null, At(5), null, At(6)?.Split('|'));
            if (Check(row) is { Count: > 0 } errors)
            {
                problems.Add($"Line {index}: {string.Join(" ", errors.Values.SelectMany(v => v))}");
                continue;
            }

            var key = row.Name!.Trim().ToLower();
            var trade = ShopTypes.Name(Enum.Parse<ShopType>(row.Trade!.Replace("_", ""), ignoreCase: true));
            var item = inPaste.GetValueOrDefault($"{trade}|{key}") ?? await db.MasterItems.FirstOrDefaultAsync(i => i.Trade == trade && i.Name.ToLower() == key, ct);
            if (item is null)
            {
                item = new MasterItem { Trade = "", Category = "", Name = "", CreatedAt = DateTimeOffset.UtcNow };
                inPaste[$"{trade}|{key}"] = item;
                db.MasterItems.Add(item);
                added++;
            }
            else
            {
                updated++;
            }

            Apply(item, row);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new MasterImportResult(added, updated, problems.Take(50).ToList()));
    }

    /// <summary>A packed size of something sold by weight or volume is sold by the packet; a sized bag or piece keeps its unit.</summary>
    private static Unit PackUnit(Unit unit) => unit is Unit.Kg or Unit.G or Unit.Litre or Unit.Ml ? Unit.Packet : unit;

    private static List<string> Sizes(IReadOnlyList<string>? sizes) =>
        (sizes ?? []).Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>How long an item an admin adds to a trade's list shows as new to its shops (edits don't count).</summary>
    private static readonly TimeSpan NewFor = TimeSpan.FromDays(30);

    /// <summary>
    /// The shop's items matched to its trades' master items by the name picking gives them ("तूर दाल", "Bhujia 1 kg"),
    /// paired where the master's GST rate or HSN (when set) differs. Items the shop renamed or made itself never match.
    /// </summary>
    private static async Task<(List<(Product Product, MasterItem Master)> Changed, List<MasterItem> Fresh)> CompareAsync(AppDbContext db, ITenantContext tenant, bool track, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.Type, s.ExtraTypes }).SingleAsync(ct);
        var trades = shop.ExtraTypes.Append(ShopTypes.Name(shop.Type)).ToList();
        var masters = await db.MasterItems.AsNoTracking().Where(i => i.IsActive && trades.Contains(i.Trade)).ToListAsync(ct);
        var products = await (track ? db.Products : db.Products.AsNoTracking()).Where(p => p.IsActive).ToListAsync(ct);

        var byName = new Dictionary<string, MasterItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in masters)
        {
            var name = m.NameHi ?? m.Name;
            foreach (var n in m.PackSizes.Count == 0 ? [name, m.Name] : m.PackSizes.SelectMany(size => new[] { $"{name} {size}", $"{m.Name} {size}" }))
            {
                byName.TryAdd(n, m);
            }
        }

        var changed = products
            .Where(p => byName.ContainsKey(p.Name.Trim()))
            .Select(p => (Product: p, Master: byName[p.Name.Trim()]))
            .Where(x => (x.Master.GstRate is { } g && x.Product.GstRate != g) || (x.Master.HsnCode is { } h && x.Product.HsnCode != h))
            .ToList();
        var held = products.Select(p => p.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var since = DateTimeOffset.UtcNow - NewFor;
        var fresh = masters
            .Where(m => m.CreatedAt >= since && !byName.Where(kv => kv.Value == m).Any(kv => held.Contains(kv.Key)))
            .OrderBy(m => m.Category).ThenBy(m => m.Name).Take(200).ToList();
        return (changed, fresh);
    }

    private static async Task<MasterUpdatesDto> UpdatesAsync(AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var (changed, fresh) = await CompareAsync(db, tenant, track: false, ct);
        return new MasterUpdatesDto(
            changed.Select(x => new MasterChangeDto(x.Product.Id, x.Product.Name, x.Product.GstRate, x.Master.GstRate ?? x.Product.GstRate, x.Product.HsnCode, x.Master.HsnCode ?? x.Product.HsnCode)).ToList(),
            fresh.Select(ToDto).ToList());
    }

    /// <summary>Takes the master's GST rate and HSN for the chosen items. Bills already made keep the rate they were made with.</summary>
    private static async Task<IResult> ApplyChangesAsync(ApplyChangesRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var ids = (request.ProductIds ?? []).ToHashSet();
        var (changed, _) = await CompareAsync(db, tenant, track: true, ct);
        var applied = 0;
        foreach (var (product, master) in changed.Where(x => ids.Contains(x.Product.Id)))
        {
            (product.GstRate, product.HsnCode) = (master.GstRate ?? product.GstRate, master.HsnCode ?? product.HsnCode);
            applied++;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new { applied });
    }

    private static SuggestionDto ToDto(MasterSuggestion s) => new(s.Id, s.ShopName, s.Trade, s.Category, s.Name, s.Unit, s.Note, s.Status, s.MasterItemId, s.CreatedAt);

    /// <summary>A shop asks for an item missing from one of its trades' lists. Its own copy is made the usual way; this only reaches the admins.</summary>
    private static async Task<IResult> SuggestAsync(SuggestRequest request, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.Name, s.Type, s.ExtraTypes }).SingleAsync(ct);
        var trades = shop.ExtraTypes.Append(ShopTypes.Name(shop.Type)).ToList();
        var trade = request.Trade ?? ShopTypes.Name(shop.Type);
        var errors = Check(new MasterItemRequest(trade, request.Category, request.Name, null, request.Unit, null, null));
        if (!trades.Contains(trade)) errors["Trade"] = ["Suggest for one of your shop's trades."];
        if (request.Note?.Length > 200) errors["Note"] = ["Keep the note under 200 letters."];
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var name = request.Name!.Trim();
        if (await db.MasterSuggestions.AnyAsync(s => s.ShopId == tenant.ShopId && s.Trade == trade && s.Status == SuggestionStatus.Pending && s.Name.ToLower() == name.ToLower(), ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "You've already suggested this item.");
        }

        var suggestion = new MasterSuggestion
        {
            ShopId = tenant.ShopId, ShopName = shop.Name, Trade = trade, Category = request.Category!.Trim(), Name = name, Unit = request.Unit!.Value,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), CreatedAt = DateTimeOffset.UtcNow,
        };
        db.MasterSuggestions.Add(suggestion);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(suggestion));
    }

    private static async Task<IReadOnlyList<SuggestionDto>> SuggestionsAsync(SuggestionStatus? status, AppDbContext db, CancellationToken ct) =>
        await db.MasterSuggestions.AsNoTracking().Where(s => s.Status == (status ?? SuggestionStatus.Pending))
            .OrderBy(s => s.CreatedAt).Take(500).Select(s => ToDto(s)).ToListAsync(ct);

    /// <summary>Puts the suggested item on the master list, with the admin's corrections (name, Hindi name, GST, HSN, sizes).</summary>
    private static async Task<IResult> ApproveAsync(int id, MasterItemRequest request, AppDbContext db, CancellationToken ct)
    {
        var suggestion = await db.MasterSuggestions.SingleOrDefaultAsync(s => s.Id == id && s.Status == SuggestionStatus.Pending, ct);
        if (suggestion is null)
        {
            return TypedResults.NotFound();
        }

        var added = await AddAsync(request with { Trade = suggestion.Trade }, db, ct);
        if (added is not Microsoft.AspNetCore.Http.HttpResults.Ok<MasterItemDto> ok)
        {
            return added;
        }

        (suggestion.Status, suggestion.MasterItemId, suggestion.DecidedAt) = (SuggestionStatus.Approved, ok.Value!.Id, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return added;
    }

    private static async Task<IResult> RejectAsync(int id, AppDbContext db, CancellationToken ct)
    {
        var suggestion = await db.MasterSuggestions.SingleOrDefaultAsync(s => s.Id == id && s.Status == SuggestionStatus.Pending, ct);
        if (suggestion is null)
        {
            return TypedResults.NotFound();
        }

        (suggestion.Status, suggestion.DecidedAt) = (SuggestionStatus.Rejected, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>One CSV line's fields: commas inside double quotes stay in the field, and "" is a quote.</summary>
    private static string[] SplitCsv(string line)
    {
        var (fields, field, quoted) = (new List<string>(), new System.Text.StringBuilder(), false);
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"')
            {
                field.Append('"');
                i++;
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString().Trim());
        return [.. fields];
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
        if (Sizes(r.PackSizes) is { } sizes && (sizes.Count > 10 || sizes.Any(s => s.Length > 20))) errors["PackSizes"] = ["Up to 10 pack sizes, each up to 20 letters."];
        return errors;
    }

    private static void Apply(MasterItem i, MasterItemRequest r)
    {
        i.Trade = ShopTypes.Name(Enum.Parse<ShopType>(r.Trade!.Replace("_", ""), ignoreCase: true));
        (i.Category, i.Name, i.NameHi, i.Unit) = (r.Category!.Trim(), r.Name!.Trim(), string.IsNullOrWhiteSpace(r.NameHi) ? null : r.NameHi.Trim(), r.Unit!.Value);
        (i.GstRate, i.HsnCode, i.IsActive, i.UpdatedAt) = (r.GstRate, string.IsNullOrWhiteSpace(r.HsnCode) ? null : r.HsnCode.Trim(), r.IsActive ?? i.IsActive, DateTimeOffset.UtcNow);
        i.PackSizes = Sizes(r.PackSizes);
    }

    private static async Task<IResult> AddAsync(MasterItemRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Check(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var item = new MasterItem { Trade = "", Category = "", Name = "", CreatedAt = DateTimeOffset.UtcNow };
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
