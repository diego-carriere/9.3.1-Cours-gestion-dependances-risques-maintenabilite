namespace Meteo.Api.E2ETests;

/// <summary>
/// Une <see cref="MeteoApiFactory"/> neuve par test (constructeur/Dispose xUnit), pas
/// partagée via <c>IClassFixture</c> : la file de réponses scriptées et les requêtes
/// enregistrées de <see cref="FakeUpstream"/> ne doivent jamais fuiter d'un test à l'autre.
/// </summary>
public abstract class ApiTestBase : IDisposable
{
    protected ApiTestBase()
    {
        Factory = new MeteoApiFactory();
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
