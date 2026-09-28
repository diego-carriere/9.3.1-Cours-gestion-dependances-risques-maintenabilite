using System.Net;
using System.Net.Http.Json;
using System.Text;
using Meteo.Api.Contracts;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Bout en bout, TP2 : le choix du fournisseur (demande n°2), les deux nouveaux adaptateurs
/// (demande n°1) et l'absence de fuite de format propriétaire dans la réponse publique
/// (demande n°4), le tout observé depuis l'extérieur de l'API — jamais en inspectant un
/// type interne, y compris les clés de fournisseur (littérales ici, comme dans
/// appsettings.json ; <c>Meteo.Infrastructure.Providers.ProviderKeys</c> est internal et
/// n'est pas visible de ce projet, volontairement). Seul le transport sortant est simulé
/// (<see cref="FakeUpstream"/>) ; hôte, routage, conteneur DI et pipeline de résilience
/// restent réels.
/// </summary>
public sealed class ProviderSwitchingTests
{
    private const string NominatimKey = "nominatim";
    private const string BanKey = "ban";
    private const string OpenMeteoKey = "open-meteo";
    private const string MetNoKey = "met-no";

    private const string NominatimHost = "nominatim.openstreetmap.org";
    private const string BanHost = "api-adresse.data.gouv.fr";
    private const string OpenMeteoHost = "api.open-meteo.com";
    private const string MetNoHost = "api.met.no";

    private const string NominatimMatch = """
        [{ "lat": "44.1258858", "lon": "4.0806915", "display_name": "Alès, Gard, France" }]
        """;

    private const string BanMatch = """
        {"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[4.089242,44.125356]},"properties":{"label":"Alès"}}],"query":"Alès"}
        """;

    private const string OpenMeteoForecast = """
        {
          "hourly_units": { "temperature_2m": "°C" },
          "hourly": { "time": ["2026-09-18T00:00"], "temperature_2m": [18.4] }
        }
        """;

    private const string MetNoForecast = """
        {
          "properties": {
            "meta": { "units": { "air_temperature": "celsius" } },
            "timeseries": [
              { "time": "2026-09-18T15:00:00Z", "data": { "instant": { "details": { "air_temperature": 27.1 } } } }
            ]
          }
        }
        """;

    private static readonly string[] ProviderLeakingFields =
    [
        "features", "coordinates", "banId", // BAN
        "timeseries", "instant", "air_pressure_at_sea_level", // MET Norway
        "hourly_units", "temperature_2m", // Open-Meteo
        "display_name", "importance", // Nominatim
    ];

    public static IEnumerable<object[]> AllProviderCombinations()
    {
        yield return [NominatimKey, OpenMeteoKey];
        yield return [NominatimKey, MetNoKey];
        yield return [BanKey, OpenMeteoKey];
        yield return [BanKey, MetNoKey];
    }

