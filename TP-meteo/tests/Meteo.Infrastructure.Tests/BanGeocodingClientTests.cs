using System.Net;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Infrastructure.Geocoding;
using Meteo.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Ce qui est propre à la BAN : l'ordre GeoJSON <c>[longitude, latitude]</c>, inversé par
/// rapport à Nominatim. Le contrat partagé avec tout <see cref="IGeocoder"/> HTTP vit dans
/// <see cref="BanGeocodingClientContractTests"/>.
/// </summary>
public sealed class BanGeocodingClientTests
{
    // Capturée par un appel réel à l'API le 2026-09-18 (GET /search/?q=Alès&limit=1).
    private const string ResponseWithOneMatch = """
        {"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[4.089242,44.125356]},"properties":{"label":"Alès","score":0.9529118181818181,"id":"30007","banId":"36d3fd12-a908-4f11-a665-bae4b0dfdb87","type":"municipality","name":"Alès","postcode":"30100","citycode":"30007","x":787169.06,"y":6336862.47,"population":46125,"city":"Alès","context":"30, Gard, Occitanie","importance":0.48203,"depcode":"30","municipality":"Alès","_type":"address"}}],"query":"Alès"}
        """;

    private static (BanGeocodingClient Client, StubHttpMessageHandler Handler) CreateSut()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://ban.example/") };
        var options = Options.Create(new BanOptions());
        var client = new BanGeocodingClient(httpClient, options, NullLogger<BanGeocodingClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task ResolveAsync_reads_GeoJSON_coordinates_as_longitude_then_latitude()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseWithOneMatch) });

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // GeoJSON donne [4.089242, 44.125356] = [longitude, latitude] : l'inverse serait
        // une latitude de ~4° (l'équateur), le bug que ce test rend impossible de manquer.
        Assert.Equal(44.125356, result.Value.Latitude, precision: 6);
        Assert.Equal(4.089242, result.Value.Longitude, precision: 6);
        Assert.Equal("Alès", result.Value.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_sends_a_descriptive_User_Agent_even_though_the_BAN_does_not_require_one()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseWithOneMatch) });

        await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        var sent = Assert.Single(handler.Requests);
        Assert.False(string.IsNullOrWhiteSpace(sent.Headers.UserAgent.ToString()));
    }
}

/// <summary>
/// Instancie le contrat HTTP partagé (<see cref="HttpGeocoderContractTests"/>) pour le vrai
/// client BAN : adresse valide, introuvable, réponse vide, accents.
/// </summary>
public sealed class BanGeocodingClientContractTests : HttpGeocoderContractTests
{
    protected override IGeocoder CreateSut(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://ban.example/") };
        var options = Options.Create(new BanOptions());
        return new BanGeocodingClient(httpClient, options, NullLogger<BanGeocodingClient>.Instance);
    }

    // Capturée par un appel réel à l'API le 2026-09-18.
    protected override string ValidMatchBody => """
        {"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[4.089242,44.125356]},"properties":{"label":"Alès","city":"Alès"}}],"query":"Alès"}
        """;

    protected override (double Latitude, double Longitude) ExpectedCoordinates => (44.125356, 4.089242);

    protected override string ExpectedDisplayName => "Alès";

    // Capturée par un appel réel à l'API le 2026-09-18 (GET /search/?q=zzz...&limit=1).
    protected override string NoMatchBody => """
        {"type":"FeatureCollection","features":[],"query":"zzzzzzzzzzzzzzzzzzz"}
        """;

    protected override string EmptyBody => "not json";

    protected override string AccentedMatchBody => ValidMatchBody;

    protected override string ExpectedAccentedDisplayName => "Alès";
}
