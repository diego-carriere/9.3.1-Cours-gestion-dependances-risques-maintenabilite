using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Meteo.Infrastructure.Providers;
using Meteo.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// <see cref="GeocoderSelector"/> et <see cref="WeatherProviderSelector"/> ne parlent à
/// aucun réseau : ils délèguent à un port keyed résolu par nom. Testés ici avec des fakes
/// manuscrits enregistrés sous clé, et un <see cref="IOptionsMonitor{TOptions}"/> mutable
/// pour simuler un rechargement de configuration entre deux appels.
/// </summary>
public sealed class ProviderSelectionTests
{
    private sealed class MutableOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public MutableOptionsMonitor(T initial) => CurrentValue = initial;

        public T CurrentValue { get; set; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }

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

        var options = new MutableOptionsMonitor<ProvidersOptions>(new ProvidersOptions { Geocoder = ProviderKeys.Ban });
        var sut = new GeocoderSelector(provider, options, NullLogger<GeocoderSelector>.Instance);

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

        var options = new MutableOptionsMonitor<ProvidersOptions>(new ProvidersOptions { Geocoder = ProviderKeys.Nominatim });
        var sut = new GeocoderSelector(provider, options, NullLogger<GeocoderSelector>.Instance);

        var first = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);
        options.CurrentValue = new ProvidersOptions { Geocoder = ProviderKeys.Ban };
        var second = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.Equal("nominatim", first.Value.DisplayName);
        Assert.Equal("ban", second.Value.DisplayName);
    }

    [Fact]
    public async Task GeocoderSelector_falls_back_to_the_default_provider_for_an_unknown_key()
    {
        var nominatim = new FakeGeocoder();
        nominatim.Enqueue(Result.Success(new GeoLocation(1, 1, "nominatim")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IGeocoder>(ProviderKeys.Nominatim, nominatim);
        var provider = services.BuildServiceProvider();

        var options = new MutableOptionsMonitor<ProvidersOptions>(new ProvidersOptions { Geocoder = "unknown-provider" });
        var sut = new GeocoderSelector(provider, options, NullLogger<GeocoderSelector>.Instance);

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

        var options = new MutableOptionsMonitor<ProvidersOptions>(new ProvidersOptions { Weather = ProviderKeys.MetNo });
        var sut = new WeatherProviderSelector(provider, options, NullLogger<WeatherProviderSelector>.Instance);

        await sut.GetForecastAsync(location, CancellationToken.None);

        Assert.Equal(0, openMeteo.CallCount);
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

        var options = new MutableOptionsMonitor<ProvidersOptions>(new ProvidersOptions { Weather = "unknown-provider" });
        var sut = new WeatherProviderSelector(provider, options, NullLogger<WeatherProviderSelector>.Instance);

        var result = await sut.GetForecastAsync(location, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, openMeteo.CallCount);
    }
}
