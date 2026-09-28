using Meteo.Domain.Results;

namespace Meteo.Application.Forecasting;

/// <summary>
/// Couture d'abstraction — pas une inversion de dépendance : interface et implémentation
/// vivent toutes deux dans Meteo.Application. Elle existe pour que Meteo.Api dépende d'un
/// contrat plutôt que de <c>GetForecastUseCase</c> directement (utile pour un fake d'endpoint
/// en test de présentation), à ne pas confondre avec IGeocoder/IWeatherProvider (Domaine).
/// </summary>
public interface IGetForecastUseCase
{
    public Task<Result<ForecastResult>> ExecuteAsync(string? rawAddress, CancellationToken cancellationToken);
}
