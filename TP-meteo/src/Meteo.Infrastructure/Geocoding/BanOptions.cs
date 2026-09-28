namespace Meteo.Infrastructure.Geocoding;

/// <summary>
/// L'API Adresse de la Base Adresse Nationale (géocodeur souverain exigé au TP2) n'impose
/// pas de User-Agent descriptif comme Nominatim — envoyé quand même par correction, une
/// bonne pratique n'a pas besoin d'être imposée pour être suivie.
/// </summary>
public sealed class BanOptions
{
    public const string SectionName = "Ban";

    public string BaseUrl { get; init; } = "https://api-adresse.data.gouv.fr/";

    public string UserAgent { get; init; } = "TP2-MeteoApi/1.0";

    public int Limit { get; init; } = 1;
}
