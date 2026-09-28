using System.ComponentModel.DataAnnotations;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// MET Norway rejette avec 403 toute requête sans User-Agent identifiable, ou avec le
/// User-Agent par défaut d'une bibliothèque HTTP (python-requests, okhttp...) — voir
/// TP1-meteo/TP2.md. Même parade que
/// <see cref="Meteo.Infrastructure.Geocoding.NominatimOptions"/> : <see cref="UserAgent"/>
/// n'a pas de valeur par défaut et sa validation est appliquée au démarrage
/// (<c>ValidateOnStart</c>), l'application refuse de démarrer plutôt que d'échouer en
/// démonstration.
/// </summary>
public sealed class MetNoOptions
{
    public const string SectionName = "MetNo";

    [Required]
    [MinLength(10)]
    public string UserAgent { get; init; } = string.Empty;

    [Required]
    public string BaseUrl { get; init; } = "https://api.met.no/";
}
