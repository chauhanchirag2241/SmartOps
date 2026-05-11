using FluentMigrator.Runner;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartOps.Infrastructure.MultiTenancy;

namespace SmartOps.Infrastructure.DependencyInjection;

public static class InfrastructureApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSmartOpsMigrations(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IMigrationRunner runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
         runner.MigrateUp();
        //runner.MigrateDown(0);
        return app;
    }

    public static IApplicationBuilder UseTenantResolver(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolverMiddleware>();
    }
}
