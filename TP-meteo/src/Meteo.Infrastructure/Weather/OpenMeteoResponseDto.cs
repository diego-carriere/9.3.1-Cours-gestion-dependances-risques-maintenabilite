using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// Forme brute de la réponse Open-Meteo (tableaux parallèles). Interne : ne franchit jamais
/// la frontière du Domaine — voir <see cref="Meteo.Domain.Model.Forecast"/>, qui est une
/// liste de points, pas deux tableaux à zipper soi-même.
///
/// <c>Hourly</c>/<c>HourlyUnits</c> sont indexés par nom de champ plutôt que déclarés comme
/// propriétés fixes : Open-Meteo nomme sa série demandée d'après
/// <see cref="OpenMeteoOptions.HourlyVariable"/>
/// (<c>hourly.temperature_2m</c>, <c>hourly.shortwave_radiation</c>...), donc une propriété
/// figée sur un seul nom romprait dès que la configuration change de variable.
/// </summary>
internal sealed record OpenMeteoResponseDto
{
    [JsonPropertyName("hourly_units")]
    public Dictionary<string, string>? HourlyUnits { get; init; }

    [JsonPropertyName("hourly")]
    public OpenMeteoHourlyDto? Hourly { get; init; }
}

internal sealed record OpenMeteoHourlyDto
{
    [JsonPropertyName("time")]
    public List<string>? Time { get; init; }

    /// <summary>Toute clé autre que <c>time</c> : capturée en vrac, lue par nom dans le client.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? OtherSeries { get; init; }
}
