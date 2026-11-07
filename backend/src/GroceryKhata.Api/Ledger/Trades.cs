using System.Security.Claims;
using System.Text.Json;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// A delivery challan: goods sent to a contractor's site before the invoice. Stock leaves the shelf now; nothing goes
/// on the khata (and nothing counts as a sale) until challans are made into one invoice. Kept apart from bills so
/// reports never count the same cement twice.
/// </summary>
public sealed class Challan : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid CustomerId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The lines (JSON): product, name, quantity, unit, rate.</summary>
    public required string Lines { get; set; }

    public decimal Total { get; set; }

    public string? Site { get; set; }

    public string? VehicleNumber { get; set; }

    /// <summary>The invoice these goods were billed on. Null: still open.</summary>
    public Guid? InvoiceId { get; set; }

    public Guid CreatedByUserId { get; set; }
}

/// <summary>A painter who brings customers to a paint shop, paid a commission on what they bring.</summary>
public sealed class Painter : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public required string Name { get; set; }

    public string? Phone { get; set; }

    public decimal CommissionPercent { get; set; }
}

public sealed record ChallanRequest(Guid Id, Guid CustomerId, IReadOnlyList<BillLineRequest>? Lines, string? Site, string? VehicleNumber, DateTimeOffset? OccurredAt);

public sealed record ChallanDto(
    Guid Id, Guid CustomerId, DateTimeOffset OccurredAt, IReadOnlyList<BillLineDto> Lines, decimal Total, string? Site, string? VehicleNumber, Guid? InvoiceId);

/// <summary>Make open challans into one invoice (the bill that goes on the khata).</summary>
public sealed record InvoiceChallansRequest(Guid Id, IReadOnlyList<Guid>? ChallanIds, decimal PaidAmount, PaymentMode? PaymentMode);

public sealed record CreditLimitRequest(decimal? CreditLimit);

public sealed record CustomerGstinRequest(string? Gstin);

public sealed record CustomerAddressRequest(string? Address);

public sealed record CustomerDetailsRequest(string? Name, string? Phone);

/// <summary>What a contractor's khata adds up to at one site.</summary>
public sealed record SiteTotal(string Site, int Bills, decimal Billed, int OpenChallans);

public sealed record PainterRequest(Guid Id, string? Name, string? Phone, decimal CommissionPercent);

public sealed record PainterDto(Guid Id, string Name, string? Phone, decimal CommissionPercent);

/// <summary>One painter's month: bills tagged to them, the paint sold on them, and the commission due.</summary>
public sealed record PainterCommission(Guid PainterId, string Name, string? Phone, decimal CommissionPercent, int Bills, decimal Sales, decimal Commission);

