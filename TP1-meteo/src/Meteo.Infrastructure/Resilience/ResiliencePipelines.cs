using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Meteo.Infrastructure.Resilience;

/// <summary>
/// Compose le pipeline Polly attaché à un client HTTP typé : timeout total (englobant),
/// retry, circuit breaker, timeout par tentative (englobé) — dans cet ordre d'imbrication.
/// Réutilisé par les deux clients (Nominatim, Open-Meteo) avec des seuils différents, et par
/// les tests de résilience (qui construisent leur propre pipeline à seuils réduits).
/// </summary>
internal static class ResiliencePipelines
{
    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, UpstreamResilienceOptions options)
    {
        builder
            .AddTimeout(options.TotalTimeout)
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = options.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.RetryBaseDelay,
                ShouldHandle = static args => ValueTask.FromResult(ShouldRetry(args.Outcome)),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                BreakDuration = options.CircuitBreakerBreakDuration,
                ShouldHandle = static args => ValueTask.FromResult(IsFailure(args.Outcome)),
            })
            .AddTimeout(options.AttemptTimeout);
    }

    // Ne réessaie jamais un circuit ouvert (on veut échouer vite, pas retarder l'échec), ni
    // un 4xx (une adresse introuvable ou une requête malformée n'est pas transitoire — et
    // réessayer gâcherait le budget 1 req/s de Nominatim).
    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is not BrokenCircuitException && IsFailure(outcome);

    private static bool IsFailure(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            return true;
        }

        var status = outcome.Result?.StatusCode;
        return status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (status is not null && (int)status >= 500);
    }
}