    [Theory]
    [MemberData(nameof(AllProviderCombinations))]
    public async Task Forecast_has_the_same_public_shape_for_every_provider_combination(string geocoder, string weather)
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?>
        {
            ["Providers:Geocoder"] = geocoder,
            ["Providers:Weather"] = weather,
        });
        EnqueueGeocoder(factory, geocoder);
        EnqueueWeather(factory, weather);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        foreach (var leakingField in ProviderLeakingFields)
        {
            Assert.DoesNotContain($"\"{leakingField}\"", body, StringComparison.Ordinal);
        }

        var payload = await response.Content.ReadFromJsonAsync<ForecastResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Alès", payload.RequestedAddress);
        Assert.NotEmpty(payload.ResolvedPlace);
        Assert.NotEmpty(payload.Hourly);
        Assert.Equal("air_temperature", payload.Variable);
        Assert.Equal("°C", payload.Unit);
    }

    [Fact]
    public async Task Switching_to_BAN_calls_the_BAN_and_never_Nominatim()
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?> { ["Providers:Geocoder"] = BanKey });
        factory.Upstream.EnqueueFor(BanHost, Json(BanMatch));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Upstream.CallCountFor(BanHost));
        Assert.Equal(0, factory.Upstream.CallCountFor(NominatimHost));
    }

    [Fact]
    public async Task Switching_to_MetNo_sends_a_descriptive_User_Agent()
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?> { ["Providers:Weather"] = MetNoKey });
        factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        factory.Upstream.EnqueueFor(MetNoHost, Json(MetNoForecast));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metNoRequest = Assert.Single(factory.Upstream.Requests, r => r.RequestUri?.Host == MetNoHost);
        var userAgent = metNoRequest.Headers.UserAgent.ToString();
        Assert.False(string.IsNullOrWhiteSpace(userAgent));
        Assert.DoesNotContain("python-requests", userAgent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("okhttp", userAgent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Weather_provider_switches_between_two_requests_without_recreating_the_host()
    {
        using var factory = new MeteoApiFactory();
        using var client = factory.CreateClient();

        factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        var first = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, factory.Upstream.CallCountFor(OpenMeteoHost));

        // "Sans redéploiement" (TP2) : le même hôte, en cours d'exécution, bascule vers
        // MET Norway parce que la configuration a changé — pas parce qu'on l'a redémarré.
        factory.ReloadableConfiguration.Set("Providers:Weather", MetNoKey);

        factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        factory.Upstream.EnqueueFor(MetNoHost, Json(MetNoForecast));
        var second = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, factory.Upstream.CallCountFor(MetNoHost));
        Assert.Equal(1, factory.Upstream.CallCountFor(OpenMeteoHost)); // toujours 1 : pas rappelé après la bascule
    }

    [Fact]
    public void An_unknown_provider_key_at_startup_prevents_the_host_from_starting()
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?> { ["Providers:Geocoder"] = "unknown-provider" });

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
    }

    [Fact]
    public async Task An_unknown_provider_key_after_a_hot_reload_falls_back_instead_of_failing_the_request()
    {
        using var factory = new MeteoApiFactory();
        using var client = factory.CreateClient(); // démarre l'hôte avec une configuration valide.

        // ValidateOnStart ne protège que le démarrage : après coup, une faute de frappe en
        // configuration ne doit pas couper le service — GeocoderSelector se replie sur
        // Nominatim (voir sa note) plutôt que de laisser échouer la requête.
        factory.ReloadableConfiguration.Set("Providers:Geocoder", "unknown-provider");
        factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));

        var response = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Upstream.CallCountFor(NominatimHost));
    }

    [Fact]
    public async Task An_invalid_User_Agent_after_a_hot_reload_neither_throws_nor_changes_the_one_in_use()
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?> { ["Providers:Weather"] = MetNoKey });
        using var client = factory.CreateClient();

        factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch));
        factory.Upstream.EnqueueFor(MetNoHost, Json(MetNoForecast));
        var first = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Même mécanisme que pour Providers : la validation au démarrage ne doit pas se
        // transformer en revalidation à chaque rechargement, levée hors de toute requête.
        var reload = Record.Exception(() => factory.ReloadableConfiguration.Set("MetNo:UserAgent", ""));
        Assert.Null(reload);

        // Autre adresse : Alès est encore en cache de géocodage (24 h), Nominatim ne serait pas rappelé.
        factory.Upstream.EnqueueFor(NominatimHost, Json(NominatimMatch.Replace("44.1258858", "43.6", StringComparison.Ordinal)));
        factory.Upstream.EnqueueFor(MetNoHost, Json(MetNoForecast));
        var second = await client.GetAsync(new Uri("/forecast?address=Nîmes", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.All(
            factory.Upstream.Requests.Where(r => r.RequestUri?.Host == MetNoHost),
            r => Assert.Equal("TP2-Meteo-E2ETests/1.0 (test)", r.Headers.UserAgent.ToString()));
        Assert.Equal(2, factory.Upstream.CallCountFor(MetNoHost));
    }

    [Fact]
    public async Task A_provider_key_in_another_case_is_accepted_end_to_end()
    {
        using var factory = new MeteoApiFactory(new Dictionary<string, string?> { ["Providers:Geocoder"] = "BAN" });
        factory.Upstream.EnqueueFor(BanHost, Json(BanMatch));
        factory.Upstream.EnqueueFor(OpenMeteoHost, Json(OpenMeteoForecast));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/forecast?address=Alès", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Upstream.CallCountFor(BanHost));
    }

    private static void EnqueueGeocoder(MeteoApiFactory factory, string geocoder)
    {
        var (host, body) = geocoder == BanKey ? (BanHost, BanMatch) : (NominatimHost, NominatimMatch);
        factory.Upstream.EnqueueFor(host, Json(body));
    }

    private static void EnqueueWeather(MeteoApiFactory factory, string weather)
    {
        var (host, body) = weather == MetNoKey ? (MetNoHost, MetNoForecast) : (OpenMeteoHost, OpenMeteoForecast);
        factory.Upstream.EnqueueFor(host, Json(body));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
