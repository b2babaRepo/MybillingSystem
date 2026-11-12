using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

public static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedger(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Khata").RequireAuthorization(AppPolicies.ShopMember);

        group.MapGet("/customers", (string? search, LedgerService ledger, CancellationToken ct) => ledger.ListCustomersAsync(search, ct));

        group.MapPost("/customers", async (CustomerRequest request, LedgerService ledger, CancellationToken ct) =>
            (await ledger.CreateCustomerAsync(request, ct)).ToHttp($"/api/v1/customers/{request.Id}/statement"))
            .AddEndpointFilter(Billing.PlanGate.CustomerLimitAsync);

        group.MapGet("/customers/{id:guid}/statement", async (Guid id, string? month, LedgerService ledger, CancellationToken ct) =>
        {
            var period = IndiaTime.ParseMonth(month);
            if (month is not null && period is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Use a month like 2026-10."] });
            }

            return await ledger.GetStatementAsync(id, period, ct) is { } statement ? Results.Ok(statement) : Results.NotFound();
        });

        group.MapGet("/bills/{id:guid}", async (Guid id, LedgerService ledger, CancellationToken ct) =>
            await ledger.GetBillAsync(id, ct) is { } bill ? Results.Ok(bill) : Results.NotFound());

        group.MapPost("/khata-entries", async (KhataEntryRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.AddKhataEntryAsync(request, user.UserId()!.Value, ct, IsOwner(user))).ToHttp($"/api/v1/customers/{request.CustomerId}/statement"));

        group.MapPost("/ledger-entries/{id:guid}/reverse",
            async (Guid id, ReverseRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
                (await ledger.ReverseAsync(id, request, user.UserId()!.Value, IsOwner(user), ct)).ToHttp($"/api/v1/ledger-entries/{request.Id}"));

        group.MapPost("/bills", async (BillRequest request, ClaimsPrincipal user, LedgerService ledger, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var result = await ledger.CreateBillAsync(request, user.UserId()!.Value, ct, isOwner: IsOwner(user));
            if (result.Value is not null && request.GodownId is { } godown && await Catalogue.Godowns.IsActiveAsync(db, godown, ct))
            {
                // Sold from a godown: what the bill took leaves the godown (as much as it holds), not the shelf. Same
                // id as the bill, so a retried bill moves nothing twice.
                var sold = await db.StockMovements.AsNoTracking().Where(m => m.BillId == request.Id && m.Kind == Catalogue.StockMovementKind.Sale)
                    .Select(m => new { m.ProductId, m.Quantity }).ToListAsync(ct);
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await Catalogue.Godowns.MoveForAsync(db, request.Id, godown, null, sold.Select(m => (m.ProductId, -m.Quantity)), "Sold from the godown on a bill", user.UserId()!.Value, clock.GetUtcNow(), ct);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }

            return result.ToHttp($"/api/v1/bills/{request.Id}");
        });

        group.MapPost("/bills/{id:guid}/attach-customer", async (Guid id, AttachCustomerRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.AttachCustomerAsync(id, request, user.UserId()!.Value, IsOwner(user), ct)).ToHttp($"/api/v1/bills/{id}"));

        group.MapPost("/bills/{id:guid}/cancel", async (Guid id, CancelBillRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.CancelBillAsync(id, request, user.UserId()!.Value, ct)).ToHttp($"/api/v1/bills/{id}"))
            .RequireAuthorization(AppPolicies.ShopOwner);

        group.MapGet("/ledger/reconcile", (LedgerService ledger, CancellationToken ct) => ledger.ReconcileAsync(ct))
            .RequireAuthorization(AppPolicies.ShopOwner);

        return app;
    }

    internal static bool IsOwner(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner));
}

