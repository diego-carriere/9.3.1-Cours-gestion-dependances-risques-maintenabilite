using System.Reflection;

namespace Meteo.Domain.Tests;

/// <summary>
/// Rend exécutable la règle de Support J1 "chaque couche dépend de celle du dessous,
/// jamais de celle du dessus" : ces tests échouent dès qu'un import distrait fait fuiter
/// une dépendance technique dans une couche interne.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Domain_assembly_has_no_external_references()
    {
        var offending = ReferencesOutsideBcl(typeof(Meteo.Domain.Model.Address).Assembly);

        Assert.True(
            offending.Count == 0,
            $"Meteo.Domain doit rester sans dépendance externe, mais référence : {string.Join(", ", offending)}");
    }

    [Fact]
    public void Application_assembly_does_not_reference_AspNetCore_or_Polly()
    {
        var application = Assembly.Load("Meteo.Application");

        var forbidden = application.GetReferencedAssemblies()
            .Where(a => a.Name is not null &&
                        (a.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                         a.Name.StartsWith("Polly", StringComparison.Ordinal)))
            .Select(a => a.Name)
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            $"Meteo.Application ne doit connaître ni ASP.NET Core ni Polly, mais référence : {string.Join(", ", forbidden)}");
    }

    /// <summary>
    /// TP2 demande n°4 : les objets propres à chaque API (DTO Nominatim, BAN, Open-Meteo,
    /// MET Norway...) ne franchissent jamais la frontière de Meteo.Infrastructure. Vérifié
    /// côté Infrastructure par <c>Meteo.Infrastructure.Tests.AdapterIsolationTests</c> ;
    /// vérifié ici côté Domaine/Application, qui ne devraient même pas connaître le mot "Dto".
    /// </summary>
    [Fact]
    public void Domain_and_Application_declare_no_provider_DTO_type()
    {
        var domain = typeof(Meteo.Domain.Model.Address).Assembly;
        var application = Assembly.Load("Meteo.Application");

        var offending = domain.GetTypes().Concat(application.GetTypes())
            .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(
            offending.Count == 0,
            $"Type(s) de type fournisseur (DTO) trouvés hors de Meteo.Infrastructure : {string.Join(", ", offending)}");
    }

    [Fact]
    public void Domain_and_Application_hold_no_mutable_static_state()
    {
        var types = typeof(Meteo.Domain.Model.Address).Assembly.GetTypes()
            .Concat(Assembly.Load("Meteo.Application").GetTypes());

        var offending = Meteo.TestSupport.StaticStateInspector.FindMutableStaticFields(types);

        Assert.True(offending.Count == 0, $"État statique mutable (TP3, « pas de static ») : {string.Join(", ", offending)}");
    }

    private static List<string?> ReferencesOutsideBcl(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Where(a => a.Name is not null
                        && a.Name != "netstandard"
                        && a.Name != "System"
                        && !a.Name.StartsWith("System.", StringComparison.Ordinal))
            .Select(a => a.Name)
            .ToList();
}
