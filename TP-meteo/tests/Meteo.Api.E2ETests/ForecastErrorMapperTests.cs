using Meteo.Api.Errors;
using Meteo.Domain.Results;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Compense l'impossibilité d'un <c>switch</c> C# réellement exhaustif sur un enum
/// (CS8524) : garantit par un test, plutôt que par le seul compilateur, que
/// <see cref="ForecastErrorMapper"/> gère chaque valeur déclarée de
/// <see cref="ForecastErrorKind"/> sans tomber dans la branche <c>default</c> qui lève.
/// </summary>
public sealed class ForecastErrorMapperTests
{
    [Theory]
    [MemberData(nameof(AllKinds))]
    public void ToProblem_handles_every_declared_kind(ForecastErrorKind kind)
    {
        var error = new ForecastError(kind, "test");

        var exception = Record.Exception(() => ForecastErrorMapper.ToProblem(error));

        Assert.Null(exception);
    }

    public static TheoryData<ForecastErrorKind> AllKinds() => [.. Enum.GetValues<ForecastErrorKind>()];
}
