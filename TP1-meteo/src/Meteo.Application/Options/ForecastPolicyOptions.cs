namespace Meteo.Application.Options;

/// <summary>
/// Seuils de fraîcheur du mode dégradé. Comparés à <see cref="Meteo.Domain.Abstractions.IClock.UtcNow"/>
/// par <c>GetForecastUseCase</c>, jamais par le cache lui-même (le rôle d'<c>ICache&lt;T&gt;</c>
/// se limite à horodater, pas à juger).
/// </summary>
public sealed class ForecastPolicyOptions
{
    /// <summary>Durée pendant laquelle un lieu géocodé est réutilisé sans rappeler Nominatim.</summary>
    public TimeSpan GeocodingFreshness { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Durée pendant laquelle une prévision est réutilisée sans rappeler Open-Meteo.</summary>
    public TimeSpan WeatherFreshness { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Durée pendant laquelle une prévision périmée reste utilisable en mode dégradé.</summary>
    public TimeSpan WeatherStaleRetention { get; init; } = TimeSpan.FromHours(6);
}
