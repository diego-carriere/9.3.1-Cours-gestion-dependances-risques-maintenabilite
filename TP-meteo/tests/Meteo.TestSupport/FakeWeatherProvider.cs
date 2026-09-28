using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;

namespace Meteo.TestSupport;

/// <summary>Fake manuscrit d'<see cref="IWeatherProvider"/> : voir <see cref="FakeGeocoder"/>.</summary>
public sealed class FakeWeatherProvider : IWeatherProvider
{
    private readonly Queue<Result<Forecast>> _responses = new();

    public int CallCount { get; private set; }

    public void Enqueue(Result<Forecast> response) => _responses.Enqueue(response);

    public Task<Result<Forecast>> GetForecastAsync(GeoLocation location, CancellationToken cancellationToken)
    {
        CallCount++;
        cancellationToken.ThrowIfCancellationRequested();

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(FakeWeatherProvider)} has no scripted response left for '{location.DisplayName}'.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
