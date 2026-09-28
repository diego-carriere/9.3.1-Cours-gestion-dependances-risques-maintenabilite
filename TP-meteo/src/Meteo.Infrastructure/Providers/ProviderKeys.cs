using System.Collections.Frozen;

namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Clés de sélection DI, une par fournisseur, et l'ensemble des clés valides pour chaque
/// port. Seul endroit qui nomme ces chaînes littérales : ni la configuration ni un
/// sélecteur ne les répète.
///
/// Les ensembles ignorent la casse ("BAN" est accepté), mais le conteneur DI compare les
/// clés à la casse près : un sélecteur doit donc résoudre la clé canonique stockée ici
/// (<see cref="FrozenSet{T}.TryGetValue"/>), jamais la valeur brute lue en configuration.
/// </summary>
internal static class ProviderKeys
{
    public const string Nominatim = "nominatim";
    public const string Ban = "ban";
    public const string OpenMeteo = "open-meteo";
    public const string MetNo = "met-no";

    public static readonly FrozenSet<string> Geocoders =
        new[] { Nominatim, Ban }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenSet<string> WeatherProviders =
        new[] { OpenMeteo, MetNo }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
