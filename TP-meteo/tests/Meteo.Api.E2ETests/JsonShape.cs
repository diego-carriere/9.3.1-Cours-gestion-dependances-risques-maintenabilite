using System.Text.Json;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Réduit un document JSON à sa forme : chaque chemin de propriété avec le type JSON de sa
/// valeur, sans les valeurs. Deux réponses de même forme ont la même signature quel que soit
/// leur contenu : c'est le critère de réussite du TP3, rendu comparable par Assert.Equal.
/// </summary>
internal static class JsonShape
{
    /// <summary>La forme qu'exige le brief du TP3 pour toute réponse 200 de /forecast.</summary>
    public static IReadOnlyList<string> Unified { get; } = Sorted(
    [
        "$:Object",
        "$.address:String",
        "$.latitude:Number",
        "$.longitude:Number",
        "$.hourly:Array",
        "$.hourly[]:Object",
        "$.hourly[].time:String",
        "$.hourly[].temperatureCelsius:Number",
    ]);

    public static IReadOnlyList<string> Of(string json)
    {
        using var document = JsonDocument.Parse(json);
        var paths = new List<string>();
        Walk(document.RootElement, "$", paths);
        return Sorted(paths);
    }

    private static void Walk(JsonElement element, string path, List<string> paths)
    {
        paths.Add($"{path}:{element.ValueKind}");

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Walk(property.Value, $"{path}.{property.Name}", paths);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, $"{path}[]", paths);
            }
        }
    }

    // Distinct : les éléments d'un tableau partagent le même chemin « [] ».
    private static string[] Sorted(IEnumerable<string> paths) => [.. paths.Distinct().Order(StringComparer.Ordinal)];
}
