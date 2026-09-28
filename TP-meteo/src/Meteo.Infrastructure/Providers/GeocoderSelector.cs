using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Le seul <see cref="IGeocoder"/> exposé au conteneur. Il ne géocode rien lui-même : à
/// chaque appel, il relit <see cref="ProvidersOptions.Geocoder"/> via
/// <see cref="IOptionsMonitor{TOptions}"/> (jamais mise en cache) et délègue au fournisseur
/// enregistré sous cette clé (<see cref="ProviderKeys"/>) — c'est ce qui rend la bascule de
/// fournisseur possible sans redémarrage (Support J1 TP2). Sans état propre : aucune
/// dépendance captive possible, quelle que soit sa propre durée de vie.
///
/// <see cref="IServiceProvider"/> est injecté plutôt que résolu depuis la racine : au sein
/// d'un service Scoped ou Transient, ASP.NET Core fournit le fournisseur de LA PORTÉE
/// courante, pas la racine — un adaptateur keyed reste donc résolu au bon niveau de portée.
/// </summary>
internal sealed partial class GeocoderSelector : IGeocoder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<ProvidersOptions> _options;
    private readonly ILogger<GeocoderSelector> _logger;

    public GeocoderSelector(
        IServiceProvider serviceProvider, IOptionsMonitor<ProvidersOptions> options, ILogger<GeocoderSelector> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    public Task<Result<GeoLocation>> ResolveAsync(Address address, CancellationToken cancellationToken)
    {
        var geocoder = _serviceProvider.GetRequiredKeyedService<IGeocoder>(ResolveKey());
        return geocoder.ResolveAsync(address, cancellationToken);
    }

    private string ResolveKey()
    {
        var requested = _options.CurrentValue.Geocoder;

        if (ProviderKeys.Geocoders.Contains(requested))
        {
            return requested;
        }

        // ProvidersOptionsValidator (ValidateOnStart) rend ce cas inatteignable au
        // démarrage ; il ne reste possible qu'après un rechargement à chaud de la
        // configuration vers une clé invalide. Replier plutôt que planter : une faute de
        // frappe en configuration ne doit pas couper le service en production.
        LogUnknownProviderKey("Geocoder", requested, ProviderKeys.Nominatim);
        return ProviderKeys.Nominatim;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Providers:{Port} '{Key}' is unknown; falling back to '{Fallback}'.")]
    private partial void LogUnknownProviderKey(string port, string key, string fallback);
}
