using Meteo.Application.Forecasting;
using Meteo.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meteo.Application;

/// <summary>
/// Point d'entrée unique de la couche Application dans le composition root
/// (Meteo.Api/Program.cs). N'enregistre que ce que cette couche possède : le cas d'usage et
/// ses options. Les ports qu'il consomme (IGeocoder, IWeatherProvider, ICache&lt;T&gt;,
/// IClock) sont enregistrés par AddInfrastructure — Application ne connaît pas leurs
/// implémentations.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<ForecastPolicyOptions>();

        // Scoped, pas Singleton : une unité de travail par requête HTTP, sans état entre
        // deux requêtes. Voir la table des durées de vie du README pour l'argument complet
        // sur la dépendance captive.
        services.TryAddScoped<IGetForecastUseCase, GetForecastUseCase>();

        return services;
    }
}
