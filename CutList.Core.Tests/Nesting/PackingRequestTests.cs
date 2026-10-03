using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class PackingRequestTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Negative_max_bin_count_means_unlimited(int maxBinCount)
    {
        var request = new PackingRequest(new List<BinItem>(), 96, 0.125, maxBinCount);

        Assert.Equal(int.MaxValue, request.MaxBinCount);
    }

    [Fact]
    public void Positive_max_bin_count_is_kept()
    {
        Assert.Equal(3, new PackingRequest(new List<BinItem>(), 96, 0, 3).MaxBinCount);
    }
}
