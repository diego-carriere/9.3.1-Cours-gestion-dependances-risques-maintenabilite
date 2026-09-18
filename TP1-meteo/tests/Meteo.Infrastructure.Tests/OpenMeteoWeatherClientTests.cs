using System.Globalization;
using System.Net;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Meteo.Infrastructure.Weather;
using Meteo.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

public sealed class OpenMeteoWeatherClientTests
{
    private static readonly GeoLocation Ales = new(44.1258, 4.0806, "Alès");

    private const string ResponseBody = """
        {
          "hourly_units": { "shortwave_radiation": "W/m²" },
          "hourly": {
            "time": ["2026-09-18T00:00", "2026-09-18T01:00", "2026-09-18T02:00"],
            "shortwave_radiation": [0.0, 12.5, 340.2]
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
        Assert.Equal(340.2, result.Value.Points[2].Value);
        Assert.Equal("W/m²", result.Value.Unit);
    }

    [Fact]
    public async Task GetForecastAsync_handles_mismatched_array_lengths_without_crashing()
    {
        const string mismatched = """
            {
              "hourly": {
                "time": ["2026-09-18T00:00", "2026-09-18T01:00"],
                "shortwave_radiation": [0.0]
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
    public async Task GetForecastAsync_reads_the_unit_from_hourly_units()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) });

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.Equal("W/m²", result.Value.Unit);
    }

    [Fact]
    public async Task GetForecastAsync_builds_a_culture_invariant_url_under_fr_FR()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

        try
        {
            var (sut, handler) = CreateSut();
            handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) });

            await sut.GetForecastAsync(Ales, CancellationToken.None);

            var sent = Assert.Single(handler.Requests);
            var query = sent.RequestUri!.Query;
            Assert.Contains("latitude=44.1258", query, StringComparison.Ordinal);
            Assert.Contains("longitude=4.0806", query, StringComparison.Ordinal);
            Assert.DoesNotContain(',', query);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task GetForecastAsync_returns_WeatherUnavailable_for_a_persisting_server_error()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.WeatherUnavailable, result.Error.Kind);
    }
}
