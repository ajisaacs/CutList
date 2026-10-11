using CutList.Core.Nesting;
using CutList.Core.Nesting.Pipeline;
using CutList.Core.Nesting.Search;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public sealed class PatternReplacementTests
{
    private const long AmpleBudget = 1_000_000;

    [Fact]
    public void Finds_a_concrete_combination_the_existing_greedy_pass_misses()
    {
        // Gap 8: starting at 4 greedily takes 3, while starting later cannot revisit 4.
        var bin = MakeBin(18, 0, 10, 7);
        var remaining = Items(4, 3, 2, 2);
        var input = bin.Items.Concat(remaining).ToList();
        SwapInLeftoversStep.ImproveBin(bin, remaining, 0);
        Assert.Equal(new[] { 10.0, 7 }, bin.Items.Select(i => i.Length));
        var budget = new SearchBudget(AmpleBudget);

        PatternReplacementEngine.ImproveBin(bin, remaining, budget);

        Assert.Equal(new[] { 10.0, 4, 2, 2 }, bin.Items.Select(i => i.Length));
        Assert.Equal(new[] { 3.0, 7 }, remaining.Select(i => i.Length));
        Assert.True(budget.Used > 0);
        Assert.False(budget.Exhausted);
        AssertValid(input, new PackResult([bin], remaining));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Pack_runs_search_before_later_bars_consume_the_replacement_parts(int maxBars)
    {
        var items = Items(10, 7, 4, 3, 2, 2);
        var request = new PackingRequest(items, 18, maxBinCount: maxBars);
        var baseline = new FirstFitEngine().Pack(request);
        var engine = new PatternReplacementEngine(AmpleBudget);

        var result = engine.Pack(request);

        Assert.Equal(new[] { 10.0, 7 }, baseline.Bins[0].Items.Select(i => i.Length));
        Assert.Equal(new[] { 10.0, 4, 2, 2 }, result.Bins[0].Items.Select(i => i.Length));
        Assert.Same(engine.LastUnguardedResult, result);
        Assert.False(engine.LastGuardRejected);
        AssertNotWorse(baseline, result);
        AssertValid(items, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Zero_or_tiny_budget_preserves_the_exact_baseline_outcome(long limit)
    {
        var items = Items(10, 7, 4, 3, 2, 2);
        var request = new PackingRequest(items, 18, maxBinCount: 1);
        var engine = new PatternReplacementEngine(limit);

        var result = engine.Pack(request);

        AssertSamePlan(new FirstFitEngine().Pack(request), result);
        Assert.Equal(limit + 1, engine.LastBudgetUsed);
        Assert.True(engine.LastBudgetExhausted);
        Assert.Equal(new[] { 10.0, 7 }, result.Bins[0].Items.Select(i => i.Length));
        // The ample-budget test above has a different, strictly fuller result on this same input.
        AssertValid(items, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Exhausted_attempt_does_not_mutate_either_collection(long limit)
    {
        var bin = MakeBin(18, 0, 10, 7);
        var remaining = Items(4, 3, 2, 2);
        var originalBin = bin.Items.ToArray();
        var originalRemaining = remaining.ToArray();
        var budget = new SearchBudget(limit);

        PatternReplacementEngine.ImproveBin(bin, remaining, budget);
        PatternReplacementEngine.ImproveBin(bin, remaining, budget); // No new search after exhaustion.

        AssertReferences(originalBin, bin.Items);
        AssertReferences(originalRemaining, remaining);
        Assert.Equal(limit + 1, budget.Used);
        Assert.True(budget.Exhausted);
    }

    [Fact]
    public void Budget_is_shared_across_bins_and_final_pass_and_is_fresh_per_pack()
    {
        // Neither bar has a strict gain. Each search fits this limit on its own, but their sum does not.
        var firstWork = new SearchBudget(AmpleBudget);
        PatternReplacementEngine.ImproveBin(MakeBin(18, 0, 10, 7), Items(10, 7, 4, 3, 2), firstWork);
        var laterWork = new SearchBudget(AmpleBudget);
        PatternReplacementEngine.ImproveBin(MakeBin(18, 0, 10, 7), Items(4, 3, 2), laterWork);
        Assert.False(firstWork.Exhausted);
        Assert.InRange(laterWork.Used, 1, firstWork.Used);
        var request = new PackingRequest(Items(10, 10, 7, 7, 4, 3, 2), 18, maxBinCount: 2);
        var engine = new PatternReplacementEngine(firstWork.Used);

        var result = engine.Pack(request);

        Assert.True(engine.LastBudgetExhausted); // Would be false if each bin reset its counter.
        Assert.Equal(firstWork.Used + 1, engine.LastBudgetUsed);
        AssertSamePlan(new FirstFitEngine().Pack(request), result);
        AssertSamePlan(result, engine.Pack(request));
        Assert.Equal(firstWork.Used + 1, engine.LastBudgetUsed);
        Assert.True(engine.LastBudgetExhausted);
        var ample = new PatternReplacementEngine(AmpleBudget);
        ample.Pack(request);
        Assert.False(ample.LastBudgetExhausted);
        Assert.Equal(firstWork.Used + 3 * laterWork.Used, ample.LastBudgetUsed);
        engine.Pack(new PackingRequest(Items(18), 18));
        Assert.Equal(0, engine.LastBudgetUsed);
        Assert.False(engine.LastBudgetExhausted);
        Assert.False(engine.LastGuardRejected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.125)]
    public void Reserved_capacity_subtracts_retained_sizes_from_the_original_capacity(double kerf)
    {
        var retained = Items(10, 1);
        long reserved = retained.Sum(i => CutFit.Size(i.Length, kerf));
        var full = CutDemand.From(Items(4, 2, 2), 20, kerf);
        var gap = CutDemand.From(Items(4, 2, 2), 20, kerf, reservedCapacity: reserved);

        Assert.Equal(CutFit.Capacity(20, kerf), full.Capacity);
        Assert.Equal(full.Capacity - reserved, gap.Capacity);
        Assert.Equal(full.LengthUnits, gap.LengthUnits);
        Assert.Equal(full.Sizes, gap.Sizes);
        Assert.Equal(full.Counts, gap.Counts);
        Assert.Equal(20, gap.StockLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.125)]
    public void Accepts_the_original_tolerance_boundary_without_granting_it_twice(double kerf)
    {
        var bin = MakeBin(18 + kerf, kerf, 10, 7);
        var remaining = Items(8 + Tolerance.Epsilon);
        var replacement = remaining[0];
        var input = bin.Items.Concat(remaining).ToList();

        PatternReplacementEngine.ImproveBin(bin, remaining, new SearchBudget(AmpleBudget));

        Assert.Contains(bin.Items, i => ReferenceEquals(i, replacement));
        Assert.Equal(CutFit.Capacity(bin.Length, kerf), bin.Items.Sum(i => CutFit.Size(i.Length, kerf)));
        AssertValid(input, new PackResult([bin], remaining));
    }

    [Theory]
    [InlineData(0, 0.000000001)]
    [InlineData(0.125, 0.000000001)]
    [InlineData(0.125, 0.125)]
    public void Rejects_a_replacement_requiring_an_extra_tolerance_or_last_kerf(double kerf, double excess)
    {
        var bin = MakeBin(18 + kerf, kerf, 10, 7);
        var remaining = Items(8 + Tolerance.Epsilon + excess);
        var originalBin = bin.Items.ToArray();
        var originalRemaining = remaining.ToArray();
        long reserved = CutFit.Size(10, kerf);
        var demand = CutDemand.From(remaining, bin.Length, kerf, reserved);
        long replacementSize = CutFit.Size(remaining[0].Length, kerf);
        Assert.True(replacementSize > demand.Capacity);
        // Reinterpreting available Size units as a new stock grants another kerf/tolerance.
        Assert.True(replacementSize <= CutFit.Capacity(demand.Capacity * CutFit.Resolution, kerf));

        PatternReplacementEngine.ImproveBin(bin, remaining, new SearchBudget(AmpleBudget));

        AssertReferences(originalBin, bin.Items);
        AssertReferences(originalRemaining, remaining);
        Assert.True(CutFit.Fits(bin.Items.Select(i => i.Length), bin.Length, kerf));
    }

    [Fact]
    public void Preserves_the_longest_group_even_when_unanchored_packing_would_fill_more()
    {
        var bin = MakeBin(10, 0, 4, 4);
        var remaining = Items(3, 3, 3);
        var originalBin = bin.Items.ToArray();
        var originalRemaining = remaining.ToArray();
        var budget = new SearchBudget(AmpleBudget);

        PatternReplacementEngine.ImproveBin(bin, remaining, budget);

        AssertReferences(originalBin, bin.Items);
        AssertReferences(originalRemaining, remaining);
        Assert.Equal(0, budget.Used);
    }

    [Fact]
    public void Commits_only_part_length_gains_not_extra_kerf_usage_or_equal_length_patterns()
    {
        // {4,3} uses more kerf than {7}, but consumes no more part length.
        var bin = MakeBin(18.5, 0.125, 10, 7);
        var remaining = Items(4, 3);
        var originalBin = bin.Items.ToArray();
        var originalRemaining = remaining.ToArray();
        var budget = new SearchBudget(AmpleBudget);

        PatternReplacementEngine.ImproveBin(bin, remaining, budget);

        AssertReferences(originalBin, bin.Items);
        AssertReferences(originalRemaining, remaining);
        Assert.True(budget.Used > 0);
        Assert.False(budget.Exhausted);
    }

    [Fact]
    public void Preserves_references_when_value_equality_would_remove_the_anchored_item()
    {
        var anchor = new BinItem("copy", 7.000005);
        var displaced = new BinItem("copy", 7);
        Assert.Equal(anchor, displaced); // Bin.RemoveItem(displaced) would remove anchor instead.
        var bin = new Bin(16);
        bin.AddItems([anchor, displaced]);
        var remaining = Items(8);
        var replacement = remaining[0];
        var input = bin.Items.Concat(remaining).ToList();

        PatternReplacementEngine.ImproveBin(bin, remaining, new SearchBudget(AmpleBudget));

        AssertReferences([anchor, replacement], bin.Items);
        Assert.Same(displaced, Assert.Single(remaining));
        AssertValid(input, new PackResult([bin], remaining));
    }

    [Fact]
    public void Characterizes_the_inherited_near_equal_named_part_identity_defect_at_pack_level()
    {
        // Known pre-existing greedy-helper defect, not a claim of universal prototype safety.
        // Keep this characterization tied to docs/replacement-search-evaluation.md until that
        // separate production repair changes these assertions to exact reference conservation.
        var items = Items(7.000005, 7, 4, 3);
        var request = new PackingRequest(items, 16, maxBinCount: 1);
        var baseline = new FirstFitEngine().Pack(request);
        var engine = new PatternReplacementEngine(AmpleBudget);
        var candidate = engine.Pack(request);

        foreach (var result in new[] { baseline, candidate })
        {
            var returned = result.Bins.SelectMany(b => b.Items).Concat(result.ItemsNotUsed).ToList();
            Assert.DoesNotContain(returned, i => ReferenceEquals(i, items[0]));
            Assert.Equal(2, returned.Count(i => ReferenceEquals(i, items[1])));
            Assert.Equal(items.Count, returned.Count); // Aggregate demand checks miss the corruption.
        }
        Assert.False(engine.LastGuardRejected);
    }

    [Fact]
    public void High_distinct_length_jobs_do_not_enter_recursive_pattern_enumeration()
    {
        var bin = MakeBin(1_200, 0, 1_000, 100);
        var remaining = Items(Enumerable.Range(1, ExhaustiveSearchEngine.MaxDistinctLengths + 1)
            .Select(i => (double)i).ToArray());
        var originalBin = bin.Items.ToArray();
        var originalRemaining = remaining.ToArray();
        var budget = new SearchBudget(AmpleBudget);

        PatternReplacementEngine.ImproveBin(bin, remaining, budget);

        Assert.Equal(0, budget.Used);
        Assert.False(budget.Exhausted);
        AssertReferences(originalBin, bin.Items);
        AssertReferences(originalRemaining, remaining);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Whole_job_guard_rejects_search_specific_increases_in_bars_or_unplaced_parts(int maxBars)
    {
        // {4,3,2,2} fills the later gaps of 2 and 9. A tighter first bar strands {7,3}.
        var items = Items(30, 18, 18, 15, 14, 7, 4, 3, 2, 2);
        var request = new PackingRequest(items, 38, maxBinCount: maxBars);
        var baseline = new FirstFitEngine().Pack(request);
        var engine = new PatternReplacementEngine(AmpleBudget);

        var result = engine.Pack(request);

        Assert.Equal(3, baseline.Bins.Count);
        Assert.Empty(baseline.ItemsNotUsed);
        var candidate = engine.LastUnguardedResult!;
        Assert.Equal(maxBars < 0 ? 4 : 3, candidate.Bins.Count);
        Assert.Equal(maxBars < 0 ? 0 : 1, candidate.ItemsNotUsed.Count);
        Assert.True(engine.LastGuardRejected);
        Assert.NotSame(candidate, result);
        AssertSamePlan(baseline, result);
        AssertValid(items, result);
        AssertValid(items, candidate);
        // Disabling search removes the bad local swap, so this is not a vacuous guard assertion.
        var disabled = new PatternReplacementEngine(0);
        AssertSamePlan(baseline, disabled.Pack(request));
        Assert.False(disabled.LastGuardRejected);
    }

    [Fact]
    public void Whole_job_guard_rejects_more_unplaced_length_even_with_equal_bar_and_part_counts()
    {
        var items = Items(20, 19, 18, 17, 17, 13, 12, 11, 9, 7, 4, 4, 3, 2);
        var request = new PackingRequest(items, 25, maxBinCount: 2);
        var baseline = new FirstFitEngine().Pack(request);
        var engine = new PatternReplacementEngine(AmpleBudget);

        var result = engine.Pack(request);

        var candidate = engine.LastUnguardedResult!;
        Assert.Equal(baseline.Bins.Count, candidate.Bins.Count);
        Assert.Equal(baseline.ItemsNotUsed.Count, candidate.ItemsNotUsed.Count);
        Assert.True(UnusedLength(candidate) > UnusedLength(baseline));
        Assert.True(engine.LastGuardRejected);
        AssertSamePlan(baseline, result);
        AssertValid(items, result);
        AssertValid(items, candidate);
    }

    [Fact]
    public void Small_seeded_replacement_gaps_match_an_independent_subset_oracle()
    {
        var random = new Random(951);
        int improved = 0;
        for (int trial = 0; trial < 300; trial++)
        {
            double kerf = random.Next(2) == 0 ? 0 : 0.125;
            double gap = random.Next(8, 41) / 4.0;
            var bin = MakeBin(100 + gap + kerf, kerf, 100, gap - 0.125);
            var remaining = Items(Enumerable.Range(0, random.Next(2, 9))
                .Select(_ => random.Next(1, (int)(gap * 4) + 1) / 4.0).ToArray());
            var input = bin.Items.Concat(remaining).ToList();
            var original = CutFit.Units(gap - 0.125);
            long capacity = CutFit.Capacity(bin.Length, kerf) - CutFit.Size(100, kerf);
            long optimum = Math.Max(original, MostSubsetLength(remaining, capacity, kerf));
            var budget = new SearchBudget(AmpleBudget);

            PatternReplacementEngine.ImproveBin(bin, remaining, budget);

            // The displaced near-full-gap item cannot share the gap with any alternative (>= .25).
            Assert.Equal(CutFit.Units(100) + optimum, bin.Items.Sum(i => CutFit.Units(i.Length)));
            Assert.False(budget.Exhausted);
            AssertValid(input, new PackResult([bin], remaining));
            if (optimum > original) improved++;
        }
        Assert.True(improved > 0, "The corpus must exercise successful replacements, not only no-ops.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Seeded_whole_jobs_never_worsen_the_production_baseline(int maxBars)
    {
        var random = new Random(952);
        var engine = new PatternReplacementEngine(10_000);
        for (int trial = 0; trial < 100; trial++)
        {
            var items = Items(Enumerable.Range(0, random.Next(4, 35))
                .Select(_ => random.Next(1, 100) / 8.0).ToArray());
            var request = new PackingRequest(items, 10, 0.125, maxBars);
            var baseline = new FirstFitEngine().Pack(request);

            var result = engine.Pack(request);

            AssertNotWorse(baseline, result);
            AssertValid(items, result);
            Assert.InRange(engine.LastBudgetUsed, 0, 10_001);
            Assert.True(result.Bins.Count <= request.MaxBinCount);
        }
    }

    private static long MostSubsetLength(IReadOnlyList<BinItem> items, long capacity, double kerf)
    {
        long best = 0;
        for (int mask = 0; mask < 1 << items.Count; mask++)
        {
            long used = 0, length = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if ((mask & 1 << i) == 0) continue;
                used += CutFit.Size(items[i].Length, kerf);
                length += CutFit.Units(items[i].Length);
            }
            if (used <= capacity) best = Math.Max(best, length);
        }
        return best;
    }

    private static List<BinItem> Items(params double[] lengths) =>
        lengths.Select(length => new BinItem("copy", length)).ToList();

    private static Bin MakeBin(double stock, double kerf, params double[] lengths)
    {
        var bin = new Bin(stock) { Spacing = kerf };
        bin.AddItems(Items(lengths));
        return bin;
    }

    private static long UnusedLength(PackResult result) => result.ItemsNotUsed.Sum(i => CutFit.Units(i.Length));

    private static void AssertNotWorse(PackResult baseline, PackResult result)
    {
        Assert.True(result.Bins.Count <= baseline.Bins.Count);
        Assert.True(result.ItemsNotUsed.Count <= baseline.ItemsNotUsed.Count);
        Assert.True(UnusedLength(result) <= UnusedLength(baseline));
    }

    private static void AssertSamePlan(PackResult expected, PackResult actual)
    {
        Assert.Equal(expected.Bins.Count, actual.Bins.Count);
        for (int i = 0; i < expected.Bins.Count; i++)
            AssertReferences(expected.Bins[i].Items, actual.Bins[i].Items);
        AssertReferences(expected.ItemsNotUsed, actual.ItemsNotUsed);
    }

    private static void AssertReferences(IReadOnlyList<BinItem> expected, IReadOnlyList<BinItem> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++) Assert.Same(expected[i], actual[i]);
    }

    private static void AssertValid(List<BinItem> input, PackResult result)
    {
        var returned = result.Bins.SelectMany(b => b.Items).Concat(result.ItemsNotUsed).ToList();
        Assert.Equal(input.Count, returned.Count);
        var outstanding = new HashSet<BinItem>(input, ReferenceEqualityComparer.Instance);
        Assert.All(returned, item => Assert.True(outstanding.Remove(item), "Duplicate or foreign reference"));
        Assert.Empty(outstanding);
        Assert.All(result.Bins, bin => Assert.True(CutFit.Fits(bin.Items.Select(i => i.Length), bin.Length, bin.Spacing)));
    }
}
