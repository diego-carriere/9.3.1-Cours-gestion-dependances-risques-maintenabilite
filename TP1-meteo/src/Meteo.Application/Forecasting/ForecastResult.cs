using Meteo.Domain.Model;

namespace Meteo.Application.Forecasting;

/// <summary>Le résultat exposé par le cas d'usage, avant tout mapping HTTP.</summary>
public sealed record ForecastResult(
    Address RequestedAddress,
    Forecast Forecast,
    bool IsDegraded,
    DateTimeOffset DataAsOfUtc);
