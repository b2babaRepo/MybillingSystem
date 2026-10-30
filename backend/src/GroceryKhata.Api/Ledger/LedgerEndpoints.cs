using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;

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
            (await ledger.AddKhataEntryAsync(request, user.UserId()!.Value, ct)).ToHttp($"/api/v1/customers/{request.CustomerId}/statement"));

        group.MapPost("/ledger-entries/{id:guid}/reverse",
            async (Guid id, ReverseRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
                (await ledger.ReverseAsync(id, request, user.UserId()!.Value, user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner)), ct)).ToHttp($"/api/v1/ledger-entries/{request.Id}"));

        group.MapPost("/bills", async (BillRequest request, ClaimsPrincipal user, LedgerService ledger, CancellationToken ct) =>
            (await ledger.CreateBillAsync(request, user.UserId()!.Value, ct)).ToHttp($"/api/v1/bills/{request.Id}"));

        group.MapGet("/ledger/reconcile", (LedgerService ledger, CancellationToken ct) => ledger.ReconcileAsync(ct))
            .RequireAuthorization(AppPolicies.ShopOwner);

        return app;
    }
}
