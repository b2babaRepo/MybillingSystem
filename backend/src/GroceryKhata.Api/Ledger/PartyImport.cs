using System.Security.Claims;
using GroceryKhata.Api.Billing;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary><see cref="Balance"/>: what they owe the shop today; below zero, an advance the shop holds for them.</summary>
public sealed record PartyRow(string? Name, string? Phone, decimal Balance);

public sealed record PartyImportRequest(IReadOnlyList<PartyRow>? Rows);

/// <summary><see cref="Problems"/>: the rows left out and why, by their place in what was sent.</summary>
public sealed record PartyImportResult(int Created, int Skipped, decimal TotalOwed, IReadOnlyList<string> Problems);

/// <summary>
/// Bringing a shop's customers and what they owe from another app or a notebook. A customer already here is left
/// alone, balance and all, so importing twice changes nothing. A balance goes in as an Opening entry: it stays out of
/// the day's udhaar and sales, and the customer's statement shows where it came from.
/// </summary>
public static class PartyImport
{
    public const int MaxRows = 2000;

    public static IEndpointRouteBuilder MapPartyImport(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/customers/import", ImportAsync).WithTags("Khata").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    private static async Task<IResult> ImportAsync(
        PartyImportRequest request, ClaimsPrincipal user, AppDbContext db, ITenantContext tenant, Entitlements entitlements, TimeProvider clock, CancellationToken ct)
    {
        if (request.Rows is not { Count: > 0 and <= MaxRows } rows)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Rows"] = [$"Send between 1 and {MaxRows} customers."] });
        }

        var known = await db.Customers.AsNoTracking().Select(c => new { c.Name, c.Phone }).ToListAsync(ct);
        var phones = known.Where(c => c.Phone is not null).Select(c => c.Phone!).ToHashSet();
        var names = known.Select(c => c.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var phonelessNames = known.Where(c => c.Phone is null).Select(c => c.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var problems = new List<string>();
        var created = new List<(Customer Customer, decimal Balance)>();
        var skipped = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = row.Name?.Trim();
            var phone = PhoneNumber.Normalize(row.Phone);
            if (string.IsNullOrEmpty(name) || name.Length > 120)
            {
                problems.Add($"Row {i + 1}: the name is missing or too long.");
            }
            else if (!string.IsNullOrWhiteSpace(row.Phone) && phone is null)
            {
                problems.Add($"Row {i + 1} ({name}): the phone number isn't valid.");
            }
            else if (row.Balance != 0 && !Money.IsValidAmount(Math.Abs(row.Balance)))
            {
                problems.Add($"Row {i + 1} ({name}): the balance isn't a valid amount.");
            }
            // The same name with two different phones is two people; a phone settles it, a bare name can't.
            else if (phone is not null ? phones.Contains(phone) || phonelessNames.Contains(name) : names.Contains(name))
            {
                skipped++;
            }
            else
            {
                created.Add((new Customer { Id = Guid.CreateVersion7(), Name = name, Phone = phone, Balance = row.Balance }, row.Balance));
                names.Add(name);
                if (phone is not null) phones.Add(phone);
                else phonelessNames.Add(name);
            }
        }

        var limit = (await entitlements.ForShopAsync(tenant.ShopId, ct)).MaxCustomers;
        if (limit is { } max && known.Count + created.Count > max)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: $"Your plan allows {max} customers, and this would make {known.Count + created.Count}.",
                detail: "Upgrade for unlimited customers, or import fewer.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Customers.AddRange(created.Select(c => c.Customer));
        await db.SaveChangesAsync(ct);
        var userId = user.UserId()!.Value;
        var now = clock.GetUtcNow();
        db.LedgerEntries.AddRange(created.Where(c => c.Balance != 0).Select(c => new LedgerEntry
        {
            Id = Guid.CreateVersion7(), CustomerId = c.Customer.Id, Kind = LedgerEntryKind.Opening, Amount = c.Balance,
            Note = "Opening balance", OccurredAt = now, CreatedByUserId = userId,
        }));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(new PartyImportResult(created.Count, skipped, created.Where(c => c.Balance > 0).Sum(c => c.Balance), problems));
    }
}
