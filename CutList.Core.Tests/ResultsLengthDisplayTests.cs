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
}
