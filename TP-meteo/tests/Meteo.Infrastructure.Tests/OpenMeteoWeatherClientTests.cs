using System.Net;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Infrastructure.Weather;
using Meteo.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Ce qui est propre à Open-Meteo (tableaux parallèles à zipper, lecture d'une variable
/// configurée par nom). Le comportement partagé avec tout
/// <see cref="Meteo.Domain.Abstractions.IWeatherProvider"/> HTTP (réponse valide, panne
/// amont, réponse vide, culture invariante) vit dans
/// <see cref="OpenMeteoWeatherProviderContractTests"/>.
/// </summary>
public sealed class OpenMeteoWeatherClientTests
{
    private static readonly GeoLocation Ales = new(44.1258, 4.0806, "Alès");

    private const string ResponseBody = """
        {
          "hourly_units": { "temperature_2m": "°C" },
          "hourly": {
            "time": ["2026-09-18T00:00", "2026-09-18T01:00", "2026-09-18T02:00"],
            "temperature_2m": [8.0, 12.5, 18.4]
          }
        }
        """;

    private static (OpenMeteoWeatherClient Client, StubHttpMessageHandler Handler) CreateSut()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://open-meteo.example/") };
        var client = new OpenMeteoWeatherClient(httpClient, Options.Create(new OpenMeteoOptions()), NullLogger<OpenMeteoWeatherClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task GetForecastAsync_maps_parallel_arrays_into_ordered_points()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) });

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Points.Count);
        Assert.Equal(18.4, result.Value.Points[2].Value);
        Assert.Equal(WeatherVariables.AirTemperature, result.Value.Variable);
    }

    [Fact]
    public async Task GetForecastAsync_handles_mismatched_array_lengths_without_crashing()
    {
        const string mismatched = """
            {
              "hourly": {
                "time": ["2026-09-18T00:00", "2026-09-18T01:00"],
                "temperature_2m": [8.0]
              }
            }
            """;
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(mismatched) });

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Points);
    }

    [Fact]
    public async Task GetForecastAsync_skips_hours_without_a_value()
    {
        const string withNull = """
            {
              "hourly": {
                "time": ["2026-09-18T00:00", "2026-09-18T01:00", "2026-09-18T02:00"],
                "temperature_2m": [8.0, null, 18.4]
              }
            }
            """;
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(withNull) });

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([8.0, 18.4], result.Value.Points.Select(p => p.Value));
    }

    [Fact]
    public async Task GetForecastAsync_reads_a_differently_configured_hourly_variable()
    {
        // Le bug corrigé au TP2 : au TP1, la désérialisation restait câblée sur
        // shortwave_radiation quelle que soit la valeur de HourlyVariable en configuration.
        const string radiationBody = """
            {
              "hourly_units": { "shortwave_radiation": "W/m²" },
              "hourly": {
                "time": ["2026-09-18T00:00"],
                "shortwave_radiation": [340.2]
              }
            }
            """;
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://open-meteo.example/") };
        var client = new OpenMeteoWeatherClient(
            httpClient,
            Options.Create(new OpenMeteoOptions { HourlyVariable = "shortwave_radiation" }),
            NullLogger<OpenMeteoWeatherClient>.Instance);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(radiationBody) });

        var result = await client.GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(340.2, result.Value.Points[0].Value);
        Assert.Equal("W/m²", result.Value.Unit);

        // La variable exposée suit celle réellement demandée : un rayonnement étiqueté
        // air_temperature serait une réponse fausse, pas seulement mal nommée.
        Assert.Equal(WeatherVariables.ShortwaveRadiation, result.Value.Variable);
    }

    [Fact]
    public async Task GetForecastAsync_falls_back_to_the_unit_of_the_configured_variable()
    {
        const string withoutUnits = """
            { "hourly": { "time": ["2026-09-18T00:00"], "shortwave_radiation": [340.2] } }
            """;
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://open-meteo.example/") };
        var client = new OpenMeteoWeatherClient(
            httpClient,
            Options.Create(new OpenMeteoOptions { HourlyVariable = "shortwave_radiation" }),
            NullLogger<OpenMeteoWeatherClient>.Instance);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(withoutUnits) });

        var result = await client.GetForecastAsync(Ales, CancellationToken.None);

        Assert.Equal("W/m²", result.Value.Unit);
    }
}

/// <summary>
/// Instancie le contrat HTTP partagé (<see cref="HttpWeatherProviderContractTests"/>) pour
/// le vrai client Open-Meteo : réponse valide, panne amont, réponse vide, culture invariante.
/// </summary>
public sealed class OpenMeteoWeatherProviderContractTests : HttpWeatherProviderContractTests
{
    protected override IWeatherProvider CreateSut(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://open-meteo.example/") };
        var options = Options.Create(new OpenMeteoOptions());
        return new OpenMeteoWeatherClient(httpClient, options, NullLogger<OpenMeteoWeatherClient>.Instance);
    }

    protected override string ValidForecastBody => """
        {
          "hourly_units": { "temperature_2m": "°C" },
          "hourly": {
            "time": ["2026-09-18T00:00", "2026-09-18T01:00"],
            "temperature_2m": [8.0, 12.5]
          }
        }
        """;

    protected override string EmptyBody => "not json";
}
