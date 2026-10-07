using CutList.Core.Nesting;
using CutList.Core.Nesting.Pipeline;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class FirstFitImprovementTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(0.125, 10.25)]
    public void Improves_each_bar_before_packing_the_displaced_part_on_the_next_bar(double kerf, double stock)
    {
        var items = Items(6, 3, 2, 2);

        var result = new FirstFitEngine().Pack(new PackingRequest(items, stock, kerf));

        Assert.Equal(2, result.Bins.Count);
        Assert.Equal(new[] { 6.0, 2, 2 }, result.Bins[0].Items.Select(i => i.Length));
        Assert.Same(items[1], Assert.Single(result.Bins[1].Items));
        Assert.Empty(result.ItemsNotUsed);
        AssertConservedAndFits(items, result);
    }

    [Fact]
    public void Finite_stock_keeps_the_displaced_part_as_a_leftover()
    {
        var items = Items(6, 3, 2, 2);

        var result = new FirstFitEngine().Pack(new PackingRequest(items, 10, 0, maxBinCount: 1));

        Assert.Equal(new[] { 6.0, 2, 2 }, Assert.Single(result.Bins).Items.Select(i => i.Length));
        Assert.Same(items[1], Assert.Single(result.ItemsNotUsed));
        AssertConservedAndFits(items, result);
    }

    [Fact]
    public void Rejects_a_replacement_that_only_fits_without_the_required_kerfs()
    {
        var items = Items(6, 3, 2, 2);

        var result = new FirstFitEngine().Pack(new PackingRequest(items, 10, 0.125));

        Assert.Equal(new[] { 6.0, 3 }, result.Bins[0].Items.Select(i => i.Length));
        Assert.Equal(2, result.Bins.Count);
        Assert.Empty(result.ItemsNotUsed);
        AssertConservedAndFits(items, result);
    }

    [Fact]
    public void Preserves_equal_named_part_instances_across_repeated_improvements()
    {
        var items = Items(6, 6, 3, 3, 2, 2, 2, 2);

        var result = new FirstFitEngine().Pack(new PackingRequest(items, 10));

        Assert.Equal(3, result.Bins.Count);
        Assert.All(result.Bins.Take(2), b => Assert.Equal(new[] { 6.0, 2, 2 }, b.Items.Select(i => i.Length)));
        Assert.Equal(new[] { 3.0, 3 }, result.Bins[2].Items.Select(i => i.Length));
        Assert.Empty(result.ItemsNotUsed);
        AssertConservedAndFits(items, result);
    }

    [Fact]
    public void Keeps_the_existing_longest_part_group_anchor()
    {
        var items = Items(4, 4, 3, 3, 3, 3);

        var result = new FirstFitEngine().Pack(new PackingRequest(items, 10));

        Assert.Contains(result.Bins, b => b.Items.Select(i => i.Length).SequenceEqual(new[] { 4.0, 4 }));
        AssertConservedAndFits(items, result);
    }

    [Fact]
    public void Stock_orchestration_packs_displaced_demand_on_the_next_stock_type()
    {
        var items = Items(6, 3, 2, 2);
        var packer = new MultiBinPacker(new FirstFitEngine());
        packer.SetBins(new[] { new MultiBin(10, 1, 1), new MultiBin(4, -1, 2) });

        var result = packer.Pack(items);

        Assert.Equal(new[] { 10.0, 4 }, result.Bins.Select(b => b.Length));
        Assert.Equal(new[] { 6.0, 2, 2 }, result.Bins[0].Items.Select(i => i.Length));
        Assert.Same(items[1], Assert.Single(result.Bins[1].Items));
        Assert.Empty(result.ItemsNotUsed);
        AssertConservedAndFits(items, result);
    }

    [Fact]
    public void Exhaustive_incumbent_uses_the_improved_first_fit_layout()
    {
        var items = Items(6, 3, 2, 2);

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, 10));

        Assert.Equal(2, result.Bins.Count);
        Assert.Equal(new[] { 6.0, 2, 2 }, result.Bins[0].Items.Select(i => i.Length));
        Assert.Same(items[1], Assert.Single(result.Bins[1].Items));
        Assert.Null(result.FallbackEngine);
        AssertConservedAndFits(items, result);
    }

    [Fact]
    public void Local_improvement_does_not_increase_the_whole_job_bar_count()
    {
        // A fuller first bar must not strand parts that make the overall plan worse.
        var random = new Random(1001);
        var items = Benchmarks.SearchBaselineBenchmark.Scenarios[0].Generate(random);

        var result = new FirstFitEngine().Pack(new PackingRequest(items, 240, 0.125));

        Assert.True(result.Bins.Count <= 90, $"Expected at most the baseline's 90 bars, got {result.Bins.Count}");
        Assert.Empty(result.ItemsNotUsed);
        AssertConservedAndFits(items, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Candidate_never_worsens_baseline_stock_or_unplaced_demand(int maxBars)
    {
        var baseline = new PackingPipeline()
            .AddStep(new FilterOversizedItemsStep())
            .AddStep(new SortItemsDescendingStep())
            .AddStep(new FirstFitDecreasingStep())
            .AddStep(new SwapInLeftoversStep());
        var random = new Random(412);
        for (var job = 0; job < 100; job++)
        {
            var items = Items(Enumerable.Range(0, random.Next(4, 40))
                .Select(_ => random.Next(1, 100) / 8.0).ToArray());
            var request = new PackingRequest(items, 10, 0.125, maxBars);
            var original = baseline.Execute(request);

            var result = new FirstFitEngine().Pack(request);

            Assert.True(result.Bins.Count <= original.Bins.Count);
            Assert.True(result.ItemsNotUsed.Count <= original.ItemsNotUsed.Count);
            Assert.True(result.ItemsNotUsed.Sum(i => CutFit.Units(i.Length)) <=
                original.ItemsNotUsed.Sum(i => CutFit.Units(i.Length)));
            AssertConservedAndFits(items, result);
        }
    }

    private static List<BinItem> Items(params double[] lengths) =>
        lengths.Select(length => new BinItem($"P{length}", length)).ToList();

    private static void AssertConservedAndFits(List<BinItem> input, PackResult result)
    {
        var returned = result.Bins.SelectMany(b => b.Items).Concat(result.ItemsNotUsed).ToList();
        Assert.Equal(input.Count, returned.Count);
        Assert.All(input, item => Assert.Single(returned, r => ReferenceEquals(item, r)));
        Assert.All(result.Bins, bin =>
            Assert.True(CutFit.Fits(bin.Items.Select(i => i.Length), bin.Length, bin.Spacing)));
    }
}
