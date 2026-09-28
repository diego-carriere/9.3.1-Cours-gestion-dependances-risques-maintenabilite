using System.Net;
using System.Text;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Xunit;

namespace Meteo.TestSupport;

/// <summary>
/// Suite de contrat unique exécutée contre chaque implémentation HTTP d'<see cref="IGeocoder"/>
/// (Nominatim, BAN...), pilotée par <see cref="StubHttpMessageHandler"/> : le TP2 exige les
/// mêmes quatre cas pour tout fournisseur — adresse valide, adresse introuvable, réponse
/// vide, caractères accentués. Une seule classe de test écrite ; chaque fournisseur ne
/// fournit que ses corps de réponse et le résultat attendu.
///
/// Hérite de <see cref="GeocoderContractTests"/> : le contrat comportemental (jamais
/// d'exception pour un échec attendu) reste valable pour toute implémentation, HTTP ou fake.
/// </summary>
public abstract class HttpGeocoderContractTests : GeocoderContractTests
{
    /// <summary>Construit l'implémentation réelle, câblée sur <paramref name="handler"/> comme transport.</summary>
    protected abstract IGeocoder CreateSut(StubHttpMessageHandler handler);

    /// <summary>Un corps de réponse représentant une adresse résolue avec succès.</summary>
    protected abstract string ValidMatchBody { get; }

    /// <summary>Latitude/longitude attendues (précision 1e-3) pour <see cref="ValidMatchBody"/>.</summary>
    protected abstract (double Latitude, double Longitude) ExpectedCoordinates { get; }

    /// <summary><see cref="GeoLocation.DisplayName"/> attendu pour <see cref="ValidMatchBody"/>.</summary>
    protected abstract string ExpectedDisplayName { get; }

    /// <summary>Un corps de réponse valide ne contenant aucun résultat (adresse introuvable).</summary>
    protected abstract string NoMatchBody { get; }

    /// <summary>Un corps de réponse vide ou illisible (panne côté fournisseur, pas une adresse introuvable).</summary>
    protected abstract string EmptyBody { get; }

    /// <summary>Un corps de réponse dont le libellé contient des caractères accentués.</summary>
    protected abstract string AccentedMatchBody { get; }

    /// <summary><see cref="GeoLocation.DisplayName"/> attendu pour <see cref="AccentedMatchBody"/>.</summary>
    protected abstract string ExpectedAccentedDisplayName { get; }

    protected sealed override IGeocoder CreateSutReturningAddressNotFound()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(Json(NoMatchBody));
        return CreateSut(handler);
    }

    [Fact]
    public async Task ResolveAsync_for_a_valid_address_returns_a_usable_location()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(Json(ValidMatchBody));
        var sut = CreateSut(handler);

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExpectedCoordinates.Latitude, result.Value.Latitude, precision: 3);
        Assert.Equal(ExpectedCoordinates.Longitude, result.Value.Longitude, precision: 3);
        Assert.Equal(ExpectedDisplayName, result.Value.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_for_an_unknown_address_returns_AddressNotFound()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(Json(NoMatchBody));
        var sut = CreateSut(handler);

        var result = await sut.ResolveAsync(Address.Create("une adresse qui n'existe pas").Value, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.AddressNotFound, result.Error.Kind);
    }

    [Fact]
    public async Task ResolveAsync_for_an_empty_response_never_throws()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(EmptyBody) });
        var sut = CreateSut(handler);

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ResolveAsync_for_an_accented_address_encodes_the_request_and_restores_the_label()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(Json(AccentedMatchBody));
        var sut = CreateSut(handler);

        var result = await sut.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        var sentUri = Assert.Single(handler.Requests).RequestUri!.AbsoluteUri;
        Assert.Contains("Al%C3%A8s", sentUri, StringComparison.OrdinalIgnoreCase);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExpectedAccentedDisplayName, result.Value.DisplayName);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
