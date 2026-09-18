using Meteo.Domain.Results;

namespace Meteo.Domain.Tests;

public class ResultTests
{
    [Fact]
    public void Success_exposes_value_and_no_error()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_exposes_error_and_throws_on_value_access()
    {
        var result = Result.Failure<int>(ForecastErrorKind.InvalidAddress, "nope");

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.InvalidAddress, result.Error.Kind);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Success_throws_on_error_access()
    {
        var result = Result.Success(1);

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }
}
