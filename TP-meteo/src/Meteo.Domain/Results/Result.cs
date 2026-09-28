namespace Meteo.Domain.Results;

/// <summary>
/// Catégories d'échecs métier attendus du cas d'usage de prévision. Jamais une exception :
/// voir Support J1, "le new partout" et la frontière domaine/HTTP (Meteo.Api en fait le
/// mapping vers un code de statut, dans un seul fichier).
/// </summary>
public enum ForecastErrorKind
{
    InvalidAddress,
    AddressNotFound,
    GeocodingUnavailable,
    WeatherUnavailable,
    UpstreamTimeout,
}

public sealed record ForecastError(ForecastErrorKind Kind, string Message);

/// <summary>
/// Résultat d'une opération qui peut échouer de façon attendue (adresse introuvable,
/// service externe indisponible...). Les bugs continuent de lever des exceptions ; ce type
/// ne sert qu'aux échecs qui font partie du comportement normal du cas d'usage.
/// Construit exclusivement via la classe statique non générique <see cref="Result"/>
/// (CA1000 : un type générique ne doit pas exposer de membres statiques publics).
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly ForecastError? _error;

    private Result(bool isSuccess, T? value, ForecastError? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>La valeur de succès. Lève si <see cref="IsSuccess"/> est faux.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    /// <summary>L'erreur d'échec. Lève si <see cref="IsSuccess"/> est vrai.</summary>
    public ForecastError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error of a successful Result.");

    internal static Result<T> CreateSuccess(T value) => new(true, value, null);

    internal static Result<T> CreateFailure(ForecastError error) => new(false, default, error);
}

/// <summary>Fabriques pour <see cref="Result{T}"/> (CA1000-compliant : classe non générique).</summary>
public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.CreateSuccess(value);

    public static Result<T> Failure<T>(ForecastError error) => Result<T>.CreateFailure(error);

    public static Result<T> Failure<T>(ForecastErrorKind kind, string message) =>
        Failure<T>(new ForecastError(kind, message));
}
