using Application.Abstractions;
using Infrastructure.Declaration;
using Infrastructure.Excel;
using Infrastructure.Parsing.UltimaBcs;
using Infrastructure.Rates;
using Infrastructure.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<IFxRateProvider, CbrFxRateProvider>();
        services.AddSingleton<IUltimaBcsReportParser, UltimaBcsReportParser>();
        services.AddSingleton<IReportSessionStore, MemoryReportSessionStore>();
        services.AddSingleton<IExplanationExcelExporter, ExplanationExcelExporter>();
        services.AddSingleton<IDeclarationDeExporter, DeclarationDeExporter>();
        return services;
    }
}
