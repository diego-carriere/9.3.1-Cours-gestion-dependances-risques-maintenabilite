using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Xunit;

namespace Meteo.TestSupport;

/// <summary>
/// Contrat de substituabilité (Liskov) commun à toute implémentation d'<see cref="IGeocoder"/>,
/// réelle ou fake : hérité par les tests de <c>FakeGeocoder</c> et de
/// <c>GeocodingClient</c> (Meteo.Infrastructure.Tests). Si une implémentation laisse fuir
/// une exception pour un échec attendu, ce test échoue quel que soit le côté du contrat.
/// </summary>
public abstract class GeocoderContractTests
{
    /// <summary>Doit renvoyer une instance scriptée pour produire un échec attendu (adresse introuvable).</summary>
    protected abstract IGeocoder CreateSutReturningAddressNotFound();

    [Fact]
    public async Task ResolveAsync_never_throws_for_an_expected_failure()
    {
        var sut = CreateSutReturningAddressNotFound();
        var address = Address.Create("une adresse qui n'existe pas").Value;

        var result = await sut.ResolveAsync(address, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
