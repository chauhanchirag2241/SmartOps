using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Infrastructure.Modules.Authorization.Context;
using SmartOps.Infrastructure.Modules.Authorization.Repositories;
using SmartOps.Infrastructure.Modules.Authorization.Services;

namespace SmartOps.Infrastructure.DependencyInjection;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddSmartOpsAuthorizationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthorizationOptions>(configuration.GetSection(AuthorizationOptions.SectionName));
        services.AddMemoryCache();

        services.AddScoped<IScopeMappingRepository, ScopeMappingRepository>();
        services.AddScoped<IUserScopeService, UserScopeService>();
        services.AddScoped<IUserScopeContext, UserScopeContext>();
        services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
