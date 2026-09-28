using Meteo.Domain.Abstractions;

namespace Meteo.Infrastructure.Caching;

/// <summary>
/// Objet nul d'<see cref="ICache{T}"/> : il ne retient rien et chaque lecture manque, ce que
/// le cas d'usage traite déjà comme un cas normal. Il n'est injecté qu'à la composition du
/// mode démo (TP3). Les données simulées ne coûtent rien à recalculer, et surtout elles ne
/// doivent jamais atteindre le cache réel : une démo sur « Alès » y écrirait sinon des
/// coordonnées fictives, servies ensuite pendant 24 h aux vraies requêtes.
/// </summary>
internal sealed class NullCache<T> : ICache<T>
{
    public CacheEntry<T>? Read(string key) => null;

    public void Write(string key, T value, TimeSpan retention)
    {
    }
}
