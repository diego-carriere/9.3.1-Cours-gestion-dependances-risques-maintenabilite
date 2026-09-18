using System.Net;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Meteo.Infrastructure.Geocoding;
using Meteo.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

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
    public async Task ResolveAsync_maps_a_matching_place_to_a_GeoLocation()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseWithOneMatch) });

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(44.1258858, result.Value.Latitude, precision: 6);
        Assert.Equal(4.0806915, result.Value.Longitude, precision: 6);
        Assert.Equal("Alès, Gard, Occitanie, France métropolitaine, France", result.Value.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_returns_AddressNotFound_for_an_empty_result_array()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });

        var result = await sut.ResolveAsync(Address.Create("nowhere").Value, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.AddressNotFound, result.Error.Kind);
    }

    [Fact]
    public async Task ResolveAsync_returns_GeocodingUnavailable_for_a_persisting_server_error()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.GeocodingUnavailable, result.Error.Kind);
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

    [Fact]
    public async Task ResolveAsync_never_throws_when_the_response_body_is_malformed()
    {
        var (sut, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") });

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}

/// <summary>Instancie le contrat de substituabilité partagé (LSP) pour le vrai client Nominatim.</summary>
public sealed class NominatimGeocodingClientContractTests : GeocoderContractTests
{
    protected override Meteo.Domain.Abstractions.IGeocoder CreateSutReturningAddressNotFound()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.example/") };
        var options = Options.Create(new NominatimOptions { UserAgent = "TP1-Meteo-Tests/1.0 (test)" });

        return new NominatimGeocodingClient(httpClient, options, NullLogger<NominatimGeocodingClient>.Instance);
    }
}
