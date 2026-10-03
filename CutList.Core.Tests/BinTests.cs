using Xunit;

namespace CutList.Core.Tests;

public class BinTests
{
    [Fact]
    public void Bar_cut_exactly_full_with_decimal_lengths_has_no_negative_waste()
    {
        // 4 + 3 + 3 plus two 0.1" kerfs is exactly 10.2"; the kerf after the last cut runs off the
        // end. In binary floating point the overrun is a hair more than 0.1, which must still count.
        var bin = new Bin(10.2) { Spacing = 0.1 };
        bin.AddItems(new[] { new BinItem("A", 4), new BinItem("B", 3), new BinItem("C", 3) });

        Assert.Equal(10.2, bin.UsedLength);
        Assert.Equal(0, bin.RemainingLength);
        Assert.Equal(1, bin.Utilization);
    }
}
