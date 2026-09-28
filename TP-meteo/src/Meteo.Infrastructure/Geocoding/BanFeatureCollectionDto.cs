using System.Text.Json.Serialization;

namespace Meteo.Infrastructure.Geocoding;

/// <summary>
/// Forme brute d'une réponse BAN : un FeatureCollection GeoJSON. Interne : ne franchit
/// jamais la frontière du Domaine (voir <see cref="Meteo.Domain.Model.GeoLocation"/>).
///
/// Piège de format propre à ce fournisseur : GeoJSON ordonne ses coordonnées
/// <c>[longitude, latitude]</c> — l'inverse de la paire <c>lat</c>/<c>lon</c> de Nominatim.
/// <see cref="BanGeocodingClient"/> est le seul endroit qui connaît cet ordre.
/// </summary>
internal sealed record BanFeatureCollectionDto
{
    [JsonPropertyName("features")]
    public List<BanFeatureDto>? Features { get; init; }
}

internal sealed record BanFeatureDto
{
    [JsonPropertyName("geometry")]
    public BanGeometryDto? Geometry { get; init; }

    [JsonPropertyName("properties")]
    public BanPropertiesDto? Properties { get; init; }
}

internal sealed record BanGeometryDto
{
    /// <summary>GeoJSON : <c>[longitude, latitude]</c>, jamais l'inverse.</summary>
    [JsonPropertyName("coordinates")]
    public List<double>? Coordinates { get; init; }
}

internal sealed record BanPropertiesDto
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;
}
