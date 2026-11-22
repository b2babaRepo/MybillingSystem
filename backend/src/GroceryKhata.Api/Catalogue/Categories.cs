using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Catalogue;

/// <summary>
/// One of the shop's item groups ("Atta &amp; rice"). Every item points at one, so a name is typed once and used the
/// same way on every screen; typing a new name on an item creates it. Names match ignoring case and extra spaces.
/// A merged category is kept (pointing at the one it went into) so a phone that was offline can still sync.
/// <see cref="Product.Category"/> keeps a copy of the name for the many screens that group by it.
/// </summary>
public sealed class Category : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    /// <summary>Lower case, trimmed, single spaces: what makes two spellings the same category.</summary>
    public required string NormalisedName { get; set; }

    /// <summary>The group above it, at most one level ("Grocery" › "Pulses").</summary>
    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? MergedIntoId { get; set; }

    /// <summary>Filled into a new item of this category when the item doesn't say.</summary>
    public decimal? DefaultGstRate { get; set; }

    public string? DefaultHsn { get; set; }

    public Unit? DefaultUnit { get; set; }

    /// <summary>Days of stock the shop wants on hand for this group.</summary>
    public int? TargetCoverDays { get; set; }

    /// <summary>Days held without a sale before an item counts as not selling (stock health). Default 30.</summary>
    public int? SlowAfterDays { get; set; }
}

public sealed record CategoryDto(Guid Id, string Name, Guid? ParentId, int SortOrder, bool IsActive, int Items,
    decimal? DefaultGstRate, string? DefaultHsn, Unit? DefaultUnit, int? TargetCoverDays, int? SlowAfterDays);

public sealed record CategoryRequest(string? Name, Guid? ParentId = null, bool IsActive = true, decimal? DefaultGstRate = null, string? DefaultHsn = null,
    Unit? DefaultUnit = null, int? TargetCoverDays = null, int? SlowAfterDays = null);

public sealed record MergeCategoryRequest(Guid IntoId);

public sealed record CategoryOrderRequest(IReadOnlyList<Guid>? Ids);

public static class Categories
{
    public const int DefaultSlowAfterDays = 30;

    private static readonly decimal[] GstRates = [0m, 0.25m, 3m, 5m, 12m, 18m, 28m, 40m];

