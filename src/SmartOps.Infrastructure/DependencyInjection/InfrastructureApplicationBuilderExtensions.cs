using FluentMigrator.Runner;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Infrastructure.MultiTenancy;

namespace SmartOps.Infrastructure.DependencyInjection;

public static class InfrastructureApplicationBuilderExtensions
{
    public static async Task UseSmartOpsMigrationsAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SmartOps.Migrations");

        IMigrationRunner runner = services.GetRequiredService<IMigrationRunner>();
        logger.LogInformation("Running FluentMigrator migrations...");
        runner.MigrateUp();
        logger.LogInformation("FluentMigrator migrations completed.");

        ITenantSchemaSyncService tenantSchemaSync = services.GetRequiredService<ITenantSchemaSyncService>();
        logger.LogInformation("Syncing all active school tenant schemas from template...");
        await tenantSchemaSync.SyncAllActiveSchoolSchemasAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Tenant schema sync completed.");
    }

    public static IApplicationBuilder UseTenantResolver(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolverMiddleware>();
    }
}
