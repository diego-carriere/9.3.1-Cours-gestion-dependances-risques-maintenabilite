using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Héberge l'application complète en mémoire : vrai routage, vrai conteneur DI, vrais
/// middlewares, vrai pipeline de résilience. Seul le transport HTTP sortant est remplacé
/// (<see cref="FakeUpstream"/>), via <c>ConfigureHttpClientDefaults</c> qui s'applique à
/// tous les clients nommés/typés en une fois.
/// </summary>
public sealed class MeteoApiFactory : WebApplicationFactory<Program>
{
    public FakeUpstream Upstream { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("Nominatim:UserAgent", "TP1-Meteo-E2ETests/1.0 (test)"),

            // Délais Polly raccourcis au minimum : la suite e2e doit rester rapide et
            // déterministe, jamais soumise aux vrais délais de retry/breaker.
            new KeyValuePair<string, string?>("Resilience:Nominatim:RetryBaseDelay", "00:00:00.001"),
            new KeyValuePair<string, string?>("Resilience:Nominatim:CircuitBreakerBreakDuration", "00:00:00.500"),
            new KeyValuePair<string, string?>("Resilience:OpenMeteo:RetryBaseDelay", "00:00:00.001"),
            new KeyValuePair<string, string?>("Resilience:OpenMeteo:CircuitBreakerBreakDuration", "00:00:00.500"),
        ]));

        builder.ConfigureTestServices(services =>
            services.ConfigureHttpClientDefaults(http =>
                http.ConfigurePrimaryHttpMessageHandler(() => Upstream.CreateHandler())));
    }
}