    public static IEndpointRouteBuilder MapCategories(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories").WithTags("Items").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/merge", MergeAsync).RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/order", OrderAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>"  Atta   &amp; RICE " → "atta &amp; rice".</summary>
    public static string Normalise(string name) => string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static string Tidy(string name) => string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The category for a typed name: the existing one (following a merge), or a new one made on the spot. Safe when two
    /// items name a new category at once (the insert skips on conflict).
    /// </summary>
    public static async Task<Category?> ResolveAsync(AppDbContext db, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var tidy = Tidy(name);
        tidy = tidy[..Math.Min(60, tidy.Length)];
        var norm = Normalise(tidy);
        var found = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.NormalisedName == norm && c.MergedIntoId == null, ct);
        // A name that was merged away still means the category it went into (a phone that was offline may send it).
        if (found is null && await db.Categories.AsNoTracking().Where(c => c.NormalisedName == norm && c.MergedIntoId != null).Select(c => c.MergedIntoId).FirstOrDefaultAsync(ct) is { } into)
        {
            found = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == into && c.MergedIntoId == null, ct);
        }
        if (found is null)
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO categories (id, shop_id, name, normalised_name, sort_order, is_active, created_at, updated_at)
                VALUES (gen_random_uuid(), NULLIF(current_setting('app.shop_id', true), '')::uuid, {tidy}, {norm},
                        COALESCE((SELECT max(sort_order) + 1 FROM categories), 0), true, now(), now())
                ON CONFLICT (shop_id, normalised_name) WHERE merged_into_id IS NULL DO NOTHING
                """, ct);
            found = await db.Categories.AsNoTracking().SingleAsync(c => c.NormalisedName == norm && c.MergedIntoId == null, ct);
        }

        return found;
    }

    /// <summary>Points the item at the category it names and copies the category's own spelling onto it.</summary>
    public static async Task LinkAsync(AppDbContext db, Product product, CancellationToken ct, Dictionary<string, Category?>? cache = null)
    {
        Category? category;
        var key = product.Category is null ? "" : Normalise(product.Category);
        if (cache is not null && cache.TryGetValue(key, out var hit))
        {
            category = hit;
        }
        else
        {
            category = await ResolveAsync(db, product.Category, ct);
            cache?.TryAdd(key, category);
        }

        (product.CategoryId, product.Category) = (category?.Id, category?.Name);
    }

    private static CategoryDto ToDto(Category c, int items) =>
        new(c.Id, c.Name, c.ParentId, c.SortOrder, c.IsActive, items, c.DefaultGstRate, c.DefaultHsn, c.DefaultUnit, c.TargetCoverDays, c.SlowAfterDays);

    /// <summary>Every category in the shop's order, with how many active items each holds.</summary>
    private static async Task<IReadOnlyList<CategoryDto>> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var counts = await db.Products.AsNoTracking().Where(p => p.IsActive && p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var rows = await db.Categories.AsNoTracking().Where(c => c.MergedIntoId == null).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        return rows.Select(c => ToDto(c, counts.GetValueOrDefault(c.Id))).ToList();
    }

    private static Dictionary<string, string[]> Check(CategoryRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        if (r.Name is null || Tidy(r.Name).Length is < 1 or > 60) errors["Name"] = ["Enter a name up to 60 letters."];
        if (r.DefaultGstRate is { } g && !GstRates.Contains(g)) errors["DefaultGstRate"] = ["Pick a GST rate."];
        if (r.DefaultHsn is { Length: > 0 } h && (h.Trim().Length is < 4 or > 8 || !h.Trim().All(char.IsAsciiDigit))) errors["DefaultHsn"] = ["HSN is 4 to 8 digits."];
        if (r.TargetCoverDays is < 1 or > 365) errors["TargetCoverDays"] = ["Between 1 and 365 days."];
        if (r.SlowAfterDays is < 7 or > 730) errors["SlowAfterDays"] = ["Between 7 and 730 days."];
        if (r.DefaultUnit is { } u && !Enum.IsDefined(u)) errors["DefaultUnit"] = ["Pick a unit."];
        return errors;
    }

    private static void Apply(Category c, CategoryRequest r)
    {
        (c.IsActive, c.DefaultGstRate, c.DefaultUnit, c.TargetCoverDays, c.SlowAfterDays) = (r.IsActive, r.DefaultGstRate, r.DefaultUnit, r.TargetCoverDays, r.SlowAfterDays);
        c.DefaultHsn = string.IsNullOrWhiteSpace(r.DefaultHsn) ? null : r.DefaultHsn.Trim();
    }

    /// <summary>A parent must be another live top-level category, so groups stay two levels deep.</summary>
    private static async Task<string?> ParentProblemAsync(AppDbContext db, Guid? id, Guid? parentId, CancellationToken ct)
    {
        if (parentId is not { } p)
        {
            return null;
        }

        if (p == id || !await db.Categories.AnyAsync(c => c.Id == p && c.MergedIntoId == null && c.ParentId == null, ct)
            || (id is { } self && await db.Categories.AnyAsync(c => c.ParentId == self && c.MergedIntoId == null, ct)))
        {
            return "A group can sit under one top-level group only.";
        }

        return null;
    }

    private static async Task<IResult> CreateAsync(CategoryRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Check(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await ParentProblemAsync(db, null, request.ParentId, ct) is { } problem)
        {
            return Invalid("ParentId", problem);
        }

        var made = (await ResolveAsync(db, request.Name, ct))!;
        var category = await db.Categories.SingleAsync(c => c.Id == made.Id, ct);
        Apply(category, request);
        category.ParentId = request.ParentId;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(category, await db.Products.CountAsync(p => p.IsActive && p.CategoryId == category.Id, ct)));
    }

    /// <summary>Rename (every item follows), move under a group, hide, or set the defaults.</summary>
    private static async Task<IResult> UpdateAsync(Guid id, CategoryRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Check(request) is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.MergedIntoId == null, ct);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        if (await ParentProblemAsync(db, id, request.ParentId, ct) is { } problem)
        {
            return Invalid("ParentId", problem);
        }

        var name = Tidy(request.Name!);
        var norm = Normalise(name);
        if (norm != category.NormalisedName && await db.Categories.AnyAsync(c => c.Id != id && c.NormalisedName == norm && c.MergedIntoId == null, ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "There is already a category with that name: merge into it instead.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        (category.Name, category.NormalisedName, category.ParentId) = (name, norm, request.ParentId);
        Apply(category, request);
        await db.SaveChangesAsync(ct);
        await db.Products.Where(p => p.CategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Category, name), ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(ToDto(category, await db.Products.CountAsync(p => p.IsActive && p.CategoryId == id, ct)));
    }

    /// <summary>Two names for one group: every item moves into the other, and this one is kept only as a pointer.</summary>
    private static async Task<IResult> MergeAsync(Guid id, MergeCategoryRequest request, AppDbContext db, CancellationToken ct)
    {
        var from = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.MergedIntoId == null, ct);
        var into = await db.Categories.SingleOrDefaultAsync(c => c.Id == request.IntoId && c.MergedIntoId == null, ct);
        if (from is null || into is null || from.Id == into.Id)
        {
            return Invalid("IntoId", "Pick another category to merge into.");
        }

        if (into.ParentId == from.Id)
        {
            return Invalid("IntoId", "That group sits under this one: move it out first.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        (from.MergedIntoId, from.IsActive) = (into.Id, false);
        await db.SaveChangesAsync(ct);
        await db.Products.Where(p => p.CategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.CategoryId, into.Id).SetProperty(p => p.Category, into.Name), ct);
        await db.Categories.Where(c => c.ParentId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentId, into.ParentId == null ? into.Id : into.ParentId), ct);
        await db.Categories.Where(c => c.MergedIntoId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.MergedIntoId, into.Id), ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>The shop's own order for the chips and lists: the ids top to bottom.</summary>
    private static async Task<IResult> OrderAsync(CategoryOrderRequest request, AppDbContext db, CancellationToken ct)
    {
        var ids = request.Ids ?? [];
        if (ids.Count is 0 or > 1000 || ids.Distinct().Count() != ids.Count)
        {
            return Invalid("Ids", "Send each category once.");
        }

        var rows = await db.Categories.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        foreach (var c in rows)
        {
            c.SortOrder = ids.ToList().IndexOf(c.Id);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
