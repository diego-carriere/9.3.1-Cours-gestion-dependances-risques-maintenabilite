using System.Net;
using System.Net.Http.Json;
using System.Text;
using Meteo.Api.Contracts;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Prouve de bout en bout ce que Support J1 appelle "circuit breaker et cache pour un mode
/// dégradé" (stratégie SPOF), et le bénéfice concret du cache de géocodage face à la
/// politique de débit de Nominatim (1 req/s). MeteoApiFactory réduit WeatherFreshness à
/// 1 ms pour ces tests : la fraîcheur du géocodage (24 h) reste au défaut.
/// </summary>
public sealed class DegradedModeTests : ApiTestBase
{
    private const string NominatimHost = "nominatim.openstreetmap.org";
    private const string OpenMeteoHost = "api.open-meteo.com";

    private const string NominatimMatch = """
        [{ "lat": "44.1258858", "lon": "4.0806915", "display_name": "Alès, Gard, France" }]
        """;

    private const string OpenMeteoForecast = """
        {
          "hourly_units": { "shortwave_radiation": "W/m²" },
          "hourly": { "time": ["2026-09-18T00:00"], "shortwave_radiation": [0.0] }
        }
        """;

    [Fact]
    public async Task Forecast_degrades_to_a_stale_cache_when_weather_becomes_unavailable()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        var first = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Laisse le temps de dépasser WeatherFreshness (1 ms) avant le second appel.
        await Task.Delay(20);

        // Open-Meteo tombe : 1 tentative initiale + 3 retries (défaut Open-Meteo) à scripter.
        for (var i = 0; i < 4; i++)
        {
            Factory.Upstream.EnqueueFor(OpenMeteoHost, new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        var second = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("cache-stale", second.Headers.GetValues("X-Data-Source").Single());

        var payload = await second.Content.ReadFromJsonAsync<ForecastResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Degraded);

        // Le géocodage, lui, n'a jamais été réappelé : la fraîcheur (24h) a tenu.
        Assert.Equal(1, Factory.Upstream.CallCountFor(NominatimHost));
    }

    [Fact]
    public async Task Two_identical_requests_only_contact_Nominatim_once()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));
        await Task.Delay(20);
        var second = await Client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, Factory.Upstream.CallCountFor(NominatimHost));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
