using System.Text.Json.Serialization;

namespace Meteo.Api.Contracts;

/// <summary>
/// Le contrat public de <c>GET /forecast</c> (TP3, « format de sortie unifié ») : exactement ces
/// quatre champs, quel que soit le fournisseur actif ou le mode démo — seul le contenu change.
/// Les noms JSON sont fixés ici plutôt que déduits de la politique de nommage de l'hôte : le
/// contrat ne dépend d'aucun réglage global (dépendance cachée, Support J1). L'origine et la
/// date des données voyagent en en-têtes (voir ForecastEndpoint), jamais dans le corps.
/// </summary>
public sealed record ForecastResponse(
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] IReadOnlyList<ForecastPointResponse> Hourly);

/// <summary>
/// Un point horaire. <see cref="Time"/> est un <see cref="DateTime"/> UTC, pas un
/// <see cref="DateTimeOffset"/> : System.Text.Json écrit le premier « 2025-06-10T14:00:00Z »
/// (la forme du brief), le second « 2025-06-10T14:00:00+00:00 ».
/// </summary>
public sealed record ForecastPointResponse(
    [property: JsonPropertyName("time")] DateTime Time,
    [property: JsonPropertyName("temperatureCelsius")] double TemperatureCelsius);
