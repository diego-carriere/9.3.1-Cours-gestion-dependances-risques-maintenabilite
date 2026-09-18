using System.Net;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Meteo.Infrastructure.Geocoding;
using Meteo.Infrastructure.Resilience;
using Meteo.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Meteo.Infrastructure.Tests;

/// <summary>
/// Exerce le pipeline Polly réel (retry, circuit breaker) via un conteneur DI minimal
/// construit dans le test — même logique de câblage qu'InfrastructureServiceCollectionExtensions,
/// mais à seuils réduits pour rester rapide. Toujours sans réseau : StubHttpMessageHandler
/// remplace le transport.
/// </summary>
public sealed class ResilienceTests
{
    private static readonly UpstreamResilienceOptions FastOptions = new()
    {
        TotalTimeout = TimeSpan.FromSeconds(5),
        AttemptTimeout = TimeSpan.FromSeconds(2),
        RetryCount = 2,
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerMinimumThroughput = 5,
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(500), // minimum imposé par Polly
    };

    private static (IGeocoder Geocoder, StubHttpMessageHandler Handler) CreateSut(UpstreamResilienceOptions? options = null)
    {
        var handler = new StubHttpMessageHandler();
        var services = new ServiceCollection();

        services.AddSingleton(Options.Create(new NominatimOptions
        {
            UserAgent = "TP1-Meteo-ResilienceTests/1.0 (test)",
            BaseUrl = "https://nominatim.example/",
        }));

        services.AddHttpClient<IGeocoder, GeocodingClient>((provider, client) =>
            {
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<NominatimOptions>>().Value.BaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddResilienceHandler("nominatim-test", (builder, _) =>
                ResiliencePipelines.Configure(builder, options ?? FastOptions));

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IGeocoder>(), handler);
    }

    [Fact]
    public async Task Persisting_server_error_is_retried_the_configured_number_of_times_then_fails()
    {
        var (geocoder, handler) = CreateSut();
        for (var i = 0; i < 5; i++)
        {
            handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        var result = await geocoder.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.GeocodingUnavailable, result.Error.Kind);
        // 1 tentative initiale + FastOptions.RetryCount (2) retries.
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Client_error_is_never_retried()
    {
        var (geocoder, handler) = CreateSut();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await geocoder.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Circuit_breaker_opens_after_threshold_and_never_leaks_a_broken_circuit_exception()
    {
        var lowThreshold = FastOptions with { CircuitBreakerMinimumThroughput = 2 };
        var (geocoder, handler) = CreateSut(lowThreshold);
        for (var i = 0; i < 20; i++)
        {
            handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        Result<GeoLocation> result = default;
        for (var i = 0; i < 4; i++)
        {
            result = await geocoder.ResolveAsync(Address.Create("Alès").Value, CancellationToken.None);
        }

        // Le disjoncteur a fini par s'ouvrir : le dernier appel échoue vite, sans jamais
        // laisser fuir une BrokenCircuitException (contrat LSP du port).
        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.GeocodingUnavailable, result.Error.Kind);
        Assert.True(handler.CallCount < 4 * 3, "le disjoncteur devrait avoir court-circuité au moins un appel");
    }
}
