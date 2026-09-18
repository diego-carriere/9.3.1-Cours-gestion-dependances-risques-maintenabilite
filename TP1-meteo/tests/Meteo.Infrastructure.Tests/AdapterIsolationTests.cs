using System.Reflection;
using Meteo.Domain.Abstractions;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// TP2 demande n°4 : les objets propres à chaque API (DTO, noms de champs, formats) ne
/// sortent jamais de leur adaptateur. Rendu exécutable par réflexion sur l'assembly
/// Meteo.Infrastructure — le même réflexe que Meteo.Domain.Tests.ArchitectureTests pour la
/// règle "chaque couche dépend de celle du dessous" (Support J1).
/// </summary>
public sealed class AdapterIsolationTests
{
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;

    /// <summary>Le seul vocabulaire que Meteo.Infrastructure a le droit d'exposer hors de l'assembly.</summary>
    private static readonly HashSet<string> AllowedPublicTypeNames = new(StringComparer.Ordinal)
    {
        "InfrastructureServiceCollectionExtensions",
        "NominatimOptions",
        "BanOptions",
        "OpenMeteoOptions",
        "MetNoOptions",
        "ProvidersOptions",
        "ResilienceOptions",
        "UpstreamResilienceOptions",
    };

    [Fact]
    public void Every_provider_DTO_is_internal_and_sealed()
    {
        var offending = InfrastructureAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Where(t => t.IsVisible || !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(
            offending.Count == 0,
            $"DTO(s) de fournisseur non internal/sealed (fuite potentielle hors de l'adaptateur) : {string.Join(", ", offending)}");
    }

    [Fact]
    public void The_public_surface_is_only_composition_and_options()
    {
        var offending = InfrastructureAssembly.GetExportedTypes()
            .Where(t => !AllowedPublicTypeNames.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(
            offending.Count == 0,
            $"Type(s) exposés publiquement en trop — un adaptateur ou un DTO fuit hors de l'assembly : {string.Join(", ", offending)}");
    }

    [Fact]
    public void No_DTO_appears_in_a_ports_public_method_signature()
    {
        var portImplementations = InfrastructureAssembly.GetTypes()
            .Where(t => typeof(IGeocoder).IsAssignableFrom(t) || typeof(IWeatherProvider).IsAssignableFrom(t));

        var offending = new List<string>();

        foreach (var type in portImplementations)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var involvedTypes = method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType);

                offending.AddRange(
                    from involvedType in involvedTypes
                    where involvedType.Name.EndsWith("Dto", StringComparison.Ordinal)
                    select $"{type.Name}.{method.Name} -> {involvedType.Name}");
            }
        }

        Assert.True(offending.Count == 0, $"Signature de port exposant un DTO : {string.Join(", ", offending)}");
    }
}
