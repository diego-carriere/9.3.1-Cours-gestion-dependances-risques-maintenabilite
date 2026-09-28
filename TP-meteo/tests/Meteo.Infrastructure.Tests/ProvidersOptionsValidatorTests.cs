using Meteo.Infrastructure.Providers;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Branché avec <c>.ValidateOnStart()</c> dans <c>InfrastructureServiceCollectionExtensions</c> :
/// une clé de fournisseur inconnue doit empêcher l'application de démarrer, pas échouer en
/// silence au premier <c>/forecast</c>.
/// </summary>
public sealed class ProvidersOptionsValidatorTests
{
    private readonly ProvidersOptionsValidator _sut = new();

    [Fact]
    public void Validate_succeeds_for_the_default_options()
    {
        var result = _sut.Validate(name: null, new ProvidersOptions());

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData(ProviderKeys.Nominatim)]
    [InlineData(ProviderKeys.Ban)]
    public void Validate_succeeds_for_every_known_geocoder_key(string key)
    {
        var result = _sut.Validate(name: null, new ProvidersOptions { Geocoder = key });

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData(ProviderKeys.OpenMeteo)]
    [InlineData(ProviderKeys.MetNo)]
    public void Validate_succeeds_for_every_known_weather_key(string key)
    {
        var result = _sut.Validate(name: null, new ProvidersOptions { Weather = key });

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_fails_for_an_unknown_geocoder_key()
    {
        var result = _sut.Validate(name: null, new ProvidersOptions { Geocoder = "unknown" });

        Assert.True(result.Failed);
        Assert.Contains("Providers:Geocoder", result.FailureMessage);
    }

    [Fact]
    public void Validate_fails_for_an_unknown_weather_key()
    {
        var result = _sut.Validate(name: null, new ProvidersOptions { Weather = "unknown" });

        Assert.True(result.Failed);
        Assert.Contains("Providers:Weather", result.FailureMessage);
    }
}
