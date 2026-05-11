using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SmartOps.Application.Common.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        System.Reflection.Assembly assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
