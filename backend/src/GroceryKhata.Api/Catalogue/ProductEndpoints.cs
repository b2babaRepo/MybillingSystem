using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary><see cref="Id"/> is made by the app, so a product added offline syncs once.</summary>
public sealed record ProductRequest(
    Guid Id,
    string? Name,
    Unit? Unit,
    decimal SellingPrice,
    decimal? CostPrice,
    string? Category = null,
    decimal? ReorderLevel = null,
    decimal? OpeningStock = null,
    decimal? GstRate = null,
    string? HsnCode = null,
    bool IsService = false,
    int? DurationMinutes = null,
    DrugSchedule? Schedule = null,
    int? UnitsPerPack = null,
    bool TracksSerials = false,
    int? WarrantyMonths = null,
    decimal? Mrp = null,
    string? BillName = null,
    string? StyleName = null,
    string? Size = null,
    string? Colour = null,
    string? Barcode = null,
    decimal? BagSize = null);

/// <summary><see cref="GstRate"/> and <see cref="HsnCode"/>: null keeps what the item has (older apps don't send them), unless <see cref="ReplaceTax"/> says null means clear it.</summary>
public sealed record ProductUpdate(
    string? Name, Unit? Unit, decimal SellingPrice, decimal? CostPrice, string? Category, decimal? ReorderLevel, bool? IsActive,
    decimal? GstRate = null, string? HsnCode = null, bool ReplaceTax = false, bool? IsService = null, int? DurationMinutes = null, DrugSchedule? Schedule = null, bool ReplaceSchedule = false, int? UnitsPerPack = null, bool? TracksSerials = null, int? WarrantyMonths = null, decimal? Mrp = null, string? BillName = null, string? StyleName = null, string? Size = null, string? Colour = null, bool? Uncounted = null,
    // Null keeps it; 0 clears it (no longer kept in bags).
    decimal? BagSize = null);

/// <summary><see cref="IsLow"/>: out of stock, or at/below the reorder level.</summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Category,
    Unit Unit,
    decimal SellingPrice,
    decimal? CostPrice,
    decimal Stock,
    decimal? ReorderLevel,
    bool IsLow,
    decimal Reserved = 0,
    int? CatalogueItemId = null,
    decimal? PackSize = null,
    Unit? PackUnit = null,
    bool IsDefaultOption = false,
    DateTimeOffset? ImageAt = null,
    decimal? GstRate = null,
    string? HsnCode = null,
    bool IsService = false,
    int? DurationMinutes = null,
    DrugSchedule? Schedule = null,
    int? UnitsPerPack = null,
    bool TracksSerials = false,
    int? WarrantyMonths = null,
    decimal? Mrp = null,
    string? Barcode = null,
    string? ImageCredit = null,
    int PhotoReports = 0,
    string? PhotoReportNote = null,
    string? BillName = null,
    string? StyleName = null,
    string? Size = null,
    string? Colour = null,
    bool Uncounted = false,
    DateOnly? OutOn = null,
    // Average sold a day over the last 30 days (null: none sold): stock over this is the days of stock left.
    decimal? DailySales = null,
    // Kept in bags of this size; Bags is how many sealed bags are on hand.
    decimal? BagSize = null,
    decimal Bags = 0,
    // Promised on open sale orders and not yet delivered: Stock − Reserved − Promised is free to sell.
    decimal Promised = 0);

public sealed record OutTodayRequest(bool Out);

/// <summary>Open sealed bags to sell loose: the bag count goes down, the weight doesn't change.</summary>
public sealed record OpenBagRequest(Guid Id, decimal Bags);

/// <summary>A purchase (positive) or an adjustment (signed, with a reason).</summary>
public sealed record StockMovementRequest(Guid Id, Guid ProductId, StockMovementKind? Kind, decimal Quantity, StockReason? Reason, decimal? CostPrice, string? Note, decimal? Bags = null);

public sealed record StockMovementDto(
    Guid Id,
    Guid ProductId,
    StockMovementKind Kind,
    decimal Quantity,
    StockReason? Reason,
    Guid? BillId,
    decimal? CostPrice,
    string? Note,
    DateTimeOffset OccurredAt);

public sealed record StocktakeCount(Guid ProductId, decimal Counted);

