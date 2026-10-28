using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// The shop's side of a household list: the inbox, packing (weigh, price, substitute, remove), and delivery,
/// which turns the order into an ordinary bill, so anything unpaid lands on the customer's khata.
/// </summary>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrders(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders").WithTags("List inbox").RequireAuthorization(AppPolicies.ShopMember);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/{id:guid}/pack", PackAsync);
        group.MapPost("/{id:guid}/deliver", DeliverAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync);
        return app;
    }

    private static async Task<IReadOnlyList<OrderDto>> ListAsync(string? status, AppDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.ShopId == tenant.ShopId);
        // Query strings carry the JSON spelling ("placed"), which enum binding wouldn't accept.
        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var wanted))
        {
            query = query.Where(o => o.Status == wanted);
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(ct);
        return await PlannerEndpoints.WithShopNamesAsync(db, orders, ct);
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await FindAsync(db, tenant, id, ct, tracked: false) is { } order
            ? TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0])
            : TypedResults.NotFound();

    /// <summary>Sets what was actually packed and its price. Every line must be answered: packed or removed.</summary>
    private static async Task<IResult> PackAsync(Guid id, PackRequest request, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var order = await FindAsync(db, tenant, id, ct, tracked: true);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status is not (OrderStatus.Placed or OrderStatus.Packed))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This order is already delivered or cancelled.");
        }

        var answers = (request.Lines ?? []).ToDictionary(l => l.Id);
        // The shop may pack another of its options than the one on the line (another brand, another pack size).
        var swaps = answers.Values.Where(a => a.ProductId is not null).Select(a => a.ProductId!.Value).ToList();
        var options = await db.Products.AsNoTracking().Where(p => swaps.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var errors = new Dictionary<string, string[]>();
        foreach (var line in order.Lines)
        {
            var field = $"Lines[{line.LineNo}]";
            if (!answers.TryGetValue(line.Id, out var answer))
            {
                errors[field] = [$"Say whether \"{line.Name}\" was packed or removed."];
                continue;
            }

            if (answer.ShopNote?.Length > 200)
            {
                errors[field] = ["Keep the note under 200 characters."];
                continue;
            }

            line.Removed = answer.Removed;
            line.ShopNote = string.IsNullOrWhiteSpace(answer.ShopNote) ? null : answer.ShopNote.Trim();
            if (!answer.Removed && answer.ProductId is { } swap && swap != line.ProductId)
            {
                if (!options.TryGetValue(swap, out var option))
                {
                    errors[field] = ["That item is not in your shop."];
                    continue;
                }

                line.ProductId = option.Id;
                line.Name = option.Name;
                line.Unit = option.Unit;
            }
            if (answer.Removed)
            {
                line.PackedQuantity = null;
                line.UnitPrice = null;
                continue;
            }

            var quantity = answer.PackedQuantity ?? line.Quantity;
            if (!Money.IsValidQuantity(quantity))
            {
                errors[field] = ["Enter the quantity packed."];
            }
            else if (answer.UnitPrice is not { } price || price < 0 || price > Money.MaxAmount || decimal.Round(price, 2) != price)
            {
                errors[field] = [$"Enter the price of \"{line.Name}\"."];
            }
            else
            {
                line.PackedQuantity = quantity;
                line.UnitPrice = price;
            }

            var (needsApproval, error) = OrderLines.Check(line);
            if (error is not null)
            {
                errors[field] = [error];
            }

            line.Approval = needsApproval ? LineApproval.Pending : LineApproval.None;
        }

        if (errors.Count == 0 && order.Lines.All(l => l.Removed))
        {
            errors["Lines"] = ["Pack at least one item, or cancel the order."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        order.FinalAmount = FinalAmount(order);
        order.Status = OrderStatus.Packed;
        order.PackedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
    }

    /// <summary>
    /// Hands the order over. Makes a bill under the order's own ID (so a retry finds it), linked to the customer's
    /// khata by phone: a new khata customer is opened if this is their first time. Unpaid amount goes on khata.
    /// </summary>
    private static async Task<IResult> DeliverAsync(
        Guid id, DeliverRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, LedgerService ledger,
        TimeProvider clock, CancellationToken ct)
    {
        var order = await FindAsync(db, tenant, id, ct, tracked: true);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status == OrderStatus.Delivered)
        {
            return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
        }

        if (order.Status != OrderStatus.Packed)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Pack the order before delivering it.");
        }

        if (order.Lines.Any(l => l.Approval == LineApproval.Pending))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Waiting for the customer to OK the changed items.");
        }

        var paid = request.PaidAmount ?? (order.Payment == OrderPayment.Khata ? 0m : order.FinalAmount!.Value);
        var mode = request.PaymentMode ?? (order.Payment == OrderPayment.Upi ? PaymentMode.Upi : PaymentMode.Cash);

        var customerId = await db.Customers.Where(c => c.Phone == order.CustomerPhone).OrderBy(c => c.CreatedAt)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        if (customerId is null)
        {
            var created = await ledger.CreateCustomerAsync(new CustomerRequest(Guid.CreateVersion7(), order.CustomerName, order.CustomerPhone), ct);
            customerId = created.Value?.Id;
        }

        var bill = await ledger.CreateBillAsync(
            new BillRequest(
                order.Id,
                customerId,
                order.Lines.Where(l => !l.Removed).OrderBy(l => l.LineNo)
                    .Select(l => new BillLineRequest(l.ProductId, l.Name, l.PackedQuantity!.Value, l.Unit, l.UnitPrice!.Value)).ToList(),
                paid,
                mode,
                clock.GetUtcNow()),
            user.UserId()!.Value,
            ct);
        if (bill.Value is null)
        {
            return bill.ToHttp($"/api/v1/bills/{order.Id}");
        }

        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        order = (await FindAsync(db, tenant, id, ct, tracked: true))!;
        order.BillId = bill.Value.Id;
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = clock.GetUtcNow();
        // The bill has taken the real stock off the shelf; the hold placed at ordering goes, in the same step.
        await ReleaseReservationsAsync(db, order, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
    }

    private static async Task<IResult> CancelAsync(Guid id, AppDbContext db, ITenantContext tenant, TimeProvider clock, CancellationToken ct)
    {
        var order = await FindAsync(db, tenant, id, ct, tracked: true);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (order.Status == OrderStatus.Delivered)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "A delivered order can't be cancelled; reverse its bill instead.");
        }

        if (order.Status != OrderStatus.Cancelled)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = clock.GetUtcNow();
            await ReleaseReservationsAsync(db, order, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return TypedResults.Ok((await PlannerEndpoints.WithShopNamesAsync(db, [order], ct))[0]);
    }

    /// <summary>
    /// Gives back the stock an order's lines were holding (store orders reserve at checkout). Each line's hold is
    /// zeroed as it is released, so calling this twice for one order releases once.
    /// </summary>
    private static async Task ReleaseReservationsAsync(AppDbContext db, Order order, CancellationToken ct)
    {
        foreach (var line in order.Lines.Where(l => l.ProductId is not null && l.ReservedQuantity > 0))
        {
            var held = line.ReservedQuantity;
            await db.Products.Where(p => p.Id == line.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Reserved, p => p.Reserved - held), ct);
            line.ReservedQuantity = 0;
        }
    }

    internal static decimal FinalAmount(Order order) =>
        order.Lines.Where(l => !l.Removed).Sum(l => Money.LineAmount(l.PackedQuantity!.Value, l.UnitPrice!.Value));

    /// <summary>Only this shop's orders: the shop's staff never see what a household sent elsewhere.</summary>
    private static Task<Order?> FindAsync(AppDbContext db, ITenantContext tenant, Guid id, CancellationToken ct, bool tracked)
    {
        var query = db.Orders.Include(o => o.Lines).Where(o => o.Id == id && o.ShopId == tenant.ShopId);
        return (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }
}
