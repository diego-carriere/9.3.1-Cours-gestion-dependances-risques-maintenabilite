using System.Globalization;
using System.Net;
using System.Text;
using Meteo.Domain.Abstractions;
using Meteo.Domain.Model;
using Meteo.Domain.Results;
using Xunit;

namespace Meteo.TestSupport;

/// <summary>
/// Suite de contrat unique exécutée contre chaque implémentation HTTP d'<see cref="IWeatherProvider"/>
/// (Open-Meteo, MET Norway...), pilotée par <see cref="StubHttpMessageHandler"/> : réponse
/// valide, panne amont persistante, réponse vide, culture d'exécution non invariante — la
/// dépendance cachée "séparateur décimal" de Support J1, désormais couverte pour tout
/// fournisseur et non plus pour le seul client Open-Meteo.
/// </summary>
public abstract class HttpWeatherProviderContractTests : WeatherProviderContractTests
{
    /// <summary>Construit l'implémentation réelle, câblée sur <paramref name="handler"/> comme transport.</summary>
    protected abstract IWeatherProvider CreateSut(StubHttpMessageHandler handler);

    /// <summary>Un corps de réponse représentant une prévision valide pour <see cref="WeatherProviderContractTests.SampleLocation"/>.</summary>
    protected abstract string ValidForecastBody { get; }

    /// <summary>Un corps de réponse vide ou illisible.</summary>
    protected abstract string EmptyBody { get; }

    protected sealed override IWeatherProvider CreateSutReturningWeatherUnavailable()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        return CreateSut(handler);
    }

    [Fact]
    public async Task GetForecastAsync_for_a_valid_location_returns_ordered_points_in_the_canonical_variable()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(Json(ValidForecastBody));
        var sut = CreateSut(handler);

        var result = await sut.GetForecastAsync(SampleLocation, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Points);
        Assert.Equal(WeatherVariables.AirTemperature, result.Value.Variable);
        Assert.Equal("°C", result.Value.Unit);

        var timestamps = result.Value.Points.Select(p => p.TimestampUtc).ToList();
        Assert.Equal(timestamps.OrderBy(t => t).ToList(), timestamps);
    }

    [Fact]
    public async Task GetForecastAsync_returns_WeatherUnavailable_for_a_persisting_server_error()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = CreateSut(handler);

        var result = await sut.GetForecastAsync(SampleLocation, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.WeatherUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task GetForecastAsync_for_an_empty_response_never_throws()
    {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(EmptyBody) });
        var sut = CreateSut(handler);

        var result = await sut.GetForecastAsync(SampleLocation, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetForecastAsync_builds_a_culture_invariant_url_under_fr_FR()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

        try
        {
            var handler = new StubHttpMessageHandler();
            handler.Enqueue(Json(ValidForecastBody));
            var sut = CreateSut(handler);

            await sut.GetForecastAsync(SampleLocation, CancellationToken.None);

            var query = Assert.Single(handler.Requests).RequestUri!.Query;
            Assert.DoesNotContain(',', query);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
