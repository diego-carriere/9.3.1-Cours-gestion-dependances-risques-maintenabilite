using Meteo.Domain.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Meteo.Infrastructure.Caching;

/// <summary>
/// Implémente <see cref="ICache{T}"/> au-dessus d'<see cref="IMemoryCache"/>. Enregistré en
/// Singleton (générique ouvert) : un cache par requête ne cache rien (Support J1,
/// "AddSingleton ... Cache, configuration"). L'horodatage vient de l'<see cref="IClock"/>
/// injecté, jamais de <c>DateTimeOffset.UtcNow</c> directement.
/// </summary>
internal sealed class MemoryCache<T> : ICache<T>
{
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;

    public MemoryCache(IMemoryCache cache, IClock clock)
    {
        _cache = cache;
        _clock = clock;
    }

    public CacheEntry<T>? Read(string key) =>
        _cache.TryGetValue(key, out CacheEntry<T>? entry) ? entry : null;

    public void Write(string key, T value, TimeSpan retention) =>
        _cache.Set(key, new CacheEntry<T>(value, _clock.UtcNow), retention);
}
