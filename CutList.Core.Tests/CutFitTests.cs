using Xunit;

namespace CutList.Core.Tests;

public class CutFitTests
{
    [Theory]
    [InlineData(10.19999, true)]  // {4,3,3} plus two 0.1" kerfs is 10.2": the bar plus exactly the tolerance
    [InlineData(10.19998, false)] // 0.00001" beyond the tolerance
    public void Parts_may_overrun_the_bar_by_the_tolerance_and_no_more(double stock, bool fits)
    {
        Assert.Equal(fits, CutFit.Fits(new[] { 4.0, 3, 3 }, stock, 0.1));
    }

    [Fact]
    public void The_kerf_after_the_last_cut_may_run_off_the_bar()
    {
        Assert.True(CutFit.Fits(new[] { 5.0, 5 }, 10.125, 0.125)); // 5 + 0.125 + 5 = 10.125
        Assert.False(CutFit.Fits(new[] { 5.0, 5, 0.1 }, 10.125, 0.125));
    }
}
