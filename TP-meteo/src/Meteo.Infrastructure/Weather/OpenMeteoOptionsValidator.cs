using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Weather;

/// <summary>
/// Vérifie que <c>OpenMeteo:HourlyVariable</c> a un nom canonique connu
/// (<see cref="OpenMeteoVariables"/>). Branché avec <c>.ValidateOnStart()</c>.
/// </summary>
internal sealed class OpenMeteoOptionsValidator : IValidateOptions<OpenMeteoOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenMeteoOptions options) =>
        OpenMeteoVariables.Known.ContainsKey(options.HourlyVariable)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"OpenMeteo:HourlyVariable '{options.HourlyVariable}' n'a pas de nom canonique. " +
                $"Valeurs valides : {string.Join(", ", OpenMeteoVariables.Known.Keys)}.");
}
