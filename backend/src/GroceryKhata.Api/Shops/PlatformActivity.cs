using System.Text.Json;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>
/// One thing a platform admin did: approved or blocked a shop, gave free access, confirmed or refunded a payment,
/// opened a shop, changed an admin... Written for every successful change under /api/v1/platform, so nothing an admin
/// does is missing from the record. Never edited or deleted.
/// </summary>
public sealed class PlatformActivity : Entity
{
    public Guid AdminUserId { get; set; }

    /// <summary>The method and route: "POST /shops/{id}/block".</summary>
    public required string Action { get; set; }

    /// <summary>The shop it was about, when the route names one.</summary>
    public Guid? ShopId { get; set; }

    /// <summary>The route's other ids and what was sent (reason, months, amount…), as JSON.</summary>
    public required string Detail { get; set; }
}

public sealed record PlatformActivityDto(Guid Id, DateTimeOffset At, string Admin, string Action, Guid? ShopId, string? ShopName, JsonElement Detail);

public static class PlatformActivityLog
{
    /// <summary>Records every successful POST, PUT or DELETE in the group after it has run.</summary>
    public static RouteGroupBuilder RecordsActivity(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            var http = context.HttpContext;
            if (HttpMethods.IsGet(http.Request.Method) || http.User.UserId() is not { } adminId)
            {
                return result;
            }

            var status = result is IStatusCodeHttpResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;
            if (status >= 300)
            {
                return result;
            }

            var pattern = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value ?? "";
            var route = http.Request.RouteValues;
            Guid? shopId = pattern.Contains("/shops/{id", StringComparison.Ordinal) && Guid.TryParse(route["id"]?.ToString(), out var sid) ? sid : null;
            var detail = new Dictionary<string, object?>();
            foreach (var (key, value) in route)
            {
                if (key != "id" || shopId is null)
                {
                    detail[key] = value;
                }
            }

            // What was sent: the request records and plain values, not the services the handler also takes.
            foreach (var argument in context.Arguments)
            {
                if (argument is not null && argument.GetType().Name.EndsWith("Request", StringComparison.Ordinal))
                {
                    detail["body"] = argument;
                }
            }

            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            db.PlatformActivities.Add(new PlatformActivity
            {
                AdminUserId = adminId,
                Action = $"{http.Request.Method} {pattern.Replace("/api/v1/platform", "", StringComparison.Ordinal)}",
                ShopId = shopId,
                Detail = JsonSerializer.Serialize(detail, JsonOptions),
            });
            await db.SaveChangesAsync(http.RequestAborted);
            return result;
        });
        return group;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>Super admins: the latest actions, newest first; optionally one shop's or one admin's.</summary>
    public static async Task<IReadOnlyList<PlatformActivityDto>> ListAsync(Guid? shopId, Guid? adminId, AppDbContext db, CancellationToken ct)
    {
        var query = db.PlatformActivities.AsNoTracking();
        if (shopId is { } s) query = query.Where(a => a.ShopId == s);
        if (adminId is { } u) query = query.Where(a => a.AdminUserId == u);
        var rows = await (from a in query.OrderByDescending(a => a.CreatedAt).Take(200)
                          join user in db.Users.AsNoTracking() on a.AdminUserId equals user.Id
                          join shop in db.Shops.AsNoTracking() on a.ShopId equals (Guid?)shop.Id into shops
                          from shop in shops.DefaultIfEmpty()
                          select new { a.Id, a.CreatedAt, user.FullName, a.Action, a.ShopId, ShopName = shop == null ? null : shop.Name, a.Detail })
            .ToListAsync(ct);
        return rows.Select(r => new PlatformActivityDto(r.Id, r.CreatedAt, r.FullName, r.Action, r.ShopId, r.ShopName, JsonDocument.Parse(r.Detail).RootElement.Clone())).ToList();
    }

}
