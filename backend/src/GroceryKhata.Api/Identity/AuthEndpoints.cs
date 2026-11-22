using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GroceryKhata.Api.Identity;

/// <summary>
/// Sign-up (opens a shop) and sign-in by phone and password → a shop token straight away when the user
/// has one shop, otherwise a user token + memberships → select-shop. Refresh rotates the refresh token;
/// sign-out revokes it.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Sign-in");

        group.MapPost("/sign-up", SignUpAsync).AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit);
        group.MapPost("/register", RegisterAsync).AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit);
        group.MapPost("/sign-in", SignInAsync).AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit);
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit);
        group.MapPost("/sign-out", SignOutAsync).AllowAnonymous().RequireRateLimiting(AuthSetup.AuthRateLimit); // the access token may have expired
        group.MapPost("/select-shop", SelectShopAsync).RequireAuthorization();
        group.MapGet("/me", GetMeAsync).RequireAuthorization(AppPolicies.AnySession);

        return app;
    }

    /// <summary>The one membership a PIN sign-in may see; null for a real sign-in.</summary>
    private static Guid? PinMembership(ClaimsPrincipal user) =>
        user.HasClaim(c => c.Type == AppClaims.PinOnly) && Guid.TryParse(user.FindFirst(AppClaims.MembershipId)?.Value, out var id) ? id : null;

    private static async Task<IResult> SignUpAsync(SignUpRequest request, AuthService auth, CancellationToken cancellationToken)
    {
        if (request.Validate() is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return ToResult(await auth.SignUpAsync(request, cancellationToken));
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, AuthService auth, CancellationToken cancellationToken)
    {
        if (request.Validate() is { Count: > 0 } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return ToResult(await auth.RegisterAsync(request, cancellationToken));
    }

    private static async Task<IResult> SignInAsync(SignInRequest request, AuthService auth, CancellationToken cancellationToken) =>
        ToResult(await auth.SignInAsync(request, cancellationToken));

    private static async Task<IResult> RefreshAsync(RefreshRequest request, AuthService auth, CancellationToken cancellationToken) =>
        ToResult(await auth.RefreshAsync(request, cancellationToken));

    private static async Task<NoContent> SignOutAsync(RefreshRequest request, AuthService auth, CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(request, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SelectShopAsync(
        SelectShopRequest request, ClaimsPrincipal user, AuthService auth, CancellationToken cancellationToken)
    {
        if (user.UserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        return ToResult(await auth.SelectShopAsync(userId, request, cancellationToken));
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetMeAsync(
        ClaimsPrincipal user, AuthService auth, CancellationToken cancellationToken)
    {
        if (user.UserId() is not { } userId || await auth.GetMeAsync(userId, cancellationToken, PinMembership(user)) is not { } me)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(me);
    }

    private static IResult ToResult<T>(AuthResult<T> result)
        where T : class =>
        result switch
        {
            { Value: { } value } => TypedResults.Ok(value),
            { Error: AuthError.LockedOut } => TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Account locked for a while.",
                detail: "Too many wrong passwords. Please try again later."),
            { Error: AuthError.MembershipNotFound } => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Shop not found for this account."),
            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sign-in failed.",
                detail: "The details are wrong or have expired. Please try again."),
        };
}
