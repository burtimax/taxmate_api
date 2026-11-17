using Application.Services;
using Application.Tax;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<UltimaBcsTaxCalculator>();
        services.AddScoped<UltimaBcsReportService>();
        return services;
    }
}
