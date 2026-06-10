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

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;

        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SmartOps.Migrations");



        IDatabaseMigrationService migrationService = services.GetRequiredService<IDatabaseMigrationService>();



        logger.LogInformation("Running global database migrations...");

        await migrationService.MigrateGlobalDatabaseAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Global database migrations completed.");



        logger.LogInformation("Running school database migrations for all schools...");

        await migrationService.MigrateAllSchoolDatabasesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("School database migrations completed.");

    }



    public static IApplicationBuilder UseTenantResolver(this IApplicationBuilder app)

    {

        return app.UseMiddleware<TenantResolverMiddleware>();

    }

}

