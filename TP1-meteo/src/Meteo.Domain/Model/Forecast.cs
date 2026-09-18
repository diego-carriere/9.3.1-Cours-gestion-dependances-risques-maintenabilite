namespace Meteo.Domain.Model;

/// <summary>
/// Noms de variable canoniques que <see cref="Forecast.Variable"/> peut porter. Chaque
/// fournisseur météo (Open-Meteo, MET Norway...) a son propre vocabulaire de champ
/// (<c>temperature_2m</c>, <c>air_temperature</c>...) : la traduction vers ce nom canonique
/// est le travail de son adaptateur dans Meteo.Infrastructure, pour que basculer de
/// fournisseur (Support J1 TP2) ne change jamais le sens de la réponse.
/// </summary>
public static class WeatherVariables
{
    public const string AirTemperature = "air_temperature";
}

/// <summary>Un lieu résolu par le géocodage.</summary>
public sealed record GeoLocation(double Latitude, double Longitude, string DisplayName);

/// <summary>Une mesure horaire de la prévision (ex. rayonnement solaire).</summary>
public sealed record ForecastPoint(DateTimeOffset TimestampUtc, double Value);

/// <summary>
/// Le contrat de sortie du domaine. La forme en tableaux parallèles d'Open-Meteo
/// (<c>hourly.time[]</c> + <c>hourly.shortwave_radiation[]</c>) ne franchit jamais cette
/// frontière : le mapping vers ce modèle est fait exclusivement par Meteo.Infrastructure.
/// </summary>
public sealed record Forecast(
    GeoLocation Location,
    string Variable,
    string Unit,
    IReadOnlyList<ForecastPoint> Points);
