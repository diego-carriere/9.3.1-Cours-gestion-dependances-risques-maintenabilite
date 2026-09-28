using Meteo.Api.Contracts;
using Meteo.Application.Forecasting;
using Meteo.Domain.Model;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Le mapper est le seul chemin du résultat du cas d'usage vers le JSON public (TP3), commun
/// au mode réel et au mode démo. Testé directement, comme ForecastErrorMapper côté erreurs.
/// </summary>
public sealed class ForecastResponseMapperTests
{
    private static readonly GeoLocation Ales = new(44.1258, 4.0806, "Alès, Gard, France");

    private static ForecastResult ResultWith(string variable, string unit) => new(
        Address.Create("Alès").Value,
        new Forecast(Ales, variable, unit, [new ForecastPoint(new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero), 24.3)]),
        IsDegraded: false,
        DataAsOfUtc: new DateTimeOffset(2026, 9, 18, 14, 5, 0, TimeSpan.Zero));

    [Fact]
    public void Maps_the_requested_address_the_coordinates_and_utc_hourly_temperatures()
    {
        var response = ForecastResponseMapper.ToResponse(ResultWith(WeatherVariables.AirTemperature, "°C"));

        Assert.Equal("Alès", response.Address);
        Assert.Equal(44.1258, response.Latitude);
        Assert.Equal(4.0806, response.Longitude);
        var point = Assert.Single(response.Hourly);
        Assert.Equal(new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc), point.Time);
        Assert.Equal(DateTimeKind.Utc, point.Time.Kind); // DateTime.Equals ignore Kind : vérifié à part.
        Assert.Equal(24.3, point.TemperatureCelsius);
    }

    [Theory]
    [InlineData(WeatherVariables.ShortwaveRadiation, "W/m²")]
    [InlineData(WeatherVariables.AirTemperature, "°F")]
    public void Refuses_to_publish_anything_but_air_temperature_in_celsius(string variable, string unit) =>
        Assert.Throws<InvalidOperationException>(() => ForecastResponseMapper.ToResponse(ResultWith(variable, unit)));
}
