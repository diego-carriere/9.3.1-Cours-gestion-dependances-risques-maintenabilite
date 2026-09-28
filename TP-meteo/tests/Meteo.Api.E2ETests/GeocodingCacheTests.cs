using System.Net;
using System.Text;

namespace Meteo.Api.E2ETests;

/// <summary>
/// TP3, « deux appels successifs portant sur la même adresse ne déclenchent qu'un seul appel
/// réseau réel vers le service de géocodage actif ». Satisfait depuis le TP1 par
/// ICache&lt;GeoLocation&gt; dans GetForecastUseCase ; prouvé ici pour chaque géocodeur. La
/// fraîcheur météo vaut 1 ms dans MeteoApiFactory : Open-Meteo est donc scripté deux fois.
/// </summary>
public sealed class GeocodingCacheTests
{
    private const string NominatimHost = "nominatim.openstreetmap.org";
    private const string BanHost = "api-adresse.data.gouv.fr";
    private const string OpenMeteoHost = "api.open-meteo.com";

    private const string NominatimMatch = """
        [{ "lat": "44.1258858", "lon": "4.0806915", "display_name": "Alès, Gard, France" }]
        """;

    private const string BanMatch = """
        {"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[4.089242,44.125356]},"properties":{"label":"Alès"}}],"query":"Alès"}
        """;

    private const string OpenMeteoForecast = """
        {
          "hourly_units": { "temperature_2m": "°C" },
          "hourly": { "time": ["2026-09-18T00:00"], "temperature_2m": [8.0] }
        }
        """;

    public static TheoryData<string, string, string> Geocoders => new()
    {
        { "nominatim", NominatimHost, NominatimMatch },
        { "ban", BanHost, BanMatch },
    };

    [Theory]
    [MemberData(nameof(Geocoders))]
    public async Task Two_successive_requests_for_the_same_address_call_the_active_geocoder_once(
        string geocoder, string host, string match)
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?> { ["Providers:Geocoder"] = geocoder });
        factory.Upstream.EnqueueFor(host, Json(match));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        using var client = factory.CreateClient();

        var first = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));
        var second = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, factory.Upstream.CallCountFor(host));
    }

    [Fact]
    public async Task The_same_address_written_differently_calls_the_geocoder_once()
    {
        // Address.CacheKey normalise espaces et casse. Sous InvariantGlobalization=true, .NET 8+
        // met aussi en minuscule les lettres non ASCII (« È » → « è ») : ce test le fige.
        using var factory = new MeteoApiFactory();
        factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        using var client = factory.CreateClient();

        await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));
        var second = await client.GetAsync(
            new Uri($"/forecast?address={Uri.EscapeDataString("  ALÈS  ")}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, factory.Upstream.CallCountFor(NominatimHost));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
