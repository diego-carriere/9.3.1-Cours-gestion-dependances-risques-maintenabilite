using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// Forme brute d'une réponse MET Norway Locationforecast (GeoJSON Feature). Interne : ne
/// franchit jamais la frontière du Domaine — voir <see cref="Meteo.Domain.Model.Forecast"/>.
/// Piège de format propre à ce fournisseur : l'unité est annoncée en toutes lettres
/// (<c>"celsius"</c>), pas en symbole — <see cref="MetNoWeatherClient"/> la normalise en
/// <c>"°C"</c>, le seul endroit qui connaît la différence.
/// </summary>
internal sealed record MetNoForecastDto
{
    [JsonPropertyName("properties")]
    public MetNoPropertiesDto? Properties { get; init; }
}

internal sealed record MetNoPropertiesDto
{
    [JsonPropertyName("meta")]
    public MetNoMetaDto? Meta { get; init; }

    [JsonPropertyName("timeseries")]
    public List<MetNoTimeseriesEntryDto>? Timeseries { get; init; }
}

internal sealed record MetNoMetaDto
{
    [JsonPropertyName("units")]
    public Dictionary<string, string>? Units { get; init; }
}

internal sealed record MetNoTimeseriesEntryDto
{
    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public MetNoDataDto? Data { get; init; }
}

internal sealed record MetNoDataDto
{
    [JsonPropertyName("instant")]
    public MetNoInstantDto? Instant { get; init; }
}

internal sealed record MetNoInstantDto
{
    /// <summary>Toute grandeur instantanée (température, vent, pression...) : lue par nom dans le client.</summary>
    [JsonPropertyName("details")]
    public Dictionary<string, JsonElement>? Details { get; init; }
}
