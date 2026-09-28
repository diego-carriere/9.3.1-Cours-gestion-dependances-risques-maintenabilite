namespace Meteo.TestSupport;

/// <summary>
/// Remplace le transport HTTP sortant au point de substitution le plus bas : le
/// <see cref="HttpMessageHandler"/> primaire. Tout ce qui est au-dessus reste réel — le
/// client typé, la désérialisation, le pipeline Polly, le routage. Utilisé directement par
/// Meteo.Infrastructure.Tests (un client) et composé par host dans FakeUpstream (E2E).
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public int CallCount => Requests.Count;

    public void Enqueue(HttpResponseMessage response) => Enqueue(_ => response);

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responses.Enqueue(responder);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(StubHttpMessageHandler)} has no scripted response left for {request.RequestUri}.");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}
