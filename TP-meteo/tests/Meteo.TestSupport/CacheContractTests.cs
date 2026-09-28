using Meteo.Domain.Abstractions;
using Xunit;

namespace Meteo.TestSupport;

/// <summary>
/// Contrat commun à toute implémentation d'<see cref="ICache{T}"/> qui retient ce qu'on lui
/// écrit (TP3 : « la solution de cache peut évoluer dans le futur »). La suite est héritée par
/// les tests de MemoryCache&lt;T&gt; (Infrastructure) et d'<see cref="InMemoryCache{T}"/> (fake).
/// Un futur cache distribué en héritera avant de remplacer MemoryCache&lt;T&gt; : c'est la même
/// discipline de substituabilité (Liskov) que les suites de contrat des ports au TP2.
/// (NullCache&lt;T&gt;, qui par construction ne retient rien, n'en relève pas.)
/// </summary>
public abstract class CacheContractTests
{
    protected static readonly DateTimeOffset Start = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>Une instance neuve, indépendante de toute autre, horodatée par <paramref name="clock"/>.</summary>
    protected abstract ICache<string> CreateSut(IClock clock);

    [Fact]
    public void Read_of_a_key_never_written_misses() =>
        Assert.Null(CreateSut(new FakeClock(Start)).Read("geo:alès"));

    [Fact]
    public void Read_returns_the_written_value_stamped_with_the_clock_time_of_the_write()
    {
        var clock = new FakeClock(Start);
        var sut = CreateSut(clock);

        sut.Write("geo:alès", "Alès", Retention);
        clock.UtcNow = Start.AddMinutes(5);
        var entry = sut.Read("geo:alès");

        Assert.NotNull(entry);
        Assert.Equal("Alès", entry.Value);
        Assert.Equal(Start, entry.StoredAtUtc);
    }

    [Fact]
    public void A_second_write_replaces_the_first()
    {
        var clock = new FakeClock(Start);
        var sut = CreateSut(clock);

        sut.Write("geo:alès", "v1", Retention);
        clock.UtcNow = Start.AddMinutes(5);
        sut.Write("geo:alès", "v2", Retention);
        var entry = sut.Read("geo:alès");

        Assert.NotNull(entry);
        Assert.Equal("v2", entry.Value);
        Assert.Equal(Start.AddMinutes(5), entry.StoredAtUtc);
    }

    [Fact]
    public void Keys_never_share_an_entry()
    {
        var sut = CreateSut(new FakeClock(Start));

        sut.Write("geo:alès", "Alès", Retention);

        Assert.Null(sut.Read("geo:nîmes"));
    }

    [Fact]
    public void Two_instances_never_share_entries()
    {
        // TP3, « pas de static » : un état partagé entre deux instances trahirait un champ statique.
        var clock = new FakeClock(Start);
        var first = CreateSut(clock);
        var second = CreateSut(clock);

        first.Write("geo:alès", "Alès", Retention);

        Assert.Null(second.Read("geo:alès"));
    }
}
