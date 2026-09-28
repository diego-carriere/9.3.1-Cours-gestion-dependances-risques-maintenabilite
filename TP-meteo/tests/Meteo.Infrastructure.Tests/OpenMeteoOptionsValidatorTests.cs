using Meteo.Infrastructure.Weather;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Branché avec <c>.ValidateOnStart()</c> : depuis le TP3, le contrat public nomme son champ
/// <c>temperatureCelsius</c>, donc seule une variable Open-Meteo de température de l'air est
/// acceptée. Toute autre empêche l'application de démarrer, plutôt que d'être servie sous une
/// étiquette fausse.
/// </summary>
public sealed class OpenMeteoOptionsValidatorTests
{
    private readonly OpenMeteoOptionsValidator _sut = new();

    [Fact]
    public void Validate_succeeds_for_temperature_2m()
    {
        var result = _sut.Validate(name: null, new OpenMeteoOptions { HourlyVariable = "temperature_2m" });

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("shortwave_radiation")] // connue de l'adaptateur, mais ce n'est pas une température
    [InlineData("relative_humidity_2m")] // inconnue de l'adaptateur
    public void Validate_fails_for_a_variable_that_is_not_air_temperature(string variable)
    {
        var result = _sut.Validate(name: null, new OpenMeteoOptions { HourlyVariable = variable });

        Assert.True(result.Failed);
        Assert.Contains(variable, result.FailureMessage, StringComparison.Ordinal);
    }
}
