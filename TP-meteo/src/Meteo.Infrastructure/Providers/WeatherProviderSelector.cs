using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Le seul <see cref="IWeatherProvider"/> exposé au conteneur. Même rôle que
/// <see cref="GeocoderSelector"/>, côté météo : voir sa note pour le raisonnement complet,
/// notamment pourquoi la lecture se fait sur <see cref="IConfiguration"/> brute plutôt que
/// sur un <c>IOptionsMonitor&lt;ProvidersOptions&gt;</c>.
/// </summary>
internal sealed partial class WeatherProviderSelector : IWeatherProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeatherProviderSelector> _logger;

    public WeatherProviderSelector(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<WeatherProviderSelector> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<Result<Forecast>> GetForecastAsync(GeoLocation location, CancellationToken cancellationToken)
    {
        var provider = _serviceProvider.GetRequiredKeyedService<IWeatherProvider>(ResolveKey());
        return provider.GetForecastAsync(location, cancellationToken);
    }

    private string ResolveKey()
    {
        var requested = _configuration[$"{ProvidersOptions.SectionName}:Weather"] ?? ProviderKeys.OpenMeteo;

        if (ProviderKeys.WeatherProviders.TryGetValue(requested, out var canonical))
        {
            return canonical;
        }

        LogUnknownProviderKey("Weather", requested, ProviderKeys.OpenMeteo);
        return ProviderKeys.OpenMeteo;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Providers:{Port} '{Key}' is unknown; falling back to '{Fallback}'.")]
    private partial void LogUnknownProviderKey(string port, string key, string fallback);
}
