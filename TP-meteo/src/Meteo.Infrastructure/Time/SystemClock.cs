using Meteo.Domain.Abstractions;

namespace Meteo.Infrastructure.Time;

/// <summary>
/// Neutralise la dépendance cachée "horloge système". Délègue à <see cref="TimeProvider.System"/>
/// (BCL depuis .NET 8) plutôt qu'à <see cref="DateTimeOffset.UtcNow"/> directement : une seule
/// indirection, remplaçable par un <c>FakeTimeProvider</c> si le projet en avait besoin ailleurs.
/// Enregistré en Singleton : sans état, sans dépendance scoped, donc aucun risque de
/// dépendance captive.
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
}
