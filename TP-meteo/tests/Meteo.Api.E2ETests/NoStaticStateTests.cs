using Meteo.TestSupport;

namespace Meteo.Api.E2ETests;

/// <summary>
/// TP3, « pas de static », pour la couche Présentation : les mêmes règles que pour Domain,
/// Application et Infrastructure. Endpoint et mappers sont des classes statiques sans état ;
/// ce test empêche qu'un cache de réponses s'y glisse un jour.
/// </summary>
public sealed class NoStaticStateTests
{
    [Fact]
    public void Api_holds_no_mutable_static_state()
    {
        var offending = StaticStateInspector.FindMutableStaticFields(typeof(Program).Assembly.GetTypes());

        Assert.True(offending.Count == 0, $"État statique mutable : {string.Join(", ", offending)}");
    }
}
