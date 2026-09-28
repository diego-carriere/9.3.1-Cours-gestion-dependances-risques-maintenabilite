using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Infrastructure.Caching;
using Meteo.Infrastructure.Demo;
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
        // BindOnce plutôt que Bind pour toute option validée au démarrage : voir BindOnce.
        services.AddOptions<NominatimOptions>()
            .BindOnce(configuration.GetSection(NominatimOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart(); // L'app refuse de démarrer sans User-Agent Nominatim valide.

        services.AddOptions<BanOptions>()
            .Bind(configuration.GetSection(BanOptions.SectionName));

        services.AddOptions<OpenMeteoOptions>()
            .BindOnce(configuration.GetSection(OpenMeteoOptions.SectionName))
            .ValidateOnStart(); // Une variable sans nom canonique empêche l'app de démarrer.
        services.AddSingleton<IValidateOptions<OpenMeteoOptions>, OpenMeteoOptionsValidator>();

        services.AddOptions<MetNoOptions>()
            .BindOnce(configuration.GetSection(MetNoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart(); // L'app refuse de démarrer sans User-Agent MET Norway valide.

        services.AddOptions<ResilienceOptions>()
            .Bind(configuration.GetSection(ResilienceOptions.SectionName));

        // Providers:Geocoder / Providers:Weather choisissent le fournisseur actif par port
        // (TP2, "changer de fournisseur sans redéploiement") : voir GeocoderSelector et
        // WeatherProviderSelector, qui relisent IConfiguration à chaque appel. Ce type-ci ne
        // sert qu'à la validation au démarrage.
        services.AddOptions<ProvidersOptions>()
            .BindOnce(configuration.GetSection(ProvidersOptions.SectionName))
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

        services.AddHttpClient<MetNoWeatherClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MetNoOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddResilienceHandler("met-no", (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            ResiliencePipelines.Configure(builder, options.MetNo);
        });
        services.AddKeyedTransient<IWeatherProvider>(
            ProviderKeys.MetNo, static (provider, _) => provider.GetRequiredService<MetNoWeatherClient>());

        // Le port public : sans état propre, transient comme les clients qu'il délègue.
        services.AddTransient<IGeocoder, GeocoderSelector>();
        services.AddTransient<IWeatherProvider, WeatherProviderSelector>();

        return services;
    }

    /// <summary>
    /// Mode démo (TP3) : enregistre sous <paramref name="serviceKey"/> un géocodeur et un
    /// fournisseur météo simulés, ainsi que des caches qui ne retiennent rien. Rien n'est
    /// enregistré sous les ports non keyed, donc le mode réel n'est pas touché. La clé est
    /// choisie par le composition root, pas par cette couche.
    /// </summary>
    public static IServiceCollection AddSimulatedProviders(this IServiceCollection services, string serviceKey)
    {
        services.TryAddSingleton<IClock, SystemClock>(); // DemoWeatherProvider en dépend ; idempotent.

        services.AddKeyedTransient<IGeocoder, DemoGeocoder>(serviceKey);
        services.AddKeyedTransient<IWeatherProvider, DemoWeatherProvider>(serviceKey);
        services.AddKeyedSingleton<ICache<GeoLocation>, NullCache<GeoLocation>>(serviceKey);
        services.AddKeyedSingleton<ICache<Forecast>, NullCache<Forecast>>(serviceKey);

        return services;
    }

    /// <summary>
    /// Lie la section une fois, à la création des options, sans s'abonner à ses
    /// rechargements. <c>OptionsBuilder.Bind</c> enregistre une source de jeton de
    /// changement ; combinée à <c>ValidateOnStart</c> (qui résout un
    /// <c>IOptionsMonitor&lt;T&gt;</c>), elle fait revalider l'option à chaque rechargement
    /// de configuration, et une valeur invalide lève alors depuis le callback de
    /// rechargement, hors de toute requête. Aucun consommateur de ces options ne lit
    /// <c>CurrentValue</c> : la validation au démarrage suffit, la bascule à chaud passe
    /// par <see cref="GeocoderSelector"/> et <see cref="WeatherProviderSelector"/>.
    /// </summary>
    private static OptionsBuilder<TOptions> BindOnce<TOptions>(this OptionsBuilder<TOptions> builder, IConfiguration section)
        where TOptions : class =>
        builder.Configure(options => section.Bind(options));
}
