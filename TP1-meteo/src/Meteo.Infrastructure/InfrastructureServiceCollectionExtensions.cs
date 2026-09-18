using Meteo.Domain.Abstractions;
using Meteo.Infrastructure.Caching;
using Meteo.Infrastructure.Geocoding;
using Meteo.Infrastructure.Providers;
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

        services.AddOptions<BanOptions>()
            .Bind(configuration.GetSection(BanOptions.SectionName));

        services.AddOptions<OpenMeteoOptions>()
            .Bind(configuration.GetSection(OpenMeteoOptions.SectionName));

        services.AddOptions<ResilienceOptions>()
            .Bind(configuration.GetSection(ResilienceOptions.SectionName));

        // Providers:Geocoder / Providers:Weather choisissent le fournisseur actif par port
        // (TP2, "changer de fournisseur sans redéploiement") : voir GeocoderSelector et
        // WeatherProviderSelector, qui la relisent à chaque appel via IOptionsMonitor.
        services.AddOptions<ProvidersOptions>()
            .Bind(configuration.GetSection(ProvidersOptions.SectionName))
            .ValidateOnStart(); // Une clé de fournisseur inconnue empêche l'app de démarrer.
        services.AddSingleton<IValidateOptions<ProvidersOptions>, ProvidersOptionsValidator>();

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
        //
        // Chaque client typé n'est plus enregistré directement sous le port (IGeocoder,
        // IWeatherProvider) : il est enregistré sous son propre type, puis exposé au port
        // via une clé DI (AddKeyedTransient). Le port lui-même n'a plus qu'une seule
        // implémentation enregistrée : le sélecteur (voir plus bas), qui choisit la clé à
        // résoudre à chaque appel.
        services.AddHttpClient<NominatimGeocodingClient>((provider, client) =>
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
        services.AddKeyedTransient<IGeocoder>(
            ProviderKeys.Nominatim, static (provider, _) => provider.GetRequiredService<NominatimGeocodingClient>());

        services.AddHttpClient<BanGeocodingClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<BanOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddResilienceHandler("ban", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            ResiliencePipelines.Configure(builder, options.Ban);
        });
        services.AddKeyedTransient<IGeocoder>(
            ProviderKeys.Ban, static (provider, _) => provider.GetRequiredService<BanGeocodingClient>());

        services.AddHttpClient<OpenMeteoWeatherClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddResilienceHandler("open-meteo", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            ResiliencePipelines.Configure(builder, options.OpenMeteo);
        });
        services.AddKeyedTransient<IWeatherProvider>(
            ProviderKeys.OpenMeteo, static (provider, _) => provider.GetRequiredService<OpenMeteoWeatherClient>());

        // Le port public : sans état propre, transient comme les clients qu'il délègue.
        services.AddTransient<IGeocoder, GeocoderSelector>();
        services.AddTransient<IWeatherProvider, WeatherProviderSelector>();

        return services;
    }
}
