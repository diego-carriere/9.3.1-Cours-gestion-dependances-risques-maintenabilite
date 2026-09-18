using System.Diagnostics;
using Meteo.Api.Http;
using Meteo.Domain.Results;

namespace Meteo.Api.Errors;

/// <summary>
/// Le seul endroit de la solution qui traduit <see cref="ForecastErrorKind"/> (vocabulaire
/// métier) en code de statut HTTP. Un <c>switch</c> sur enum n'est jamais réellement
/// exhaustif en C# (CS8524 : une valeur entière hors plage reste possible), donc une
/// clause <c>default</c> qui lève est nécessaire ici — l'exhaustivité "toute valeur
/// déclarée est gérée" est vérifiée par un test dédié
/// (Meteo.Api.E2ETests.ForecastErrorMapperTests), pas par le compilateur seul.
/// </summary>
internal static class ForecastErrorMapper
{
    public static IResult ToProblem(ForecastError error) => error.Kind switch
    {
        ForecastErrorKind.InvalidAddress => Results.Problem(
            detail: error.Message,
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://tp1-meteo.local/problems/invalid-address"),

        ForecastErrorKind.AddressNotFound => Results.Problem(
            detail: error.Message,
            statusCode: StatusCodes.Status404NotFound,
            type: "https://tp1-meteo.local/problems/address-not-found"),

        ForecastErrorKind.GeocodingUnavailable => Results.Problem(
            detail: error.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: "https://tp1-meteo.local/problems/geocoding-unavailable")
            .WithHeader("Retry-After", "30"),

        ForecastErrorKind.WeatherUnavailable => Results.Problem(
            detail: error.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: "https://tp1-meteo.local/problems/weather-unavailable")
            .WithHeader("Retry-After", "30"),

        ForecastErrorKind.UpstreamTimeout => Results.Problem(
            detail: error.Message,
            statusCode: StatusCodes.Status504GatewayTimeout,
            type: "https://tp1-meteo.local/problems/upstream-timeout"),

        _ => throw new UnreachableException($"Unhandled {nameof(ForecastErrorKind)}: {error.Kind}"),
    };
}
