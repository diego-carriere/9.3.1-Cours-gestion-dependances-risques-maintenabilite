using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;

namespace Meteo.Infrastructure.Demo;

/// <summary>
/// <see cref="IWeatherProvider"/> simulé du mode démo (TP3) : <see cref="HourCount"/> points
/// horaires à partir de l'heure courante, sur une courbe journalière fixe. Le résultat est
/// déterministe pour une heure donnée. L'heure vient d'<see cref="IClock"/>, jamais de
/// DateTimeOffset.UtcNow (dépendance cachée « horloge », Support J1), ce qui permet de le
/// tester avec un FakeClock.
/// </summary>
internal sealed class DemoWeatherProvider : IWeatherProvider
{
    public const int HourCount = 24;

    private readonly IClock _clock;

    public DemoWeatherProvider(IClock clock) => _clock = clock;

    public Task<Result<Forecast>> GetForecastAsync(GeoLocation location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow.ToUniversalTime();
        var firstHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var points = Enumerable.Range(0, HourCount)
            .Select(offset => firstHour.AddHours(offset))
            .Select(time => new ForecastPoint(time, SimulatedTemperature(time.Hour)))
            .ToList();

        return Task.FromResult(Result.Success(new Forecast(location, WeatherVariables.AirTemperature, "°C", points)));
    }

    // Courbe journalière plausible : 7 °C vers 3 h UTC, 23 °C vers 15 h UTC.
    private static double SimulatedTemperature(int hourUtc) =>
        Math.Round(15 + (8 * Math.Sin(2 * Math.PI * (hourUtc - 9) / 24)), 1);
}
