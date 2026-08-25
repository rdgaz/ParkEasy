using ParkEasy.Application.Services;
using Xunit;

namespace ParkEasy.Tests;

public class PlateNormalizerTests
{
    [Theory]
    [InlineData("abc-1d23", "ABC1D23")]
    [InlineData("abc 1d23", "ABC1D23")]
    [InlineData("ABC1D23", "ABC1D23")]
    [InlineData("abc-1234", "ABC1234")]
    [InlineData(" abc 1234 ", "ABC1234")]
    public void Normalize_ReturnsCleanUppercaseAlphanumeric(string input, string expected)
    {
        var result = PlateNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ABC1D23", true)]  // Mercosul
    [InlineData("ABC1234", true)]  // Old Brazilian format
    [InlineData("XYZ9Z99", true)]  // Mercosul
    [InlineData("INVALID", false)]
    [InlineData("1234567", false)]
    [InlineData("ABC-1234", false)] // Raw string with hyphens is invalid before normalization
    [InlineData("", false)]
    public void IsValid_ValidatesCorrectFormats(string input, bool expected)
    {
        var result = PlateNormalizer.IsValid(input);
        Assert.Equal(expected, result);
    }
}
