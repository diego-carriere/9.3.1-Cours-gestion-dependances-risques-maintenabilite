using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Vérifie que <c>Providers:Geocoder</c>/<c>Providers:Weather</c> nomment une clé connue.
/// Branché avec <c>.ValidateOnStart()</c>, comme <c>NominatimOptions</c> : une faute de
/// frappe en configuration empêche l'application de démarrer plutôt que d'échouer en
/// silence au premier appel. Ne protège que le démarrage — voir <see cref="GeocoderSelector"/>
/// et <see cref="WeatherProviderSelector"/> pour le repli après un rechargement à chaud.
/// </summary>
internal sealed class ProvidersOptionsValidator : IValidateOptions<ProvidersOptions>
{
    public ValidateOptionsResult Validate(string? name, ProvidersOptions options)
    {
        List<string>? failures = null;

        if (!ProviderKeys.Geocoders.Contains(options.Geocoder))
        {
            (failures ??= []).Add(
                $"Providers:Geocoder '{options.Geocoder}' est inconnu. Valeurs valides : {string.Join(", ", ProviderKeys.Geocoders)}.");
        }

        if (!ProviderKeys.WeatherProviders.Contains(options.Weather))
        {
            (failures ??= []).Add(
                $"Providers:Weather '{options.Weather}' est inconnu. Valeurs valides : {string.Join(", ", ProviderKeys.WeatherProviders)}.");
        }

        return failures is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
