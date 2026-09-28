using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Meteo.Api.Contracts;
using Meteo.Api.Endpoints;
using Meteo.Application.Forecasting;
using Microsoft.Extensions.DependencyInjection;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Bout en bout : hôte réel, routage réel, conteneur réel — seul le socket sortant vers
/// Nominatim/Open-Meteo est remplacé (<see cref="FakeUpstream"/>). Aucun appel réseau dans
/// cette suite : déterministe, hors-ligne, jamais soumise au rate-limit de Nominatim.
/// </summary>
public sealed class ForecastEndpointTests : ApiTestBase
{
    private const string NominatimHost = "nominatim.openstreetmap.org";
    private const string OpenMeteoHost = "api.open-meteo.com";

    private const string NominatimMatch = """
        [
          {
            "lat": "44.1258858",
            "lon": "4.0806915",
            "display_name": "Alès, Gard, Occitanie, France métropolitaine, France"
          }
        ]
        """;

    private const string OpenMeteoForecast = """
        {
          "hourly_units": { "temperature_2m": "°C" },
          "hourly": {
            "time": ["2026-09-18T00:00", "2026-09-18T01:00"],
            "temperature_2m": [8.0, 12.5]
          }
        }
        """;

    [Fact]
    public async Task Forecast_for_a_known_address_returns_200_with_our_own_contract()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        var response = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hourly_units", body, StringComparison.Ordinal);
        Assert.DoesNotContain("temperature_2m\":[", body, StringComparison.Ordinal);

        var payload = await response.Content.ReadFromJsonAsync<ForecastResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Alès", payload.Address);
        Assert.Equal("live", response.Headers.GetValues("X-Data-Source").Single());
        Assert.Equal(2, payload.Hourly.Count);
    }

    [Fact]
    public async Task Forecast_body_has_exactly_the_unified_shape_with_Z_suffixed_times()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        var response = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(JsonShape.Unified, JsonShape.Of(body));
        using var document = JsonDocument.Parse(body);
        var hourly = document.RootElement.GetProperty("hourly");
        Assert.Equal("2026-09-18T00:00:00Z", hourly[0].GetProperty("time").GetString());
        Assert.Equal(12.5, hourly[1].GetProperty("temperatureCelsius").GetDouble());
    }

    [Fact]
    public async Task Forecast_carries_the_date_of_its_data_in_Last_Modified()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        var response = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.NotNull(response.Content.Headers.LastModified);
    }

    [Fact]
    public void A_non_temperature_Open_Meteo_variable_prevents_the_host_from_starting()
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?> { ["OpenMeteo:HourlyVariable"] = "shortwave_radiation" });

        Assert.NotNull(Record.Exception(() => factory.CreateClient()));
    }

    [Fact]
    public async Task Forecast_with_a_missing_hourly_value_still_returns_200()
    {
        // Open-Meteo renvoie null pour une heure sans donnée : un NaN qui atteindrait la
        // sérialisation JSON de la réponse ferait échouer toute la requête.
        const string withNull = """
            {
              "hourly_units": { "temperature_2m": "°C" },
              "hourly": { "time": ["2026-09-18T00:00", "2026-09-18T01:00"], "temperature_2m": [8.0, null] }
            }
            """;
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(withNull));

        var response = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ForecastResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload.Hourly);
    }

    [Fact]
    public async Task Forecast_without_an_address_returns_400_problem_json()
    {
        var response = await Client.GetAsync(new Uri("/forecast", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Forecast_for_an_unknown_address_returns_404()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json("[]"));

        var response = await Client.GetAsync(new Uri("/forecast?address=zzzzzz", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Forecast_returns_503_with_retry_after_when_geocoding_is_down()
    {
        // Le pipeline de résilience réessaie : 1 tentative initiale + 2 retries (défaut
        // Nominatim) avant d'abandonner. Il faut scripter les trois réponses.
        for (var i = 0; i < 3; i++)
        {
            Factory.Upstream.EnqueueFor(NominatimHost, new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        var response = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task Health_endpoint_returns_200()
    {
        var response = await Client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Rend exécutable la slide "durées de vie / dépendance captive" de Support J1 :
    /// construire l'hôte réel avec ValidateScopes/ValidateOnBuild (Program.cs) réussit,
    /// c'est-à-dire qu'aucune dépendance captive n'existe dans le graphe.
    /// </summary>
    [Fact]
    public void The_DI_container_builds_without_any_captive_dependency()
    {
        using var factory = new MeteoApiFactory();

        // IGetForecastUseCase est Scoped : le résoudre depuis une vraie portée (comme le
        // fait chaque requête HTTP) doit réussir sans capturer de service au mauvais niveau.
        var exception = Record.Exception(() =>
        {
            using var scope = factory.Services.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<IGetForecastUseCase>();
            _ = scope.ServiceProvider.GetRequiredKeyedService<IGetForecastUseCase>(ForecastEndpoint.DemoServiceKey);
        });

        Assert.Null(exception);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
