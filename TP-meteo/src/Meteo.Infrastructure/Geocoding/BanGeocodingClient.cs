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

namespace Meteo.Infrastructure.Geocoding;

/// <summary>
/// Implémente <see cref="IGeocoder"/> par un appel à l'API Adresse de la Base Adresse
/// Nationale (le géocodeur souverain du TP2). Même patron que
/// <see cref="NominatimGeocodingClient"/> : mêmes catégories de <see cref="Result{T}"/>,
/// jamais d'exception qui fuit — voir sa note pour le raisonnement complet sur
/// l'accessibilité <c>internal</c> et le contrat de substituabilité.
/// </summary>
internal sealed partial class BanGeocodingClient : IGeocoder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly BanOptions _options;
    private readonly ILogger<BanGeocodingClient> _logger;

    public BanGeocodingClient(HttpClient httpClient, IOptions<BanOptions> options, ILogger<BanGeocodingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<GeoLocation>> ResolveAsync(Address address, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(BuildRequest(address), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsTimeout(ex, cancellationToken))
        {
            LogTimeout(ex, address.Value);
            return Result.Failure<GeoLocation>(ForecastErrorKind.UpstreamTimeout, "La BAN a dépassé le délai imparti.");
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException)
        {
            LogUnavailable(ex, address.Value);
            return Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "La BAN est indisponible.");
        }

        if (!response.IsSuccessStatusCode)
        {
            LogNonSuccessStatus(response.StatusCode, address.Value);
            return Result.Failure<GeoLocation>(
                ForecastErrorKind.GeocodingUnavailable, $"La BAN a répondu {(int)response.StatusCode}.");
        }

        BanFeatureCollectionDto? payload;
        try
        {
            payload = await response.Content
                .ReadFromJsonAsync<BanFeatureCollectionDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            LogMalformedBody(ex, address.Value);
            return Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "Réponse BAN illisible.");
        }

        if (payload?.Features is null or [])
        {
            return Result.Failure<GeoLocation>(ForecastErrorKind.AddressNotFound, $"Aucun résultat pour '{address.Value}'.");
        }

        var feature = payload.Features[0];

        // GeoJSON : [longitude, latitude], l'inverse de la paire lat/lon de Nominatim.
        if (feature.Geometry?.Coordinates is not [var longitude, var latitude, ..])
        {
            LogUnparsableCoordinate(address.Value);
            return Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "Coordonnées BAN illisibles.");
        }

        return Result.Success(new GeoLocation(latitude, longitude, feature.Properties?.Label ?? string.Empty));
    }

    private HttpRequestMessage BuildRequest(Address address)
    {
        var query = Uri.EscapeDataString(address.Value);
        var uri = $"search/?q={query}&limit={_options.Limit.ToString(CultureInfo.InvariantCulture)}";

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

        return request;
    }

    private static bool IsTimeout(Exception ex, CancellationToken requestToken) =>
        ex is TimeoutRejectedException
        || (ex is OperationCanceledException && !requestToken.IsCancellationRequested);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BAN timed out resolving '{Address}'.")]
    private partial void LogTimeout(Exception exception, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BAN unavailable resolving '{Address}'.")]
    private partial void LogUnavailable(Exception exception, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BAN returned {StatusCode} resolving '{Address}'.")]
    private partial void LogNonSuccessStatus(System.Net.HttpStatusCode statusCode, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BAN returned an unparsable coordinate for '{Address}'.")]
    private partial void LogUnparsableCoordinate(string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BAN returned a malformed body resolving '{Address}'.")]
    private partial void LogMalformedBody(Exception exception, string address);
}
