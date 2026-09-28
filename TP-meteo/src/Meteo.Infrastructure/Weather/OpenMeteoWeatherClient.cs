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
    private readonly (string Canonical, string DefaultUnit) _variable;
    private readonly ILogger<OpenMeteoWeatherClient> _logger;

    public OpenMeteoWeatherClient(HttpClient httpClient, IOptions<OpenMeteoOptions> options, ILogger<OpenMeteoWeatherClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Garanti au démarrage par OpenMeteoOptionsValidator ; ne peut manquer ici que si
        // l'adaptateur est construit hors du conteneur avec une option invalide.
        if (!OpenMeteoVariables.Known.TryGetValue(_options.HourlyVariable, out _variable))
        {
            throw new InvalidOperationException(
                $"OpenMeteo:HourlyVariable '{_options.HourlyVariable}' n'a pas de nom canonique.");
        }
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

        if (payload?.Hourly?.Time is null
            || !TryReadSeries(payload.Hourly.OtherSeries, _options.HourlyVariable, out var values))
        {
            LogUnexpectedPayload();
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "Réponse Open-Meteo inattendue.");
        }

        var points = ZipPoints(payload.Hourly.Time, values);
        var unit = ReadUnit(payload.HourlyUnits, _options.HourlyVariable) ?? _variable.DefaultUnit;

        return Result.Success(new Forecast(location, _variable.Canonical, unit, points));
    }

    // La série demandée porte le nom brut Open-Meteo (ex. "temperature_2m") : capturée en
    // JsonExtensionData plutôt qu'en propriété fixe, elle ne franchit jamais cette classe —
    // voir Meteo.Infrastructure.Tests.AdapterIsolationTests.
    private static bool TryReadSeries(
        Dictionary<string, JsonElement>? series, string variableName, out List<double?> values)
    {
        values = [];

        if (series is null || !series.TryGetValue(variableName, out var element)
            || element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            // null pour une heure sans donnée : conservé comme trou, pour garder l'alignement
            // avec hourly.time, puis ignoré par ZipPoints (même règle que MET Norway).
            values.Add(item.ValueKind == JsonValueKind.Number ? item.GetDouble() : null);
        }

        return true;
    }

    private static string? ReadUnit(Dictionary<string, string>? units, string variableName) =>
        units is not null && units.TryGetValue(variableName, out var unit) ? unit : null;

    private string BuildRequestUri(GeoLocation location)
    {
        // Formatage explicitement invariant : sous une culture fr-FR (virgule décimale),
        // un ToString() implicite produirait "latitude=44,1258" et casserait la requête —
        // la dépendance cachée "séparateur décimal" de Support J1, ici côté sortant.
        var latitude = location.Latitude.ToString("F4", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("F4", CultureInfo.InvariantCulture);
        return $"v1/forecast?latitude={latitude}&longitude={longitude}&hourly={_options.HourlyVariable}&timezone=UTC";
    }

    private static List<ForecastPoint> ZipPoints(List<string> times, List<double?> values)
    {
        var count = Math.Min(times.Count, values.Count);
        var points = new List<ForecastPoint>(count);

        for (var i = 0; i < count; i++)
        {
            if (values[i] is { } value
                && DateTimeOffset.TryParse(
                    times[i],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp))
            {
                points.Add(new ForecastPoint(timestamp, value));
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
