using CutList.Core.Formatting;
using Xunit;

namespace CutList.Core.Tests;

public class ResultsLengthDisplayTests
{
    [Theory]
    [InlineData(150, "150\"")]
    [InlineData(150.375, "150-3/8\"")]
    public void FormatInches_uses_total_inches_without_converting_to_feet(double inches, string expected)
    {
        Assert.Equal(expected, ArchUnits.FormatInches(inches));
    }

    [Theory]
    [InlineData(0.0598, "1/16\"")]
    [InlineData(0.1196, "1/8\"")]
    [InlineData(0.031249999, "0\"")]
    [InlineData(0.03125, "1/16\"")]
    [InlineData(0.031250001, "1/16\"")]
    [InlineData(-0.03125, "-1/16\"")]
    [InlineData(-0.001, "0\"")]
    [InlineData(1.999, "2\"")]
    [InlineData(-1.999, "-2\"")]
    [InlineData(0, "0\"")]
    [InlineData(11.999, "12\"")]
    [InlineData(95.999, "96\"")]
    [InlineData(96.0625, "96-1/16\"")]
    [InlineData(-96.0625, "-96-1/16\"")]
    public void All_inches_display_rounds_without_introducing_feet(double inches, string expected)
    {
        Assert.Equal(expected, ArchUnits.FormatInches(inches));
    }

    [Theory]
    [InlineData(0, "0\"")]
    [InlineData(0.0598, "1/16\"")]
    [InlineData(0.031249999, "0\"")]
    [InlineData(0.03125, "1/16\"")]
    [InlineData(0.031250001, "1/16\"")]
    [InlineData(1.999, "2\"")]
    [InlineData(11.968749999, "11-15/16\"")]
    [InlineData(11.96875, "1'  0\"")]
    [InlineData(11.968750001, "1'  0\"")]
    [InlineData(11.999, "1'  0\"")]
    [InlineData(12, "1'  0\"")]
    [InlineData(12.0625, "1'  0-1/16\"")]
    [InlineData(18.375, "1'  6-3/8\"")]
    [InlineData(95.999, "8'  0\"")]
    [InlineData(96.0625, "8'  0-1/16\"")]
    [InlineData(-0.001, "0\"")]
    [InlineData(-0.031249999, "0\"")]
    [InlineData(-0.03125, "-1/16\"")]
    [InlineData(-0.031250001, "-1/16\"")]
    [InlineData(-1.999, "-2\"")]
    [InlineData(-11.968749999, "-11-15/16\"")]
    [InlineData(-11.96875, "-1'  0\"")]
    [InlineData(-11.968750001, "-1'  0\"")]
    [InlineData(-11.999, "-1'  0\"")]
    [InlineData(-95.999, "-8'  0\"")]
    [InlineData(-96.0625, "-8'  0-1/16\"")]
    [InlineData(-18.375, "-1'  6-3/8\"")]
    public void Feet_display_rounds_before_splitting_and_applies_the_sign_once(double inches, string expected)
    {
        Assert.Equal(expected, ArchUnits.FormatFromInches(inches));
    }

    [Theory]
    [InlineData("12'", 144)]
    [InlineData("6\"", 6)]
    [InlineData("12 1/2\"", 12.5)]
    [InlineData("1' 6\"", 18)]
    [InlineData("12.03", 12.03)]
    [InlineData("0.0598", 0.0598)]
    public void Parsing_preserves_the_existing_syntax_and_unrounded_measurement(string input, double expected)
    {
        Assert.Equal(expected, ArchUnits.ParseToInches(input));
    }

    [Fact]
    public void Results_markup_includes_unit_toggle_and_divider_between_part_and_length()
    {
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../CutList.Web/Components/Pages/Jobs/Edit.razor"));
        var markup = File.ReadAllText(sourcePath);

        Assert.Contains("FormatResultLength", markup);
        Assert.Contains("cut-part-badge-divider", markup);
        Assert.Contains("Show feet + inches", markup);
    }

    [Fact]
    public void Printed_cut_list_rows_are_kept_together()
    {
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../CutList.Web/wwwroot/css/report.css"));
        var css = File.ReadAllText(sourcePath);

        Assert.Contains(".cutlist-material-card tbody tr", css);
        Assert.Contains("break-inside: avoid", css);
        Assert.Contains("page-break-inside: avoid", css);
    }
}
