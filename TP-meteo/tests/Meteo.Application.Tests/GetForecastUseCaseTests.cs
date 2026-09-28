using Meteo.Application.Forecasting;
using Meteo.Application.Options;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Meteo.TestSupport;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Meteo.Application.Tests;

/// <summary>
/// Le cœur du TP : la matrice complète du mode dégradé, testée avec des fakes manuscrits
/// et un FakeClock — sans aucune infrastructure, sans réseau. C'est la démonstration que
/// l'IoC/DI rend le code testable (Support J1, "Pourquoi l'IoC rend le code testable").
/// </summary>
public sealed class GetForecastUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly GeoLocation Ales = new(44.1258, 4.0806, "Alès, Gard, Occitanie, France");

    private readonly FakeGeocoder _geocoder = new();
    private readonly FakeWeatherProvider _weatherProvider = new();
    private readonly FakeClock _clock;
    private readonly InMemoryCache<GeoLocation> _geoCache;
    private readonly InMemoryCache<Forecast> _forecastCache;
    private readonly GetForecastUseCase _sut;

    public GetForecastUseCaseTests()
    {
        _clock = new FakeClock(Now);
        _geoCache = new InMemoryCache<GeoLocation>(_clock);
        _forecastCache = new InMemoryCache<Forecast>(_clock);
        _sut = new GetForecastUseCase(
            _geocoder,
            _weatherProvider,
            _geoCache,
            _forecastCache,
            _clock,
            MsOptions.Create(new ForecastPolicyOptions()));
    }

    private static Forecast SampleForecast(GeoLocation location) => new(
        location,
        WeatherVariables.AirTemperature,
        "°C",
        [new ForecastPoint(Now, 18.4)]);

    [Fact]
    public async Task Happy_path_returns_success_and_is_not_degraded()
    {
        _geocoder.Enqueue(Result.Success(Ales));
        _weatherProvider.Enqueue(Result.Success(SampleForecast(Ales)));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDegraded);
        Assert.Equal(Ales.Latitude, result.Value.Forecast.Location.Latitude);
        Assert.Equal(Ales.Longitude, result.Value.Forecast.Location.Longitude);
        Assert.Single(result.Value.Forecast.Points);
    }

    [Fact]
    public async Task AddressNotFound_never_calls_the_weather_provider()
    {
        _geocoder.Enqueue(Result.Failure<GeoLocation>(ForecastErrorKind.AddressNotFound, "no match"));

        var result = await _sut.ExecuteAsync("nowhere", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.AddressNotFound, result.Error.Kind);
        Assert.Equal(0, _weatherProvider.CallCount);
    }

    [Fact]
    public async Task Geocoder_unavailable_without_cache_fails()
    {
        _geocoder.Enqueue(Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "down"));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.GeocodingUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task Geocoder_unavailable_with_stale_cache_degrades()
    {
        _geoCache.Seed("geo:alès", Ales, Now - TimeSpan.FromDays(2));
        _geocoder.Enqueue(Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "down"));
        _weatherProvider.Enqueue(Result.Success(SampleForecast(Ales)));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDegraded);
    }

    [Fact]
    public async Task Fresh_geocoding_cache_hit_never_calls_the_geocoder()
    {
        _geoCache.Seed("geo:alès", Ales, Now - TimeSpan.FromHours(1));
        _weatherProvider.Enqueue(Result.Success(SampleForecast(Ales)));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _geocoder.CallCount);
    }

    [Fact]
    public async Task Expired_geocoding_cache_calls_the_geocoder_again()
    {
        _geoCache.Seed("geo:alès", Ales, Now - TimeSpan.FromHours(25));
        _geocoder.Enqueue(Result.Success(Ales));
        _weatherProvider.Enqueue(Result.Success(SampleForecast(Ales)));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _geocoder.CallCount);
    }

    [Fact]
    public async Task Fresh_forecast_cache_never_calls_the_weather_provider()
    {
        _geocoder.Enqueue(Result.Success(Ales));
        _forecastCache.Seed("fc:44.1258,4.0806", SampleForecast(Ales), Now - TimeSpan.FromMinutes(2));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDegraded);
        Assert.Equal(0, _weatherProvider.CallCount);
    }

    [Fact]
    public async Task Weather_unavailable_with_two_hour_old_cache_degrades_with_cache_timestamp()
    {
        var storedAt = Now - TimeSpan.FromHours(2);
        _geocoder.Enqueue(Result.Success(Ales));
        _forecastCache.Seed("fc:44.1258,4.0806", SampleForecast(Ales), storedAt);
        _weatherProvider.Enqueue(Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "down"));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDegraded);
        Assert.Equal(storedAt, result.Value.DataAsOfUtc);
    }

    [Fact]
    public async Task Weather_unavailable_with_cache_beyond_retention_fails()
    {
        _geocoder.Enqueue(Result.Success(Ales));
        _forecastCache.Seed("fc:44.1258,4.0806", SampleForecast(Ales), Now - TimeSpan.FromHours(7));
        _weatherProvider.Enqueue(Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "down"));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.WeatherUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task Weather_unavailable_without_any_cache_fails()
    {
        _geocoder.Enqueue(Result.Success(Ales));
        _weatherProvider.Enqueue(Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "down"));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.WeatherUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task Upstream_timeout_propagates_as_its_own_kind()
    {
        _geocoder.Enqueue(Result.Success(Ales));
        _weatherProvider.Enqueue(Result.Failure<Forecast>(ForecastErrorKind.UpstreamTimeout, "timed out"));

        var result = await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.UpstreamTimeout, result.Error.Kind);
    }

    [Fact]
    public async Task Successful_weather_call_writes_the_cache()
    {
        _geocoder.Enqueue(Result.Success(Ales));
        _weatherProvider.Enqueue(Result.Success(SampleForecast(Ales)));

        await _sut.ExecuteAsync("Alès", CancellationToken.None);

        Assert.NotNull(_forecastCache.Read("fc:44.1258,4.0806"));
    }

    [Fact]
    public async Task Cancellation_token_is_forwarded_to_the_geocoder()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _geocoder.Enqueue(Result.Success(Ales));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync("Alès", cts.Token));
    }
}
