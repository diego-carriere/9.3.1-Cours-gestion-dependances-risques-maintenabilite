using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;

namespace Meteo.TestSupport;

/// <summary>
/// Fake manuscrit d'<see cref="IGeocoder"/>, à la manière du FakeUserRepository de
/// Support J1 : une file de réponses scriptées et un compteur d'appels, ce qui suffit à
/// prouver des assertions comme "le géocodeur n'a été appelé qu'une fois" (effet du cache).
/// </summary>
public sealed class FakeGeocoder : IGeocoder
{
    private readonly Queue<Result<GeoLocation>> _responses = new();

    public int CallCount { get; private set; }

    public void Enqueue(Result<GeoLocation> response) => _responses.Enqueue(response);

    public Task<Result<GeoLocation>> ResolveAsync(Address address, CancellationToken cancellationToken)
    {
        CallCount++;
        cancellationToken.ThrowIfCancellationRequested();

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(FakeGeocoder)} has no scripted response left for '{address.Value}'.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
