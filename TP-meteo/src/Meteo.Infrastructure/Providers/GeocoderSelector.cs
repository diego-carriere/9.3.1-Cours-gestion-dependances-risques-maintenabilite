using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Le seul <see cref="IGeocoder"/> exposé au conteneur. Il ne géocode rien lui-même : à
/// chaque appel, il relit <c>Providers:Geocoder</c> depuis <see cref="IConfiguration"/> et
/// délègue au fournisseur enregistré sous cette clé (<see cref="ProviderKeys"/>) — c'est ce
/// qui rend la bascule de fournisseur possible sans redémarrage (Support J1 TP2). Sans état
/// propre : aucune dépendance captive possible, quelle que soit sa propre durée de vie.
///
/// <see cref="IServiceProvider"/> est injecté plutôt que résolu depuis la racine : au sein
/// d'un service Scoped ou Transient, ASP.NET Core fournit le fournisseur de LA PORTÉE
/// courante, pas la racine — un adaptateur keyed reste donc résolu au bon niveau de portée.
///
/// <c>IConfiguration</c> brute plutôt que <c>IOptionsMonitor&lt;ProvidersOptions&gt;</c> —
/// un choix corrigé en cours de route, pas le premier essayé. <c>IOptionsMonitor&lt;T&gt;</c>
/// s'abonne au jeton de rechargement dès sa construction et RE-VALIDE alors à chaque
/// rechargement, pas seulement au démarrage : un rechargement vers une clé invalide fait
/// lever l'exception de validation depuis l'intérieur du callback de rechargement lui-même,
/// hors de toute pile d'appel applicative — sur le thread qui a déclenché le rechargement
/// (souvent un thread d'arrière-plan de surveillance de fichier), pas celui d'une requête.
/// Une exception non gérée sur un thread quelconque termine le processus .NET par défaut :
/// une simple faute de frappe en configuration deviendrait alors un arrêt complet du
/// service, l'inverse de ce que ce sélecteur doit garantir. <c>IConfiguration</c> ne valide
/// jamais rien et ne lève jamais pour une valeur absente ou invalide ; <c>ValidateOnStart</c>
/// (<c>ProvidersOptionsValidator</c>) continue de garantir un échec rapide au démarrage.
/// Attention : <c>ValidateOnStart</c> résout lui-même un <c>IOptionsMonitor&lt;T&gt;</c>
/// persistant ; c'est pourquoi la section est liée par <c>BindOnce</c> (voir
/// <c>InfrastructureServiceCollectionExtensions</c>), sans quoi ce moniteur revaliderait à
/// chaque rechargement malgré la lecture sur <c>IConfiguration</c> ici.
/// </summary>
internal sealed partial class GeocoderSelector : IGeocoder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeocoderSelector> _logger;

    public GeocoderSelector(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<GeocoderSelector> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<Result<GeoLocation>> ResolveAsync(Address address, CancellationToken cancellationToken)
    {
        var geocoder = _serviceProvider.GetRequiredKeyedService<IGeocoder>(ResolveKey());
        return geocoder.ResolveAsync(address, cancellationToken);
    }

    private string ResolveKey()
    {
        var requested = _configuration[$"{ProvidersOptions.SectionName}:Geocoder"] ?? ProviderKeys.Nominatim;

        if (ProviderKeys.Geocoders.TryGetValue(requested, out var canonical))
        {
            return canonical;
        }

        // ValidateOnStart empêche ce cas au démarrage ; il ne reste possible qu'après un
        // rechargement à chaud vers une clé invalide. Replier plutôt que planter : une
        // faute de frappe en configuration ne doit pas couper le service en production.
        LogUnknownProviderKey("Geocoder", requested, ProviderKeys.Nominatim);
        return ProviderKeys.Nominatim;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Providers:{Port} '{Key}' is unknown; falling back to '{Fallback}'.")]
    private partial void LogUnknownProviderKey(string port, string key, string fallback);
}
