using Meteo.Application.Forecasting;
using Meteo.Application.Options;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

    /// <summary>
    /// Enregistre sous <paramref name="serviceKey"/> une seconde composition du même
    /// <see cref="GetForecastUseCase"/>, câblée sur les ports enregistrés sous cette même clé
    /// (géocodeur, fournisseur météo, caches). Elle sert au mode démo (TP3) : même orchestration,
    /// mêmes validations, mêmes résultats, seules les dépendances injectées changent. C'est l'IoC
    /// de Support J1 poussée au bout : la classe ignore quelle composition l'utilise.
    /// </summary>
    public static IServiceCollection AddKeyedForecastUseCase(this IServiceCollection services, string serviceKey)
    {
        services.AddOptions<ForecastPolicyOptions>();

        services.AddKeyedScoped<IGetForecastUseCase>(serviceKey, static (provider, key) => new GetForecastUseCase(
            provider.GetRequiredKeyedService<IGeocoder>(key),
            provider.GetRequiredKeyedService<IWeatherProvider>(key),
            provider.GetRequiredKeyedService<ICache<GeoLocation>>(key),
            provider.GetRequiredKeyedService<ICache<Forecast>>(key),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IOptions<ForecastPolicyOptions>>()));

        return services;
    }
}
