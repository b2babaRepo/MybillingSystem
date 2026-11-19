using System.Security.Claims;

namespace GroceryKhata.Api.Identity;

/// <summary>
/// The shop's accountant (CA) reads and never writes: any change they send is refused here, before it reaches an
/// endpoint, so no screen or endpoint can forget the rule. Signing in and out still work.
/// </summary>
public sealed class ReadOnlyAccess(RequestDelegate next)
{
    public static bool IsRead(string method) => HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    public static bool IsAccountant(ClaimsPrincipal user) => user.HasClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Accountant));

    public async Task InvokeAsync(HttpContext http)
    {
        if (!IsRead(http.Request.Method) && IsAccountant(http.User) && !http.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            await TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Accountant access is read-only.",
                detail: "Ask the shop owner to make this change.", type: "read-only").ExecuteAsync(http);
            return;
        }

        await next(http);
    }
}
