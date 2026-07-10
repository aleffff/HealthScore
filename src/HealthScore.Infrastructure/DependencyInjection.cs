using HealthScore.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HealthScore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthScoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MariaDb")
            ?? throw new InvalidOperationException("ConnectionStrings:MariaDb is required.");

        services.AddOptions<SalesforceOptions>()
            .Bind(configuration.GetSection(SalesforceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<SyncOptions>().Bind(configuration.GetSection(SyncOptions.SectionName));
        services.AddDbContext<HealthScoreDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 0)),
                mariaDb => mariaDb.CommandTimeout(180)));
        services.AddHttpClient<ISalesforceClient, SalesforceClient>(client => client.Timeout = TimeSpan.FromMinutes(2));
        services.AddScoped<IFarmaSyncService, FarmaSyncService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<AccountGroupResolver>();
        services.AddScoped<ProductNormalizer>();
        services.AddMemoryCache();
        services.AddScoped<FilteredAnalyticsService>();
        return services;
    }
}
