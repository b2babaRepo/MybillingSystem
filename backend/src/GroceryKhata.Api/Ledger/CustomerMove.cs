using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GroceryKhata.Api.Ledger;

/// <summary>The customer a "Name later" khata turned out to be.</summary>
public sealed record MoveCustomerRequest(Guid CustomerId);

/// <summary>
/// Moves a "Name later" khata into the customer it turned out to be: every bill, khata line and anything else that names
/// it now names them, its balance is added to theirs, and the placeholder goes. Only placeholders can be moved, so two
/// real customers are never folded together by mistake.
/// </summary>
public static class CustomerMove
{
    public static IEndpointRouteBuilder MapCustomerMove(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/customers/{id:guid}/move-into", MoveAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> MoveAsync(Guid id, MoveCustomerRequest request, AppDbContext db, CancellationToken ct)
    {
        if (id == request.CustomerId)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.CustomerId)] = ["Pick a different customer."] });
        }

        var from = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (from is null)
        {
            return Results.NotFound();
        }

        if (!from.NameLater)
        {
            return Results.Conflict(new { detail = "Only a \"Name later\" khata can be moved into another customer." });
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.CustomerId)] = ["Customer not found."] });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Every table that points at a customer: found from the model, so a new one is never missed.
        foreach (var type in db.Model.GetEntityTypes())
        {
            var property = type.FindProperty("CustomerId");
            var table = type.GetTableName();
            if (property is null || table is null || type.ClrType == typeof(Customer) || (property.ClrType != typeof(Guid) && property.ClrType != typeof(Guid?)))
            {
                continue;
            }

            var column = property.GetColumnName(StoreObjectIdentifier.Table(table, type.GetSchema()));
            if (column is null)
            {
                continue;
            }

            var target = type.GetSchema() is { } schema ? $"\"{schema}\".\"{table}\"" : $"\"{table}\"";
            // Table and column names come from the EF model, never from the request; the ids are parameters.
#pragma warning disable EF1003
            await db.Database.ExecuteSqlRawAsync("UPDATE " + target + " SET \"" + column + "\" = {0} WHERE \"" + column + "\" = {1}", [request.CustomerId, id], ct);
#pragma warning restore EF1003
        }

        // Re-read inside the transaction: a payment may have landed since.
        var balance = await db.Customers.Where(c => c.Id == id).Select(c => c.Balance).SingleAsync(ct);
        await db.Customers.Where(c => c.Id == request.CustomerId).ExecuteUpdateAsync(u => u.SetProperty(c => c.Balance, c => c.Balance + balance), ct);
        await db.Customers.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);

        var to = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == request.CustomerId, ct);
        return Results.Ok(new CustomerDto(to.Id, to.Name, to.Phone, to.Balance, to.CreditLimit, to.NameLater));
    }
}
