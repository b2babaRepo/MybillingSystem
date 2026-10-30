using System.Threading.RateLimiting;
using GroceryKhata.Api.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Common;

/// <summary>
/// Production safety. The app refuses to start outside Development with settings that would be dangerous
/// there, sends security headers on every response, caps request size and request rate, and trusts the
/// Nginx proxy in front of it for the client's IP and HTTPS.
/// </summary>
public static class Hardening
{
    /// <summary>Requests per minute per IP across the whole API (shops share one IP per phone; generous on purpose).</summary>
    public const string GlobalPerMinuteKey = "Security:RequestsPerMinutePerIp";

    public static WebApplicationBuilder AddHardening(this WebApplicationBuilder builder)
    {
        // Bills, imports (2000 rows) and stocktakes fit easily; anything bigger is a mistake or an attack.
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 1_048_576);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Nginx on the same server sets these; only the loopback proxy is trusted (the default).
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });

        builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

        // Read when the limiter is first used, not here: settings added later (tests, environment) must count too.
        builder.Services.Configure<RateLimiterOptions>(limiter =>
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = http.RequestServices.GetRequiredService<IConfiguration>().GetValue(GlobalPerMinuteKey, 600),
                        Window = TimeSpan.FromMinutes(1),
                    })));
        return builder;
    }

    /// <summary>
    /// Stops a production start with development-only settings: the public dev signing key, demo data, or a
    /// database login with superuser rights (superusers skip row-level security, the second shop-isolation guard).
    /// </summary>
    public static async Task CheckProductionSafetyAsync(this WebApplication app)
    {
        var problems = new List<string>();
        var signingKey = app.Configuration["Jwt:SigningKey"] ?? "";
        if (signingKey.Contains("DEVELOPMENT", StringComparison.OrdinalIgnoreCase) || signingKey.Contains("change-me", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("Jwt:SigningKey is the public development key. Set a random one in the Jwt__SigningKey environment variable.");
        }

        // Keys behind Aadhaar duplicate checks and plan-bill links: public defaults would let anyone forge or match them.
        foreach (var key in new[] { "Agents:AadhaarHashKey", "Billing:BillLinkKey" })
        {
            if ((app.Configuration[key] ?? "dev-only").StartsWith("dev-only", StringComparison.Ordinal))
            {
                problems.Add($"{key} is the public development key. Set a random one in the {key.Replace(":", "__")} environment variable.");
            }
        }

        if (app.Configuration.GetValue<bool>("Database:SeedDemo"))
        {
            problems.Add("Database:SeedDemo is on. Demo logins must never exist on a real server.");
        }

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var superuser = await db.Database.SqlQueryRaw<bool>("SELECT rolsuper AS \"Value\" FROM pg_roles WHERE rolname = current_user").SingleAsync();
            if (superuser)
            {
                problems.Add("The database login is a superuser, which skips row-level security. Connect as the khata_app role.");
            }
        }

        if (problems.Count == 0)
        {
            return;
        }

        if (app.Environment.IsDevelopment())
        {
            // The dev key and demo data are expected here; still say so, and a superuser login is worth a warning.
            foreach (var problem in problems.Where(p => p.Contains("superuser", StringComparison.Ordinal)))
            {
                app.Logger.LogWarning("Development: {Problem}", problem);
            }

            return;
        }

        throw new InvalidOperationException("Refusing to start: " + string.Join(" ", problems));
    }

    public static WebApplication UseHardening(this WebApplication app)
    {
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            // The API only returns data: nothing in a response may run, load or be framed.
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            // Khata balances and phone numbers must not sit in shared or browser caches.
            headers.CacheControl = "no-store";
            await next(context);
        });
        return app;
    }

    /// <summary>Ready means the database answers, not just that the process is up (for the uptime monitor).</summary>
    public static IEndpointRouteBuilder MapReadiness(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health/ready", async (AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                var pending = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
                return Results.Ok(new { status = "ready", pendingMigrations = pending });
            }
            catch (Exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unavailable");
            }
        }).AllowAnonymous().DisableRateLimiting();
        return app;
    }
}
