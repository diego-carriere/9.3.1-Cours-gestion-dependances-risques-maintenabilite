namespace Meteo.Domain.Abstractions;

/// <summary>Une valeur mise en cache, horodatée au moment de l'écriture.</summary>
public sealed record CacheEntry<T>(T Value, DateTimeOffset StoredAtUtc);

/// <summary>
/// Un cache clé/valeur générique. Il ne décide jamais de la fraîcheur d'une entrée : "frais
/// pendant 10 min, utilisable 6 h en mode dégradé" est une règle métier qui appartient à
/// Meteo.Application, laquelle compare <see cref="CacheEntry{T}.StoredAtUtc"/> à
/// <see cref="IClock.UtcNow"/>. Implémenté par Meteo.Infrastructure.Caching.MemoryCache&lt;T&gt;.
/// (Nommé Read/Write plutôt que Get/Set : CA1716, mots réservés VB.NET.)
/// </summary>
public interface ICache<T>
{
    public CacheEntry<T>? Read(string key);

    public void Write(string key, T value, TimeSpan retention);
}
