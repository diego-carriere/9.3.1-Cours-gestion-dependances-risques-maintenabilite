using System.Net;
using System.Net.Http.Json;
using System.Text;
using Meteo.Api.Contracts;

namespace Meteo.Api.E2ETests;

/// <summary>
/// TP3, mode démo, vu de l'extérieur. FakeUpstream lève sur toute requête non scriptée, donc
/// un appel sortant, même accidentel, ferait échouer ces tests. Qu'aucune requête ne soit
/// enregistrée prouve « ne jamais appeler les services externes réels ».
/// </summary>
public sealed class DemoModeTests : ApiTestBase
{
    private const string NominatimHost = "nominatim.openstreetmap.org";
    private const string OpenMeteoHost = "api.open-meteo.com";

    private const string NominatimMatch = """
        [{ "lat": "44.1258858", "lon": "4.0806915", "display_name": "Alès, Gard, France" }]
        """;

    private const string OpenMeteoForecast = """
        {
          "hourly_units": { "temperature_2m": "°C" },
          "hourly": { "time": ["2026-09-18T00:00"], "temperature_2m": [8.0] }
        }
        """;

    [Fact]
    public async Task Demo_mode_answers_without_contacting_any_external_service()
    {
        var response = await Client.GetAsync(new Uri("/forecast?address=Alès&demo=true", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Factory.Upstream.Requests);
        Assert.Equal("demo", response.Headers.GetValues("X-Data-Source").Single());
    }

    [Fact]
    public async Task Demo_mode_returns_exactly_the_unified_shape_and_echoes_the_address()
    {
        var response = await Client.GetAsync(new Uri("/forecast?address=Alès&demo=true", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(JsonShape.Unified, JsonShape.Of(body));
        var payload = await response.Content.ReadFromJsonAsync<ForecastResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Alès", payload.Address);
    }

    [Fact]
    public async Task Demo_mode_still_rejects_a_missing_address_with_400()
    {
        var response = await Client.GetAsync(new Uri("/forecast?demo=true", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(Factory.Upstream.Requests);
    }

    [Fact]
    public async Task Demo_mode_never_leaks_into_the_real_geocoding_cache()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        var demoFirst = await GetForecastAsync("/forecast?address=Alès&demo=true");
        var real = await GetForecastAsync("/forecast?address=Alès");
        var demoAgain = await GetForecastAsync("/forecast?address=Alès&demo=true");

        // La démo n'a rien écrit dans le cache réel : Nominatim a bien été appelé pour la vraie requête…
        Assert.Equal(1, Factory.Upstream.CallCountFor(NominatimHost));
        Assert.Equal(44.1258858, real.Latitude, precision: 6);
        // …et la démo ne relit pas non plus le cache réel.
        Assert.Equal(demoFirst.Latitude, demoAgain.Latitude);
        Assert.NotEqual(real.Latitude, demoAgain.Latitude);
    }

    [Fact]
    public async Task Demo_false_uses_the_real_providers()
    {
        Factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        Factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        var response = await Client.GetAsync(new Uri("/forecast?address=Alès&demo=false", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, Factory.Upstream.CallCountFor(NominatimHost));
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("1")]
    public async Task An_unparseable_demo_value_is_rejected_rather_than_guessed(string value)
    {
        var response = await Client.GetAsync(new Uri($"/forecast?address=Alès&demo={value}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Factory.Upstream.Requests);
    }

    private async Task<ForecastResponse> GetForecastAsync(string relativeUri)
    {
        var response = await Client.GetAsync(new Uri(relativeUri, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ForecastResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
