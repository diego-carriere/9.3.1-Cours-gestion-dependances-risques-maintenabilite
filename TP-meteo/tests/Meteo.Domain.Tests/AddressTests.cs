using Meteo.Domain.Model;
using Meteo.Domain.Results;

namespace Meteo.Domain.Tests;

public class AddressTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_null_or_blank(string? raw)
    {
        var result = Address.Create(raw);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.InvalidAddress, result.Error.Kind);
    }

    [Fact]
    public void Create_trims_and_collapses_whitespace()
    {
        var result = Address.Create("  Alès  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Alès", result.Value.Value);
    }

    [Fact]
    public void Create_rejects_addresses_longer_than_max_length()
    {
        var tooLong = new string('a', Address.MaxLength + 1);

        var result = Address.Create(tooLong);

        Assert.True(result.IsFailure);
        Assert.Equal(ForecastErrorKind.InvalidAddress, result.Error.Kind);
    }

    [Fact]
    public void CacheKey_is_culture_invariant_and_case_insensitive()
    {
        var lower = Address.Create("Alès").Value;
        var upper = Address.Create("ALÈS").Value;

        Assert.Equal(lower.CacheKey, upper.CacheKey);
    }
}
