using Meteo.Domain.Abstractions;
using Meteo.Infrastructure.Caching;
using Meteo.TestSupport;
using Microsoft.Extensions.Caching.Memory;

namespace Meteo.Infrastructure.Tests;

/// <summary>Instancie <see cref="CacheContractTests"/> pour l'implémentation de production.</summary>
public sealed class MemoryCacheContractTests : CacheContractTests
{
    // Un IMemoryCache par instance : c'est le conteneur DI qui partage le singleton, jamais la classe elle-même.
    protected override ICache<string> CreateSut(IClock clock) =>
        new MemoryCache<string>(new MemoryCache(new MemoryCacheOptions()), clock);
}
