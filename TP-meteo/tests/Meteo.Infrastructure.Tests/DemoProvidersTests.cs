using Meteo.Domain.Model;
using Meteo.Infrastructure.Caching;
using Meteo.Infrastructure.Demo;
using Meteo.TestSupport;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Les briques du mode démo (TP3) : deux implémentations de plus des ports existants, sans
/// HttpClient, déterministes grâce à IClock, et un cache qui ne retient rien.
/// </summary>
public sealed class DemoProvidersTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 37, 12, TimeSpan.Zero);
    private static readonly GeoLocation Ales = new(DemoGeocoder.Latitude, DemoGeocoder.Longitude, "Alès");

    [Theory]
    [InlineData("Alès")]
    [InlineData("10 rue de la Paix, Paris")]
    public async Task DemoGeocoder_resolves_any_address_to_the_simulated_location_and_keeps_its_label(string raw)
    {
        var result = await new DemoGeocoder().ResolveAsync(Address.Create(raw).Value, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DemoGeocoder.Latitude, result.Value.Latitude);
        Assert.Equal(DemoGeocoder.Longitude, result.Value.Longitude);
        Assert.Equal(raw, result.Value.DisplayName);
    }

    [Fact]
    public async Task DemoWeatherProvider_returns_hourly_air_temperatures_from_the_current_hour()
    {
        var result = await new DemoWeatherProvider(new FakeClock(Now)).GetForecastAsync(Ales, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WeatherVariables.AirTemperature, result.Value.Variable);
        Assert.Equal("°C", result.Value.Unit);
        Assert.Equal(DemoWeatherProvider.HourCount, result.Value.Points.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero), result.Value.Points[0].TimestampUtc);
        Assert.All(
            result.Value.Points.Zip(result.Value.Points.Skip(1)),
            pair => Assert.Equal(TimeSpan.FromHours(1), pair.Second.TimestampUtc - pair.First.TimestampUtc));
    }

    [Fact]
    public async Task DemoWeatherProvider_is_deterministic_within_an_hour()
    {
        var first = await new DemoWeatherProvider(new FakeClock(Now)).GetForecastAsync(Ales, CancellationToken.None);
        var second = await new DemoWeatherProvider(new FakeClock(Now.AddMinutes(20))).GetForecastAsync(Ales, CancellationToken.None);

        Assert.Equal(first.Value.Points, second.Value.Points);
    }

    [Fact]
    public async Task DemoWeatherProvider_peaks_at_15h_and_bottoms_at_3h_UTC()
    {
        var at15h = new FakeClock(new DateTimeOffset(2026, 9, 28, 15, 0, 0, TimeSpan.Zero));

        var result = await new DemoWeatherProvider(at15h).GetForecastAsync(Ales, CancellationToken.None);

        Assert.Equal(23.0, result.Value.Points[0].Value);  // 15 h
        Assert.Equal(7.0, result.Value.Points[12].Value);  // 3 h le lendemain
    }

    [Fact]
    public void NullCache_never_returns_what_was_written()
    {
        var sut = new NullCache<GeoLocation>();

        sut.Write("geo:alès", Ales, TimeSpan.FromHours(24));

        Assert.Null(sut.Read("geo:alès"));
    }
}
