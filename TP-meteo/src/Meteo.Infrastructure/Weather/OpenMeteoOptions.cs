namespace Meteo.Infrastructure.Weather;

/// <summary>
/// <see cref="HourlyVariable"/> pilote à la fois l'URL demandée à Open-Meteo et la clé lue
/// dans sa réponse (<see cref="OpenMeteoResponseDto"/> indexe par nom de champ, pas par
/// propriété fixe) : les deux ne peuvent plus diverger, contrairement au TP1 où la
/// désérialisation restait câblée sur <c>shortwave_radiation</c> quelle que soit la valeur
/// configurée ici. Le nom canonique exposé au Domaine, lui, ne change pas : voir
/// <see cref="Meteo.Domain.Model.WeatherVariables"/> et <see cref="OpenMeteoWeatherClient"/>.
/// </summary>
public sealed class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    public string BaseUrl { get; init; } = "https://api.open-meteo.com/";

    public string HourlyVariable { get; init; } = "temperature_2m";
}
