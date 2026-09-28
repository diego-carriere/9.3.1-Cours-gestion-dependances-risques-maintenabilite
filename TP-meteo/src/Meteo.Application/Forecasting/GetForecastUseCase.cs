using Meteo.Application.Options;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Microsoft.Extensions.Options;

namespace Meteo.Application.Forecasting;

/// <summary>
/// Orchestre géocodage puis météo, avec cache et mode dégradé. Logique pure : aucune E/S
/// directe (tout passe par les ports injectés), aucune connaissance de HTTP. C'est la classe
/// la plus testée du projet — voir Meteo.Application.Tests — précisément parce qu'elle ne
/// dépend que d'abstractions (IoC/DI, Support J1).
/// </summary>
public sealed class GetForecastUseCase : IGetForecastUseCase
{
    private readonly IGeocoder _geocoder;
    private readonly IWeatherProvider _weatherProvider;
    private readonly ICache<GeoLocation> _geoCache;
    private readonly ICache<Forecast> _forecastCache;
    private readonly IClock _clock;
    private readonly ForecastPolicyOptions _policy;

    public GetForecastUseCase(
        IGeocoder geocoder,
        IWeatherProvider weatherProvider,
        ICache<GeoLocation> geoCache,
        ICache<Forecast> forecastCache,
        IClock clock,
        IOptions<ForecastPolicyOptions> policy)
    {
        _geocoder = geocoder;
        _weatherProvider = weatherProvider;
        _geoCache = geoCache;
        _forecastCache = forecastCache;
        _clock = clock;
        _policy = policy.Value;
    }

    public async Task<Result<ForecastResult>> ExecuteAsync(string? rawAddress, CancellationToken cancellationToken)
    {
        var addressResult = Address.Create(rawAddress);
        if (addressResult.IsFailure)
        {
            return Result.Failure<ForecastResult>(addressResult.Error);
        }

        var address = addressResult.Value;

        var locationResult = await ResolveLocationAsync(address, cancellationToken).ConfigureAwait(false);
        if (locationResult.IsFailure)
        {
            return Result.Failure<ForecastResult>(locationResult.Error);
        }

        var (location, locationDegraded) = locationResult.Value;

        var forecastResult = await ResolveForecastAsync(location, cancellationToken).ConfigureAwait(false);
        if (forecastResult.IsFailure)
        {
            return Result.Failure<ForecastResult>(forecastResult.Error);
        }

        var (forecast, forecastDegraded, dataAsOfUtc) = forecastResult.Value;

        return Result.Success(new ForecastResult(
            address,
            forecast,
            locationDegraded || forecastDegraded,
            dataAsOfUtc));
    }

    /// <summary>
    /// Étape 1 : cache frais (24 h) → sinon appel réel → sur échec, cache périmé en secours
    /// (mode dégradé) → sinon échec. Une adresse introuvable n'est jamais dégradée : ce
    /// n'est pas une panne transitoire, réessayer gâcherait le budget 1 req/s de Nominatim.
    /// </summary>
    private async Task<Result<(GeoLocation Location, bool Degraded)>> ResolveLocationAsync(
        Address address, CancellationToken cancellationToken)
    {
        var cacheKey = GeoCacheKey(address);
        var cached = _geoCache.Read(cacheKey);

        if (cached is not null && _clock.UtcNow - cached.StoredAtUtc < _policy.GeocodingFreshness)
        {
            return Result.Success((cached.Value, false));
        }

        var geocoded = await _geocoder.ResolveAsync(address, cancellationToken).ConfigureAwait(false);

        if (geocoded.IsSuccess)
        {
            _geoCache.Write(cacheKey, geocoded.Value, _policy.GeocodingFreshness);
            return Result.Success((geocoded.Value, false));
        }

        if (geocoded.Error.Kind == ForecastErrorKind.AddressNotFound)
        {
            return Result.Failure<(GeoLocation, bool)>(geocoded.Error);
        }

        if (cached is not null)
        {
            return Result.Success((cached.Value, true));
        }

        return Result.Failure<(GeoLocation, bool)>(geocoded.Error);
    }

    /// <summary>
    /// Étape 2 : même logique que <see cref="ResolveLocationAsync"/>, avec une fenêtre de
    /// rétention explicite pour la péremption utilisable (au-delà, l'entrée ne sert plus).
    /// </summary>
    private async Task<Result<(Forecast Forecast, bool Degraded, DateTimeOffset DataAsOfUtc)>> ResolveForecastAsync(
        GeoLocation location, CancellationToken cancellationToken)
    {
        var cacheKey = ForecastCacheKey(location);
        var cached = _forecastCache.Read(cacheKey);
        var now = _clock.UtcNow;

        if (cached is not null && now - cached.StoredAtUtc < _policy.WeatherFreshness)
        {
            return Result.Success((cached.Value, false, cached.StoredAtUtc));
        }

        var fetched = await _weatherProvider.GetForecastAsync(location, cancellationToken).ConfigureAwait(false);

        if (fetched.IsSuccess)
        {
            _forecastCache.Write(cacheKey, fetched.Value, _policy.WeatherStaleRetention);
            return Result.Success((fetched.Value, false, now));
        }

        if (cached is not null && now - cached.StoredAtUtc < _policy.WeatherStaleRetention)
        {
            return Result.Success((cached.Value, true, cached.StoredAtUtc));
        }

        return Result.Failure<(Forecast, bool, DateTimeOffset)>(fetched.Error);
    }

    private static string GeoCacheKey(Address address) => $"geo:{address.CacheKey}";

    private static string ForecastCacheKey(GeoLocation location) =>
        FormattableString.Invariant($"fc:{location.Latitude:F4},{location.Longitude:F4}");
}
