using System.Security.Cryptography;
using System.Text;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// Products and their stock for the current shop. Every stock change is a <see cref="StockMovement"/>; the
/// product's <see cref="Product.Stock"/> moves in the same transaction, atomically in the database.
/// </summary>
public sealed class StockService(AppDbContext db, TimeProvider clock)
{
    public const int MaxImportRows = 2000;

    /// <param name="popular">Most sold in the last 30 days first: the billing screen's tiles.</param>
    public async Task<IReadOnlyList<ProductDto>> ListAsync(string? search, string? category, bool lowOnly, CancellationToken ct, bool popular = false)
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category != null && p.Category.ToLower() == category.Trim().ToLower());
        }

        if (lowOnly)
        {
            query = query.Where(p => !p.IsService && !p.Uncounted && (p.Stock <= 0 || (p.ReorderLevel != null && p.Stock <= p.ReorderLevel)));
        }

        if (popular)
        {
            var since = clock.GetUtcNow().AddDays(-30);
            return (await query
                .OrderByDescending(p => db.StockMovements.Count(m => m.ProductId == p.Id && m.Kind == StockMovementKind.Sale && m.OccurredAt >= since))
                .ThenBy(p => p.Name)
                .Select(AsDto).ToListAsync(ct)).Select(Linked).ToList();
        }

        var list = (await query.OrderBy(p => p.Category).ThenBy(p => p.Name).Select(AsDto).ToListAsync(ct)).Select(Linked).ToList();
        return await WithDailySalesAsync(list, ct);
    }

    /// <summary>
    /// The item list for a till that keeps it on the device: everything (no <paramref name="since"/>), or only the items
    /// changed since then and the ids of those removed. <see cref="ProductChanges.At"/> is what to ask from next time.
    /// The database stamps updated_at on every change to an item (a trigger), so stock moved by any route counts.
    /// </summary>
    public async Task<ProductChanges> ChangesAsync(DateTimeOffset? since, CancellationToken ct)
    {
        // A little before now: a change saved in a transaction that started earlier but commits after this read still
        // comes next time (sending an item twice is harmless; missing one isn't).
        var at = (await db.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct)) - ProductChanges.Overlap;
        var changed = db.Products.AsNoTracking().Where(p => p.IsActive);
        IReadOnlyList<Guid> removed = [];
        if (since is { } from)
        {
            from = from.ToUniversalTime();
            changed = changed.Where(p => p.UpdatedAt > from);
            removed = await db.Products.AsNoTracking().Where(p => !p.IsActive && p.UpdatedAt > from).Select(p => p.Id).ToListAsync(ct);
        }

        var list = (await changed.OrderBy(p => p.Category).ThenBy(p => p.Name).Select(AsDto).ToListAsync(ct)).Select(Linked).ToList();
        return new ProductChanges(at, since is null, list.Count == 0 ? list : await WithDailySalesAsync(list, ct), removed);
    }

    /// <summary>Each item's average sold a day over the last 30 days, per day it was in stock (<see cref="Cover"/>).</summary>
    private async Task<IReadOnlyList<ProductDto>> WithDailySalesAsync(List<ProductDto> list, CancellationToken ct)
    {
        var daily = await Cover.DailyAsync(db, clock, ct);
        var bagged = list.Where(p => p.BagSize != null).Select(p => p.Id).ToList();
        var bags = bagged.Count == 0 ? [] : await db.StockMovements.AsNoTracking().Where(m => m.Bags != null && bagged.Contains(m.ProductId))
            .GroupBy(m => m.ProductId).Select(g => new { g.Key, Bags = g.Sum(m => m.Bags!.Value) }).ToDictionaryAsync(x => x.Key, x => x.Bags, ct);
        var promised = await Ledger.SaleOrders.PromisedAsync(db, ct);
        var photos = await ProductPhotoEndpoints.IdsAsync(db, list.Count == 1 ? [list[0].Id] : null, ct);
        return list.Select(p => (daily.TryGetValue(p.Id, out var d) && d > 0 ? p with { DailySales = decimal.Round(d, 3) } : p) with
        {
            Bags = SealedBags(bags.GetValueOrDefault(p.Id), p.Stock, p.BagSize), Promised = promised.GetValueOrDefault(p.Id), ExtraPhotos = photos.GetValueOrDefault(p.Id),
        }).ToList();
    }

    /// <summary>Items made before products remembered their common-list item still get one, from their name.</summary>
    private static ProductDto Linked(ProductDto p) => p.CatalogueItemId is null ? p with { CatalogueItemId = CommonCatalogue.Match(p.Name) } : p;

    /// <summary>Names of the categories that hold active items, in the shop's own order.</summary>
    public async Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().Where(c => c.MergedIntoId == null && c.IsActive && db.Products.Any(p => p.IsActive && p.CategoryId == c.Id))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => c.Name).ToListAsync(ct);

    public async Task<ProductDto?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.Id == id).Select(AsDto).SingleOrDefaultAsync(ct) is { } found ? (await WithDailySalesAsync([Linked(found)], ct))[0] : null;

    public async Task<IReadOnlyList<StockMovementDto>> MovementsAsync(Guid productId, CancellationToken ct) =>
        await db.StockMovements.AsNoTracking().Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt).Take(200)
            .Select(m => new StockMovementDto(m.Id, m.ProductId, m.Kind, m.Quantity, m.Reason, m.BillId, m.CostPrice, m.Note, m.OccurredAt))
            .ToListAsync(ct);

    /// <summary>Adds a product, with its opening stock as the first movement. Idempotent on the app's ID.</summary>
    public async Task<(ProductDto? Product, Dictionary<string, string[]>? Errors, bool Replayed)> CreateAsync(
        ProductRequest request, Guid userId, CancellationToken ct)
    {
        if (await FindAsync(request.Id, ct) is { } existing)
        {
            return (existing, null, true);
        }

        var errors = Validate(request.Name, request.Unit, request.SellingPrice, request.CostPrice, request.Category, request.ReorderLevel);
        if (request.Id == Guid.Empty)
        {
            errors["Id"] = ["The app must send an ID for every new record."];
        }

        if (request.OpeningStock is { } opening && (opening < 0 || opening > Money.MaxQuantity || decimal.Round(opening, 3) != opening))
        {
            errors[nameof(request.OpeningStock)] = ["Enter how many are on the shelf, or leave it empty."];
        }

        foreach (var (field, messages) in TaxErrors(request.GstRate, request.HsnCode))
        {
            errors[field] = messages;
        }

        // A new item can come with its barcode (made from a scan the shop didn't know yet).
        var barcode = Barcodes.Normalize(request.Barcode);
        if (barcode.Length > 0 && (barcode.Length is < 4 or > 30 || !Barcodes.CheckDigitOk(barcode)))
        {
            errors[nameof(request.Barcode)] = ["Check the barcode: 4 to 30 letters and digits, with the right last digit."];
        }
        else if (barcode.Length > 0 && await Barcodes.TakenAsync(db, barcode, Guid.Empty, ct))
        {
            errors[nameof(request.Barcode)] = ["Another item already has this barcode."];
        }

        if (errors.Count > 0)
        {
            return (null, errors, false);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = NewProduct(request.Id, request.Name!, request.Category, request.Unit!.Value, request.SellingPrice, request.CostPrice, request.ReorderLevel);
        await Categories.LinkAsync(db, product, ct);
        // The category's defaults fill what the item doesn't say.
        var defaults = product.CategoryId is { } cid ? await db.Categories.AsNoTracking().SingleAsync(c => c.Id == cid, ct) : null;
        product.GstRate = request.GstRate ?? defaults?.DefaultGstRate;
        product.HsnCode = Clean(request.HsnCode) ?? defaults?.DefaultHsn;
        (product.IsService, product.DurationMinutes) = (request.IsService, request.DurationMinutes is > 0 and <= 1440 ? request.DurationMinutes : null);
        product.Schedule = request.IsService ? null : request.Schedule;
        product.UnitsPerPack = request.IsService ? null : request.UnitsPerPack is > 1 and <= 1000 ? request.UnitsPerPack : null;
        (product.TracksSerials, product.WarrantyMonths) = request.IsService ? (false, null) : (request.TracksSerials, request.WarrantyMonths is > 0 and <= 240 ? request.WarrantyMonths : null);
        product.Mrp = CleanMrp(request.Mrp, request.SellingPrice);
        product.BillName = BillNames.Clean(request.BillName);
        (product.StyleName, product.Size, product.Colour) = (Short(request.StyleName, 120), Short(request.Size, 20), Short(request.Colour, 30));
        product.Barcode = barcode.Length > 0 ? barcode : null;
        product.BagSize = request.BagSize is > 0 and <= 100_000 && !request.IsService ? request.BagSize : null;
        // "बोरी" is the plain bag: kept as no purchase unit, as an edit does (else the bill treats it like a carton).
        product.PurchaseUnit = product.BagSize is null || request.PurchaseUnit == product.Unit || request.PurchaseUnit == Unit.Bag ? null : request.PurchaseUnit;
        product.WeightKg = request.WeightKg is > 0 and <= 100_000 && !request.IsService ? request.WeightKg : null;
        product.BagPrice = product.BagSize is not null && request.BagPrice is > 0 and var bp && Money.IsValidAmount(bp) ? bp : null;
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        if (request.OpeningStock is > 0)
        {
            await RecordAsync(Guid.CreateVersion7(), request.Id, StockMovementKind.Opening, request.OpeningStock.Value, null, null, null, userId, ct);
        }

        await transaction.CommitAsync(ct);
        return (await FindAsync(request.Id, ct), null, false);
    }

    /// <summary>Changes the details, never the stock (that only moves through movements).</summary>
    /// <param name="isOwner">Prices are the owner's: a helper who could change one would have no discount limit.</param>
    public async Task<(ProductDto? Product, Dictionary<string, string[]>? Errors)> UpdateAsync(Guid id, ProductUpdate request, CancellationToken ct, bool isOwner = true)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return (null, null);
        }

        var errors = Validate(request.Name, request.Unit, request.SellingPrice, request.CostPrice, request.Category, request.ReorderLevel);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        // Wholesale: a price under the regular one, from a quantity above 1. 0 clears it; null keeps it (older apps).
        if (request.WholesalePrice is > 0 and var wp && (wp >= request.SellingPrice || !Money.IsValidAmount(wp)))
        {
            return (null, new() { ["WholesalePrice"] = ["The wholesale price must be below the regular price."] });
        }

        if (request.WholesalePrice is > 0 && request.WholesaleMinQty is not (> 1 and <= 1_000_000))
        {
            return (null, new() { ["WholesaleMinQty"] = ["Say from how many the wholesale price starts (more than 1)."] });
        }

        if (!isOwner && (request.SellingPrice != product.SellingPrice || request.CostPrice != product.CostPrice
            || (request.Mrp is { } asked && CleanMrp(asked, product.SellingPrice) != product.Mrp)
            || (request.WholesalePrice is { } w && (w > 0 ? w : null) != product.WholesalePrice)))
        {
            return (null, new() { ["SellingPrice"] = ["Only the owner can change prices."] });
        }

        product.Name = request.Name!.Trim();
        // A renamed option keeps its common-list item unless the new name says another one.
        product.CatalogueItemId = CommonCatalogue.Match(product.Name) ?? product.CatalogueItemId;
        product.Category = Clean(request.Category);
        await Categories.LinkAsync(db, product, ct);
        product.Unit = request.Unit!.Value;
        product.SellingPrice = request.SellingPrice;
        product.CostPrice = request.CostPrice;
        product.ReorderLevel = request.ReorderLevel;
        product.IsActive = request.IsActive ?? product.IsActive;
        product.Uncounted = request.Uncounted ?? product.Uncounted;
        if (TaxErrors(request.GstRate, request.HsnCode) is { Count: > 0 } taxErrors)
        {
            return (null, taxErrors);
        }

        product.GstRate = request.ReplaceTax ? request.GstRate : request.GstRate ?? product.GstRate;
        product.HsnCode = request.ReplaceTax ? Clean(request.HsnCode) : Clean(request.HsnCode) ?? product.HsnCode;
        product.IsService = request.IsService ?? product.IsService;
        product.DurationMinutes = request.DurationMinutes is { } minutes ? (minutes is > 0 and <= 1440 ? minutes : null) : product.DurationMinutes;
        // Older apps don't send a schedule: null keeps it, unless ReplaceSchedule says null means over the counter.
        product.Schedule = product.IsService ? null : request.Schedule ?? (request.ReplaceSchedule ? null : product.Schedule);
        // 0 or 1 clears it (sold whole only); null keeps it.
        product.UnitsPerPack = request.UnitsPerPack is { } perPack ? (perPack is > 1 and <= 1000 && !product.IsService ? perPack : null) : product.UnitsPerPack;
        // Null keeps them (older apps don't send them); 0 months clears the warranty.
        product.TracksSerials = !product.IsService && (request.TracksSerials ?? product.TracksSerials);
        product.WarrantyMonths = request.WarrantyMonths is { } months ? (months is > 0 and <= 240 && !product.IsService ? months : null) : product.WarrantyMonths;
        // Null keeps the MRP; 0 clears it.
        product.Mrp = request.Mrp is { } mrp ? CleanMrp(mrp, product.SellingPrice) : product.Mrp;
        // Null keeps it (older apps); an empty string clears it.
        product.BillName = request.BillName is null ? product.BillName : BillNames.Clean(request.BillName);
        // Style, size and colour: null keeps them (older apps); an empty string clears them.
        product.StyleName = request.StyleName is null ? product.StyleName : Short(request.StyleName, 120);
        product.Size = request.Size is null ? product.Size : Short(request.Size, 20);
        product.Colour = request.Colour is null ? product.Colour : Short(request.Colour, 30);
        product.BagSize = request.BagSize is { } bag ? (bag is > 0 and <= 100_000 && !product.IsService ? bag : null) : product.BagSize;
        product.PurchaseUnit = product.BagSize is null ? null : request.PurchaseUnit is { } pu ? (pu == product.Unit || pu == Unit.Bag ? null : pu) : product.PurchaseUnit;
        // Null keeps it; 0 clears it.
        product.WeightKg = request.WeightKg is { } kg ? (kg is > 0 and <= 100_000 && !product.IsService ? kg : null) : product.WeightKg;
        // Null keeps it; 0 clears it; gone with the bag.
        product.BagPrice = product.BagSize is null ? null : request.BagPrice is { } bagPrice ? (bagPrice > 0 && Money.IsValidAmount(bagPrice) ? bagPrice : null) : product.BagPrice;
        if (request.WholesalePrice is { } wholesale)
        {
            (product.WholesalePrice, product.WholesaleMinQty) = wholesale > 0 ? ((decimal?)wholesale, request.WholesaleMinQty) : ((decimal?)null, (decimal?)null);
        }
        else if (product.WholesalePrice >= product.SellingPrice)
        {
            // The regular price came down to or below it: it is no longer a wholesale price.
            (product.WholesalePrice, product.WholesaleMinQty) = (null, null);
        }

        await db.SaveChangesAsync(ct);
        return (await FindAsync(id, ct), null);
    }

    /// <summary>A purchase (+) or an adjustment (±, with a reason). Idempotent on the app's ID.</summary>
    public async Task<(StockMovementDto? Movement, Dictionary<string, string[]>? Errors, bool NotFound)> AddMovementAsync(
        StockMovementRequest request, Guid userId, CancellationToken ct, bool isOwner = true)
    {
        if (await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.Id, ct) is { } existing)
        {
            return (ToDto(existing), null, false);
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty)
        {
            errors["Id"] = ["The app must send an ID for every new record."];
        }

        if (request.Kind is not (StockMovementKind.Purchase or StockMovementKind.Adjustment))
        {
            errors[nameof(request.Kind)] = ["Choose purchase or adjustment."];
        }

        if (request.Quantity == 0 || Math.Abs(request.Quantity) > Money.MaxQuantity || decimal.Round(request.Quantity, 3) != request.Quantity)
        {
            errors[nameof(request.Quantity)] = ["Enter a quantity, with at most three decimals."];
        }
        else if (request.Kind == StockMovementKind.Purchase && request.Quantity < 0)
        {
            errors[nameof(request.Quantity)] = ["A purchase adds stock: enter a positive quantity."];
        }

        if (request.Kind == StockMovementKind.Adjustment && request.Reason is null)
        {
            errors[nameof(request.Reason)] = ["Say why: damage, wastage, lost, stolen, destroyed, written off, gift, free sample, count correction, return or other."];
        }

        if (request.CostPrice is { } cost && !Money.IsValidAmount(cost))
        {
            errors[nameof(request.CostPrice)] = ["Enter a valid cost price, or leave it empty."];
        }

        if (request.Note?.Length > 200)
        {
            errors[nameof(request.Note)] = ["Keep the note under 200 characters."];
        }

        if (request.Bags is { } bagCount && (Math.Abs(bagCount) > 100_000 || decimal.Round(bagCount, 3) != bagCount || (bagCount != 0 && Math.Sign(bagCount) != Math.Sign(request.Quantity))))
        {
            errors["Bags"] = ["Bags go the same way as the weight: in with goods in, out with a reduction."];
        }

        if (errors.Count > 0)
        {
            return (null, errors, false);
        }

        if (await db.Products.AsNoTracking().Where(p => p.Id == request.ProductId).Select(p => new { p.CostPrice, p.SellingPrice }).SingleOrDefaultAsync(ct) is not { } item)
        {
            return (null, null, true);
        }

        // A helper may change stock by hand only so much either way (valued at cost, or the selling price when no cost
        // is known); more needs the owner, so stock can't be quietly written off or invented.
        if (!isOwner && request.Kind == StockMovementKind.Adjustment
            && Math.Abs(request.Quantity) * (item.CostPrice ?? item.SellingPrice) > await WriteOffLimitAsync(ct))
        {
            return (null, new() { ["Quantity"] = [$"Changing stock by more than ₹{await WriteOffLimitAsync(ct):0.##} needs the shop owner."] }, false);
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var movement = await RecordAsync(request.Id, request.ProductId, request.Kind!.Value, request.Quantity,
                request.Kind == StockMovementKind.Adjustment ? request.Reason : null, request.CostPrice, request.Note, userId, ct);
            if (request.Bags is { } bagsMoved and not 0)
            {
                movement.Bags = bagsMoved;
                await db.SaveChangesAsync(ct);
            }
            if (request.Kind == StockMovementKind.Purchase && request.CostPrice is { } latest)
            {
                // Weighted average with what was already on the shelf, the same rule supplier bills keep.
                var before = await db.Products.AsNoTracking().Where(p => p.Id == request.ProductId).Select(p => new { p.Stock, p.CostPrice }).SingleAsync(ct);
                var held = before.Stock - request.Quantity;
                var averaged = before.CostPrice is { } old && held > 0 ? decimal.Round(((held * old) + (request.Quantity * latest)) / (held + request.Quantity), 2) : latest;
                await db.Products.Where(p => p.Id == request.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CostPrice, averaged), ct);
            }

            await transaction.CommitAsync(ct);
            return (ToDto(movement), null, false);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            db.ChangeTracker.Clear();
            var saved = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.Id, ct);
            return saved is null ? (null, new() { ["Id"] = ["This ID is already in use."] }, false) : (ToDto(saved), null, false);
        }
    }

    /// <summary>
    /// A stocktake: for each counted item that differs from the books, a count-correction adjustment. Each
    /// adjustment's ID is derived from the stocktake's ID and the product, so sending the same count twice is a no-op.
    /// </summary>
    public async Task<(StocktakeResult? Result, Dictionary<string, string[]>? Errors)> StocktakeAsync(StocktakeRequest request, Guid userId, CancellationToken ct, bool isOwner = true)
    {
        if (request.Id == Guid.Empty || request.Counts is not { Count: > 0 and <= MaxImportRows })
        {
            return (null, new() { ["Counts"] = ["Send the stocktake's ID and between 1 and 2000 counts."] });
        }

        if (request.Counts.Any(c => c.Counted < 0 || c.Counted > Money.MaxQuantity || decimal.Round(c.Counted, 3) != c.Counted || c.Bags is < 0 or > 100_000))
        {
            return (null, new() { ["Counts"] = ["Counts must be 0 or more, with at most three decimals."] });
        }

        var ids = request.Counts.Select(c => c.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var adjusted = 0;
        var unchanged = 0;
        if (await db.Stocktakes.AnyAsync(t => t.Id == request.Id, ct))
        {
            return (new StocktakeResult(0, request.Counts.Count), null);
        }

        var lines = request.Counts.DistinctBy(c => c.ProductId).Where(c => products.ContainsKey(c.ProductId))
            .Select(c => { var p = products[c.ProductId]; return new StocktakeLine(p.Id, p.Name, p.Stock, c.Counted, c.Counted - p.Stock, p.CostPrice); }).ToList();
        // A helper's count may move stock only so much either way (at cost, or the selling price when no cost is known).
        decimal Worth(StocktakeLine l) => l.CostPrice ?? products[l.ProductId].SellingPrice;
        var lossAtCost = -lines.Where(l => l.Difference < 0).Sum(l => l.Difference * Worth(l));
        var gainAtCost = lines.Where(l => l.Difference > 0).Sum(l => l.Difference * Worth(l));
        if (!isOwner && lossAtCost > await WriteOffLimitAsync(ct))
        {
            return (null, new() { ["Counts"] = [$"This count writes off ₹{lossAtCost:0.##} at cost, more than a helper may (₹{await WriteOffLimitAsync(ct):0.##}). Ask the shop owner to save it."] });
        }

        if (!isOwner && gainAtCost > await WriteOffLimitAsync(ct))
        {
            return (null, new() { ["Counts"] = [$"This count adds ₹{gainAtCost:0.##} of stock, more than a helper may (₹{await WriteOffLimitAsync(ct):0.##}). Ask the shop owner to save it."] });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Stocktakes.Add(new Stocktake
        {
            Id = request.Id, Number = await db.NextNumberAsync<Catalogue.Stocktake>(ct), TakenAt = clock.GetUtcNow(), CreatedByUserId = userId,
            Lines = System.Text.Json.JsonSerializer.Serialize(lines), Items = lines.Count, Differences = lines.Count(l => l.Difference != 0),
            ValueDifference = decimal.Round(lines.Sum(l => l.Difference * (l.CostPrice ?? 0)), 2),
        });
        // Items with no stock movement yet: their first count is the shop's opening stock, not a correction.
        var moved = (await db.StockMovements.AsNoTracking().Where(m => ids.Contains(m.ProductId)).Select(m => m.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        // Sealed bags on the books, to set them to the bags counted.
        var bagsOnBooks = await db.StockMovements.AsNoTracking().Where(m => ids.Contains(m.ProductId) && m.Bags != null)
            .GroupBy(m => m.ProductId).Select(g => new { g.Key, Bags = g.Sum(m => m.Bags ?? 0) }).ToDictionaryAsync(g => g.Key, g => g.Bags, ct);
        foreach (var count in request.Counts.DistinctBy(c => c.ProductId))
        {
            if (!products.TryGetValue(count.ProductId, out var product))
            {
                continue;
            }

            if (count.Bags is { } bagsCounted && product.BagSize is > 0 && bagsCounted != bagsOnBooks.GetValueOrDefault(product.Id))
            {
                db.StockMovements.Add(new StockMovement
                {
                    Id = DerivedId(request.Id, DerivedId(product.Id, request.Id)), ProductId = product.Id, Kind = StockMovementKind.Adjustment, Reason = StockReason.CountCorrection,
                    Quantity = 0, Bags = bagsCounted - bagsOnBooks.GetValueOrDefault(product.Id), Note = "Stocktake: sealed bags", RefType = "stocktake", RefId = request.Id,
                    OccurredAt = clock.GetUtcNow(), CreatedByUserId = userId,
                });
            }

            var movementId = DerivedId(request.Id, count.ProductId);
            if (await db.StockMovements.AnyAsync(m => m.Id == movementId, ct) || count.Counted == product.Stock)
            {
                unchanged++;
                continue;
            }

            var first = !moved.Contains(product.Id);
            var correction = await RecordAsync(movementId, product.Id, first ? StockMovementKind.Opening : StockMovementKind.Adjustment, count.Counted - product.Stock,
                first ? null : StockReason.CountCorrection, first ? product.CostPrice : null, first ? "First count" : "Stocktake", userId, ct);
            (correction.RefType, correction.RefId) = ("stocktake", request.Id);
            adjusted++;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (new StocktakeResult(adjusted, unchanged), null);
    }

    /// <summary>
    /// Adds many products at once (CSV or the starter catalogue). Names the shop already has are skipped, so the
    /// same file can be imported twice. All or nothing: a bad row stops the import and is reported by its number.
    /// </summary>
    public async Task<(ImportResult? Result, Dictionary<string, string[]>? Errors)> ImportAsync(ImportRequest request, Guid userId, CancellationToken ct)
    {
        if (request.Rows is not { Count: > 0 and <= MaxImportRows })
        {
            return (null, new() { ["Rows"] = [$"Send between 1 and {MaxImportRows} rows."] });
        }

        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowErrors = Validate(row.Name, row.Unit, row.SellingPrice ?? 0, row.CostPrice, row.Category, row.ReorderLevel, allowNoPrice: true);
            foreach (var (field, messages) in TaxErrors(row.GstRate, row.HsnCode))
            {
                rowErrors[field] = messages;
            }

            if (row.OpeningStock is { } opening && (opening < 0 || opening > Money.MaxQuantity || decimal.Round(opening, 3) != opening))
            {
                rowErrors["OpeningStock"] = ["Opening stock must be 0 or more."];
            }

            if (rowErrors.Count > 0)
            {
                errors[$"Rows[{i + 1}]"] = [rowErrors.Values.First()[0]];
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var existing = (await db.Products.AsNoTracking().Select(p => p.Name).ToListAsync(ct))
            .Select(n => n.ToLowerInvariant()).ToHashSet();
        var created = 0;
        var skipped = 0;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var categoryCache = new Dictionary<string, Category?>();
        foreach (var row in request.Rows)
        {
            if (!existing.Add(row.Name!.Trim().ToLowerInvariant()))
            {
                skipped++;
                continue;
            }

            var product = NewProduct(Guid.CreateVersion7(), row.Name, row.Category, row.Unit!.Value, row.SellingPrice ?? 0, row.CostPrice, row.ReorderLevel);
            await Categories.LinkAsync(db, product, ct, categoryCache);
            product.CatalogueItemId = row.CatalogueItemId ?? product.CatalogueItemId;
            product.MasterItemId = row.MasterItemId;
            (product.StyleName, product.Size) = (Short(row.StyleName, 120), Short(row.Size, 20));
            product.PackSize = row.PackSize is > 0 ? row.PackSize : null;
            product.PackUnit = product.PackSize is null ? null : row.PackUnit;
            product.GstRate = row.GstRate;
            product.HsnCode = Clean(row.HsnCode);
            // A SAC code (99…) is a service's: from a master list, a haircut comes in as a service even without the flag.
            (product.IsService, product.DurationMinutes) = (row.IsService || (row.HsnCode?.Trim().StartsWith("99") ?? false), row.DurationMinutes);
            product.Uncounted = !product.IsService && row.Uncounted;
            product.Schedule = product.IsService ? null : row.Schedule;
            product.UnitsPerPack = product.IsService ? null : row.UnitsPerPack;
            (product.TracksSerials, product.WarrantyMonths) = product.IsService ? (false, null) : (row.TracksSerials, row.WarrantyMonths);
            db.Products.Add(product);
            if (row.OpeningStock is > 0 and var opening)
            {
                // The opening movement and the stock it gives, saved with the item in one go: a save per row re-checked every
                // item added so far, and 2,000 rows took over a minute.
                product.Stock = opening;
                db.StockMovements.Add(new StockMovement
                {
                    Id = Guid.CreateVersion7(), ProductId = product.Id, Kind = StockMovementKind.Opening, Quantity = opening,
                    OccurredAt = clock.GetUtcNow(), CreatedByUserId = userId,
                });
            }

            created++;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (new ImportResult(created, skipped), null);
    }

    /// <summary>Saves a movement and moves the product's stock with it. The caller owns the transaction.</summary>
    /// <summary>Opens sealed bags to sell loose. Refused when that many sealed bags aren't on hand.</summary>
    public async Task<string?> OpenBagAsync(Guid productId, OpenBagRequest request, Guid userId, CancellationToken ct)
    {
        if (request.Id == Guid.Empty || request.Bags is <= 0 or > 10_000 || decimal.Round(request.Bags, 0) != request.Bags)
        {
            return "Open one or more whole bags.";
        }

        if (await db.StockMovements.AnyAsync(m => m.Id == request.Id, ct))
        {
            return null;
        }

        var item = await db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => new { p.Stock, p.BagSize }).SingleAsync(ct);
        var sealedBags = SealedBags(await db.StockMovements.Where(m => m.ProductId == productId && m.Bags != null).SumAsync(m => (decimal?)m.Bags, ct) ?? 0, item.Stock, item.BagSize);
        if (sealedBags < request.Bags)
        {
            return $"Only {sealedBags:0.##} sealed bags are on hand.";
        }

        db.StockMovements.Add(new StockMovement
        {
            Id = request.Id, ProductId = productId, Kind = StockMovementKind.OpenBag, Quantity = 0, Bags = -request.Bags,
            Note = "Opened to sell loose", OccurredAt = clock.GetUtcNow(), CreatedByUserId = userId,
        });
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>
    /// Sealed cartons or strips on hand: the movements added up, but never more than the stock can fill. Selling 5 soaps
    /// loose from 2 cartons of 24 leaves 43 pieces, so one carton must have been opened even if nobody said so. Bags of
    /// grain are only "about" their size (40 bags can hold 1,985 kg), so for bags <paramref name="bagSize"/> is null.
    /// </summary>
    /// <summary>
    /// Sealed bags on hand: those counted in and out, never more than the stock can hold. A grain bag's weight varies
    /// (49.4 kg in a "50 kg" bori), so a bag counts as full from 95% of its size.
    /// </summary>
    internal static decimal SealedBags(decimal counted, decimal stock, decimal? bagSize) =>
        bagSize is > 0 and var size ? Math.Max(0, Math.Min(counted, Math.Floor(stock / (size * 0.95m)))) : counted;

    /// <summary>
    /// Bags to open when stock goes down to <paramref name="stockAfter"/> with <paramref name="sealedAfter"/> bags still
    /// counted sealed: enough that the sealed bags fit in the stock (with the same 95% leeway). 0 when none.
    /// </summary>
    internal static decimal BagsToOpen(decimal stockAfter, decimal sealedAfter, decimal size)
    {
        if (size <= 0 || sealedAfter <= 0) return 0;
        var fit = Math.Max(0, Math.Floor(stockAfter / (size * 0.95m)));
        return Math.Max(0, sealedAfter - fit);
    }

    private Task<decimal> WriteOffLimitAsync(CancellationToken ct) =>
        db.Shops.AsNoTracking().Where(s => s.Id == db.ShopIdNow).Select(s => s.StaffMaxWriteOff).FirstAsync(ct);

    private async Task<StockMovement> RecordAsync(
        Guid id, Guid productId, StockMovementKind kind, decimal quantity, StockReason? reason, decimal? costPrice, string? note,
        Guid userId, CancellationToken ct)
    {
        // A write-off or count correction keeps the cost it had that day, so later cost changes don't revalue past losses
        // (the month report and the year's P&L then agree).
        if (costPrice is null && kind == StockMovementKind.Adjustment)
        {
            costPrice = await db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.CostPrice).SingleOrDefaultAsync(ct);
        }

        var movement = new StockMovement
        {
            Id = id,
            ProductId = productId,
            Kind = kind,
            Quantity = quantity,
            Reason = reason,
            CostPrice = costPrice,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            OccurredAt = clock.GetUtcNow(),
            CreatedByUserId = userId,
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity), ct);
        if (kind == StockMovementKind.Adjustment)
        {
            await Batches.AdjustAsync(db, productId, quantity, ct);
        }

        return movement;
    }

    /// <summary>Sets many prices at once (today's rates). Only items whose price changed are saved (and audited).</summary>
    public async Task<(int Changed, Dictionary<string, string[]>? Errors)> SetRatesAsync(RatesRequest request, CancellationToken ct)
    {
        if (request.Rates is not { Count: > 0 and <= MaxImportRows }
            || request.Rates.Any(r => r.SellingPrice < 0 || r.SellingPrice > Money.MaxAmount || decimal.Round(r.SellingPrice, 2) != r.SellingPrice
                || r.CostPrice is { } c && (c < 0 || c > Money.MaxAmount || decimal.Round(c, 2) != c)))
        {
            return (0, new() { ["Rates"] = ["Enter valid prices."] });
        }

        var ids = request.Rates.Select(r => r.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (ids.Distinct().Count(id => !products.ContainsKey(id)) is > 0 and var missing)
        {
            return (0, new() { ["Rates"] = [$"{missing} item(s) not found: they may have been deleted. Refresh and try again."] });
        }

        var changed = 0;
        foreach (var rate in request.Rates)
        {
            if (products.TryGetValue(rate.ProductId, out var product) && (product.SellingPrice != rate.SellingPrice || (rate.CostPrice is { } cost && product.CostPrice != cost)))
            {
                product.SellingPrice = rate.SellingPrice;
                if (rate.CostPrice is { } newCost) product.CostPrice = newCost;
                changed++;
            }
        }

        await db.SaveChangesAsync(ct);
        return (changed, null);
    }

    /// <summary>GST is one of the rates in use (0, 0.25, 3, 5, 12, 18, 28, 40); an HSN code is 4 to 8 digits.</summary>
    private static Dictionary<string, string[]> TaxErrors(decimal? gst, string? hsn)
    {
        var errors = new Dictionary<string, string[]>();
        if (gst is { } rate && rate is not (0m or 0.25m or 3m or 5m or 12m or 18m or 28m or 40m))
        {
            errors["GstRate"] = ["Choose a GST rate: 0, 5, 18 or 40%."];
        }

        if (!string.IsNullOrWhiteSpace(hsn) && hsn.Trim() is { } code && (code.Length is < 4 or > 8 || !code.All(char.IsAsciiDigit)))
        {
            errors["HsnCode"] = ["An HSN code is 4 to 8 digits."];
        }

        return errors;
    }

    private static string? Short(string? text, int max) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(max, text.Trim().Length)];

    private static Product NewProduct(Guid id, string name, string? category, Unit unit, decimal price, decimal? cost, decimal? reorderLevel) => new()
    {
        Id = id,
        Name = name.Trim(),
        CatalogueItemId = CommonCatalogue.Match(name),
        Category = Clean(category),
        Unit = unit,
        SellingPrice = price,
        CostPrice = cost,
        ReorderLevel = reorderLevel,
    };

    private static Dictionary<string, string[]> Validate(
        string? name, Unit? unit, decimal price, decimal? cost, string? category, decimal? reorderLevel, bool allowNoPrice = false)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
        {
            errors["Name"] = ["Enter the product's name."];
        }

        if (unit is null)
        {
            errors["Unit"] = ["Choose a unit."];
        }

        if (!(Money.IsValidAmount(price) || (allowNoPrice && price == 0)))
        {
            errors["SellingPrice"] = ["Enter a selling price above ₹0."];
        }

        if (cost is { } c && !Money.IsValidAmount(c))
        {
            errors["CostPrice"] = ["Enter a valid cost price, or leave it empty."];
        }

        if (category?.Trim().Length > 60)
        {
            errors["Category"] = ["Keep the category under 60 characters."];
        }

        if (reorderLevel is { } level && (level < 0 || level > Money.MaxQuantity || decimal.Round(level, 3) != level))
        {
            errors["ReorderLevel"] = ["Enter a reorder level of 0 or more, or leave it empty."];
        }

        return errors;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>A stable ID from two others (same inputs, same ID), for idempotent batch operations.</summary>
    private static Guid DerivedId(Guid batch, Guid item) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{batch:N}:{item:N}")));

    private static readonly System.Linq.Expressions.Expression<Func<Product, ProductDto>> AsDto = p => new ProductDto(
        p.Id, p.Name, p.Category, p.Unit, p.SellingPrice, p.CostPrice, p.Stock, p.ReorderLevel,
        !p.IsService && !p.Uncounted && (p.Stock <= 0 || (p.ReorderLevel != null && p.Stock <= p.ReorderLevel)), p.Reserved, p.CatalogueItemId, p.PackSize, p.PackUnit, p.IsDefaultOption,
        // The shop's own photo, else its master item's (the server serves whichever it is at the same address).
        p.ImageAt ?? (p.MasterItem != null ? p.MasterItem.ImageAt : null),
        p.GstRate, p.HsnCode, p.IsService, p.DurationMinutes, p.Schedule, p.UnitsPerPack, p.TracksSerials, p.WarrantyMonths, p.Mrp, p.Barcode,
        p.ImageAt != null ? p.ImageCredit : p.MasterItem != null && p.MasterItem.ImageAt != null ? p.MasterItem.ImageCredit ?? MasterPhotos.Representative : null,
        p.PhotoReports, p.PhotoReportNote, p.BillName, p.StyleName, p.Size, p.Colour, p.Uncounted, p.OutOn, null, p.BagSize, 0, 0, p.PurchaseUnit, p.WeightKg,
        p.MasterItem != null ? p.MasterItem.Name : p.CatalogueItem != null ? p.CatalogueItem.NameEn : null,
        p.MasterItem != null ? p.MasterItem.NameHi : p.CatalogueItem != null ? p.CatalogueItem.Name : null,
        p.WholesalePrice, p.WholesaleMinQty, null, p.ExtraBarcodes, p.BagPrice);

    /// <summary>An MRP below the selling price is a typing slip. At the price it stays: it's what stops a bill going above it.</summary>
    private static decimal? CleanMrp(decimal? mrp, decimal sellingPrice) =>
        mrp is { } m && m >= sellingPrice && Money.IsValidAmount(m) ? m : null;

    private static StockMovementDto ToDto(StockMovement m) =>
        new(m.Id, m.ProductId, m.Kind, m.Quantity, m.Reason, m.BillId, m.CostPrice, m.Note, m.OccurredAt);
}
