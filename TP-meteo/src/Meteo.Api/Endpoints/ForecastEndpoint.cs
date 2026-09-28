using System.Globalization;
using Meteo.Api.Contracts;
using Meteo.Api.Errors;
using Meteo.Api.Http;
using Meteo.Application.Forecasting;

namespace Meteo.Api.Endpoints;

/// <summary>
/// <c>GET /forecast?address=...[&amp;demo=true]</c>. Ne fait que traduire : délègue tout le travail à
/// <see cref="IGetForecastUseCase"/> (injecté, jamais <c>new</c>é) et met en forme le
/// résultat. Aucune règle métier ici — c'est la couche Présentation, pas l'Application.
/// </summary>
internal static class ForecastEndpoint
{
    /// <summary>
    /// Clé DI de la composition « mode démo » (TP3). Program.cs enregistre sous cette clé des
    /// ports simulés et une seconde instance du cas d'usage câblée sur eux.
    /// </summary>
    public const string DemoServiceKey = "demo";

    public static IEndpointRouteBuilder MapForecastEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/forecast", HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string? address,
        bool? demo,
        IGetForecastUseCase useCase,
        [FromKeyedServices(DemoServiceKey)] IGetForecastUseCase demoUseCase,
        CancellationToken cancellationToken)
    {
        // TP3 : ?demo=true ne change que la composition qui sert la requête. Les deux sont
        // injectées, sans new ni localisateur de services. Validation, mapping de la réponse et
        // mapping des erreurs sont communs : la forme de la réponse ne peut pas diverger. Une
        // valeur illisible (demo=maybe) est rejetée en 400 par le binding, jamais devinée.
        var isDemo = demo == true;
        var result = await (isDemo ? demoUseCase : useCase)
            .ExecuteAsync(address, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return ForecastErrorMapper.ToProblem(result.Error);
        }

        var forecastResult = result.Value;

        // Le corps ne porte que les quatre champs du contrat (TP3). L'origine et la date des
        // données voyagent en en-têtes. Simplification assumée : on ne distingue que dégradé
        // et non dégradé, pas les trois états live/cache-fresh/cache-stale — ForecastResult
        // ne porte pas cette nuance (voir README).
        var ok = Results.Ok(ForecastResponseMapper.ToResponse(forecastResult))
            .WithHeader("Last-Modified", forecastResult.DataAsOfUtc.ToString("R", CultureInfo.InvariantCulture));

        if (isDemo)
        {
            return ok.WithHeader("X-Data-Source", "demo");
        }

        return forecastResult.IsDegraded
            ? ok.WithHeader("X-Data-Source", "cache-stale").WithHeader("Warning", "110 - \"Response is stale\"")
            : ok.WithHeader("X-Data-Source", "live");
    }
}
