namespace Meteo.Infrastructure.Weather;

public sealed class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    public string BaseUrl { get; init; } = "https://api.open-meteo.com/";

    public string HourlyVariable { get; init; } = "shortwave_radiation";
}
