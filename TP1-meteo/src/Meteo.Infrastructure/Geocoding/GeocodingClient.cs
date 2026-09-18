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
/// Implémente <see cref="IGeocoder"/> par un appel à Nominatim. Internal : le seul type
/// public de cet assembly est <c>InfrastructureServiceCollectionExtensions</c>, ce qui rend
/// "pas de <c>new</c> hors composition root" garanti par le compilateur (Support J1, "le
/// problème du new partout").
///
/// Contrat de substituabilité (LSP) : ne laisse jamais fuir d'exception HTTP ou Polly, même
/// quand le circuit breaker est ouvert — tout devient un <see cref="Result{T}"/> en échec.
/// </summary>
internal sealed partial class GeocodingClient : IGeocoder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly NominatimOptions _options;
    private readonly ILogger<GeocodingClient> _logger;

    public GeocodingClient(HttpClient httpClient, IOptions<NominatimOptions> options, ILogger<GeocodingClient> logger)
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
            return Result.Failure<GeoLocation>(ForecastErrorKind.UpstreamTimeout, "Nominatim a dépassé le délai imparti.");
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException)
        {
            LogUnavailable(ex, address.Value);
            return Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "Nominatim est indisponible.");
        }

        if (!response.IsSuccessStatusCode)
        {
            LogNonSuccessStatus(response.StatusCode, address.Value);
            return Result.Failure<GeoLocation>(
                ForecastErrorKind.GeocodingUnavailable, $"Nominatim a répondu {(int)response.StatusCode}.");
        }

        List<NominatimPlaceDto>? places;
        try
        {
            places = await response.Content
                .ReadFromJsonAsync<List<NominatimPlaceDto>>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            LogMalformedBody(ex, address.Value);
            return Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "Réponse Nominatim illisible.");
        }

        if (places is null or [])
        {
            return Result.Failure<GeoLocation>(ForecastErrorKind.AddressNotFound, $"Aucun résultat pour '{address.Value}'.");
        }

        var place = places[0];

        if (!TryParseCoordinate(place.Lat, out var latitude) || !TryParseCoordinate(place.Lon, out var longitude))
        {
            LogUnparsableCoordinate(address.Value);
            return Result.Failure<GeoLocation>(ForecastErrorKind.GeocodingUnavailable, "Coordonnées Nominatim illisibles.");
        }

        return Result.Success(new GeoLocation(latitude, longitude, place.DisplayName));
    }

    private HttpRequestMessage BuildRequest(Address address)
    {
        var query = Uri.EscapeDataString(address.Value);
        var uri = $"search?q={query}&format=jsonv2&limit=1&accept-language={_options.AcceptLanguage}";

        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Politique d'usage Nominatim : User-Agent descriptif obligatoire, sous peine de 403.
        // TryAddWithoutValidation plutôt que Headers.UserAgent.ParseAdd : la valeur vient de
        // la configuration et ne doit pas faire planter une requête sur une chaîne atypique.
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

        return request;
    }

    // Nominatim renvoie toujours lat/lon à séparateur point : parsing explicitement en
    // culture invariante, indépendamment de la culture d'exécution du processus.
    private static bool TryParseCoordinate(string raw, out double value) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool IsTimeout(Exception ex, CancellationToken requestToken) =>
        ex is TimeoutRejectedException
        || (ex is OperationCanceledException && !requestToken.IsCancellationRequested);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nominatim timed out resolving '{Address}'.")]
    private partial void LogTimeout(Exception exception, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nominatim unavailable resolving '{Address}'.")]
    private partial void LogUnavailable(Exception exception, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nominatim returned {StatusCode} resolving '{Address}'.")]
    private partial void LogNonSuccessStatus(System.Net.HttpStatusCode statusCode, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nominatim returned an unparsable coordinate for '{Address}'.")]
    private partial void LogUnparsableCoordinate(string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nominatim returned a malformed body resolving '{Address}'.")]
    private partial void LogMalformedBody(Exception exception, string address);
}
