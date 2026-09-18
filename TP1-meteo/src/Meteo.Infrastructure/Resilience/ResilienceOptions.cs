namespace Meteo.Infrastructure.Resilience;

/// <summary>
/// Timeout total (englobe les tentatives), retry avec jitter, circuit breaker, timeout par
/// tentative. Chaque fournisseur a ses propres seuils : Nominatim et la BAN sont appelés
/// moins souvent (grâce au cache de géocodage), leur disjoncteur peut donc se permettre un
/// seuil plus bas.
/// </summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    public UpstreamResilienceOptions Nominatim { get; init; } = new()
    {
        TotalTimeout = TimeSpan.FromSeconds(8),
        AttemptTimeout = TimeSpan.FromSeconds(2.5),
        RetryCount = 2,
        RetryBaseDelay = TimeSpan.FromMilliseconds(500),
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerMinimumThroughput = 5,
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(15),
    };

    // La BAN n'impose pas de limite de débit documentée aussi stricte que Nominatim (pas
    // de FixedWindowRateLimiter dédié) : mêmes seuils que Nominatim par prudence, le cache
    // de géocodage (24h) la rend de toute façon rarement appelée.
    public UpstreamResilienceOptions Ban { get; init; } = new()
    {
        TotalTimeout = TimeSpan.FromSeconds(8),
        AttemptTimeout = TimeSpan.FromSeconds(2.5),
        RetryCount = 2,
        RetryBaseDelay = TimeSpan.FromMilliseconds(500),
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerMinimumThroughput = 5,
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(15),
    };

    public UpstreamResilienceOptions OpenMeteo { get; init; } = new()
    {
        TotalTimeout = TimeSpan.FromSeconds(6),
        AttemptTimeout = TimeSpan.FromSeconds(2),
        RetryCount = 3,
        RetryBaseDelay = TimeSpan.FromMilliseconds(300),
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerMinimumThroughput = 10,
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(10),
    };
}

/// <summary>Un <c>record</c> (plutôt qu'une classe) pour que les tests puissent dériver des variantes via <c>with</c>.</summary>
public sealed record UpstreamResilienceOptions
{
    public TimeSpan TotalTimeout { get; init; }

    public TimeSpan AttemptTimeout { get; init; }

    public int RetryCount { get; init; }

    public TimeSpan RetryBaseDelay { get; init; }

    public double CircuitBreakerFailureRatio { get; init; }

    public TimeSpan CircuitBreakerSamplingDuration { get; init; }

    public int CircuitBreakerMinimumThroughput { get; init; }

    public TimeSpan CircuitBreakerBreakDuration { get; init; }
}
