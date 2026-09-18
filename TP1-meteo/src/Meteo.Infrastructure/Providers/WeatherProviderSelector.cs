using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Providers;

/// <summary>
/// Le seul <see cref="IWeatherProvider"/> exposé au conteneur. Même rôle que
/// <see cref="GeocoderSelector"/>, côté météo : voir sa note pour le raisonnement complet
/// (IOptionsMonitor relu à chaque appel, IServiceProvider de portée, sans état propre).
/// </summary>
internal sealed partial class WeatherProviderSelector : IWeatherProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<ProvidersOptions> _options;
    private readonly ILogger<WeatherProviderSelector> _logger;

    public WeatherProviderSelector(
        IServiceProvider serviceProvider, IOptionsMonitor<ProvidersOptions> options, ILogger<WeatherProviderSelector> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    public Task<Result<Forecast>> GetForecastAsync(GeoLocation location, CancellationToken cancellationToken)
    {
        var provider = _serviceProvider.GetRequiredKeyedService<IWeatherProvider>(ResolveKey());
        return provider.GetForecastAsync(location, cancellationToken);
    }

    private string ResolveKey()
    {
        var requested = _options.CurrentValue.Weather;

        if (ProviderKeys.WeatherProviders.Contains(requested))
        {
            return requested;
        }

        LogUnknownProviderKey("Weather", requested, ProviderKeys.OpenMeteo);
        return ProviderKeys.OpenMeteo;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Providers:{Port} '{Key}' is unknown; falling back to '{Fallback}'.")]
    private partial void LogUnknownProviderKey(string port, string key, string fallback);
}
