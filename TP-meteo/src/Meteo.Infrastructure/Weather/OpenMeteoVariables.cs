using System.Collections.Frozen;
using Meteo.Domain.Model;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// Les variables horaires Open-Meteo que cet adaptateur sait traduire vers un nom canonique
/// de <see cref="WeatherVariables"/>, avec l'unité à supposer si la réponse ne l'annonce pas.
/// Seul endroit qui connaît le vocabulaire Open-Meteo (<c>temperature_2m</c>...) : une
/// variable absente d'ici est refusée au démarrage (<see cref="OpenMeteoOptionsValidator"/>)
/// plutôt que servie sous une étiquette fausse.
/// </summary>
internal static class OpenMeteoVariables
{
    public static readonly FrozenDictionary<string, (string Canonical, string DefaultUnit)> Known =
        new Dictionary<string, (string Canonical, string DefaultUnit)>
        {
            ["temperature_2m"] = (WeatherVariables.AirTemperature, "°C"),
            ["shortwave_radiation"] = (WeatherVariables.ShortwaveRadiation, "W/m²"),
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
