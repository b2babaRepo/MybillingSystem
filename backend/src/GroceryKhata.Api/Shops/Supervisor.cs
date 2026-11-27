using System.Collections.Concurrent;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Shops;

/// <summary>
/// A supervisor's approval at the counter: a helper the owner made a supervisor types their own counter PIN (sent in
/// <see cref="Header"/>) to let a cashier cancel a bill, take goods back, or sell below the helper's discount limit.
/// The approver is kept on what they approved. Five wrong PINs lock approvals in the shop for 10 minutes.
/// </summary>
public static class Supervisor
{
    public const string Header = "X-Supervisor-Pin";
    public const string ProblemType = "supervisor-pin";
    private const string ApprovedKey = "supervisor-approved-by";
    private const int MaxTries = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<Guid, (int Tries, DateTimeOffset Until)> Wrong = new();

    /// <summary>The supervisor whose PIN came with the request, or null (none sent, wrong, or locked).</summary>
    public static async Task<Guid?> ApproverAsync(HttpContext http)
    {
        if (http.Items[ApprovedKey] is Guid already)
        {
            return already;
        }

        var pin = http.Request.Headers[Header].ToString();
        if (string.IsNullOrEmpty(pin))
        {
            return null;
        }

        var services = http.RequestServices;
        var tenant = services.GetRequiredService<ITenantContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        if (Wrong.TryGetValue(tenant.ShopId, out var wrong) && wrong.Tries >= MaxTries && wrong.Until > now)
        {
            return null;
        }

        var db = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var supervisors = await (from m in db.Memberships.AsNoTracking()
                                 join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                                 where m.ShopId == tenant.ShopId && m.IsActive && m.IsSupervisor && m.PinHash != null
                                 select new { u, m.PinHash }).ToListAsync(http.RequestAborted);
        var match = supervisors.FirstOrDefault(s => hasher.VerifyHashedPassword(s.u, s.PinHash!, pin) != PasswordVerificationResult.Failed);
        if (match is null)
        {
            Wrong.AddOrUpdate(tenant.ShopId, (1, now + LockFor), (_, w) => (w.Until > now ? w.Tries + 1 : 1, now + LockFor));
            return null;
        }

        Wrong.TryRemove(tenant.ShopId, out _);
        http.Items[ApprovedKey] = match.u.Id;
        return match.u.Id;
    }

    /// <summary>The owner, or a helper with a supervisor's PIN: otherwise 403 asking for one.</summary>
    public static RouteHandlerBuilder RequireOwnerOrSupervisor(this RouteHandlerBuilder builder) => builder.AddEndpointFilter(CheckAsync);

    public static RouteGroupBuilder RequireOwnerOrSupervisor(this RouteGroupBuilder builder) => builder.AddEndpointFilter(CheckAsync);

    /// <summary>A helper's action a supervisor approved: the shop's bill passcode isn't asked again.</summary>
    public static bool Approved(HttpContext http) => http.Items[ApprovedKey] is Guid;

    private static async ValueTask<object?> CheckAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || Ledger.LedgerEndpoints.IsOwner(http.User) || await ApproverAsync(http) is not null)
        {
            return await next(context);
        }

        var sent = !string.IsNullOrEmpty(http.Request.Headers[Header].ToString());
        return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, type: ProblemType, title: "Supervisor needed",
            detail: sent ? "That supervisor PIN is wrong." : "A supervisor must approve this: enter their PIN.");
    }
}
