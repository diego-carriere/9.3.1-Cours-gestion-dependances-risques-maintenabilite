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
/// Implémente <see cref="IWeatherProvider"/> par un appel à MET Norway Locationforecast.
/// Même patron que <see cref="OpenMeteoWeatherClient"/> : mêmes catégories de
/// <see cref="Result{T}"/>, jamais d'exception qui fuit. Deux traits propres à ce
/// fournisseur : le User-Agent est obligatoire (voir <see cref="MetNoOptions"/>, sous
/// peine de 403) et l'unité de température est donnée en toutes lettres
/// (<c>"celsius"</c>), normalisée ici en <c>"°C"</c> avant de franchir la frontière du
/// Domaine.
/// </summary>
internal sealed partial class MetNoWeatherClient : IWeatherProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string AirTemperatureField = "air_temperature";

    private readonly HttpClient _httpClient;
    private readonly MetNoOptions _options;
    private readonly ILogger<MetNoWeatherClient> _logger;

    public MetNoWeatherClient(HttpClient httpClient, IOptions<MetNoOptions> options, ILogger<MetNoWeatherClient> logger)
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
                .SendAsync(BuildRequest(location), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsTimeout(ex, cancellationToken))
        {
            LogTimeout(ex, location.Latitude, location.Longitude);
            return Result.Failure<Forecast>(ForecastErrorKind.UpstreamTimeout, "MET Norway a dépassé le délai imparti.");
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException)
        {
            LogUnavailable(ex, location.Latitude, location.Longitude);
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "MET Norway est indisponible.");
        }

        if (!response.IsSuccessStatusCode)
        {
            LogNonSuccessStatus(response.StatusCode);
            return Result.Failure<Forecast>(
                ForecastErrorKind.WeatherUnavailable, $"MET Norway a répondu {(int)response.StatusCode}.");
        }

        MetNoForecastDto? payload;
        try
        {
            payload = await response.Content
                .ReadFromJsonAsync<MetNoForecastDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            LogMalformedBody(ex);
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "Réponse MET Norway illisible.");
        }

        if (payload?.Properties?.Timeseries is null or [])
        {
            LogUnexpectedPayload();
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "Réponse MET Norway inattendue.");
        }

        var points = ExtractPoints(payload.Properties.Timeseries);

        if (points.Count == 0)
        {
            LogUnexpectedPayload();
            return Result.Failure<Forecast>(ForecastErrorKind.WeatherUnavailable, "Réponse MET Norway inattendue.");
        }

        var unit = NormalizeUnit(ReadUnit(payload.Properties.Meta?.Units));

        return Result.Success(new Forecast(location, WeatherVariables.AirTemperature, unit, points));
    }

    private HttpRequestMessage BuildRequest(GeoLocation location)
    {
        // MET Norway limite les coordonnées à 4 décimales ; le formatage explicitement
        // invariant évite aussi la dépendance cachée "séparateur décimal" de Support J1.
        var latitude = location.Latitude.ToString("F4", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("F4", CultureInfo.InvariantCulture);
        var uri = $"weatherapi/locationforecast/2.0/compact?lat={latitude}&lon={longitude}";

        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Politique d'usage MET Norway : User-Agent descriptif obligatoire, sous peine de
        // 403 — voir TryAddWithoutValidation dans NominatimGeocodingClient pour le même
        // raisonnement (la valeur vient de la configuration, jamais d'une chaîne littérale).
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

        return request;
    }

    private static List<ForecastPoint> ExtractPoints(List<MetNoTimeseriesEntryDto> timeseries)
    {
        var points = new List<ForecastPoint>(timeseries.Count);

        foreach (var entry in timeseries)
        {
            if (!TryReadAirTemperature(entry.Data?.Instant?.Details, out var value))
            {
                continue;
            }

            if (DateTimeOffset.TryParse(
                    entry.Time,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp))
            {
                points.Add(new ForecastPoint(timestamp, value));
            }
        }

        return points;
    }

    private static bool TryReadAirTemperature(Dictionary<string, JsonElement>? details, out double value)
    {
        value = default;

        if (details is null
            || !details.TryGetValue(AirTemperatureField, out var element)
            || element.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        value = element.GetDouble();
        return true;
    }

    private static string? ReadUnit(Dictionary<string, string>? units) =>
        units is not null && units.TryGetValue(AirTemperatureField, out var unit) ? unit : null;

    // MET Norway annonce l'unité en toutes lettres ("celsius") : traduite ici en symbole,
    // pour que le contrat de sortie ne dépende jamais du vocabulaire d'un fournisseur.
    private static string NormalizeUnit(string? rawUnit) =>
        string.Equals(rawUnit, "celsius", StringComparison.OrdinalIgnoreCase) ? "°C" : rawUnit ?? "°C";

    private static bool IsTimeout(Exception ex, CancellationToken requestToken) =>
        ex is TimeoutRejectedException
        || (ex is OperationCanceledException && !requestToken.IsCancellationRequested);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MET Norway timed out for {Latitude},{Longitude}.")]
    private partial void LogTimeout(Exception exception, double latitude, double longitude);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MET Norway unavailable for {Latitude},{Longitude}.")]
    private partial void LogUnavailable(Exception exception, double latitude, double longitude);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MET Norway returned {StatusCode}.")]
    private partial void LogNonSuccessStatus(System.Net.HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MET Norway returned an unexpected payload shape.")]
    private partial void LogUnexpectedPayload();

    [LoggerMessage(Level = LogLevel.Warning, Message = "MET Norway returned a malformed body.")]
    private partial void LogMalformedBody(Exception exception);
}
