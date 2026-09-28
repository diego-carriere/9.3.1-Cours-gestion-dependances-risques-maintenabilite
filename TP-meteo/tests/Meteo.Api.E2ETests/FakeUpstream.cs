namespace Meteo.Api.E2ETests;

/// <summary>
/// Route les requêtes HTTP sortantes par hôte vers des réponses scriptées, et enregistre
/// chaque requête envoyée (pour des assertions comme "Nominatim contacté une seule fois").
/// Remplace le <see cref="HttpMessageHandler"/> primaire — le point de substitution le plus
/// bas : le client typé, la désérialisation, le pipeline de résilience, le routage et le
/// mapping ProblemDetails restent tous réels au-dessus.
/// </summary>
public sealed class FakeUpstream
{
    private readonly Dictionary<string, Queue<Func<HttpRequestMessage, HttpResponseMessage>>> _responsesByHost =
        new(StringComparer.OrdinalIgnoreCase);

    public List<HttpRequestMessage> Requests { get; } = [];

    public void EnqueueFor(string host, HttpResponseMessage response) => EnqueueFor(host, _ => response);

    public void EnqueueFor(string host, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        if (!_responsesByHost.TryGetValue(host, out var queue))
        {
            queue = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
            _responsesByHost[host] = queue;
        }

        queue.Enqueue(responder);
    }

    public int CallCountFor(string host) =>
        Requests.Count(r => string.Equals(r.RequestUri?.Host, host, StringComparison.OrdinalIgnoreCase));

    public HttpMessageHandler CreateHandler() => new RoutingHandler(this);

    private sealed class RoutingHandler(FakeUpstream upstream) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            upstream.Requests.Add(request);
            var host = request.RequestUri?.Host
                ?? throw new InvalidOperationException("Request has no URI.");

            if (!upstream._responsesByHost.TryGetValue(host, out var queue) || queue.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(FakeUpstream)} has no scripted response left for host '{host}'.");
            }

            return Task.FromResult(queue.Dequeue()(request));
        }
    }
}
