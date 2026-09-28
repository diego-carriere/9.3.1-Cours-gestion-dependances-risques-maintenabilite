using Meteo.Api.Contracts;
using Meteo.Api.Errors;
using Meteo.Api.Http;
using Meteo.Application.Forecasting;

namespace Meteo.Api.Endpoints;

/// <summary>
/// <c>GET /forecast?address=...</c>. Ne fait que traduire : délègue tout le travail à
/// <see cref="IGetForecastUseCase"/> (injecté, jamais <c>new</c>é) et met en forme le
/// résultat. Aucune règle métier ici — c'est la couche Présentation, pas l'Application.
/// </summary>
internal static class ForecastEndpoint
{
    public static IEndpointRouteBuilder MapForecastEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/forecast", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string? address,
        IGetForecastUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(address, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return ForecastErrorMapper.ToProblem(result.Error);
        }

        var forecastResult = result.Value;
        var response = ToResponse(forecastResult);
        var ok = Results.Ok(response);

        // Simplification assumée : on ne distingue que dégradé/non dégradé, pas les trois
        // états (live/cache-fresh/cache-stale) — ForecastResult ne porte pas cette nuance
        // et aucun scénario testé n'en a besoin (voir README).
        return forecastResult.IsDegraded
            ? ok.WithHeader("X-Data-Source", "cache-stale").WithHeader("Warning", "110 - \"Response is stale\"")
            : ok.WithHeader("X-Data-Source", "live");
    }

    private static ForecastResponse ToResponse(ForecastResult result) => new(
        RequestedAddress: result.RequestedAddress.Value,
        ResolvedPlace: result.Forecast.Location.DisplayName,
        Latitude: result.Forecast.Location.Latitude,
        Longitude: result.Forecast.Location.Longitude,
        Variable: result.Forecast.Variable,
        Unit: result.Forecast.Unit,
        Hourly: [.. result.Forecast.Points.Select(p => new ForecastPointResponse(p.TimestampUtc, p.Value))],
        Degraded: result.IsDegraded,
        DataAsOf: result.DataAsOfUtc);
}
