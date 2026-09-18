using System.Text.Json.Serialization;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// Forme brute de la réponse Open-Meteo (tableaux parallèles). Interne : ne franchit jamais
/// la frontière du Domaine — voir <see cref="Meteo.Domain.Model.Forecast"/>, qui est une
/// liste de points, pas deux tableaux à zipper soi-même.
/// </summary>
internal sealed record OpenMeteoResponseDto
{
    [JsonPropertyName("hourly_units")]
    public OpenMeteoHourlyUnitsDto? HourlyUnits { get; init; }

    [JsonPropertyName("hourly")]
    public OpenMeteoHourlyDto? Hourly { get; init; }
}

internal sealed record OpenMeteoHourlyUnitsDto
{
    [JsonPropertyName("shortwave_radiation")]
    public string? ShortwaveRadiation { get; init; }
}

internal sealed record OpenMeteoHourlyDto
{
    [JsonPropertyName("time")]
    public List<string>? Time { get; init; }

    [JsonPropertyName("shortwave_radiation")]
    public List<double>? ShortwaveRadiation { get; init; }
}
