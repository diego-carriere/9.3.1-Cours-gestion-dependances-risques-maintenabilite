namespace Meteo.Api.E2ETests;

/// <summary>
/// Une <see cref="MeteoApiFactory"/> neuve par test (constructeur/Dispose xUnit), pas
/// partagée via <c>IClassFixture</c> : la file de réponses scriptées et les requêtes
/// enregistrées de <see cref="FakeUpstream"/> ne doivent jamais fuiter d'un test à l'autre.
/// </summary>
public abstract class ApiTestBase : IDisposable
{
    protected ApiTestBase()
        : this(initialConfiguration: null)
    {
    }

    /// <summary>Pour un test qui a besoin de démarrer l'hôte avec un fournisseur non-TP1 déjà actif.</summary>
    protected ApiTestBase(IReadOnlyDictionary<string, string?>? initialConfiguration)
    {
        Factory = new MeteoApiFactory(initialConfiguration);
        Client = Factory.CreateClient();
    }

    protected MeteoApiFactory Factory { get; }

    protected HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
