using System.Text.Json.Serialization;

namespace Meteo.Infrastructure.Geocoding;

/// <summary>
/// Forme brute d'un résultat Nominatim. Interne : ne franchit jamais la frontière du
/// Domaine (voir <see cref="Meteo.Domain.Model.GeoLocation"/>). Nominatim renvoie lat/lon
/// en chaînes, toujours à séparateur point — d'où le parsing explicite en culture
/// invariante côté <see cref="GeocodingClient"/>.
/// </summary>
internal sealed record NominatimPlaceDto
{
    [JsonPropertyName("lat")]
    public string Lat { get; init; } = string.Empty;

    [JsonPropertyName("lon")]
    public string Lon { get; init; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = string.Empty;
}
