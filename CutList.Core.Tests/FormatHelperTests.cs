using System.Globalization;
using CutList.Core.Formatting;
using Xunit;

namespace CutList.Core.Tests;

public class FormatHelperTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("0.0625", "1/16")]
    [InlineData("0.125", "1/8")]
    [InlineData("0.1875", "3/16")]
    [InlineData("0.25", "1/4")]
    [InlineData("0.3125", "5/16")]
    [InlineData("0.375", "3/8")]
    [InlineData("0.4375", "7/16")]
    [InlineData("0.5", "1/2")]
    [InlineData("0.5625", "9/16")]
    [InlineData("0.625", "5/8")]
    [InlineData("0.6875", "11/16")]
    [InlineData("0.75", "3/4")]
    [InlineData("0.8125", "13/16")]
    [InlineData("0.875", "7/8")]
    [InlineData("0.9375", "15/16")]
    [InlineData("1.375", "1-3/8")]
    [InlineData("0.0598", "1/16")]
    [InlineData("0.1196", "1/8")]
    [InlineData("0.031249999", "0")]
    [InlineData("0.03125", "1/16")]
    [InlineData("0.031250001", "1/16")]
    [InlineData("1.093749999", "1-1/16")]
    [InlineData("1.09375", "1-1/8")]
    [InlineData("1.093750001", "1-1/8")]
    [InlineData("0.999", "1")]
    [InlineData("1.999", "2")]
    [InlineData("-0.001", "0")]
    [InlineData("-0.031249999", "0")]
    [InlineData("-0.03125", "-1/16")]
    [InlineData("-0.031250001", "-1/16")]
    [InlineData("-0.0625", "-1/16")]
    [InlineData("-0.1196", "-1/8")]
    [InlineData("-1", "-1")]
    [InlineData("-1.375", "-1-3/8")]
    [InlineData("-1.093749999", "-1-1/16")]
    [InlineData("-1.09375", "-1-1/8")]
    [InlineData("-1.093750001", "-1-1/8")]
    [InlineData("-0.999", "-1")]
    [InlineData("-1.999", "-2")]
    public void Decimal_display_rounds_complete_measurement_to_nearest_sixteenth(string input, string expected)
    {
        Assert.Equal(expected, FormatHelper.ConvertToMixedFraction(Parse(input)));
    }

    [Theory]
    [InlineData(0.0598, "1/16")]
    [InlineData(0.1196, "1/8")]
    [InlineData(0.03125, "1/16")]
    [InlineData(-0.03125, "-1/16")]
    [InlineData(1.999, "2")]
    [InlineData(0, "0")]
    public void Double_display_uses_the_same_rounding_contract(double input, string expected)
    {
        Assert.Equal(expected, FormatHelper.ConvertToMixedFraction(input));
    }

    [Theory]
    [InlineData("0.062499999", 8, "0")]
    [InlineData("0.0625", 8, "1/8")]
    [InlineData("0.062500001", 8, "1/8")]
    [InlineData("-0.0625", 8, "-1/8")]
    [InlineData("1.9375", 8, "2")]
    [InlineData("0.015625", 32, "1/32")]
    [InlineData("1.25", 32, "1-1/4")]
    [InlineData("0.25", 10, "3/10")]
    [InlineData("-0.25", 10, "-3/10")]
    [InlineData("1.2", 3, "1-1/3")]
    [InlineData("1.8", 3, "1-2/3")]
    [InlineData("1.5", 1, "2")]
    [InlineData("-1.5", 1, "-2")]
    public void Custom_denominators_round_ticks_before_splitting_and_reducing(string input, int precision, string expected)
    {
        Assert.Equal(expected, FormatHelper.ConvertToMixedFraction(Parse(input), precision));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0", -16)]
    [InlineData("1", 0)]
    [InlineData("1.25", -16)]
    public void Nonpositive_precision_is_rejected_even_for_whole_measurements(string input, int precision)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FormatHelper.ConvertToMixedFraction(Parse(input), precision));
        Assert.Equal("precision", exception.ParamName);
    }

    [Theory]
    [InlineData("0", "0\"")]
    [InlineData("0.0598", "0.0598\"")]
    [InlineData("0.065", "0.065\"")]
    [InlineData("0.1196", "0.1196\"")]
    [InlineData("0.05980000", "0.0598\"")]
    [InlineData("0.0650000", "0.065\"")]
    [InlineData("0.0625", "1/16\"")]
    [InlineData("0.06250000", "1/16\"")]
    [InlineData("0.125", "1/8\"")]
    [InlineData("0.1875", "3/16\"")]
    [InlineData("1.375", "1-3/8\"")]
    [InlineData("12.0625", "12-1/16\"")]
    [InlineData("-0.0625", "-1/16\"")]
    [InlineData("-1.375", "-1-3/8\"")]
    [InlineData("-0.0598", "-0.0598\"")]
    [InlineData("0.0598123456789", "0.0598123456789\"")]
    [InlineData("0.0625000000000000000000000001", "0.0625000000000000000000000001\"")]
    [InlineData("1.2345678901234567890123456789", "1.2345678901234567890123456789\"")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000001\"")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335\"")]
    [InlineData("-79228162514264337593543950335", "-79228162514264337593543950335\"")]
    public void Exact_inches_use_fractions_only_for_exact_sixteenths(string input, string expected)
    {
        Assert.Equal(expected, FormatHelper.FormatExactOrFractionInches(Parse(input)));
    }

    [Fact]
    public void Exact_inches_use_a_decimal_point_independent_of_the_current_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal("0.0598\"", FormatHelper.FormatExactOrFractionInches(0.05980000m));
            Assert.Equal("1-3/8\"", FormatHelper.FormatExactOrFractionInches(1.375m));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
