using System.Security.Claims;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>One product a service uses each time (Hair colour: 60 ml colour, 90 ml developer). Billing the service takes it from stock and counts its cost.</summary>
public sealed class ServiceProduct : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid ServiceId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }
}

/// <summary>What the salon knows about a client: hair and skin, allergies, her colour formula and the last patch test.</summary>
public sealed class ClientCard : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public string? HairType { get; set; }

    public string? SkinType { get; set; }

    public string? Allergies { get; set; }

    public string? ColourFormula { get; set; }

    public DateOnly? PatchTestOn { get; set; }

    public bool PatchTestReaction { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record ServiceProductLine(Guid ProductId, decimal Quantity);

public sealed record ServiceProductsRequest(IReadOnlyList<ServiceProductLine>? Products);

public sealed record ServiceProductDto(Guid ProductId, string Name, Unit Unit, decimal Quantity, decimal? CostPrice, decimal Stock);

public sealed record ServiceProductsDto(Guid ServiceId, string Name, decimal Price, IReadOnlyList<ServiceProductDto> Products, decimal Cost);

public sealed record ClientCardRequest(string? HairType, string? SkinType, string? Allergies, string? ColourFormula, DateOnly? PatchTestOn, bool PatchTestReaction, string? Notes);

public sealed record ClientVisitDto(DateOnly Date, string Services, string? StylistName, decimal Total);

/// <summary><see cref="PatchTestDue"/>: no patch test, or the last is over six months old; a colour service needs one 48 hours before.</summary>
public sealed record ClientCardDto(
    Guid CustomerId, string CustomerName, string? HairType, string? SkinType, string? Allergies, string? ColourFormula, DateOnly? PatchTestOn, bool PatchTestReaction, string? Notes,
    bool PatchTestDue, IReadOnlyList<ClientVisitDto> Visits);

public static class SalonCards
{
    public static IEndpointRouteBuilder MapSalonCards(this IEndpointRouteBuilder app)
    {
        var services = app.MapGroup("/api/v1/services").WithTags("Appointments").RequireAuthorization(AppPolicies.ShopMember);
        services.MapGet("/{serviceId:guid}/products", GetProductsAsync);
        services.MapPut("/{serviceId:guid}/products", SetProductsAsync).RequireAuthorization(AppPolicies.ShopOwner);
        var cards = app.MapGroup("/api/v1/customers").WithTags("Appointments").RequireAuthorization(AppPolicies.ShopMember);
        cards.MapGet("/{customerId:guid}/card", GetCardAsync);
        cards.MapPut("/{customerId:guid}/card", SetCardAsync);
        return app;
    }

    private static IResult Invalid(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static async Task<IResult> GetProductsAsync(Guid serviceId, AppDbContext db, CancellationToken ct)
    {
        var service = await db.Products.AsNoTracking().Where(p => p.Id == serviceId && p.IsService).Select(p => new { p.Name, p.SellingPrice }).SingleOrDefaultAsync(ct);
        if (service is null)
        {
            return TypedResults.NotFound();
        }

        var lines = await (from s in db.ServiceProducts.AsNoTracking()
                           join p in db.Products.AsNoTracking() on s.ProductId equals p.Id
                           where s.ServiceId == serviceId
                           orderby p.Name
                           select new ServiceProductDto(p.Id, p.Name, p.Unit, s.Quantity, p.CostPrice, p.Stock)).ToListAsync(ct);
        return TypedResults.Ok(new ServiceProductsDto(serviceId, service.Name, service.SellingPrice, lines, lines.Sum(l => Money.LineAmount(l.Quantity, l.CostPrice ?? 0))));
    }

    /// <summary>Replaces the list of products the service uses. Only stock items (not other services) can be on it.</summary>
    private static async Task<IResult> SetProductsAsync(Guid serviceId, ServiceProductsRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == serviceId && p.IsService, ct))
        {
            return TypedResults.NotFound();
        }

        var lines = request.Products ?? [];
        if (lines.Count > 20 || lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
        {
            return Invalid("Products", "List each product once, up to 20.");
        }

        if (lines.Any(l => l.Quantity <= 0 || l.Quantity > Money.MaxQuantity || decimal.Round(l.Quantity, 3) != l.Quantity))
        {
            return Invalid("Products", "Enter how much of each is used, with at most three decimals.");
        }

        var ids = lines.Select(l => l.ProductId).ToList();
        if (await db.Products.CountAsync(p => ids.Contains(p.Id) && p.IsActive && !p.IsService, ct) != ids.Count)
        {
            return Invalid("Products", "Choose stock items, not services.");
        }

        await db.ServiceProducts.Where(s => s.ServiceId == serviceId).ExecuteDeleteAsync(ct);
        db.ServiceProducts.AddRange(lines.Select(l => new ServiceProduct { ServiceId = serviceId, ProductId = l.ProductId, Quantity = l.Quantity }));
        await db.SaveChangesAsync(ct);
        return await GetProductsAsync(serviceId, db, ct);
    }

    /// <summary>
    /// After a bill: takes each service's products from stock and sets each line's cost, so the day's profit counts what the
    /// salon used up. Items with no cost price still leave stock but cost nothing.
    /// </summary>
    public static async Task UseProductsAsync(AppDbContext db, Guid billId, string note, Guid userId, DateTimeOffset at, CancellationToken ct)
    {
        var lines = await db.BillLines.Where(l => l.BillId == billId && l.ProductId != null).ToListAsync(ct);
        var serviceIds = lines.Select(l => l.ProductId!.Value).Distinct().ToList();
        var recipes = await (from s in db.ServiceProducts.AsNoTracking()
                             join p in db.Products.AsNoTracking() on s.ProductId equals p.Id
                             where serviceIds.Contains(s.ServiceId)
                             select new { s.ServiceId, s.ProductId, s.Quantity, p.CostPrice }).ToListAsync(ct);
        if (recipes.Count == 0)
        {
            return;
        }

        foreach (var line in lines)
        {
            var used = recipes.Where(r => r.ServiceId == line.ProductId).ToList();
            if (used.Count == 0)
            {
                continue;
            }

            foreach (var r in used)
            {
                var quantity = r.Quantity * line.Quantity;
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = r.ProductId, Kind = StockMovementKind.UsedInService, Quantity = -quantity, CostPrice = r.CostPrice, Note = note, OccurredAt = at, CreatedByUserId = userId,
                });
                await db.Products.Where(p => p.Id == r.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), ct);
            }

            line.UnitCost = used.Sum(r => Money.LineAmount(r.Quantity, r.CostPrice ?? 0));
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<IResult> GetCardAsync(Guid customerId, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == customerId).Select(c => new { c.Name }).SingleOrDefaultAsync(ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        var card = await db.ClientCards.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == customerId, ct);
        var stylists = await (from m in db.Memberships.AsNoTracking() join u in db.Users.AsNoTracking() on m.UserId equals u.Id where m.ShopId == tenant.ShopId select new { m.Id, u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var visits = (await db.Appointments.AsNoTracking().Include(a => a.Services)
                .Where(a => a.CustomerId == customerId && a.Status == AppointmentStatus.Done).OrderByDescending(a => a.Date).ThenByDescending(a => a.Start).Take(10).ToListAsync(ct))
            .Select(a => new ClientVisitDto(a.Date, string.Join(", ", a.Services.Select(s => s.Name)), a.StylistId is { } s ? stylists.GetValueOrDefault(s) : null, a.Services.Sum(s => s.Price)))
            .ToList();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        var due = card?.PatchTestOn is not { } on || on < today.AddMonths(-6);
        return TypedResults.Ok(new ClientCardDto(customerId, customer.Name, card?.HairType, card?.SkinType, card?.Allergies, card?.ColourFormula, card?.PatchTestOn,
            card?.PatchTestReaction ?? false, card?.Notes, due, visits));
    }

    private static async Task<IResult> SetCardAsync(Guid customerId, ClientCardRequest request, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            return TypedResults.NotFound();
        }

        if (new[] { request.HairType, request.SkinType }.Any(t => t?.Length > 60) || new[] { request.Allergies, request.ColourFormula, request.Notes }.Any(t => t?.Length > 500))
        {
            return Invalid("Notes", "Keep each note shorter.");
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        if (request.PatchTestOn > today)
        {
            return Invalid("PatchTestOn", "The patch test date can't be in the future.");
        }

        var card = await db.ClientCards.SingleOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (card is null)
        {
            card = new ClientCard { CustomerId = customerId };
            db.ClientCards.Add(card);
        }

        (card.HairType, card.SkinType, card.Allergies, card.ColourFormula, card.PatchTestOn, card.PatchTestReaction, card.Notes, card.UpdatedAt) =
            (Clean(request.HairType), Clean(request.SkinType), Clean(request.Allergies), Clean(request.ColourFormula), request.PatchTestOn, request.PatchTestReaction, Clean(request.Notes), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await GetCardAsync(customerId, db, tenant, clock, ct);
    }
}
