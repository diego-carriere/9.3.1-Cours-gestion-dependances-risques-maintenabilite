namespace Meteo.Domain.Model;

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
