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
            query = query.Where(p => p.Category == category.Trim());
        }

        if (lowOnly)
        {
            query = query.Where(p => !p.IsService && (p.Stock <= 0 || (p.ReorderLevel != null && p.Stock <= p.ReorderLevel)));
        }

        if (popular)
        {
            var since = clock.GetUtcNow().AddDays(-30);
            return (await query
                .OrderByDescending(p => db.StockMovements.Count(m => m.ProductId == p.Id && m.Kind == StockMovementKind.Sale && m.OccurredAt >= since))
                .ThenBy(p => p.Name)
                .Select(AsDto).ToListAsync(ct)).Select(Linked).ToList();
        }

        return (await query.OrderBy(p => p.Category).ThenBy(p => p.Name).Select(AsDto).ToListAsync(ct)).Select(Linked).ToList();
    }

    /// <summary>Items made before products remembered their common-list item still get one, from their name.</summary>
    private static ProductDto Linked(ProductDto p) => p.CatalogueItemId is null ? p with { CatalogueItemId = CommonCatalogue.Match(p.Name) } : p;

    public async Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.IsActive && p.Category != null)
            .Select(p => p.Category!).Distinct().OrderBy(c => c).ToListAsync(ct);

    public async Task<ProductDto?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.Id == id).Select(AsDto).SingleOrDefaultAsync(ct) is { } found ? Linked(found) : null;

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

        if (errors.Count > 0)
        {
            return (null, errors, false);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = NewProduct(request.Id, request.Name!, request.Category, request.Unit!.Value, request.SellingPrice, request.CostPrice, request.ReorderLevel);
        product.GstRate = request.GstRate;
        product.HsnCode = Clean(request.HsnCode);
        (product.IsService, product.DurationMinutes) = (request.IsService, request.DurationMinutes is > 0 and <= 1440 ? request.DurationMinutes : null);
        product.Schedule = request.IsService ? null : request.Schedule;
        product.UnitsPerPack = request.IsService ? null : request.UnitsPerPack is > 1 and <= 1000 ? request.UnitsPerPack : null;
        (product.TracksSerials, product.WarrantyMonths) = request.IsService ? (false, null) : (request.TracksSerials, request.WarrantyMonths is > 0 and <= 240 ? request.WarrantyMonths : null);
        product.Mrp = CleanMrp(request.Mrp, request.SellingPrice);
        product.BillName = BillNames.Clean(request.BillName);
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
    public async Task<(ProductDto? Product, Dictionary<string, string[]>? Errors)> UpdateAsync(Guid id, ProductUpdate request, CancellationToken ct)
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

        product.Name = request.Name!.Trim();
        // A renamed option keeps its common-list item unless the new name says another one.
        product.CatalogueItemId = CommonCatalogue.Match(product.Name) ?? product.CatalogueItemId;
        product.Category = Clean(request.Category);
        product.Unit = request.Unit!.Value;
        product.SellingPrice = request.SellingPrice;
        product.CostPrice = request.CostPrice;
        product.ReorderLevel = request.ReorderLevel;
        product.IsActive = request.IsActive ?? product.IsActive;
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
        await db.SaveChangesAsync(ct);
        return (await FindAsync(id, ct), null);
    }

    /// <summary>A purchase (+) or an adjustment (±, with a reason). Idempotent on the app's ID.</summary>
    public async Task<(StockMovementDto? Movement, Dictionary<string, string[]>? Errors, bool NotFound)> AddMovementAsync(
        StockMovementRequest request, Guid userId, CancellationToken ct)
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
            errors[nameof(request.Reason)] = ["Say why: damage, wastage, count correction, return or other."];
        }

        if (request.CostPrice is { } cost && !Money.IsValidAmount(cost))
        {
            errors[nameof(request.CostPrice)] = ["Enter a valid cost price, or leave it empty."];
        }

        if (request.Note?.Length > 200)
        {
            errors[nameof(request.Note)] = ["Keep the note under 200 characters."];
        }

        if (errors.Count > 0)
        {
            return (null, errors, false);
        }

        if (!await db.Products.AnyAsync(p => p.Id == request.ProductId, ct))
        {
            return (null, null, true);
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var movement = await RecordAsync(request.Id, request.ProductId, request.Kind!.Value, request.Quantity,
                request.Kind == StockMovementKind.Adjustment ? request.Reason : null, request.CostPrice, request.Note, userId, ct);
            if (request.Kind == StockMovementKind.Purchase && request.CostPrice is { } latest)
            {
                // The latest purchase price becomes the product's cost, for profit estimates.
                await db.Products.Where(p => p.Id == request.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CostPrice, latest), ct);
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
    public async Task<(StocktakeResult? Result, Dictionary<string, string[]>? Errors)> StocktakeAsync(StocktakeRequest request, Guid userId, CancellationToken ct)
    {
        if (request.Id == Guid.Empty || request.Counts is not { Count: > 0 and <= MaxImportRows })
        {
            return (null, new() { ["Counts"] = ["Send the stocktake's ID and between 1 and 2000 counts."] });
        }

        if (request.Counts.Any(c => c.Counted < 0 || c.Counted > Money.MaxQuantity || decimal.Round(c.Counted, 3) != c.Counted))
        {
            return (null, new() { ["Counts"] = ["Counts must be 0 or more, with at most three decimals."] });
        }

        var ids = request.Counts.Select(c => c.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var adjusted = 0;
        var unchanged = 0;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var count in request.Counts.DistinctBy(c => c.ProductId))
        {
            if (!products.TryGetValue(count.ProductId, out var product))
            {
                continue;
            }

            var movementId = DerivedId(request.Id, count.ProductId);
            if (await db.StockMovements.AnyAsync(m => m.Id == movementId, ct) || count.Counted == product.Stock)
            {
                unchanged++;
                continue;
            }

            await RecordAsync(movementId, product.Id, StockMovementKind.Adjustment, count.Counted - product.Stock,
                StockReason.CountCorrection, null, "Stocktake", userId, ct);
            adjusted++;
        }

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
        var openings = new List<(Guid ProductId, decimal Quantity)>();
        foreach (var row in request.Rows)
        {
            if (!existing.Add(row.Name!.Trim().ToLowerInvariant()))
            {
                skipped++;
                continue;
            }

            var product = NewProduct(Guid.CreateVersion7(), row.Name, row.Category, row.Unit!.Value, row.SellingPrice ?? 0, row.CostPrice, row.ReorderLevel);
            product.CatalogueItemId = row.CatalogueItemId ?? product.CatalogueItemId;
            product.MasterItemId = row.MasterItemId;
            product.PackSize = row.PackSize is > 0 ? row.PackSize : null;
            product.PackUnit = product.PackSize is null ? null : row.PackUnit;
            product.GstRate = row.GstRate;
            product.HsnCode = Clean(row.HsnCode);
            // A SAC code (99…) is a service's: from a master list, a haircut comes in as a service even without the flag.
            (product.IsService, product.DurationMinutes) = (row.IsService || (row.HsnCode?.Trim().StartsWith("99") ?? false), row.DurationMinutes);
            product.Schedule = product.IsService ? null : row.Schedule;
            product.UnitsPerPack = product.IsService ? null : row.UnitsPerPack;
            (product.TracksSerials, product.WarrantyMonths) = product.IsService ? (false, null) : (row.TracksSerials, row.WarrantyMonths);
            db.Products.Add(product);
            if (row.OpeningStock is > 0)
            {
                openings.Add((product.Id, row.OpeningStock.Value));
            }

            created++;
        }

        await db.SaveChangesAsync(ct);
        foreach (var (productId, quantity) in openings)
        {
            await RecordAsync(Guid.CreateVersion7(), productId, StockMovementKind.Opening, quantity, null, null, null, userId, ct);
        }

        await transaction.CommitAsync(ct);
        return (new ImportResult(created, skipped), null);
    }

    /// <summary>Saves a movement and moves the product's stock with it. The caller owns the transaction.</summary>
    private async Task<StockMovement> RecordAsync(
        Guid id, Guid productId, StockMovementKind kind, decimal quantity, StockReason? reason, decimal? costPrice, string? note,
        Guid userId, CancellationToken ct)
    {
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
        return movement;
    }

    /// <summary>Sets many prices at once (today's rates). Only items whose price changed are saved (and audited).</summary>
    public async Task<(int Changed, Dictionary<string, string[]>? Errors)> SetRatesAsync(RatesRequest request, CancellationToken ct)
    {
        if (request.Rates is not { Count: > 0 and <= MaxImportRows }
            || request.Rates.Any(r => r.SellingPrice < 0 || r.SellingPrice > Money.MaxAmount || decimal.Round(r.SellingPrice, 2) != r.SellingPrice))
        {
            return (0, new() { ["Rates"] = ["Enter valid prices."] });
        }

        var ids = request.Rates.Select(r => r.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var changed = 0;
        foreach (var rate in request.Rates)
        {
            if (products.TryGetValue(rate.ProductId, out var product) && product.SellingPrice != rate.SellingPrice)
            {
                product.SellingPrice = rate.SellingPrice;
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
        !p.IsService && (p.Stock <= 0 || (p.ReorderLevel != null && p.Stock <= p.ReorderLevel)), p.Reserved, p.CatalogueItemId, p.PackSize, p.PackUnit, p.IsDefaultOption,
        // The shop's own photo, else its master item's (the server serves whichever it is at the same address).
        p.ImageAt ?? (p.MasterItem != null ? p.MasterItem.ImageAt : null),
        p.GstRate, p.HsnCode, p.IsService, p.DurationMinutes, p.Schedule, p.UnitsPerPack, p.TracksSerials, p.WarrantyMonths, p.Mrp, p.Barcode,
        p.ImageAt != null ? p.ImageCredit : p.MasterItem != null && p.MasterItem.ImageAt != null ? p.MasterItem.ImageCredit ?? MasterPhotos.Representative : null,
        p.PhotoReports, p.PhotoReportNote, p.BillName);

    /// <summary>An MRP only counts when it is above the selling price (otherwise there is nothing "off" to show).</summary>
    private static decimal? CleanMrp(decimal? mrp, decimal sellingPrice) =>
        mrp is { } m && m > sellingPrice && Money.IsValidAmount(m) ? m : null;

    private static StockMovementDto ToDto(StockMovement m) =>
        new(m.Id, m.ProductId, m.Kind, m.Quantity, m.Reason, m.BillId, m.CostPrice, m.Note, m.OccurredAt);
}
