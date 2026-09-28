using Meteo.Domain.Model;
using Meteo.Domain.Results;

namespace Meteo.Domain.Abstractions;

/// <summary>
/// Obtient la prévision pour un lieu. Déclaré dans le Domaine, implémenté par
/// Meteo.Infrastructure.Weather.OpenMeteoWeatherClient (Open-Meteo) et MetNoWeatherClient
/// (MET Norway), choisis par configuration. Même contrat de
/// substituabilité que <see cref="IGeocoder"/> : jamais d'exception pour un échec attendu.
/// </summary>
public interface IWeatherProvider
{
    public Task<Result<Forecast>> GetForecastAsync(GeoLocation location, CancellationToken cancellationToken);
}
