using Meteo.Domain.Results;

namespace Meteo.Domain.Model;

/// <summary>
/// Objet-valeur auto-validant : une <see cref="Address"/> invalide ne peut pas exister,
/// on ne peut passer que par <see cref="Create"/>. La normalisation et <see cref="CacheKey"/>
/// (minuscule invariante) garantissent que "Alès" et "ALÈS" partagent la même entrée de
/// cache géocodage.
/// </summary>
public sealed record Address
{
    public const int MaxLength = 200;

    public string Value { get; }

    public string CacheKey { get; }

    private Address(string value)
    {
        Value = value;
        CacheKey = value.ToLowerInvariant();
    }

    public static Result<Address> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Failure<Address>(ForecastErrorKind.InvalidAddress, "L'adresse est requise.");
        }

        var normalized = CollapseWhitespace(raw.Trim());

        if (normalized.Length > MaxLength)
        {
            return Result.Failure<Address>(
                ForecastErrorKind.InvalidAddress,
                $"L'adresse ne peut pas dépasser {MaxLength} caractères.");
        }

        return Result.Success(new Address(normalized));
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public override string ToString() => Value;
}