public sealed record StocktakeRequest(Guid Id, IReadOnlyList<StocktakeCount>? Counts);

public sealed record StocktakeResult(int Adjusted, int Unchanged);

public sealed record BarcodeRequest(string? Barcode);

/// <summary>One selling price for all sizes of <see cref="StyleName"/>.</summary>
public sealed record StylePriceRequest(string? StyleName, decimal? SellingPrice);

public sealed record ImportRow(
    string? Name, Unit? Unit, decimal? SellingPrice, decimal? CostPrice, string? Category, decimal? OpeningStock, decimal? ReorderLevel,
    int? CatalogueItemId = null, decimal? PackSize = null, Unit? PackUnit = null, decimal? GstRate = null, string? HsnCode = null,
    bool IsService = false, int? DurationMinutes = null, DrugSchedule? Schedule = null, int? UnitsPerPack = null, bool TracksSerials = false, int? WarrantyMonths = null,
    int? MasterItemId = null, string? StyleName = null, string? Size = null);

/// <summary>One item's new rate on the "today's rates" screen.</summary>
public sealed record RateChange(Guid ProductId, decimal SellingPrice);

public sealed record RatesRequest(IReadOnlyList<RateChange>? Rates);

public sealed record ImportRequest(IReadOnlyList<ImportRow>? Rows);

public sealed record ImportResult(int Created, int Skipped);

/// <summary>
/// One common-list item the shop starts selling, at its own price. With a <see cref="Name"/> it is one more
/// option of that item ("Aashirvaad atta 5 kg", sold per packet with <see cref="PackSize"/> 5 kg).
/// </summary>
public sealed record FromCatalogueRow(
    int CatalogueItemId, decimal SellingPrice, decimal? OpeningStock, string? Name = null, Unit? Unit = null, decimal? PackSize = null, Unit? PackUnit = null);

public sealed record FromCatalogueRequest(IReadOnlyList<FromCatalogueRow>? Items);

public static class ProductEndpoints
{
    /// <summary>The EAN-13 check digit for the first 12 digits.</summary>
    public static int Ean13Check(string twelve) => (10 - twelve.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum() % 10) % 10;

