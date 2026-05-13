using FluentMigrator.Runner;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Migrations;
using SmartOps.Infrastructure.Modules.Identity.Repositories;
using SmartOps.Infrastructure.Modules.Identity.Services;
using SmartOps.Infrastructure.Modules.Identity.Stores;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Factories;
using SmartOps.Infrastructure.Persistence.TypeHandlers;
using SmartOps.Domain.Modules.Student.Interfaces;
using SmartOps.Domain.Modules.Class.Interfaces;
using SmartOps.Domain.Modules.Subject.Interfaces;
using SmartOps.Infrastructure.Persistence.Repositories;
using Dapper;

namespace SmartOps.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSmartOpsDatabaseInfrastructure(configuration);
        services.AddSmartOpsIdentityInfrastructure(configuration);
        services.AddSmartOpsMultiTenancyInfrastructure();
        return services;
    }

    public static IServiceCollection AddSmartOpsDatabaseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<DapperContext>();
        
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();

        string? connectionString = configuration.GetConnectionString("GlobalDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'GlobalDb' is not configured.");
        }

        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(M001_CreateUsersTable).Assembly).For.Migrations());

        services.AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }

    public static IServiceCollection AddSmartOpsIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<UserRepository>();
        services.AddScoped<IUserRepository>(sp => sp.GetRequiredService<UserRepository>());

        services.AddScoped<RoleRepository>();

        services.AddScoped<RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenRepository>(sp => sp.GetRequiredService<RefreshTokenRepository>());

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IUserCredentialValidator, UserCredentialValidator>();
        services.AddScoped<IIdentityService, IdentityService>();

        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();
        services.AddScoped<IdentityErrorDescriber>();

        services.AddScoped<IUserStore<ApplicationUser>, CustomUserStore>();
        services.AddScoped<IRoleStore<ApplicationRole>, CustomRoleStore>();

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>();

        return services;
    }

    public static IServiceCollection AddSmartOpsMultiTenancyInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantProvider, TenantProvider>();
        return services;
    }
}
