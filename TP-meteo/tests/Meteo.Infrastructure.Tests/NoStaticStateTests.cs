using Meteo.TestSupport;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// TP3, « pas de static » : aucun état partagé caché dans un champ statique, et en premier
/// lieu aucun cache maison en Dictionary statique. Le cache vit dans un singleton DI, remplaçable.
/// </summary>
public sealed class NoStaticStateTests
{
    [Fact]
    public void The_inspector_catches_a_naive_static_cache()
    {
        var offending = StaticStateInspector.FindMutableStaticFields([typeof(NaiveStaticCache)]);

        Assert.Equal(2, offending.Count);
    }

    [Fact]
    public void Infrastructure_holds_no_mutable_static_state()
    {
        var offending = StaticStateInspector.FindMutableStaticFields(
            typeof(InfrastructureServiceCollectionExtensions).Assembly.GetTypes());

        Assert.True(offending.Count == 0, $"État statique mutable : {string.Join(", ", offending)}");
    }

    // Les deux formes naïves : champ readonly d'un type mutable, et propriété statique (champ support).
    private static class NaiveStaticCache
    {
        public static readonly Dictionary<string, string> Entries = [];

        public static Dictionary<string, string> Other { get; } = [];
    }
}
