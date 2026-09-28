using Meteo.Domain.Model;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// Vérifie que <c>OpenMeteo:HourlyVariable</c> produit la température de l'air. Depuis le TP3,
/// le contrat public nomme son champ <c>temperatureCelsius</c> : une autre variable connue de
/// l'adaptateur (<c>shortwave_radiation</c>) y serait servie sous une étiquette fausse.
/// Branché avec <c>.ValidateOnStart()</c> : l'application refuse alors de démarrer.
/// </summary>
internal sealed class OpenMeteoOptionsValidator : IValidateOptions<OpenMeteoOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenMeteoOptions options) =>
        OpenMeteoVariables.Known.TryGetValue(options.HourlyVariable, out var variable)
        && variable.Canonical == WeatherVariables.AirTemperature
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"OpenMeteo:HourlyVariable '{options.HourlyVariable}' ne produit pas {WeatherVariables.AirTemperature}, " +
                $"seule variable du contrat public (temperatureCelsius). Valeurs valides : {string.Join(", ", AirTemperatureVariables)}.");

    private static IEnumerable<string> AirTemperatureVariables =>
        OpenMeteoVariables.Known.Where(v => v.Value.Canonical == WeatherVariables.AirTemperature).Select(v => v.Key);
}
