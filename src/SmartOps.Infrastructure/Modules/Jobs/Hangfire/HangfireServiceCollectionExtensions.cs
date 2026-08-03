using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartOps.Application.Modules.Jobs.Interfaces;

namespace SmartOps.Infrastructure.Modules.Jobs.Hangfire;

public static class HangfireServiceCollectionExtensions
{
    public static IServiceCollection AddSmartOpsHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        string? cs = configuration.GetConnectionString("GlobalDatabase")
            ?? configuration.GetConnectionString("GlobalDb")
            ?? configuration.GetConnectionString("PlatformDb");

        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException(
                "Connection string 'GlobalDatabase', 'GlobalDb', or 'PlatformDb' is required for Hangfire.");
        }

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(cs), new PostgreSqlStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true
            }));

        services.AddSingleton<IHangfireJobSync, HangfireJobSync>();
        services.AddSingleton<HangfireRuntime>();
        services.AddSingleton<IHangfireRuntime>(sp => sp.GetRequiredService<HangfireRuntime>());
        services.AddHostedService(sp => sp.GetRequiredService<HangfireRuntime>());

        return services;
    }
}
