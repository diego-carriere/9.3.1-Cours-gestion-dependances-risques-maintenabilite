namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Choix du fournisseur actif par port. Lue par
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> (pas
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>) dans
/// <see cref="GeocoderSelector"/> et <see cref="WeatherProviderSelector"/> : relue à chaque
/// appel, elle peut être modifiée en configuration sans redémarrer le processus — c'est
/// l'exigence du TP2, "changer de fournisseur sans redéploiement".
/// </summary>
public sealed class ProvidersOptions
{
    public const string SectionName = "Providers";

    public string Geocoder { get; init; } = ProviderKeys.Nominatim;

    public string Weather { get; init; } = ProviderKeys.OpenMeteo;
}
