using System.Net;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Infrastructure.Geocoding;
using Meteo.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Ce qui est propre à Nominatim (en-tête User-Agent). Le contrat partagé avec tout
/// <see cref="IGeocoder"/> HTTP (adresse valide, introuvable, réponse vide, accents) vit
/// dans <see cref="NominatimGeocodingClientContractTests"/>.
/// </summary>
public sealed class NominatimGeocodingClientTests
{
    private const string ResponseWithOneMatch = """
        [
          {
            "lat": "44.1258858",
            "lon": "4.0806915",
            "display_name": "Alès, Gard, Occitanie, France métropolitaine, France"
          }
        ]
        """;

    private static (NominatimGeocodingClient Client, StubHttpMessageHandler Handler) CreateSut(
        string userAgent = "TP1-Meteo-Tests/1.0 (test)")
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.example/") };
        var options = Options.Create(new NominatimOptions { UserAgent = userAgent });
        var client = new NominatimGeocodingClient(httpClient, options, NullLogger<NominatimGeocodingClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task ResolveAsync_sends_the_configured_User_Agent_on_every_request()
    {
        var (sut, handler) = CreateSut(userAgent: "TP1-Meteo/1.0 (formation EMA)");
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseWithOneMatch) });

        await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("TP1-Meteo/1.0 (formation EMA)", sent.Headers.UserAgent.ToString());
    }
}

/// <summary>
/// Instancie le contrat HTTP partagé (<see cref="HttpGeocoderContractTests"/>, TP2 demande
/// n°3) pour le vrai client Nominatim : adresse valide, introuvable, réponse vide, accents.
/// </summary>
public sealed class NominatimGeocodingClientContractTests : HttpGeocoderContractTests
{
    private const string DisplayName = "Alès, Gard, Occitanie, France métropolitaine, France";

    protected override IGeocoder CreateSut(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.example/") };
        var options = Options.Create(new NominatimOptions { UserAgent = "TP1-Meteo-Tests/1.0 (test)" });
        return new NominatimGeocodingClient(httpClient, options, NullLogger<NominatimGeocodingClient>.Instance);
    }

    protected override string ValidMatchBody => $$"""
        [{ "lat": "44.1258858", "lon": "4.0806915", "display_name": "{{DisplayName}}" }]
        """;

    protected override (double Latitude, double Longitude) ExpectedCoordinates => (44.1258858, 4.0806915);

    protected override string ExpectedDisplayName => DisplayName;

    protected override string NoMatchBody => "[]";

    protected override string EmptyBody => "not json";

    protected override string AccentedMatchBody => ValidMatchBody;

    protected override string ExpectedAccentedDisplayName => DisplayName;
}
