using System.ComponentModel.DataAnnotations;

namespace Meteo.Infrastructure.Geocoding;

/// <summary>
/// La politique d'usage de Nominatim exige un User-Agent descriptif ; un agent générique
/// est rejeté (403). <see cref="UserAgent"/> n'a pas de valeur par défaut et sa validation
/// est appliquée au démarrage (<c>ValidateOnStart</c>) : l'application refuse de démarrer
/// plutôt que d'échouer en démonstration.
/// </summary>
public sealed class NominatimOptions
{
    public const string SectionName = "Nominatim";

    [Required]
    [MinLength(10)]
    public string UserAgent { get; init; } = string.Empty;

    [Required]
    public string BaseUrl { get; init; } = "https://nominatim.openstreetmap.org/";

    public string AcceptLanguage { get; init; } = "fr";
}