    public static IEndpointRouteBuilder MapCatalogue(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/v1/products").WithTags("Catalogue").RequireAuthorization(AppPolicies.ShopMember);

        products.MapGet("", (string? search, string? category, bool? low, string? sort, StockService stock, CancellationToken ct) =>
            stock.ListAsync(search, category, low == true, ct, popular: sort == "popular"));

        products.MapGet("/categories", (StockService stock, CancellationToken ct) => stock.CategoriesAsync(ct));

        products.MapGet("/{id:guid}", async (Guid id, StockService stock, CancellationToken ct) =>
            await stock.FindAsync(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());

        products.MapGet("/{id:guid}/movements", (Guid id, StockService stock, CancellationToken ct) => stock.MovementsAsync(id, ct));

        products.MapPost("", async (ProductRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.CreateAsync(request, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Replayed: true, Product: { } product } => Results.Ok(product),
                { Product: { } product } => Results.Created($"/api/v1/products/{product.Id}", product),
                _ => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This ID is already in use."),
            });

        products.MapPut("/{id:guid}", async (Guid id, ProductUpdate request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.UpdateAsync(id, request, ct, Ledger.LedgerEndpoints.IsOwner(user)) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Product: { } product } => Results.Ok(product),
                _ => Results.NotFound(),
            });

        // Today's rates: building-material prices move daily, so the owner changes many at once. Each change is in
        // the audit log (old and new price), which is the rate history.
        // Sold out for today. Helpers can too: they are the ones at the shelf.
        products.MapPut("/{id:guid}/out-today", async (Guid id, OutTodayRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            DateOnly? day = request.Out ? IndiaTime.Today(clock) : null;
            return await db.Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.OutOn, day), ct) > 0 ? Results.NoContent() : Results.NotFound();
        });

        // Shops that never counted: every item at zero stops showing as out, in one go. Owner only.
        products.MapPost("/stop-counting-empty", async (AppDbContext db, CancellationToken ct) =>
            TypedResults.Ok(new { Changed = await db.Products.Where(p => !p.IsService && !p.Uncounted && p.Stock <= 0).ExecuteUpdateAsync(s => s.SetProperty(p => p.Uncounted, true), ct) }))
            .RequireAuthorization(AppPolicies.ShopOwner);

        products.MapPut("/rates", async (RatesRequest request, StockService stock, CancellationToken ct) =>
            await stock.SetRatesAsync(request, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Changed: var changed } => Results.Ok(new { changed }),
            }).RequireAuthorization(AppPolicies.ShopOwner);

        // A new shop of another trade starts with that trade's usual items (prices to fill in on the rates screen).
        products.MapPost("/starter", async (string? type, ClaimsPrincipal user, StockService stock, AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            // One of the shop's own trades ("sweets"), else its main one.
            var shop = await db.Shops.AsNoTracking().Where(s => s.Id == tenant.ShopId).Select(s => new { s.Type, s.ExtraTypes }).SingleAsync(ct);
            var asked = Enum.TryParse<GroceryKhata.Api.Shops.ShopType>(type?.Replace("_", ""), ignoreCase: true, out var t) ? t : shop.Type;
            var trade = asked == shop.Type || shop.ExtraTypes.Contains(GroceryKhata.Api.Shops.ShopTypes.Name(asked)) ? asked : shop.Type;
            var rows = StarterItems.For(trade);
            if (rows.Count == 0)
            {
                return Results.Ok(new ImportResult(0, 0));
            }

            var (result, errors) = await stock.ImportAsync(new ImportRequest(rows), user.UserId()!.Value, ct);
            return errors is null ? Results.Ok(result) : Results.ValidationProblem(errors);
        }).RequireAuthorization(AppPolicies.ShopOwner);

        products.MapPost("/import", async (ImportRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.ImportAsync(request, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Result: { } result } => Results.Ok(result),
                _ => Results.BadRequest(),
            });

        // The shop picks items from the common list and sets its own price: names, units and categories come from
        // the list, so every shop's atta is the same atta to households. Items the shop already sells are skipped.
        products.MapPost("/from-catalogue", async (FromCatalogueRequest request, ClaimsPrincipal user, StockService stock, AppDbContext db, CancellationToken ct) =>
        {
            var rows = request.Items ?? [];
            var unknown = rows.Where(r => CommonCatalogue.Items.All(i => i.Id != r.CatalogueItemId)).Select(r => r.CatalogueItemId).ToList();
            if (rows.Count == 0 || unknown.Count > 0 || rows.Any(r => r.SellingPrice <= 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Items"] = [unknown.Count > 0 ? "Some items are not on the common list." : "Choose items and enter your price for each."],
                });
            }

            var sold = (await db.Products.AsNoTracking().Select(p => new { p.CatalogueItemId, p.Name }).ToListAsync(ct))
                .Select(p => p.CatalogueItemId ?? CommonCatalogue.Match(p.Name)).OfType<int>().ToHashSet();
            // A plain row starts selling the item (skipped if already sold); a named row adds another option of it.
            var fresh = rows.Where(r => !string.IsNullOrWhiteSpace(r.Name) || sold.Add(r.CatalogueItemId)).ToList();
            if (fresh.Count == 0)
            {
                return Results.Ok(new ImportResult(0, rows.Count));
            }

            var byId = CommonCatalogue.Items.ToDictionary(i => i.Id);
            var import = new ImportRequest(fresh.Select(r => byId[r.CatalogueItemId] is var item
                ? new ImportRow(string.IsNullOrWhiteSpace(r.Name) ? item.Name : r.Name.Trim(), r.Unit ?? item.Unit, r.SellingPrice, null, item.Category,
                    r.OpeningStock, null, item.Id, r.PackSize, r.PackUnit)
                : null!).ToList());
            return await stock.ImportAsync(import, user.UserId()!.Value, ct) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Result: { } result } => Results.Ok(result with { Skipped = result.Skipped + rows.Count - fresh.Count }),
                _ => Results.BadRequest(),
            };
        });

        // The option the shop gives when a household leaves the choice to it: one per common-list item.
        products.MapPost("/{id:guid}/default", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var all = await db.Products.Where(p => p.IsActive).ToListAsync(ct);
            var chosen = all.SingleOrDefault(p => p.Id == id);
            var itemId = chosen is null ? null : chosen.CatalogueItemId ?? CommonCatalogue.Match(chosen.Name);
            if (itemId is null)
            {
                return Results.NotFound();
            }

            foreach (var option in all.Where(p => (p.CatalogueItemId ?? CommonCatalogue.Match(p.Name)) == itemId))
            {
                option.IsDefaultOption = option.Id == id;
            }

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // The item's barcode: a code from its pack ("8901234567890"), set or cleared by the shop. Unique in the shop,
        // since scanning it at the counter adds this item to the bill.
        products.MapPut("/{id:guid}/barcode", async (Guid id, BarcodeRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return Results.NotFound();
            }

            var code = Barcodes.Normalize(request.Barcode);
            if (code.Length is > 0 and (< 4 or > 30))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Barcode"] = ["A barcode is 4 to 30 letters and digits."] });
            }

            if (!Barcodes.CheckDigitOk(code))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Barcode"] = ["The last digit of this barcode doesn't match the rest: check the number on the pack, or scan it again."] });
            }

            if (code.Length > 0 && await db.Products.AnyAsync(p => p.Id != id && p.Barcode == code && p.IsActive, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Barcode"] = ["Another item already has this barcode."] });
            }

            product.Barcode = code.Length > 0 ? code : null;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                // Saved at the same moment for another item: the database's one-code-per-item rule caught it.
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Barcode"] = ["Another item already has this barcode."] });
            }

            return Results.Ok(new { product.Barcode });
        });

        // A shop code for an item with no barcode of its own (loose clothes): EAN-13 in the in-store range (starts with 2),
        // with its check digit, so any scanner reads the printed tag. Numbered after the shop's highest such code.
        products.MapPost("/{id:guid}/barcode/new", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return Results.NotFound();
            }

            if (product.Barcode is null)
            {
                // One shop code at a time, so two items made at once never get the same number.
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('shop_barcodes'))", ct);
                var used = await db.Products.Where(p => p.Barcode != null && p.Barcode.Length == 13 && p.Barcode.StartsWith("2")).Select(p => p.Barcode!).ToListAsync(ct);
                var next = used.Select(c => long.TryParse(c[1..12], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
                var body = "2" + next.ToString("D11");
                product.Barcode = body + Ean13Check(body);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }

            return Results.Ok(new { product.Barcode });
        });

        var stockGroup = app.MapGroup("/api/v1").WithTags("Stock").RequireAuthorization(AppPolicies.ShopMember);

        // One selling price for every size of a style ("Men jeans" 28 to 42): sizes come in from the master list with no price.
        stockGroup.MapPost("/products/style-price", async (StylePriceRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var name = request.StyleName?.Trim();
            if (string.IsNullOrEmpty(name) || request.SellingPrice is not > 0 || !Money.IsValidAmount(request.SellingPrice.Value))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["SellingPrice"] = ["Enter the style and a selling price above 0."] });
            }

            var sizes = await db.Products.Where(p => p.StyleName == name && !p.IsService).ToListAsync(ct);
            if (sizes.Count == 0)
            {
                return Results.NotFound();
            }

            foreach (var p in sizes)
            {
                p.SellingPrice = request.SellingPrice.Value;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { Updated = sizes.Count });
        }).RequireAuthorization(AppPolicies.ShopOwner);

        stockGroup.MapPost("/stock-movements", async (StockMovementRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.AddMovementAsync(request, user.UserId()!.Value, ct, user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner))) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { NotFound: true } => Results.ValidationProblem(new Dictionary<string, string[]> { ["ProductId"] = ["Product not found."] }),
                { Movement: { } movement } => Results.Ok(movement),
                _ => Results.BadRequest(),
            });

        stockGroup.MapPost("/products/{id:guid}/open-bag", async (Guid id, OpenBagRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.OpenBagAsync(id, request, user.UserId()!.Value, ct) is { } problem ? Results.ValidationProblem(new Dictionary<string, string[]> { ["Bags"] = [problem] }) : Results.NoContent());

        stockGroup.MapPost("/stocktakes", async (StocktakeRequest request, ClaimsPrincipal user, StockService stock, CancellationToken ct) =>
            await stock.StocktakeAsync(request, user.UserId()!.Value, ct, user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner))) switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Result: { } result } => Results.Ok(result),
                _ => Results.BadRequest(),
            });

        return app;
    }
}
