using Meteo.Domain.Model;
using Meteo.Domain.Results;

namespace Meteo.Domain.Abstractions;

/// <summary>
/// Résout une <see cref="Address"/> en <see cref="GeoLocation"/>. Déclaré dans le Domaine,
/// implémenté par Meteo.Infrastructure.Geocoding.NominatimGeocodingClient (Nominatim) et
/// BanGeocodingClient (BAN), le choix entre les deux étant fait par configuration : c'est
/// l'inversion de dépendance de Support J1 — l'interface vit dans la couche interne, son
/// implémentation dans la couche externe, et le compilateur empêche l'inverse.
///
/// Contrat de substituabilité (Liskov) : toute implémentation, réelle ou fake, ne lève
/// jamais pour un échec attendu (adresse introuvable, service indisponible) — elle renvoie
/// un <see cref="Result{T}"/> en échec.
/// </summary>
public interface IGeocoder
{
    public Task<Result<GeoLocation>> ResolveAsync(Address address, CancellationToken cancellationToken);
}
