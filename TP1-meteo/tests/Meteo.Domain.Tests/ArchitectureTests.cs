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

    private static List<string?> ReferencesOutsideBcl(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Where(a => a.Name is not null
                        && a.Name != "netstandard"
                        && a.Name != "System"
                        && !a.Name.StartsWith("System.", StringComparison.Ordinal))
            .Select(a => a.Name)
            .ToList();
}
