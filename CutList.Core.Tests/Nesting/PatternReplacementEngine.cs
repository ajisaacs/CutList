using CutList.Core.Nesting;
using CutList.Core.Nesting.Pipeline;
using CutList.Core.Nesting.Search;

namespace CutList.Core.Tests.Nesting;

/// <summary>
/// Evaluation only: First Fit's greedy pipeline plus bounded single-item replacement searches.
/// Not registered in the production engine catalog. A single work budget covers the entire Pack.
/// </summary>
internal sealed class PatternReplacementEngine : IPackingEngine
{
    private readonly long _searchBudget;

    public PatternReplacementEngine(long searchBudget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(searchBudget);
        _searchBudget = searchBudget;
    }

    public long LastBudgetUsed { get; private set; }
    public bool LastBudgetExhausted { get; private set; }
    public bool LastGuardRejected { get; private set; }
    public PackResult? LastUnguardedResult { get; private set; }

    public PackResult Pack(PackingRequest request)
    {
        LastBudgetUsed = 0;
        LastBudgetExhausted = false;
        LastGuardRejected = false;
        LastUnguardedResult = null;
        var budget = new SearchBudget(_searchBudget);
        var baseline = new FirstFitEngine().Pack(request);
        var context = new PackingContext(request);
        new FilterOversizedItemsStep().Execute(context);
        new SortItemsDescendingStep().Execute(context);

        while (context.RemainingItems.Count > 0 && context.CanAddMoreBins())
        {
            var bin = context.CreateBin();
            FillBin(bin, context.RemainingItems);
            SwapInLeftoversStep.ImproveBin(bin, context.RemainingItems, context.Spacing);
            ImproveBin(bin, context.RemainingItems, budget);
            context.Bins.Add(bin);
        }

        new SwapInLeftoversStep().Execute(context);
        foreach (var bin in context.Bins)
        {
            if (budget.Exhausted) break;
            ImproveBin(bin, context.RemainingItems, budget);
        }
        new SortBinItemsStep().Execute(context);
        new SortBinsByUtilizationStep().Execute(context);
        var candidate = context.ToResult();
        LastUnguardedResult = candidate;
        LastBudgetUsed = budget.Used;
        LastBudgetExhausted = budget.Exhausted;

        LastGuardRejected = candidate.Bins.Count > baseline.Bins.Count ||
            candidate.ItemsNotUsed.Count > baseline.ItemsNotUsed.Count ||
            candidate.ItemsNotUsed.Sum(i => CutFit.Units(i.Length)) >
                baseline.ItemsNotUsed.Sum(i => CutFit.Units(i.Length));
        return LastGuardRejected ? baseline : candidate; // Keep the candidate on ties.
    }

    // Same exact-unit fill loop as FirstFitDecreasingStep; no artificial residual-length bar.
    private static void FillBin(Bin bin, List<BinItem> remaining)
    {
        long capacity = CutFit.Capacity(bin.Length, bin.Spacing);
        long used = 0;
        for (int i = 0; i < remaining.Count; i++)
        {
            long size = CutFit.Size(remaining[i].Length, bin.Spacing);
            if (used + size > capacity) continue;
            bin.AddItem(remaining[i]);
            used += size;
            remaining.RemoveAt(i--);
        }
    }

    /// <summary>
    /// Retains the entire longest length group, replacing one other part at a time. Commits only
    /// strict part-length gains from completed searches; an exhausted/non-improving attempt leaves
    /// both collections untouched. All capacity accounting uses the original stock's CutFit units.
    /// </summary>
    internal static void ImproveBin(Bin bin, List<BinItem> remaining, SearchBudget budget)
    {
        while (!budget.Exhausted && remaining.Count > 0)
        {
            var replaceable = bin.Items.GroupBy(i => i.Length).OrderByDescending(g => g.Key)
                .Skip(1).Select(g => g.First()).ToList();
            bool improved = false;
            foreach (var displaced in replaceable)
            {
                if (budget.Exhausted) return;
                var retained = bin.Items.Where(i => !ReferenceEquals(i, displaced)).ToList();
                long reserved = retained.Sum(i => CutFit.Size(i.Length, bin.Spacing));
                if (reserved >= CutFit.Capacity(bin.Length, bin.Spacing)) continue;
                var demand = CutDemand.From(remaining, bin.Length, bin.Spacing, reserved);
                if (demand.GroupCount > ExhaustiveSearchEngine.MaxDistinctLengths) return;

                var search = new MaxFillSearch(demand, budget, maxDepth: 1);
                var patterns = search.Solve(bars: 1, incumbentLength: CutFit.Units(displaced.Length));
                if (budget.Exhausted) return;
                if (!search.Completed || patterns == null || patterns.Count != 1) continue;

                // Materialize counts directly to actual references, not CutDemand.Build's full bars.
                var replacement = new List<BinItem>();
                var pattern = patterns[0];
                for (int group = 0; group < pattern.Length; group++)
                    replacement.AddRange(demand.Items[group].Take(pattern[group]));
                if (replacement.Sum(i => CutFit.Units(i.Length)) <= CutFit.Units(displaced.Length)) continue;
                var proposed = retained.Concat(replacement).ToList();
                if (!CutFit.Fits(proposed.Select(i => i.Length), bin.Length, bin.Spacing)) continue;

                // Bin.RemoveItem uses value equality, which can remove a near-equal anchored part.
                // Clear the old contents before re-adding exactly the retained/replacement instances.
                foreach (var old in bin.Items.ToArray()) bin.RemoveItem(old);
                bin.AddItems(proposed);
                foreach (var item in replacement)
                    remaining.RemoveAt(remaining.FindIndex(i => ReferenceEquals(i, item)));
                remaining.Add(displaced);
                improved = true;
                break; // Recompute groups after every strict gain.
            }
            if (!improved) return;
        }
    }
}
