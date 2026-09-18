using Meteo.Domain.Abstractions;

namespace Meteo.TestSupport;

/// <summary>
/// Remplace <see cref="Meteo.Domain.Abstractions.IClock"/> en test : la matrice du mode
/// dégradé s'écrit en avançant <see cref="UtcNow"/>, jamais avec <c>Thread.Sleep</c>.
/// </summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset initial) => UtcNow = initial;

    public DateTimeOffset UtcNow { get; set; }
}
