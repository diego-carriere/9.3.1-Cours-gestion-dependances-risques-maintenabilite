namespace Meteo.Api.Contracts;

/// <summary>
/// Le contrat public de <c>GET /forecast</c>. Notre propre forme, jamais celle d'Open-Meteo
/// (tableaux parallèles hourly.time[]/hourly.shortwave_radiation[]) : le mapping est fait
/// une fois pour toutes par Meteo.Domain.Model.Forecast, bien avant d'arriver ici.
/// </summary>
public sealed record ForecastResponse(
    string RequestedAddress,
    string ResolvedPlace,
    double Latitude,
    double Longitude,
    string Variable,
    string Unit,
    IReadOnlyList<ForecastPointResponse> Hourly,
    bool Degraded,
    DateTimeOffset DataAsOf);

public sealed record ForecastPointResponse(DateTimeOffset Timestamp, double Value);
