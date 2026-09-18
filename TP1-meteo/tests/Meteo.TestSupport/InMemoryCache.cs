using Meteo.Domain.Abstractions;

namespace Meteo.TestSupport;

/// <summary>
/// Fake manuscrit d'<see cref="ICache{T}"/> pour les tests unitaires d'Application : un
/// simple dictionnaire, horodaté via l'<see cref="IClock"/> injecté (généralement un
/// <see cref="FakeClock"/>) pour que les tests contrôlent l'âge des entrées.
/// </summary>
public sealed class InMemoryCache<T> : ICache<T>
{
    private readonly IClock _clock;
    private readonly Dictionary<string, CacheEntry<T>> _entries = [];

    public InMemoryCache(IClock clock) => _clock = clock;

    public CacheEntry<T>? Read(string key) => _entries.GetValueOrDefault(key);

    public void Write(string key, T value, TimeSpan retention)
    {
        // La rétention est ignorée par ce fake : la fraîcheur est une décision de
        // l'Application (comparaison à IClock.UtcNow), pas du cache lui-même.
        _ = retention;
        _entries[key] = new CacheEntry<T>(value, _clock.UtcNow);
    }

    /// <summary>Amorce une entrée avec un horodatage explicite, pour tester la péremption.</summary>
    public void Seed(string key, T value, DateTimeOffset storedAtUtc) =>
        _entries[key] = new CacheEntry<T>(value, storedAtUtc);
}
