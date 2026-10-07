using CutList.Core.Nesting;
using CutList.Core.Nesting.Search;

namespace CutList.Core.Tests.Nesting;

/// <summary>
/// Test-only restricted heuristic, not an exact search or a proof of optimality. Tries to empty
/// the least-used bar, largest pending part first, with direct moves or one/two-part ejections.
/// By default ejections strictly increase the receiving bar's integer occupancy. The optional
/// non-monotone mode also permits equal/larger ejections, but never ejects a reference already
/// placed or moved in the current branch (including direct placements and original source parts).
/// Each original resident can move at most once; repeated identical references are conservatively
/// locked together without changing their multiplicity. Locks are branch-local, not job-global.
/// A single work budget covers all attempts; at most 64 moves are allowed in any search branch.
/// No catalog registration, production changes, or new global-proof/fallback claims.
/// </summary>
internal sealed class LastBarEliminationEngine(long searchBudget, IPackingEngine? baselineEngine = null,
    bool allowNonMonotoneEjections = false) : IPackingEngine
{
    private const int MaxDepth = 64;
    private readonly IPackingEngine _baselineEngine = baselineEngine ?? new FirstFitEngine();

    // SearchBudget counts the first refused charge as Used == limit + 1, like production search.
    public long LastBudgetUsed { get; private set; }
    public bool LastBudgetExhausted { get; private set; }
    public int LastAttempts { get; private set; }
    public int LastEliminatedBars { get; private set; }
    public bool LastDepthLimitReached { get; private set; }

    public PackResult Pack(PackingRequest request)
    {
        LastBudgetUsed = 0;
        LastBudgetExhausted = false;
        LastAttempts = 0;
        LastEliminatedBars = 0;
        LastDepthLimitReached = false;
        var budget = new SearchBudget(searchBudget); // exactly one counter per whole job
        var current = _baselineEngine.Pack(request);
        Validate(request, current, "baseline"); // even with zero budget; never trust value equality
        try
        {
            while (current.Bins.Count > 1)
            {
                budget.Charge(); // startup, including impossible-capacity attempts
                LastAttempts++;
                // Only PRIVATE lists are edited. Each attempt is transactional; a failed/exhausted
                // attempt is discarded, leaving baseline bins and previously accepted results intact.
                var trial = current.Bins.Select(b => new TrialBin(b.Items, request.Spacing))
                    .OrderByDescending(b => b.Used).ToList(); // stable tie: original bin order
                long capacity = CutFit.Capacity(request.StockLength, request.Spacing);
                if (trial.Aggregate((Int128)0, (sum, b) => sum + b.Used) >
                    (Int128)capacity * (trial.Count - 1)) break;
                var pending = trial[^1].Items;
                trial.RemoveAt(trial.Count - 1);
                if (pending.Count > MaxDepth)
                {
                    LastDepthLimitReached = true; // no part-count-sized recursion
                    break;
                }
                var settled = allowNonMonotoneEjections
                    ? new HashSet<BinItem>(ReferenceEqualityComparer.Instance) : null;
                if (!Redistribute(trial, pending, 0, capacity, request.Spacing, budget, settled)) break;

                var bins = trial.OrderByDescending(b => b.Used).Select(b =>
                {
                    var bin = new Bin(request.StockLength) { Spacing = request.Spacing };
                    bin.AddItems(b.Items.OrderByDescending(i => CutFit.Size(i.Length, request.Spacing)));
                    return bin;
                });
                var candidate = new PackResult(bins, current.ItemsNotUsed)
                {
                    FallbackEngine = current.FallbackEngine
                };
                Validate(request, candidate, "candidate");
                if (candidate.Bins.Count != current.Bins.Count - 1 ||
                    !SameReferences(candidate.ItemsNotUsed, current.ItemsNotUsed))
                    throw new InvalidOperationException("Invalid candidate: whole-job guard rejected the trial.");
                current = candidate;
                LastEliminatedBars++;
            }
        }
        catch (SearchBudgetExceededException)
        {
            // Stop ALL new work, retaining earlier completed/validated eliminations.
        }
        finally
        {
            LastBudgetUsed = budget.Used;
            LastBudgetExhausted = budget.Exhausted;
        }
        return current;
    }

    private bool Redistribute(List<TrialBin> bins, List<BinItem> pending, int depth,
        long capacity, double kerf, SearchBudget budget, HashSet<BinItem>? settled)
    {
        budget.Charge(); // every search node, including completed leaves
        if (pending.Count == 0) return true;
        if (depth >= MaxDepth)
        {
            LastDepthLimitReached = true;
            return false;
        }
        int next = 0;
        for (var i = 1; i < pending.Count; i++)
            if (CutFit.Size(pending[i].Length, kerf) > CutFit.Size(pending[next].Length, kerf)) next = i;
        var incoming = pending[next];
        long size = CutFit.Size(incoming.Length, kerf);
        pending.RemoveAt(next); // index operations preserve duplicate references and value-equal parts
        bool completed = false;
        try
        {
            // Exhaust direct-placement possibilities BEFORE considering rearrangements.
            foreach (var bin in bins)
            {
                budget.Charge();
                if (bin.Used <= capacity - size && Move(bin, -1, -1)) return completed = true;
            }
            // Keep strict singles/pairs first, with exactly the original charges when disabled.
            for (var stage = 0; stage < (allowNonMonotoneEjections ? 2 : 1); stage++)
            {
                foreach (var bin in bins)
                {
                    budget.Charge();
                    for (var a = 0; a < bin.Items.Count; a++)
                    {
                        budget.Charge(); // includes candidates blocked by the branch-local lock
                        long removed = CutFit.Size(bin.Items[a].Length, kerf);
                        if ((stage == 0 ? removed < size : removed >= size) &&
                            settled?.Contains(bin.Items[a]) != true &&
                            bin.Used - removed <= capacity - size && Move(bin, a, -1))
                            return completed = true;
                    }
                }
                foreach (var bin in bins)
                {
                    budget.Charge();
                    for (var a = 0; a < bin.Items.Count; a++)
                        for (var b = a + 1; b < bin.Items.Count; b++)
                        {
                            budget.Charge(); // every pair, including locked/rejected pairs
                            long removed = CutFit.Size(bin.Items[a].Length, kerf) + CutFit.Size(bin.Items[b].Length, kerf);
                            if ((stage == 0 ? removed < size : removed >= size) &&
                                settled?.Contains(bin.Items[a]) != true && settled?.Contains(bin.Items[b]) != true &&
                                bin.Used - removed <= capacity - size && Move(bin, a, b))
                                return completed = true;
                        }
                }
            }
            return false;
        }
        finally
        {
            if (!completed) pending.Insert(next, incoming);
        }

        bool Move(TrialBin bin, int first, int second)
        {
            var one = first < 0 ? null : bin.Items[first];
            var two = second < 0 ? null : bin.Items[second];
            long usedBefore = bin.Used;
            int pendingBefore = pending.Count;
            if (second >= 0) bin.Items.RemoveAt(second);
            if (first >= 0) bin.Items.RemoveAt(first);
            bin.Items.Add(incoming);
            if (one != null) pending.Add(one);
            if (two != null) pending.Add(two);
            bin.Used += size - (one == null ? 0 : CutFit.Size(one.Length, kerf)) -
                (two == null ? 0 : CutFit.Size(two.Length, kerf));
            bool incomingAdded = settled?.Add(incoming) == true;
            bool oneAdded = one != null && settled?.Add(one) == true;
            bool twoAdded = two != null && settled?.Add(two) == true;
            bool accepted = false;
            try
            {
                return accepted = Redistribute(bins, pending, depth + 1, capacity, kerf, budget, settled);
            }
            finally
            {
                // A duplicate reference may already belong to an ancestor: only undo OUR adds.
                if (twoAdded) settled!.Remove(two!);
                if (oneAdded) settled!.Remove(one!);
                if (incomingAdded) settled!.Remove(incoming);
                // Nested failure restores its entry state first, so even a thrown budget exception
                // unwinds exactly. Success leaves only a complete private packing to be validated.
                if (!accepted)
                {
                    pending.RemoveRange(pendingBefore, pending.Count - pendingBefore);
                    bin.Items.RemoveAt(bin.Items.Count - 1);
                    if (one != null) bin.Items.Insert(first, one);
                    if (two != null) bin.Items.Insert(second, two);
                    bin.Used = usedBefore;
                }
            }
        }
    }

    private sealed class TrialBin(IEnumerable<BinItem> items, double kerf)
    {
        public List<BinItem> Items { get; } = items.ToList();
        public long Used { get; set; } = items.Sum(i => CutFit.Size(i.Length, kerf));
    }

    private static void Validate(PackingRequest request, PackResult result, string phase)
    {
        if (result.Bins.Count > request.MaxBinCount ||
            result.Bins.Any(b => b.Items.Count == 0 || b.Length != request.StockLength ||
                b.Spacing != request.Spacing || !CutFit.Fits(b.Items.Select(i => i.Length), b.Length, b.Spacing)) ||
            !SameReferences(request.Items, result.Bins.SelectMany(b => b.Items).Concat(result.ItemsNotUsed)))
            throw new InvalidOperationException($"Invalid {phase}: expected exact input reference multiset, fit and stock count.");
    }

    private static bool SameReferences(IEnumerable<BinItem> expected, IEnumerable<BinItem> actual)
    {
        var counts = new Dictionary<BinItem, int>(ReferenceEqualityComparer.Instance);
        foreach (var item in expected) counts[item] = counts.GetValueOrDefault(item) + 1;
        foreach (var item in actual)
        {
            if (!counts.TryGetValue(item, out var count) || count == 0) return false;
            counts[item] = count - 1;
        }
        return counts.Values.All(count => count == 0);
    }
}
