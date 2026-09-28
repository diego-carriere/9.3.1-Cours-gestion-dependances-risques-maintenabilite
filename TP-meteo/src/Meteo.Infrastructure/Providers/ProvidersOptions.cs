namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Choix du fournisseur actif par port. Bindée et validée une seule fois au démarrage
/// (<c>ValidateOnStart</c>, voir <see cref="ProvidersOptionsValidator"/> et
/// <c>InfrastructureServiceCollectionExtensions</c>) : une clé invalide empêche
/// l'application de démarrer. En service, <see cref="GeocoderSelector"/> et
/// <see cref="WeatherProviderSelector"/> ne lisent PAS ce type via
/// <c>IOptionsMonitor&lt;ProvidersOptions&gt;</c> — ils relisent
/// <c>Microsoft.Extensions.Configuration.IConfiguration</c> brute à chaque appel (voir la
/// note de <see cref="GeocoderSelector"/> pour la raison : un moniteur d'options revalide à
/// chaque rechargement, pas seulement au démarrage, ce qui peut faire lever une exception
/// hors de toute requête HTTP). L'un et l'autre lisent la même section
/// <see cref="SectionName"/> ; ce type documente sa forme, il n'est pas le chemin de lecture
/// en service. Éditer cette section en configuration bascule le fournisseur sans redémarrer
/// le processus — c'est l'exigence du TP2, "changer de fournisseur sans redéploiement".
/// </summary>
public sealed class ProvidersOptions
{
    public const string SectionName = "Providers";

    public string Geocoder { get; init; } = ProviderKeys.Nominatim;

    public string Weather { get; init; } = ProviderKeys.OpenMeteo;
}
