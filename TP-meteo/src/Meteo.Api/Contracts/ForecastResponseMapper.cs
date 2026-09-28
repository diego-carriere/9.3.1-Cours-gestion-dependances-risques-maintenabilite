using Meteo.Application.Forecasting;
using Meteo.Domain.Model;

namespace Meteo.Api.Contracts;

/// <summary>
/// Le seul endroit qui traduit un <see cref="ForecastResult"/> en contrat public : c'est le
/// pendant de ForecastErrorMapper côté succès. Mode réel et mode démo passent tous deux par
/// ici, donc la forme de la réponse ne peut pas diverger entre eux (TP3).
/// </summary>
internal static class ForecastResponseMapper
{
    private const string Celsius = "°C";

    public static ForecastResponse ToResponse(ForecastResult result)
    {
        var forecast = result.Forecast;

        // Le champ public s'appelle temperatureCelsius. Y servir une autre variable ou une
        // autre unité tromperait le client. OpenMeteoOptionsValidator l'empêche dès le
        // démarrage : si on arrive ici quand même, c'est un bug d'adaptateur, pas un échec attendu.
        if (forecast.Variable != WeatherVariables.AirTemperature || forecast.Unit != Celsius)
        {
            throw new InvalidOperationException(
                $"Prévision '{forecast.Variable}' en '{forecast.Unit}' : le contrat public n'expose que " +
                $"{WeatherVariables.AirTemperature} en {Celsius}.");
        }

        return new ForecastResponse(
            Address: result.RequestedAddress.Value,
            Latitude: forecast.Location.Latitude,
            Longitude: forecast.Location.Longitude,
            Hourly: [.. forecast.Points.Select(p => new ForecastPointResponse(p.TimestampUtc.UtcDateTime, p.Value))]);
    }
}