/// <summary>
/// The extra features other trades need: delivery challans, site-wise totals and credit limits for contractors
/// (building material), and painters with their commission (paint).
/// </summary>
public static class TradeEndpoints
{
    public static IEndpointRouteBuilder MapTrades(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/v1").WithTags("Trades").RequireAuthorization(AppPolicies.ShopMember);
        // Challans, sites and painters are trade features (Pro); the plan check runs on every call.
        shop.MapGet("/challans", ListChallansAsync).RequireFeature(Billing.Features.Trade);
        shop.MapPost("/challans", AddChallanAsync).RequireFeature(Billing.Features.Trade);
        shop.MapPost("/challans/invoice", InvoiceAsync).RequireFeature(Billing.Features.Trade);
        shop.MapGet("/customers/{id:guid}/sites", SitesAsync);
        shop.MapPut("/customers/{id:guid}/credit-limit", SetLimitAsync).RequireAuthorization(AppPolicies.ShopOwner);
        shop.MapPut("/customers/{id:guid}", SetDetailsAsync).RequireAuthorization(AppPolicies.ShopOwner);
        shop.MapGet("/customers/{id:guid}/gstin", async (Guid id, AppDbContext db, CancellationToken ct) =>
            Results.Ok(new CustomerGstinRequest(await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Gstin).SingleOrDefaultAsync(ct))));
        shop.MapPut("/customers/{id:guid}/gstin", SetGstinAsync).RequireAuthorization(AppPolicies.ShopOwner);
        shop.MapGet("/customers/{id:guid}/address", async (Guid id, AppDbContext db, CancellationToken ct) =>
            Results.Ok(new CustomerAddressRequest(await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Address).SingleOrDefaultAsync(ct))));
        shop.MapPut("/customers/{id:guid}/address", SetAddressAsync).RequireAuthorization(AppPolicies.ShopOwner);
        shop.MapGet("/painters", (AppDbContext db, CancellationToken ct) =>
            db.Painters.AsNoTracking().OrderBy(p => p.Name).Select(p => new PainterDto(p.Id, p.Name, p.Phone, p.CommissionPercent)).ToListAsync(ct));
        shop.MapPost("/painters", AddPainterAsync).RequireAuthorization(AppPolicies.ShopOwner).RequireFeature(Billing.Features.Trade);
        shop.MapGet("/painters/commission", CommissionAsync).RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A customer's open challans (not yet invoiced), or every open one in the shop.</summary>
    private static async Task<IReadOnlyList<ChallanDto>> ListChallansAsync(Guid? customerId, AppDbContext db, CancellationToken ct) =>
        (await db.Challans.AsNoTracking()
            .Where(c => c.InvoiceId == null && (customerId == null || c.CustomerId == customerId))
            .OrderBy(c => c.OccurredAt).ToListAsync(ct))
        .Select(ToDto).ToList();

    private static async Task<IResult> AddChallanAsync(ChallanRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.Challans.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok(ToDto(existing));
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty)
        {
            errors["Id"] = ["The app must send an ID for every new record."];
        }

        if (request.Lines is not { Count: > 0 and <= 200 } lines
            || lines.Any(l => !Money.IsValidQuantity(l.Quantity) || l.UnitPrice < 0 || decimal.Round(l.UnitPrice, 2) != l.UnitPrice))
        {
            errors["Lines"] = ["Add between 1 and 200 items with a quantity and rate."];
        }

        if (request.Site?.Length > 80 || request.VehicleNumber?.Length > 15)
        {
            errors["Site"] = ["Keep the site and vehicle number short."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["CustomerId"] = ["A challan goes to a customer: choose one."] });
        }

        var productIds = request.Lines!.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (productIds.Any(id => !products.ContainsKey(id)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Lines"] = ["Product not found."] });
        }

        var dtoLines = request.Lines!.Select(l =>
        {
            var product = l.ProductId is { } id ? products[id] : null;
            var name = string.IsNullOrWhiteSpace(l.Name) ? product?.Name ?? "Item" : l.Name.Trim()[..Math.Min(l.Name.Trim().Length, 120)];
            return new BillLineDto(l.ProductId, name, l.Quantity, l.Unit ?? product?.Unit ?? Unit.Piece, l.UnitPrice, Money.LineAmount(l.Quantity, l.UnitPrice),
                product?.GstRate, product?.HsnCode);
        }).ToList();

        var now = clock.GetUtcNow();
        var challan = new Challan
        {
            Id = request.Id,
            CustomerId = request.CustomerId,
            OccurredAt = request.OccurredAt is { } at && at <= now ? at.ToUniversalTime() : now,
            Lines = JsonSerializer.Serialize(dtoLines, Json),
            Total = dtoLines.Sum(l => l.Amount),
            Site = string.IsNullOrWhiteSpace(request.Site) ? null : request.Site.Trim(),
            VehicleNumber = string.IsNullOrWhiteSpace(request.VehicleNumber) ? null : request.VehicleNumber.Trim().ToUpperInvariant().Replace(" ", ""),
            CreatedByUserId = user.UserId()!.Value,
        };

        // The goods leave now: one sale movement per product, and the shelf count, in the same transaction.
        var sold = dtoLines.Where(l => l.ProductId is not null).GroupBy(l => l.ProductId!.Value).Select(g => (Id: g.Key, Qty: g.Sum(l => l.Quantity))).OrderBy(x => x.Id).ToList();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Challans.Add(challan);
        foreach (var (productId, quantity) in sold)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = productId, Kind = StockMovementKind.Sale, Quantity = -quantity, OccurredAt = challan.OccurredAt,
                CreatedByUserId = challan.CreatedByUserId, Note = "Delivery challan",
            });
        }

        await db.SaveChangesAsync(ct);
        if (sold.Count > 0)
        {
            var ids = sold.Select(x => x.Id).ToArray();
            var quantities = sold.Select(x => x.Qty).ToArray();
            await db.Database.ExecuteSqlAsync(
                $"UPDATE products AS p SET stock = p.stock - v.q FROM unnest({ids}, {quantities}) AS v(id, q) WHERE p.id = v.id", ct);
        }

        await transaction.CommitAsync(ct);
        return TypedResults.Created($"/api/v1/challans/{challan.Id}", ToDto(challan));
    }

    /// <summary>
    /// One invoice from a customer's open challans: the lines are copied, the total goes on the khata like any bill,
    /// and the stock isn't moved again. Safe to repeat (the invoice's ID).
    /// </summary>
    private static async Task<IResult> InvoiceAsync(InvoiceChallansRequest request, ClaimsPrincipal user, AppDbContext db, LedgerService ledger, CancellationToken ct)
    {
        var challans = request.ChallanIds is { Count: > 0 and <= 100 } ids
            ? await db.Challans.Where(c => ids.Contains(c.Id)).OrderBy(c => c.OccurredAt).ToListAsync(ct)
            : [];
        if (challans.Count == 0 || challans.Count != request.ChallanIds!.Distinct().Count())
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ChallanIds"] = ["Choose the challans to bill."] });
        }

        if (challans.Select(c => c.CustomerId).Distinct().Count() > 1)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["ChallanIds"] = ["An invoice is for one customer: pick challans of one customer."] });
        }

        if (challans.Any(c => c.InvoiceId is not null && c.InvoiceId != request.Id))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Some of these challans are already billed.");
        }

        var challanLines = challans.SelectMany(c => JsonSerializer.Deserialize<List<BillLineDto>>(c.Lines, Json)!).ToList();
        var lines = challanLines.Select(l => new BillLineRequest(l.ProductId, l.Name, l.Quantity, l.Unit, l.UnitPrice)).ToList();
        var sites = challans.Select(c => c.Site).Distinct().ToList();
        var result = await ledger.CreateBillAsync(
            new BillRequest(request.Id, challans[0].CustomerId, lines, request.PaidAmount, request.PaymentMode, null, sites.Count == 1 ? sites[0] : null),
            user.UserId()!.Value, ct, stockAlreadyOut: true, taxSnapshot: challanLines.Select(l => (l.GstRate, l.HsnCode)).ToList());
        if (result.Value is not { } bill)
        {
            return result.ToHttp($"/api/v1/bills/{request.Id}");
        }

        foreach (var challan in challans)
        {
            challan.InvoiceId = bill.Id;
        }

        await db.SaveChangesAsync(ct);
        return result.ToHttp($"/api/v1/bills/{bill.Id}");
    }

    /// <summary>A contractor's khata by site: what was billed at each, and challans still to bill there.</summary>
    private static async Task<IReadOnlyList<SiteTotal>> SitesAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var billed = await db.Bills.AsNoTracking().Where(b => b.CustomerId == id && b.CancelledAt == null)
            .GroupBy(b => b.Site ?? "")
            .Select(g => new { Site = g.Key, Bills = g.Count(), Billed = g.Sum(b => b.Total) })
            .ToListAsync(ct);
        var open = await db.Challans.AsNoTracking().Where(c => c.CustomerId == id && c.InvoiceId == null)
            .GroupBy(c => c.Site ?? "").Select(g => new { Site = g.Key, Count = g.Count() }).ToListAsync(ct);
        return billed.Select(b => b.Site).Union(open.Select(o => o.Site))
            .Select(site => new SiteTotal(site, billed.FirstOrDefault(b => b.Site == site)?.Bills ?? 0, billed.FirstOrDefault(b => b.Site == site)?.Billed ?? 0,
                open.FirstOrDefault(o => o.Site == site)?.Count ?? 0))
            .OrderBy(s => s.Site == "" ? 1 : 0).ThenBy(s => s.Site).ToList();
    }

    private static async Task<IResult> SetLimitAsync(Guid id, CreditLimitRequest request, AppDbContext db, CancellationToken ct)
    {
        if (request.CreditLimit is < 0 || request.CreditLimit > Money.MaxAmount)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["CreditLimit"] = ["Enter a limit in rupees, or leave it empty for none."] });
        }

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        customer.CreditLimit = request.CreditLimit is > 0 ? request.CreditLimit : null;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Balance, customer.CreditLimit));
    }

    /// <summary>A business customer's GSTIN, for their tax invoices. Empty clears it.</summary>
    private static async Task<IResult> SetGstinAsync(Guid id, CustomerGstinRequest request, AppDbContext db, CancellationToken ct)
    {
        var gstin = string.IsNullOrWhiteSpace(request.Gstin) ? null : request.Gstin.Trim().ToUpperInvariant();
        if (gstin is not null && !GstExport.IsValidGstin(gstin))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Gstin"] = ["This GSTIN isn't valid: check each letter and digit (the last one is a check digit)."] });
        }

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        customer.Gstin = gstin;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(request with { Gstin = gstin });
    }

    /// <summary>
    /// Puts right a customer's name or phone. A new number is a new person as far as the khata can tell: the old link
    /// stops and they must confirm again, so a wrong number that once had access keeps none. Both changes are audited.
    /// </summary>
    private static async Task<IResult> SetDetailsAsync(Guid id, CustomerDetailsRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            errors[nameof(request.Name)] = ["Enter the customer's name."];
        }

        var phone = PhoneNumber.Normalize(request.Phone);
        if (!string.IsNullOrWhiteSpace(request.Phone) && phone is null)
        {
            errors[nameof(request.Phone)] = ["Enter a valid mobile number, or leave it empty."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        if (customer.Phone != phone)
        {
            (customer.Phone, customer.PhoneConfirmedAt, customer.ConfirmCodeHash, customer.ConfirmCodeAt, customer.ConfirmCodeTries) = (phone, null, null, null, 0);
            (customer.KhataToken, customer.KhataTokenAt) = (null, null);
        }

        customer.Name = name!;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Balance, customer.CreditLimit));
    }

    /// <summary>The customer's address, for their bills. Empty clears it.</summary>
    private static async Task<IResult> SetAddressAsync(Guid id, CustomerAddressRequest request, AppDbContext db, CancellationToken ct)
    {
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (address?.Length > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Address"] = ["Keep the address under 200 letters."] });
        }

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
        {
            return TypedResults.NotFound();
        }

        customer.Address = address;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(request with { Address = address });
    }

    private static async Task<IResult> AddPainterAsync(PainterRequest request, AppDbContext db, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        var phone = PhoneNumber.Normalize(request.Phone);
        if (name is not { Length: >= 2 and <= 120 } || request.CommissionPercent is < 0 or > 50 || (!string.IsNullOrWhiteSpace(request.Phone) && phone is null))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["Enter the painter's name, a valid phone (or none) and a commission from 0 to 50%."] });
        }

        if (await db.Painters.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.Id, ct) is { } existing)
        {
            return TypedResults.Ok(new PainterDto(existing.Id, existing.Name, existing.Phone, existing.CommissionPercent));
        }

        var painter = new Painter { Id = request.Id, Name = name, Phone = phone, CommissionPercent = request.CommissionPercent };
        db.Painters.Add(painter);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v1/painters/{painter.Id}", new PainterDto(painter.Id, painter.Name, painter.Phone, painter.CommissionPercent));
    }

    /// <summary>Each painter's month: the bills tagged to them and the commission on their total.</summary>
    private static async Task<IResult> CommissionAsync(string? month, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var (year, m) = IndiaTime.ParseMonth(month) ?? (today.Year, today.Month);
        var (start, end) = IndiaTime.Month(year, m);
        var sales = await db.Bills.AsNoTracking()
            .Where(b => b.PainterId != null && b.OccurredAt >= start && b.OccurredAt < end && b.CancelledAt == null)
            .GroupBy(b => b.PainterId!.Value).Select(g => new { PainterId = g.Key, Bills = g.Count(), Sales = g.Sum(b => b.Total) })
            .ToListAsync(ct);
        var painters = await db.Painters.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        return TypedResults.Ok(painters.Select(p =>
        {
            var s = sales.FirstOrDefault(x => x.PainterId == p.Id);
            var total = s?.Sales ?? 0;
            return new PainterCommission(p.Id, p.Name, p.Phone, p.CommissionPercent, s?.Bills ?? 0, total, decimal.Round(total * p.CommissionPercent / 100, 2));
        }).ToList());
    }

    private static ChallanDto ToDto(Challan c) =>
        new(c.Id, c.CustomerId, c.OccurredAt, JsonSerializer.Deserialize<List<BillLineDto>>(c.Lines, Json) ?? [], c.Total, c.Site, c.VehicleNumber, c.InvoiceId);
}
