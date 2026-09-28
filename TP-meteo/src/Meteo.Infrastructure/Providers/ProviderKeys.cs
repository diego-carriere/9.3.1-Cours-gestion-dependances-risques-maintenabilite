namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Clés de sélection DI, une par fournisseur, et l'ensemble des clés valides pour chaque
/// port. Seul endroit qui nomme ces chaînes littérales : ni la configuration ni un
/// sélecteur ne les répète.
/// </summary>
internal static class ProviderKeys
{
    public const string Nominatim = "nominatim";
    public const string Ban = "ban";
    public const string OpenMeteo = "open-meteo";
    public const string MetNo = "met-no";

    public static readonly IReadOnlySet<string> Geocoders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Nominatim, Ban };

    public static readonly IReadOnlySet<string> WeatherProviders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { OpenMeteo, MetNo };
}
