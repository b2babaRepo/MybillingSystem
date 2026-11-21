using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Ledger;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

public sealed record MyBookingRequest(Guid Id, DateOnly Date, TimeOnly? Start, IReadOnlyList<Guid>? ServiceIds, string? Note);

/// <summary>
/// A salon visit of the signed-in person: <see cref="Upcoming"/> when still booked from today on, else their latest
/// visit that was done (for "book again").
/// </summary>
public sealed record MyVisitDto(Guid Id, Guid ShopId, string ShopName, string? ShopSlug, DateOnly Date, TimeOnly Start, TimeOnly End,
    AppointmentStatus Status, IReadOnlyList<AppointmentServiceDto> Services, decimal Total, bool Upcoming);

/// <summary>
/// The customer side of salon appointments: book a visit from the salon's page (it lands in the salon's day like a
/// phoned-in booking, for any stylist), see what's coming and cancel it, and book the last visit's services again.
/// </summary>
public static class MySalon
{
    /// <summary>How far ahead a customer can book from the app.</summary>
    public const int MaxDaysAhead = 30;

    /// <summary>The note the salon sees on a booking made in the app.</summary>
    public const string FromApp = "Booked in the app";

    public static IEndpointRouteBuilder MapMySalon(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/v1/me/salon").WithTags("Customer").RequireAuthorization();
        me.MapGet("/visits", VisitsAsync);
        me.MapPost("/{shopId:guid}/appointments", BookAsync).RequireRateLimiting(StoreEndpoints.StoreRateLimit);
        me.MapPost("/{shopId:guid}/appointments/{id:guid}/cancel", CancelAsync);
        return app;
    }

    private sealed record Me(Guid Id, string Name, string Phone);

    private static Task<Me?> MeAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == user.UserId() && u.PhoneVerifiedAt != null && u.Phone != null)
            .Select(u => new Me(u.Id, u.FullName, u.Phone!)).SingleOrDefaultAsync(ct);

    private static IQueryable<Shops.Shop> Salons(AppDbContext db) =>
        db.Shops.AsNoTracking().Where(s => s.IsActive && (s.Type == Shops.ShopType.Beauty || s.ExtraTypes.Contains("beauty")));

    private static async Task<IResult> BookAsync(Guid shopId, MyBookingRequest request, ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(user, db, ct) is not { } me)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Phone"] = ["Confirm your mobile number to book."] });
        }

        if (!await Salons(db).AnyAsync(s => s.Id == shopId, ct))
        {
            return TypedResults.NotFound();
        }

        if (request.Date > IndiaTime.Today(clock).AddDays(MaxDaysAhead))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Date"] = [$"Book up to {MaxDaysAhead} days ahead."] });
        }

        // Booked as the salon, through the same rules as a booking the salon makes (times, services, no stylist twice).
        await using var scope = scopes.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.SetShop(shopId);
        var shopDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customerId = await shopDb.Customers.AsNoTracking().Where(c => c.Phone == me.Phone).OrderBy(c => c.CreatedAt).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        var note = string.IsNullOrWhiteSpace(request.Note) ? FromApp : $"{FromApp}: {request.Note.Trim()}";
        var result = await Appointments.CreateAsync(
            new AppointmentRequest(request.Id, request.Date, request.Start, null, customerId, me.Name, me.Phone, request.ServiceIds, note[..Math.Min(note.Length, 200)]),
            shopDb, tenant, clock, ct);
        if (result is not Microsoft.AspNetCore.Http.HttpResults.Ok<AppointmentDto>)
        {
            return result;
        }

        // Booked here: the salon joins the person's shops, so the visit shows on Home and "book again" works.
        if (!await db.SavedShops.AnyAsync(x => x.UserId == me.Id && x.ShopId == shopId, ct))
        {
            db.SavedShops.Add(new SavedShop { UserId = me.Id, ShopId = shopId });
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.NoContent();
    }

    /// <summary>The person's booked visits from today on, and their latest done visit, at every salon they've been to or saved.</summary>
    private static async Task<IReadOnlyList<MyVisitDto>> VisitsAsync(ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, TimeProvider clock, CancellationToken ct)
    {
        if (await MeAsync(user, db, ct) is not { } me)
        {
            return [];
        }

        var saved = db.SavedShops.Where(x => x.UserId == me.Id).Select(x => x.ShopId);
        var known = db.Customers.IgnoreQueryFilters().Where(c => c.Phone == me.Phone).Select(c => c.ShopId);
        var salons = await Salons(db).Where(s => saved.Contains(s.Id) || known.Contains(s.Id)).Select(s => new { s.Id, s.Name, s.Slug }).ToListAsync(ct);
        var today = IndiaTime.Today(clock);
        var since = today.AddDays(-365);
        var result = new List<MyVisitDto>();
        foreach (var s in salons)
        {
            var mine = await CustomerExtras.ReadAsShopAsync(scopes, s.Id, shopDb => shopDb.Appointments.AsNoTracking().Include(a => a.Services)
                .Where(a => a.Phone == me.Phone && a.Date >= since).OrderByDescending(a => a.Date).ThenByDescending(a => a.Start).Take(50).ToListAsync(ct));
            var visits = mine.Where(a => a.Status == AppointmentStatus.Booked && a.Date >= today).Select(a => (a, true))
                .Concat(mine.Where(a => a.Status == AppointmentStatus.Done).Take(1).Select(a => (a, false)));
            result.AddRange(visits.Select(v => new MyVisitDto(v.a.Id, s.Id, s.Name, s.Slug, v.a.Date, v.a.Start, v.a.End, v.a.Status,
                v.a.Services.OrderBy(x => x.LineNo).Select(x => new AppointmentServiceDto(x.ProductId, x.Name, x.Price, x.Minutes)).ToList(), v.a.Services.Sum(x => x.Price), v.Item2)));
        }

        return result.OrderByDescending(v => v.Upcoming).ThenBy(v => v.Upcoming ? v.Date.DayNumber : -v.Date.DayNumber).ThenBy(v => v.Start).ToList();
    }

    /// <summary>The person cancels their own visit that is still booked.</summary>
    private static async Task<IResult> CancelAsync(Guid shopId, Guid id, ClaimsPrincipal user, AppDbContext db, IServiceScopeFactory scopes, CancellationToken ct)
    {
        if (await MeAsync(user, db, ct) is not { } me)
        {
            return TypedResults.NotFound();
        }

        var changed = await CustomerExtras.ReadAsShopAsync(scopes, shopId, shopDb => shopDb.Appointments
            .Where(a => a.Id == id && a.Phone == me.Phone && a.Status == AppointmentStatus.Booked)
            .ExecuteUpdateAsync(x => x.SetProperty(a => a.Status, AppointmentStatus.Cancelled), ct));
        return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}
