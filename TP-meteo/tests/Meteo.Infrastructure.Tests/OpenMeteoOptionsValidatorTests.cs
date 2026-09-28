using Meteo.Infrastructure.Weather;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Branché avec <c>.ValidateOnStart()</c> : une variable Open-Meteo sans nom canonique
/// connu empêche l'application de démarrer, plutôt que d'être servie sous une étiquette fausse.
/// </summary>
public sealed class OpenMeteoOptionsValidatorTests
{
    private readonly OpenMeteoOptionsValidator _sut = new();

    [Theory]
    [InlineData("temperature_2m")]
    [InlineData("shortwave_radiation")]
    public void Validate_succeeds_for_a_variable_with_a_canonical_name(string variable)
    {
        var result = _sut.Validate(name: null, new OpenMeteoOptions { HourlyVariable = variable });

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_fails_for_a_variable_without_a_canonical_name()
    {
        var result = _sut.Validate(name: null, new OpenMeteoOptions { HourlyVariable = "relative_humidity_2m" });

        Assert.True(result.Failed);
        Assert.Contains("relative_humidity_2m", result.FailureMessage, StringComparison.Ordinal);
    }
}
