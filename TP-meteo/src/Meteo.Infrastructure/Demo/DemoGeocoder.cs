using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;

namespace Meteo.Infrastructure.Demo;

/// <summary>
/// <see cref="IGeocoder"/> simulé du mode démo (TP3) : aucune connexion. Toute adresse valide
/// est résolue vers les coordonnées de l'exemple du brief (Alès) et garde son libellé. C'est un
/// adaptateur de plus derrière le port existant : le cas d'usage ne voit pas la différence.
/// </summary>
internal sealed class DemoGeocoder : IGeocoder
{
    public const double Latitude = 44.1279;
    public const double Longitude = 4.0817;

    public Task<Result<GeoLocation>> ResolveAsync(Address address, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result.Success(new GeoLocation(Latitude, Longitude, address.Value)));
    }
}
