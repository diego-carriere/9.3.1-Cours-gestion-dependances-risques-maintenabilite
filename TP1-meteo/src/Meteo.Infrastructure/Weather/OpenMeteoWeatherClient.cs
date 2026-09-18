using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// Implémente <see cref="IWeatherProvider"/> par un appel à Open-Meteo. Voir
/// <see cref="Meteo.Infrastructure.Geocoding.NominatimGeocodingClient"/> pour la note sur
/// l'accessibilité <c>internal</c> et le contrat de substituabilité.
/// </summary>
internal sealed partial class OpenMeteoWeatherClient : IWeatherProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OpenMeteoOptions _options;
    private readonly ILogger<OpenMeteoWeatherClient> _logger;

    public OpenMeteoWeatherClient(HttpClient httpClient, IOptions<OpenMeteoOptions> options, ILogger<OpenMeteoWeatherClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<Forecast>> GetForecastAsync(GeoLocation location, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .GetAsync(BuildRequestUri(location), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsTimeout(ex, cancellationToken))
        {
            LogTimeout(ex, location.Latitude, location.Longitude);
            return Result.Failure<Forecast>(ForecastErrorKind.UpstreamTimeout, "Open-Meteo a dépassé le délai imparti.");
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException)
        {
            LogUnavailable(ex, location.Latitude, location.Longitude);
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "Open-Meteo est indisponible.");
        }

        if (!response.IsSuccessStatusCode)
        {
            LogNonSuccessStatus(response.StatusCode);
            return Result.Failure<Forecast>(
                ForecastErrorKind.WeatherUnavailable, $"Open-Meteo a répondu {(int)response.StatusCode}.");
        }

        OpenMeteoResponseDto? payload;
        try
        {
            payload = await response.Content
                .ReadFromJsonAsync<OpenMeteoResponseDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            LogMalformedBody(ex);
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "Réponse Open-Meteo illisible.");
        }

        if (payload?.Hourly?.Time is null || payload.Hourly.ShortwaveRadiation is null)
        {
            LogUnexpectedPayload();
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "Réponse Open-Meteo inattendue.");
        }

        var points = ZipPoints(payload.Hourly.Time, payload.Hourly.ShortwaveRadiation);
        var unit = payload.HourlyUnits?.ShortwaveRadiation ?? "W/m²";

        return Result.Success(new Forecast(location, _options.HourlyVariable, unit, points));
    }

    private string BuildRequestUri(GeoLocation location)
    {
        // Formatage explicitement invariant : sous une culture fr-FR (virgule décimale),
        // un ToString() implicite produirait "latitude=44,1258" et casserait la requête —
        // la dépendance cachée "séparateur décimal" de Support J1, ici côté sortant.
        var latitude = location.Latitude.ToString("F4", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("F4", CultureInfo.InvariantCulture);
        return $"v1/forecast?latitude={latitude}&longitude={longitude}&hourly={_options.HourlyVariable}&timezone=UTC";
    }

    private static List<ForecastPoint> ZipPoints(List<string> times, List<double> values)
    {
        var count = Math.Min(times.Count, values.Count);
        var points = new List<ForecastPoint>(count);

        for (var i = 0; i < count; i++)
        {
            if (DateTimeOffset.TryParse(
                    times[i],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp))
            {
                points.Add(new ForecastPoint(timestamp, values[i]));
            }
        }

        return points;
    }

    private static bool IsTimeout(Exception ex, CancellationToken requestToken) =>
        ex is TimeoutRejectedException
        || (ex is OperationCanceledException && !requestToken.IsCancellationRequested);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Open-Meteo timed out for {Latitude},{Longitude}.")]
    private partial void LogTimeout(Exception exception, double latitude, double longitude);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Open-Meteo unavailable for {Latitude},{Longitude}.")]
    private partial void LogUnavailable(Exception exception, double latitude, double longitude);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Open-Meteo returned {StatusCode}.")]
    private partial void LogNonSuccessStatus(System.Net.HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Open-Meteo returned an unexpected payload shape.")]
    private partial void LogUnexpectedPayload();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Open-Meteo returned a malformed body.")]
    private partial void LogMalformedBody(Exception exception);
}
