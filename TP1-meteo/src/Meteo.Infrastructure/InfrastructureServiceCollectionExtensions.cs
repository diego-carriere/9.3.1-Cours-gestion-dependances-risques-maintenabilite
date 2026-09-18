using Meteo.Domain.Abstractions;
using Meteo.Infrastructure.Caching;
using Meteo.Infrastructure.Geocoding;
using Meteo.Infrastructure.Resilience;
using Meteo.Infrastructure.Time;
using Meteo.Infrastructure.Weather;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure;

/// <summary>
/// Point d'entrée unique de la couche Infrastructure dans le composition root
/// (Meteo.Api/Program.cs). Tous les adaptateurs qu'elle enregistre sont <c>internal</c> :
/// ce fichier est le seul, dans tout l'assembly, qui a le droit de les nommer.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NominatimOptions>()
            .Bind(configuration.GetSection(NominatimOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart(); // L'app refuse de démarrer sans User-Agent Nominatim valide.

        services.AddOptions<OpenMeteoOptions>()
            .Bind(configuration.GetSection(OpenMeteoOptions.SectionName));

        services.AddOptions<ResilienceOptions>()
            .Bind(configuration.GetSection(ResilienceOptions.SectionName));

        // Singleton : un cache par requête ne cache rien.
        services.AddMemoryCache();
        services.TryAddSingleton(typeof(ICache<>), typeof(MemoryCache<>));

        // Singleton : sans état, aucune dépendance scoped, donc aucun risque de dépendance
        // captive (Support J1, "durées de vie : le piège de la dépendance captive").
        services.TryAddSingleton<IClock, SystemClock>();

        // Transient (par conception d'AddHttpClient) : le HttpClient et le client typé sont
        // jetables, mais le pool de HttpMessageHandler qui les sert est mutualisé par la
        // fabrique — elle-même singleton. Voir le README pour l'argument complet sur
        // pourquoi ce n'est PAS un singleton malgré l'intuition de départ.
        services.AddHttpClient<IGeocoder, NominatimGeocodingClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<NominatimOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddResilienceHandler("nominatim", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            ResiliencePipelines.ConfigureNominatimRateLimiter(builder);
            ResiliencePipelines.Configure(builder, options.Nominatim);
        });

        services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddResilienceHandler("open-meteo", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            ResiliencePipelines.Configure(builder, options.OpenMeteo);
        });

        return services;
    }
}
