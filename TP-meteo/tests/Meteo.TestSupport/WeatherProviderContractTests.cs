using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Xunit;

namespace Meteo.TestSupport;

/// <summary>
/// Contrat de substituabilité (Liskov) commun à toute implémentation d'<see cref="IWeatherProvider"/>,
/// réelle ou fake : symétrique de <see cref="GeocoderContractTests"/> côté météo. Une
/// implémentation qui laisse fuir une exception pour un échec attendu (service indisponible)
/// viole ce contrat, quel que soit le fournisseur.
/// </summary>
public abstract class WeatherProviderContractTests
{
    /// <summary>Une localisation quelconque, valide pour toute implémentation.</summary>
    protected static readonly GeoLocation SampleLocation = new(44.1258, 4.0806, "Alès");

    /// <summary>Doit renvoyer une instance scriptée pour produire un échec attendu (service indisponible).</summary>
    protected abstract IWeatherProvider CreateSutReturningWeatherUnavailable();

    [Fact]
    public async Task GetForecastAsync_never_throws_for_an_expected_failure()
    {
        var sut = CreateSutReturningWeatherUnavailable();

        var result = await sut.GetForecastAsync(SampleLocation, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
