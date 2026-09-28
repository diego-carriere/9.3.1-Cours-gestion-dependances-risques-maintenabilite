using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Meteo.Infrastructure.Providers;
using Meteo.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// <see cref="GeocoderSelector"/> et <see cref="WeatherProviderSelector"/> ne parlent à
/// aucun réseau : ils délèguent à un port keyed résolu par nom, lu depuis
/// <see cref="IConfiguration"/> à chaque appel — jamais via
/// <c>IOptionsMonitor&lt;ProvidersOptions&gt;</c> (voir la note de
/// <see cref="GeocoderSelector"/> pour la raison : un moniteur d'options revaliderait à
/// chaque rechargement, pas seulement au démarrage, avec un risque de faire lever une
/// exception hors de toute requête HTTP). Testés ici avec des fakes manuscrits enregistrés
/// sous clé, et une vraie <see cref="IConfigurationRoot"/> en mémoire, modifiable en cours
/// de test pour simuler un rechargement.
/// </summary>
public sealed class ProviderSelectionTests
{
    private static IConfigurationRoot BuildConfiguration(string? geocoder = null, string? weather = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("Providers:Geocoder", geocoder),
                new KeyValuePair<string, string?>("Providers:Weather", weather),
            ])
            .Build();

    [Fact]
    public async Task GeocoderSelector_delegates_to_the_configured_provider()
    {
        var nominatim = new FakeGeocoder();
        var ban = new FakeGeocoder();
        nominatim.Enqueue(Result.Success(new GeoLocation(1, 1, "nominatim")));
        ban.Enqueue(Result.Success(new GeoLocation(2, 2, "ban")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Nominatim, nominatim);
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Ban, ban);
        var provider = services.BuildServiceProvider();

        var configuration = BuildConfiguration(geocoder: ProviderKeys.Ban);
        var sut = new GeocoderSelector(provider, configuration, NullLogger<GeocoderSelector>.Instance);

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.Equal("ban", result.Value.DisplayName);
        Assert.Equal(0, nominatim.CallCount);
        Assert.Equal(1, ban.CallCount);
    }

    [Fact]
    public async Task GeocoderSelector_switches_provider_between_calls_without_being_recreated()
    {
        var nominatim = new FakeGeocoder();
        var ban = new FakeGeocoder();
        nominatim.Enqueue(Result.Success(new GeoLocation(1, 1, "nominatim")));
        ban.Enqueue(Result.Success(new GeoLocation(2, 2, "ban")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Nominatim, nominatim);
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Ban, ban);
        var provider = services.BuildServiceProvider();

        var configuration = BuildConfiguration(geocoder: ProviderKeys.Nominatim);
        var sut = new GeocoderSelector(provider, configuration, NullLogger<GeocoderSelector>.Instance);

        var first = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        // Écrit dans la configuration vivante, comme le ferait un rechargement. Modifier le
        // dictionnaire passé à AddInMemoryCollection n'aurait aucun effet : il est copié à
        // la construction du fournisseur de configuration.
        configuration["Providers:Geocoder"] = ProviderKeys.Ban;
        var second = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.Equal("nominatim", first.Value.DisplayName);
        Assert.Equal("ban", second.Value.DisplayName);
    }

    [Fact]
    public async Task GeocoderSelector_accepts_a_key_in_another_case_as_the_validator_does()
    {
        var ban = new FakeGeocoder();
        ban.Enqueue(Result.Success(new GeoLocation(2, 2, "ban")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Ban, ban);
        var provider = services.BuildServiceProvider();

        // ProvidersOptionsValidator accepte "BAN" au démarrage : le sélecteur doit donc le
        // résoudre, pas lever faute de service enregistré sous cette casse exacte.
        var configuration = BuildConfiguration(geocoder: "BAN");
        var sut = new GeocoderSelector(provider, configuration, NullLogger<GeocoderSelector>.Instance);

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.Equal("ban", result.Value.DisplayName);
    }

    [Fact]
    public async Task GeocoderSelector_falls_back_to_the_default_provider_for_an_unknown_key()
    {
        var nominatim = new FakeGeocoder();
        nominatim.Enqueue(Result.Success(new GeoLocation(1, 1, "nominatim")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Nominatim, nominatim);
        var provider = services.BuildServiceProvider();

        var configuration = BuildConfiguration(geocoder: "unknown-provider");
        var sut = new GeocoderSelector(provider, configuration, NullLogger<GeocoderSelector>.Instance);

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("nominatim", result.Value.DisplayName);
    }

    [Fact]
    public async Task GeocoderSelector_falls_back_when_the_key_is_absent_from_configuration()
    {
        var nominatim = new FakeGeocoder();
        nominatim.Enqueue(Result.Success(new GeoLocation(1, 1, "nominatim")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Nominatim, nominatim);
        var provider = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder().Build(); // aucune section Providers du tout.
        var sut = new GeocoderSelector(provider, configuration, NullLogger<GeocoderSelector>.Instance);

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("nominatim", result.Value.DisplayName);
    }

    [Fact]
    public async Task WeatherProviderSelector_delegates_to_the_configured_provider()
    {
        var openMeteo = new FakeWeatherProvider();
        var metNo = new FakeWeatherProvider();
        var location = new GeoLocation(44.1258, 4.0806, "Alès");
        openMeteo.Enqueue(Result.Success(new Forecast(location, WeatherVariables.AirTemperature, "°C", [])));
        metNo.Enqueue(Result.Success(new Forecast(location, WeatherVariables.AirTemperature, "°C", [])));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IWeatherProvider>(ProviderKeys.OpenMeteo, openMeteo);
        services.AddKeyedSingleton<IWeatherProvider>(ProviderKeys.MetNo, metNo);
        var provider = services.BuildServiceProvider();

        var configuration = BuildConfiguration(weather: ProviderKeys.MetNo);
        var sut = new WeatherProviderSelector(provider, configuration, NullLogger<WeatherProviderSelector>.Instance);

        await sut.GetForecastAsync(location, CancellationToken.None);

        Assert.Equal(0, openMeteo.CallCount);
        Assert.Equal(1, metNo.CallCount);
    }

    [Fact]
    public async Task WeatherProviderSelector_accepts_a_key_in_another_case_as_the_validator_does()
    {
        var metNo = new FakeWeatherProvider();
        var location = new GeoLocation(44.1258, 4.0806, "Alès");
        metNo.Enqueue(Result.Success(new Forecast(location, WeatherVariables.AirTemperature, "°C", [])));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IWeatherProvider>(ProviderKeys.MetNo, metNo);
        var provider = services.BuildServiceProvider();

        var configuration = BuildConfiguration(weather: "MET-NO");
        var sut = new WeatherProviderSelector(provider, configuration, NullLogger<WeatherProviderSelector>.Instance);

        await sut.GetForecastAsync(location, CancellationToken.None);

        Assert.Equal(1, metNo.CallCount);
    }

    [Fact]
    public async Task WeatherProviderSelector_falls_back_to_the_default_provider_for_an_unknown_key()
    {
        var openMeteo = new FakeWeatherProvider();
        var location = new GeoLocation(44.1258, 4.0806, "Alès");
        openMeteo.Enqueue(Result.Success(new Forecast(location, WeatherVariables.AirTemperature, "°C", [])));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IWeatherProvider>(ProviderKeys.OpenMeteo, openMeteo);
        var provider = services.BuildServiceProvider();

        var configuration = BuildConfiguration(weather: "unknown-provider");
        var sut = new WeatherProviderSelector(provider, configuration, NullLogger<WeatherProviderSelector>.Instance);

        var result = await sut.GetForecastAsync(location, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, openMeteo.CallCount);
    }
}
