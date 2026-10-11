using CutList.Core.Nesting;
using Xunit;
using Xunit.Abstractions;

namespace CutList.Core.Tests.Nesting;

public class LastBarEliminationTests(ITestOutputHelper output)
{
    [Fact]
    public void Direct_elimination_commits_one_fewer_bar_without_mutating_the_baseline()
    {
        var (request, baseline) = Plan(10, 0, [6], [4]);
        var engine = new LastBarEliminationEngine(1_000, new FixedEngine(baseline));

        var result = engine.Pack(request);

        Assert.Single(result.Bins);
        Assert.Equal(1, engine.LastEliminatedBars);
        Assert.Equal(1, engine.LastAttempts);
        Assert.False(engine.LastBudgetExhausted);
        AssertContract(request, baseline, result);
        Assert.Equal(new[] { 6.0 }, baseline.Bins[0].Items.Select(i => i.Length));
        Assert.Equal(new[] { 4.0 }, baseline.Bins[1].Items.Select(i => i.Length));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 0.125)]
    [InlineData(true, 0.125)]
    public void Redistributes_a_part_by_ejecting_one_or_two_residents(bool twoResidents, double kerf)
    {
        double stock = 10 + (twoResidents ? 3 : 2) * kerf;
        var (request, baseline) = twoResidents
            ? Plan(stock, kerf, [6, 1, 1], [7, 1], [4])
            : Plan(stock, kerf, [6, 2], [7, 1], [3, 1]);
        var before = Snapshot(baseline);
        var engine = new LastBarEliminationEngine(10_000, new FixedEngine(baseline));

        var result = engine.Pack(request);

        Assert.Equal(2, result.Bins.Count);
        Assert.Equal(1, engine.LastEliminatedBars);
        AssertContract(request, baseline, result);
        AssertSnapshot(before, baseline);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000)]
    public void Existing_same_named_near_equal_FirstFit_reference_bug_fails_closed(long budget)
    {
        var items = new[] { 7.000005, 7, 4, 3 }.Select(n => new BinItem("part", n)).ToList();
        var request = new PackingRequest(items, 16, maxBinCount: 1);
        var baseline = new FirstFitEngine().Pack(request);
        Assert.False(SameReferences(items, Returned(baseline)));
        Assert.Single(baseline.Bins);
        Assert.Equal(items.Count, Returned(baseline).Count); // count-only checks miss this failure
        Assert.All(baseline.Bins, b => Assert.True(CutFit.Fits(b.Items.Select(i => i.Length), 16, 0)));
        var engine = new LastBarEliminationEngine(budget);

        var error = Assert.Throws<InvalidOperationException>(() => engine.Pack(request));

        Assert.Contains("baseline", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, engine.LastEliminatedBars);
        Assert.Equal(0, engine.LastAttempts);
        Assert.Equal(0, engine.LastBudgetUsed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Insufficient_budget_returns_unchanged_baseline_which_a_larger_budget_improves(long budget)
    {
        var (request, baseline) = Plan(10, 0, [6], [4]);
        var before = Snapshot(baseline);
        var engine = new LastBarEliminationEngine(budget, new FixedEngine(baseline));

        var result = engine.Pack(request);

        Assert.Same(baseline, result);
        Assert.Equal(0, engine.LastEliminatedBars);
        Assert.True(engine.LastBudgetExhausted);
        Assert.Equal(budget + 1, engine.LastBudgetUsed);
        AssertSnapshot(before, baseline);
        Assert.Single(new LastBarEliminationEngine(4, new FixedEngine(baseline)).Pack(request).Bins);
    }

    [Fact]
    public void One_budget_is_shared_across_attempts_and_late_exhaustion_keeps_completed_improvement()
    {
        var (request, baseline) = Plan(10, 0, [6], [1], [1], [1]);
        var before = Snapshot(baseline);
        var limited = new LastBarEliminationEngine(4, new FixedEngine(baseline));
        var full = new LastBarEliminationEngine(12, new FixedEngine(baseline));

        var partial = limited.Pack(request);
        var complete = full.Pack(request);

        Assert.Equal(3, partial.Bins.Count);
        Assert.Equal(1, limited.LastEliminatedBars);
        Assert.Equal(1, limited.LastAttempts);
        Assert.Equal(5, limited.LastBudgetUsed); // next startup is refused, not a renewed budget
        Assert.True(limited.LastBudgetExhausted);
        Assert.Single(complete.Bins);
        Assert.Equal(3, full.LastEliminatedBars);
        Assert.Equal(3, full.LastAttempts);
        Assert.Equal(12, full.LastBudgetUsed);
        Assert.False(full.LastBudgetExhausted);
        AssertContract(request, baseline, partial);
        AssertContract(request, baseline, complete);
        AssertSnapshot(before, baseline);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(100_000)]
    public void Failed_or_exhausted_rearrangement_restores_original_reference_order(long budget)
    {
        // Capacity allows two bars, but 6,6,5 cannot share any bar. Ejecting the 5 for a 6
        // is legal and must be undone when the displaced 5 cannot be rehomed.
        var (request, baseline) = Plan(10, 0, [6], [6], [5, 3]);
        var before = Snapshot(baseline);
        var engine = new LastBarEliminationEngine(budget, new FixedEngine(baseline));

        var result = engine.Pack(request);

        Assert.Same(baseline, result);
        Assert.Equal(0, engine.LastEliminatedBars);
        Assert.Equal(1, engine.LastAttempts);
        Assert.Equal(budget < 100_000, engine.LastBudgetExhausted);
        AssertSnapshot(before, baseline);
        AssertContract(request, baseline, result);
    }

    [Fact]
    public void Impossible_total_capacity_is_a_charged_attempt_without_search_nodes()
    {
        var (request, baseline) = Plan(10, 0, [8], [8], [8]);
        var engine = new LastBarEliminationEngine(100, new FixedEngine(baseline));

        Assert.Same(baseline, engine.Pack(request));

        Assert.Equal(1, engine.LastAttempts);
        Assert.Equal(1, engine.LastBudgetUsed);
        Assert.False(engine.LastBudgetExhausted);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(64, true)]
    [InlineData(65, true)]
    public void Move_depth_is_bounded_without_rejecting_the_64_move_boundary(int count, bool nonMonotone = false)
    {
        var (request, baseline) = Plan(100, 0, [1], Enumerable.Repeat(0.01, count).ToArray());
        var engine = new LastBarEliminationEngine(10_000, new FixedEngine(baseline), nonMonotone);

        var result = engine.Pack(request);

        Assert.Equal(count == 64 ? 1 : 2, result.Bins.Count);
        Assert.Equal(count == 65, engine.LastDepthLimitReached);
        Assert.False(engine.LastBudgetExhausted);
        AssertContract(request, baseline, result);
    }

    [Fact]
    public void Ejection_moves_also_count_toward_the_recursion_depth_bound()
    {
        // Only 64 initially pending parts, but the 3 needs at least one resident ejection,
        // requiring at least 65 moves even though the whole packing would fit two bars.
        var last = new[] { 3.0 }.Concat(Enumerable.Repeat(0.001, 63)).ToArray();
        var (request, baseline) = Plan(10, 0, [6, 2], [7, 1], last);
        var engine = new LastBarEliminationEngine(1_000_000, new FixedEngine(baseline));
        var before = Snapshot(baseline);

        var result = engine.Pack(request);

        Assert.Same(baseline, result);
        Assert.True(engine.LastDepthLimitReached);
        Assert.False(engine.LastBudgetExhausted);
        Assert.Equal(0, engine.LastEliminatedBars);
        AssertContract(request, baseline, result);
        AssertSnapshot(before, baseline);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0.125, 0)]
    [InlineData(0.125, 1)]
    public void Integer_fit_uses_one_last_kerf_and_exact_tolerance_not_residual_rounding(double kerf, int extraUnit)
    {
        double second = 4 - kerf + Tolerance.Epsilon + extraUnit * CutFit.Resolution;
        var (request, baseline) = Plan(10, kerf, [6], [second]);
        var engine = new LastBarEliminationEngine(1_000, new FixedEngine(baseline));

        var result = engine.Pack(request);

        Assert.Equal(extraUnit == 0 ? 1 : 2, result.Bins.Count);
        AssertContract(request, baseline, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Preserves_distinct_equal_values_and_repeated_occurrences_of_the_same_instance(bool sameInstance)
    {
        var first = new BinItem("part", 6);
        var small = new BinItem("part", 2);
        var copy = sameInstance ? small : new BinItem("part", 2);
        var bigBin = new Bin(10);
        bigBin.AddItem(first);
        var smallBin = new Bin(10);
        smallBin.AddItems([small, copy]);
        var baseline = new PackResult([bigBin, smallBin], []);
        var request = new PackingRequest([first, small, copy], 10);
        var engine = new LastBarEliminationEngine(1_000, new FixedEngine(baseline));

        var result = engine.Pack(request);

        Assert.Single(result.Bins);
        AssertContract(request, baseline, result);
        Assert.Equal(sameInstance ? 2 : 1, Returned(result).Count(i => ReferenceEquals(i, small)));
        Assert.Equal(2, smallBin.Items.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Finite_stock_and_oversized_or_unfulfilled_leftovers_remain_exactly_unchanged(bool nonMonotone)
    {
        var (initial, baseline) = Plan(10, 0, [6, 2], [7, 1], [3, 1]);
        var oversized = new BinItem("part", 11);
        var unfulfilled = new BinItem("part", 5);
        baseline.AddItemsNotUsed([oversized, unfulfilled, unfulfilled]);
        baseline.FallbackEngine = BuiltInPackingEngines.FirstFit;
        var request = new PackingRequest(initial.Items.Concat(baseline.ItemsNotUsed).ToList(), 10, maxBinCount: 3);
        var engine = new LastBarEliminationEngine(10_000, new FixedEngine(baseline), nonMonotone);

        var result = engine.Pack(request);

        Assert.Equal(2, result.Bins.Count);
        AssertContract(request, baseline, result);
        Assert.Equal(3, result.ItemsNotUsed.Count);
        for (var i = 0; i < 3; i++) Assert.Same(baseline.ItemsNotUsed[i], result.ItemsNotUsed[i]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invented")]
    [InlineData("duplicate")]
    [InlineData("overfilled")]
    [InlineData("wrong-stock")]
    [InlineData("wrong-kerf")]
    [InlineData("finite-stock")]
    [InlineData("empty-bar")]
    [InlineData("missing", true)]
    [InlineData("invented", true)]
    [InlineData("duplicate", true)]
    [InlineData("overfilled", true)]
    [InlineData("wrong-stock", true)]
    [InlineData("wrong-kerf", true)]
    [InlineData("finite-stock", true)]
    [InlineData("empty-bar", true)]
    public void Invalid_injected_baselines_fail_before_search_even_when_budget_is_zero(string fault, bool nonMonotone = false)
    {
        var (original, baseline) = Plan(10, 0, [6], [4]);
        var items = original.Items.ToList();
        var request = original;
        switch (fault)
        {
            case "missing": items.Add(new BinItem("missing", 1)); break;
            case "invented": baseline.Bins[0].AddItem(new BinItem("invented", 1)); break;
            case "duplicate": baseline.Bins[0].AddItem(items[0]); break;
            case "overfilled":
                var extra = new BinItem("extra", 5);
                items.Add(extra);
                baseline.Bins[0].AddItem(extra);
                break;
            case "wrong-stock": baseline.Bins[0].Length = 11; break;
            case "wrong-kerf": baseline.Bins[0].Spacing = 0.125; break;
            case "finite-stock": request = new PackingRequest(items, 10, maxBinCount: 1); break;
            case "empty-bar": baseline.AddBin(new Bin(10)); break;
        }
        if (fault != "finite-stock") request = new PackingRequest(items, 10);
        var before = Snapshot(baseline);
        var engine = new LastBarEliminationEngine(0, new FixedEngine(baseline), nonMonotone);

        Assert.Throws<InvalidOperationException>(() => engine.Pack(request));

        Assert.Equal(0, engine.LastAttempts);
        Assert.Equal(0, engine.LastBudgetUsed);
        Assert.Equal(0, engine.LastEliminatedBars);
        AssertSnapshot(before, baseline);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Repeated_calls_start_fresh_and_then_clear_diagnostics_for_an_empty_job(bool nonMonotone)
    {
        var (request, baseline) = Plan(10, 0, [6], [1], [1], [1]);
        var engine = new LastBarEliminationEngine(4,
            new DelegateEngine(r => r.Items.Count == 0 ? new PackResult() : baseline), nonMonotone);
        for (var run = 0; run < 2; run++)
        {
            var result = engine.Pack(request);
            Assert.Equal(3, result.Bins.Count);
            Assert.Equal(1, engine.LastEliminatedBars);
            Assert.Equal(1, engine.LastAttempts);
            Assert.Equal(5, engine.LastBudgetUsed);
            Assert.True(engine.LastBudgetExhausted);
            Assert.False(engine.LastDepthLimitReached);
        }

        var empty = engine.Pack(new PackingRequest([], 10));

        Assert.Empty(empty.Bins);
        Assert.Equal(0, engine.LastBudgetUsed);
        Assert.Equal(0, engine.LastAttempts);
        Assert.Equal(0, engine.LastEliminatedBars);
        Assert.False(engine.LastBudgetExhausted);
        Assert.False(engine.LastDepthLimitReached);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Seeded_small_jobs_satisfy_conservation_fit_and_nonregression_against_brute_force_lower_limit(bool nonMonotone)
    {
        var random = new Random(4181);
        for (var job = 0; job < 120; job++)
        {
            double kerf = job % 2 == 0 ? 0 : 0.125;
            var items = Enumerable.Range(0, random.Next(1, 9))
                .Select(i => new BinItem($"P{i}", random.Next(1, 45) / 4.0)).ToList();
            int stockLimit = (job % 4) switch { 0 => 0, 1 => 1, 2 => 3, _ => -1 };
            var request = new PackingRequest(items, 10, kerf, stockLimit);
            var baseline = new FirstFitEngine().Pack(request);
            var before = Snapshot(baseline);
            long budget = (job % 3) switch { 0 => 0, 1 => 1, _ => 10_000 };
            var engine = new LastBarEliminationEngine(budget, new FixedEngine(baseline), nonMonotone);

            var result = engine.Pack(request);

            AssertContract(request, baseline, result);
            AssertSnapshot(before, baseline);
            Assert.Equal(baseline.Bins.Count - result.Bins.Count, engine.LastEliminatedBars);
            Assert.InRange(engine.LastBudgetUsed, 0, budget + 1);
            // An independent exhaustive assignment oracle on the PLACED subset proves that
            // reported bar savings are feasible, not that this restricted heuristic is exact.
            Assert.True(result.Bins.Count >= FewestBars(result.Bins.SelectMany(b => b.Items)
                .Select(i => CutFit.Size(i.Length, kerf)).ToArray(), CutFit.Capacity(10, kerf)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Frozen_real_FirstFit_job_saves_a_bar_end_to_end_including_Exhaustive_fallback(bool exhaustiveBaseline)
    {
        // First improvement in a bounded 2,000-job scan: seed 835, job 892. Frozen, not scanned
        // during normal tests. The final 2.25 cannot be placed without resident rearrangement.
        double[] lengths = [7.5, 3, 0.5, 1.5, 2.25, 1, 7.25, 0.5, 8.75, 7.5, 3.75, 1, 6.25, 2.5, 4.5, 1.5, 0.5];
        var items = lengths.Select((n, i) => new BinItem($"P{i}", n)).ToList();
        var request = new PackingRequest(items, 10);
        IPackingEngine underlying = exhaustiveBaseline ? new ExhaustiveSearchEngine(1) : new FirstFitEngine();
        var baseline = RunThroughStockOrchestration(underlying);
        var before = Snapshot(baseline);
        var engine = exhaustiveBaseline
            ? new LastBarEliminationEngine(10_000, new ExhaustiveSearchEngine(1))
            : new LastBarEliminationEngine(10_000); // default, real production FirstFit

        var result = RunThroughStockOrchestration(engine);

        Assert.Equal(7, baseline.Bins.Count);
        Assert.Equal(6, result.Bins.Count);
        Assert.Equal(1, engine.LastEliminatedBars);
        Assert.Equal(2, engine.LastAttempts); // second attempt fails the total-capacity test
        Assert.Equal(111, engine.LastBudgetUsed);
        Assert.False(engine.LastBudgetExhausted);
        Assert.False(engine.LastDepthLimitReached);
        Assert.Empty(result.ItemsNotUsed);
        AssertContract(request, baseline, result);
        AssertSnapshot(before, baseline);
        Assert.Equal(lengths, items.Select(i => i.Length));
        Assert.Equal(items.Select((_, i) => $"P{i}"), items.Select(i => i.Name));
        if (exhaustiveBaseline) Assert.Same(BuiltInPackingEngines.FirstFit, result.FallbackEngine);
        var metrics = (engine.LastBudgetUsed, engine.LastBudgetExhausted, engine.LastAttempts,
            engine.LastEliminatedBars, engine.LastDepthLimitReached);
        var repeated = RunThroughStockOrchestration(engine);
        AssertSnapshot(Snapshot(result), repeated);
        Assert.Equal(metrics, (engine.LastBudgetUsed, engine.LastBudgetExhausted, engine.LastAttempts,
            engine.LastEliminatedBars, engine.LastDepthLimitReached));
        output.WriteLine($"stock=10, kerf=0: {baseline.Bins.Count}->{result.Bins.Count} bars; work={engine.LastBudgetUsed}");
        output.WriteLine("baseline: " + string.Join("; ", baseline.Bins.Select(b => "[" + string.Join(",", b.Items.Select(i => i.Length)) + "]")));
        output.WriteLine("improved: " + string.Join("; ", result.Bins.Select(b => "[" + string.Join(",", b.Items.Select(i => i.Length)) + "]")));

        PackResult RunThroughStockOrchestration(IPackingEngine packingEngine)
        {
            var packer = new MultiBinPacker(packingEngine) { Spacing = 0 };
            packer.SetBins([new MultiBin(10, -1, 1)]);
            return packer.Pack(items);
        }
    }

    [Fact]
    public void Frozen_non_monotone_single_ejection_saves_a_bar_that_strict_growth_cannot()
    {
        // Frozen synthetic valid plan from a bounded seed-7331 scan, not scanned by the test.
        // One successful branch: 3 ejects 2; 2 ejects 6 (non-monotone); 6 ejects 5; 5 fits.
        var (request, baseline) = Plan(10, 0, [6, 2], [5, 4], [3]);
        var before = Snapshot(baseline);
        var strict = new LastBarEliminationEngine(10_000, new FixedEngine(baseline));
        var broad = new LastBarEliminationEngine(10_000, new FixedEngine(baseline), true);

        var unchanged = strict.Pack(request);
        var improved = broad.Pack(request);

        Assert.Same(baseline, unchanged);
        Assert.Equal(27, strict.LastBudgetUsed);
        Assert.Equal(0, strict.LastEliminatedBars);
        Assert.Equal(2, improved.Bins.Count);
        Assert.Equal(268, broad.LastBudgetUsed); // search 267 + charged impossible next attempt
        Assert.Equal(1, broad.LastEliminatedBars);
        Assert.Equal(2, broad.LastAttempts);
        Assert.False(strict.LastBudgetExhausted);
        Assert.False(broad.LastBudgetExhausted);
        Assert.False(broad.LastDepthLimitReached);
        AssertContract(request, baseline, unchanged);
        AssertContract(request, baseline, improved);
        AssertSnapshot(before, baseline);
        AssertSnapshot(Snapshot(improved), broad.Pack(request)); // no branch locks leak between calls
        Assert.Equal(268, broad.LastBudgetUsed);
        output.WriteLine($"Frozen stock=10 kerf=0 plan [6,2];[5,4];[3]: strict 3->3 work={strict.LastBudgetUsed}; non-monotone 3->2 work={broad.LastBudgetUsed}");
        output.WriteLine("non-monotone: " + string.Join(";", improved.Bins.Select(b => "[" + string.Join(",", b.Items.Select(i => i.Length)) + "]")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Equal_size_single_and_pair_ejections_preserve_distinct_and_repeated_references(bool sameInstance)
    {
        // Only the broader mode empties the 5 bar: 5 ejects 6; 6 ejects the OTHER 6;
        // that 6 ejects the pair 3+3, then both threes fit. Equal-valued sixes must not
        // share a lock. A repeated three reference must retain both occurrences.
        var (request, baseline) = NonMonotonePairPlan(sameInstance);
        var before = Snapshot(baseline);
        var strict = new LastBarEliminationEngine(10_000, new FixedEngine(baseline));
        var broad = new LastBarEliminationEngine(10_000, new FixedEngine(baseline), true);

        Assert.Same(baseline, strict.Pack(request));
        var result = broad.Pack(request);

        Assert.Equal(23, strict.LastBudgetUsed);
        Assert.Equal(3, result.Bins.Count);
        Assert.Equal(104, broad.LastBudgetUsed);
        Assert.Equal(1, broad.LastEliminatedBars);
        Assert.False(broad.LastBudgetExhausted);
        Assert.False(broad.LastDepthLimitReached);
        AssertContract(request, baseline, result);
        AssertSnapshot(before, baseline);
        AssertSnapshot(Snapshot(result), broad.Pack(request));
        Assert.Equal(104, broad.LastBudgetUsed);
        output.WriteLine($"Frozen [1,6];[5];[6,2];[3,3,4], repeated3={sameInstance}: strict 4->4 work=23; non-monotone 4->3 work={broad.LastBudgetUsed}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_interrupted_non_monotone_branch_rolls_back_and_late_budget_keeps_the_accepted_bar(bool sameInstance)
    {
        var (request, baseline) = NonMonotonePairPlan(sameInstance);
        var before = Snapshot(baseline);
        // Interrupt at EVERY charge before completion, including inside both non-monotone
        // single/pair ejections and after placing a duplicate reference. No partial bar commits.
        for (long budget = 0; budget < 103; budget++)
        {
            var engine = new LastBarEliminationEngine(budget, new FixedEngine(baseline), true);
            for (var run = 0; run < 2; run++)
            {
                Assert.Same(baseline, engine.Pack(request));
                Assert.Equal(budget + 1, engine.LastBudgetUsed);
                Assert.True(engine.LastBudgetExhausted);
                Assert.Equal(0, engine.LastEliminatedBars);
                AssertSnapshot(before, baseline);
            }
        }
        var late = new LastBarEliminationEngine(103, new FixedEngine(baseline), true);
        var result = late.Pack(request);

        Assert.Equal(3, result.Bins.Count);
        Assert.Equal(1, late.LastEliminatedBars);
        Assert.Equal(1, late.LastAttempts); // next startup was refused by the SAME budget
        Assert.Equal(104, late.LastBudgetUsed);
        Assert.True(late.LastBudgetExhausted);
        AssertContract(request, baseline, result);
        AssertSnapshot(before, baseline);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(140)]
    [InlineData(10_000)]
    public void Non_monotone_cycles_are_blocked_before_the_depth_limit_and_blocked_candidates_are_charged(long budget)
    {
        // Total capacity permits two bars but the 6,6,5 cannot share. Without branch locks,
        // equal/larger swaps keep revisiting the source and moved residents until depth/budget.
        var (request, baseline) = Plan(10, 0, [6], [6], [5, 3]);
        var before = Snapshot(baseline);
        var engine = new LastBarEliminationEngine(budget, new FixedEngine(baseline), true);

        Assert.Same(baseline, engine.Pack(request));

        Assert.Equal(Math.Min(budget + 1, 141), engine.LastBudgetUsed);
        Assert.Equal(budget < 141, engine.LastBudgetExhausted);
        Assert.False(engine.LastDepthLimitReached);
        Assert.Equal(0, engine.LastEliminatedBars);
        AssertSnapshot(before, baseline);
        AssertContract(request, baseline, baseline);
    }

    private static (PackingRequest Request, PackResult Baseline) NonMonotonePairPlan(bool sameInstance)
    {
        var (request, baseline) = Plan(10, 0, [1, 6], [5], [6, 2], [3, 3, 4]);
        if (!sameInstance) return (request, baseline);
        var repeated = baseline.Bins[3].Items[0];
        var last = new Bin(10);
        last.AddItems([repeated, repeated, baseline.Bins[3].Items[2]]);
        var bins = baseline.Bins.Take(3).Append(last).ToList();
        return (new PackingRequest(bins.SelectMany(b => b.Items).ToList(), 10), new PackResult(bins, []));
    }

    private static int FewestBars(long[] sizes, long capacity)
    {
        var sorted = sizes.OrderByDescending(n => n).ToArray();
        int best = sizes.Length;
        var bins = new List<long>();
        Search(0);
        return best;

        void Search(int next)
        {
            if (bins.Count >= best) return;
            if (next == sorted.Length) { best = bins.Count; return; }
            for (var b = 0; b < bins.Count; b++)
            {
                if (bins[b] + sorted[next] > capacity) continue;
                bins[b] += sorted[next];
                Search(next + 1);
                bins[b] -= sorted[next];
            }
            bins.Add(sorted[next]);
            Search(next + 1);
            bins.RemoveAt(bins.Count - 1);
        }
    }

    private sealed class DelegateEngine(Func<PackingRequest, PackResult> pack) : IPackingEngine
    {
        public PackResult Pack(PackingRequest request) => pack(request);
    }

    private sealed class FixedEngine(PackResult result) : IPackingEngine
    {
        public PackResult Pack(PackingRequest request) => result;
    }

    private static (PackingRequest Request, PackResult Baseline) Plan(
        double stock, double kerf, params double[][] groups)
    {
        var bins = groups.Select(g =>
        {
            var bin = new Bin(stock) { Spacing = kerf };
            bin.AddItems(g.Select(n => new BinItem("part", n)));
            return bin;
        }).ToList();
        return (new PackingRequest(bins.SelectMany(b => b.Items).ToList(), stock, kerf),
            new PackResult(bins, []));
    }

    private static List<BinItem> Returned(PackResult result) =>
        result.Bins.SelectMany(b => b.Items).Concat(result.ItemsNotUsed).ToList();

    private static bool SameReferences(IEnumerable<BinItem> a, IEnumerable<BinItem> b)
    {
        var counts = new Dictionary<BinItem, int>(ReferenceEqualityComparer.Instance);
        foreach (var item in a) counts[item] = counts.GetValueOrDefault(item) + 1;
        foreach (var item in b)
        {
            if (!counts.TryGetValue(item, out var count) || count == 0) return false;
            counts[item] = count - 1;
        }
        return counts.Values.All(n => n == 0);
    }

    private static void AssertContract(PackingRequest request, PackResult baseline, PackResult result)
    {
        Assert.True(SameReferences(request.Items, Returned(result)));
        Assert.True(SameReferences(baseline.ItemsNotUsed, result.ItemsNotUsed));
        Assert.True(result.Bins.Count <= baseline.Bins.Count);
        Assert.True(result.Bins.Count <= request.MaxBinCount);
        Assert.Same(baseline.FallbackEngine, result.FallbackEngine);
        Assert.All(result.Bins, bin =>
        {
            Assert.NotEmpty(bin.Items);
            Assert.Equal(request.StockLength, bin.Length);
            Assert.Equal(request.Spacing, bin.Spacing);
            Assert.True(CutFit.Fits(bin.Items.Select(i => i.Length), bin.Length, bin.Spacing));
        });
    }

    private static BinItem[][] Snapshot(PackResult result) =>
        result.Bins.Select(b => b.Items.ToArray()).ToArray();

    private static void AssertSnapshot(BinItem[][] before, PackResult result)
    {
        Assert.Equal(before.Length, result.Bins.Count);
        for (var b = 0; b < before.Length; b++)
        {
            Assert.Equal(before[b].Length, result.Bins[b].Items.Count);
            for (var i = 0; i < before[b].Length; i++) Assert.Same(before[b][i], result.Bins[b].Items[i]);
        }
    }
}
