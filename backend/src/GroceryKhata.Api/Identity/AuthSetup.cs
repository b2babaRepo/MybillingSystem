using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GroceryKhata.Api.Identity;

public static class AuthSetup
{
    /// <summary>Rate-limit policy for sign-up, sign-in and refresh endpoints (per IP address).</summary>
    public const string AuthRateLimit = "auth";

    public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(o => o.SigningKey.Length >= JwtOptions.MinimumKeyLength,
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumKeyLength} characters. Set it with dotnet user-secrets or the Jwt__SigningKey environment variable.")
            .Validate(o => o.Issuer.Length > 0 && o.Audience.Length > 0, "Jwt:Issuer and Jwt:Audience are required.")
            .ValidateOnStart();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);

        services.AddSingleton<AccessTokenIssuer>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<AuthService>();
        services.AddScoped<ShopAccessChecker>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = AccessTokenIssuer.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // only the algorithm we sign with
                    NameClaimType = AppClaims.Subject,
                    RoleClaimType = AppClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            // An endpoint that says nothing about access still needs a signed-in user; public ones say AllowAnonymous.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AppPolicies.ShopMember, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaims.ShopId)
                .RequireClaim(AppClaims.MembershipId))
            .AddPolicy(AppPolicies.ShopOwner, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaims.ShopId)
                .RequireClaim(AppClaims.MembershipId)
                .RequireClaim(AppClaims.Role, RoleNames.ToName(ShopRole.Owner)));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(AuthRateLimit, http =>
            {
                var perMinute = http.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.AuthRequestsPerMinutePerIp;
                return RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) });
            });
        });

        return services;
    }
}
