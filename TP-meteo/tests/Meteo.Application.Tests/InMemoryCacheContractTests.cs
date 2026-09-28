using Meteo.Domain.Abstractions;
using Meteo.TestSupport;

namespace Meteo.Application.Tests;

/// <summary>
/// Le fake qui remplace le cache dans les tests du cas d'usage respecte le même contrat que
/// l'implémentation réelle : sinon, ces tests prouveraient un comportement que la production n'a pas.
/// </summary>
public sealed class InMemoryCacheContractTests : CacheContractTests
{
    protected override ICache<string> CreateSut(IClock clock) => new InMemoryCache<string>(clock);
}
