using System.Net;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Meteo.Infrastructure.Weather;
using Meteo.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Ce qui est propre à MET Norway : le User-Agent obligatoire (politique d'usage, TP2.md)
/// et l'unité donnée en toutes lettres ("celsius"). Le contrat partagé avec tout
/// <see cref="IWeatherProvider"/> HTTP vit dans <see cref="MetNoWeatherProviderContractTests"/>.
/// </summary>
public sealed class MetNoWeatherClientTests
{
    private static readonly GeoLocation Ales = new(44.1258, 4.0806, "Alès");

    // Capturée par un appel réel à l'API le 2026-09-18 (GET /compact?lat=44.1258&lon=4.0806), réduite à deux points.
    private const string ResponseBody = """
        {
          "type": "Feature",
          "geometry": { "type": "Point", "coordinates": [4.0806, 44.1258, 127] },
          "properties": {
            "meta": { "units": { "air_temperature": "celsius" } },
            "timeseries": [
              { "time": "2026-09-18T15:00:00Z", "data": { "instant": { "details": { "air_temperature": 27.1 } } } },
              { "time": "2026-09-18T16:00:00Z", "data": { "instant": { "details": { "air_temperature": 26.2 } } } }
            ]
          }
        }
        """;

    private static (MetNoWeatherClient Client, StubHttpMessageHandler Handler) CreateSut(string userAgent = "TP2-MeteoApi/1.0 (test)")
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://met-no.example/") };
        var options = Options.Create(new MetNoOptions { UserAgent = userAgent });
        var client = new MetNoWeatherClient(httpClient, options, NullLogger<MetNoWeatherClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task GetForecastAsync_sends_the_configured_User_Agent_on_every_request()
    {
        var (sut, handler) = CreateSut(userAgent: "TP2-MeteoApi/1.0 diego.carriere27@gmail.com");
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) });

        await sut.GetForecastAsync(Ales, CancellationToken.None);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("TP2-MeteoApi/1.0 diego.carriere27@gmail.com", sent.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task GetForecastAsync_maps_timeseries_into_ordered_points()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) });

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Points.Count);
        Assert.Equal(27.1, result.Value.Points[0].Value);
        Assert.Equal(26.2, result.Value.Points[1].Value);
        Assert.Equal(WeatherVariables.AirTemperature, result.Value.Variable);
    }

    [Fact]
    public async Task GetForecastAsync_normalizes_the_celsius_unit_to_its_symbol()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) });

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.Equal("°C", result.Value.Unit);
    }

    [Fact]
    public async Task GetForecastAsync_returns_WeatherUnavailable_when_rejected_for_a_missing_or_generic_User_Agent()
    {
        // MET Norway rejette avec 403 un User-Agent absent ou générique (TP2.md) : simulé
        // ici côté transport, le vrai comportement dépend du service et n'est jamais testé
        // en le contactant réellement (cf. la suite hors-ligne, déterministe).
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.WeatherUnavailable, result.Error.Kind);
    }
}

/// <summary>
/// Instancie le contrat HTTP partagé (<see cref="HttpWeatherProviderContractTests"/>) pour
/// le vrai client MET Norway : réponse valide, panne amont, réponse vide, culture invariante.
/// </summary>
public sealed class MetNoWeatherProviderContractTests : HttpWeatherProviderContractTests
{
    protected override IWeatherProvider CreateSut(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://met-no.example/") };
        var options = Options.Create(new MetNoOptions { UserAgent = "TP2-MeteoApi/1.0 (test)" });
        return new MetNoWeatherClient(httpClient, options, NullLogger<MetNoWeatherClient>.Instance);
    }

    // Capturée par un appel réel à l'API le 2026-09-18, réduite à deux points.
    protected override string ValidForecastBody => """
        {
          "type": "Feature",
          "geometry": { "type": "Point", "coordinates": [4.0806, 44.1258, 127] },
          "properties": {
            "meta": { "units": { "air_temperature": "celsius" } },
            "timeseries": [
              { "time": "2026-09-18T15:00:00Z", "data": { "instant": { "details": { "air_temperature": 27.1 } } } },
              { "time": "2026-09-18T16:00:00Z", "data": { "instant": { "details": { "air_temperature": 26.2 } } } }
            ]
          }
        }
        """;

    protected override string EmptyBody => "not json";
}
