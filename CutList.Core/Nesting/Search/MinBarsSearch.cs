namespace CutList.Core.Nesting.Search
{
    /// <summary>
    /// Fewest bars for every part: bin completion over maximal cut patterns (each bar takes the longest
    /// remaining part), tightening the bar count down from an upper bound, with a memo of
    /// (remaining parts, bars left) states proven impossible.
    /// </summary>
    internal sealed class MinBarsSearch
    {
        private const int MaxMemo = 1_000_000;
        private readonly CutDemand _d;
        private readonly SearchBudget _budget;
        private readonly HashSet<string> _failed = new();

        public MinBarsSearch(CutDemand d, SearchBudget budget)
        {
            _d = d;
            _budget = budget;
        }

        /// <summary>
        /// Patterns for a packing of every part on fewer than <paramref name="upperBound"/> bars (and at
        /// most <paramref name="maxBars"/>), using as few bars as the budget allows to find; null when no
        /// such packing exists or the budget ran out before one was found (see SearchBudget.Exhausted).
        /// </summary>
        public List<int[]>? Solve(int upperBound, int maxBars)
        {
            // Tighten from above: each success is a better plan at once, so a budget that runs out still
            // returns the best plan found.
            List<int[]>? best = null;
            int lower = _d.LowerBound(_d.Counts);
            int bars = (int)Math.Min((long)upperBound - 1, maxBars);
            try
            {
                while (bars >= lower)
                {
                    var stack = new List<int[]>();
                    if (!Fill((int[])_d.Counts.Clone(), bars, stack)) break;
                    best = stack;
                    bars = stack.Count - 1;
                }
            }
            catch (SearchBudgetExceededException)
            {
            }
            return best;
        }

        private bool Fill(int[] remaining, int barsLeft, List<int[]> stack)
        {
            int largest = Array.FindIndex(remaining, c => c > 0);
            if (largest < 0) return true;
            if (barsLeft == 0 || _d.LowerBound(remaining) > barsLeft) return false;

            var key = string.Join(',', remaining) + "|" + barsLeft;
            if (_failed.Contains(key)) return false;

            foreach (var (pattern, _) in CutPatterns.Maximal(_d, remaining, largest, double.MaxValue, _budget))
            {
                _budget.Charge();
                for (int i = 0; i < pattern.Length; i++) remaining[i] -= pattern[i];
                stack.Add(pattern);
                if (Fill(remaining, barsLeft - 1, stack)) return true;
                stack.RemoveAt(stack.Count - 1);
                for (int i = 0; i < pattern.Length; i++) remaining[i] += pattern[i];
            }

            if (_failed.Count < MaxMemo) _failed.Add(key);
            return false;
        }
    }
}
