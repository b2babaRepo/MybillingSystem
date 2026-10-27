using GroceryKhata.Api.Common;
using GroceryKhata.Api.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace GroceryKhata.Api.Persistence;

public static class PersistenceSetup
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ShopStampInterceptor>();
        services.AddScoped<ShopSessionInterceptor>();
        services.AddSingleton<TimestampInterceptor>();

        // The connection string is read when the context is built, so test hosts can override it.
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default") is { Length: > 0 } connectionString
                ? connectionString
                : throw new InvalidOperationException("ConnectionStrings:Default is required."))
            .AddInterceptors(
                sp.GetRequiredService<ShopStampInterceptor>(),
                sp.GetRequiredService<ShopSessionInterceptor>(),
                sp.GetRequiredService<TimestampInterceptor>()));

        return services;
    }

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

/// <summary>Lets "dotnet ef migrations add" build the context without the web host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=grocery_khata;Username=khata_app;Password=design-time")
            .Options, new TenantContext());
}
