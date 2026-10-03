using System.Globalization;
using Pizzeria.Services;

namespace Pizzeria.Tests;

public class PriceFormatterTests
{
    // RF-12: 10500 -> "$10.500"; no decimals, no space after "$".
    [Theory]
    [InlineData(10500, "$10.500")]
    [InlineData(9500, "$9.500")]
    [InlineData(0, "$0")]
    [InlineData(1250000, "$1.250.000")]
    [InlineData(999, "$999")]
    public void ToCop_FormatsWithDotThousandsSeparator_RF12(int price, string expected)
    {
        Assert.Equal(expected, PriceFormatter.ToCop(price));
    }

    // RF-12: stored prices are decimal(10,2); the output never shows decimals.
    [Fact]
    public void ToCop_PriceWithZeroDecimals_ShowsNoDecimals_RF12()
    {
        Assert.Equal("$10.500", PriceFormatter.ToCop(10500.00m));
    }

    // RF-12: the result does not depend on the server culture.
    [Theory]
    [InlineData("en-US")]
    [InlineData("es-CO")]
    [InlineData("de-CH")]
    [InlineData("")] // invariant culture
    public void ToCop_IsIndependentOfCurrentCulture_RF12(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            Assert.Equal("$10.500", PriceFormatter.ToCop(10500m));
            Assert.Equal("$1.250.000", PriceFormatter.ToCop(1250000m));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    // RF-12 (negative): no comma separator, no decimals, no whitespace of any kind.
    [Fact]
    public void ToCop_NeverUsesCommaDecimalsOrSpaces_RF12()
    {
        var result = PriceFormatter.ToCop(1250000m);

        Assert.DoesNotContain(",", result);
        Assert.DoesNotContain(result, char.IsWhiteSpace);
        Assert.StartsWith("$", result);
        Assert.False(result.EndsWith(".00", StringComparison.Ordinal));
    }
}
