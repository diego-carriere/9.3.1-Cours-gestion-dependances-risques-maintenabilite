namespace Meteo.Domain.Abstractions;

/// <summary>
/// Neutralise la dépendance cachée "horloge système" (Support J1, "dépendances cachées" :
/// "contexte d'exécution : horloge, fuseau horaire..."). Implémenté par
/// Meteo.Infrastructure.Time.SystemClock ; remplacé par un FakeClock en test, ce qui rend
/// la matrice du mode dégradé testable sans <c>Thread.Sleep</c>.
/// </summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
