namespace CutList.Core.Nesting.Search
{
    /// <summary>
    /// Most part length on a fixed number of identical bars that cannot hold every part. Branch and
    /// bound over maximal patterns; bars are filled in non-increasing order of placed length (any
    /// optimum can be rearranged that way), which also bounds every later bar. Lengths are CutFit units.
    /// </summary>
    internal sealed class MaxFillSearch
    {
        private readonly CutDemand _d;
        private readonly SearchBudget _budget;
        private readonly int _maxDepth;
        private long _best;
        private List<int[]>? _bestPatterns;

        /// <param name="maxDepth">Most bars to search over; the search recurses once per bar.</param>
        public MaxFillSearch(CutDemand d, SearchBudget budget, int maxDepth = int.MaxValue)
        {
            _d = d;
            _budget = budget;
            _maxDepth = maxDepth;
        }

        /// <summary>True when the search finished, so its result (or the incumbent) is optimal.</summary>
        public bool Completed { get; private set; }

        /// <summary>Patterns placing more length (units) than <paramref name="incumbentLength"/>, or null.</summary>
        public List<int[]>? Solve(int bars, long incumbentLength)
        {
            if (bars > _maxDepth)
                return null; // too many bars to recurse over safely; Completed stays false
            _best = incumbentLength;
            try
            {
                Search((int[])_d.Counts.Clone(), bars, 0, long.MaxValue, new List<int[]>());
                Completed = true;
            }
            catch (SearchBudgetExceededException)
            {
            }
            return _bestPatterns;
        }

        private void Search(int[] remaining, int barsLeft, long placed, long previousFill, List<int[]> stack)
        {
            _budget.Charge();
            if (placed > _best)
            {
                _best = placed;
                _bestPatterns = stack.Select(p => (int[])p.Clone()).ToList();
            }
            if (barsLeft == 0 || remaining.All(c => c == 0)) return;

            long remainingLength = 0;
            for (int i = 0; i < remaining.Length; i++) remainingLength += remaining[i] * _d.LengthUnits[i];
            if (placed + remainingLength <= _best) return;

            foreach (var (pattern, length) in CutPatterns.Maximal(_d, remaining, -1, previousFill, _budget))
            {
                // Sorted longest first, and later bars hold no more than this one.
                if (placed + barsLeft * length <= _best) return;
                for (int i = 0; i < pattern.Length; i++) remaining[i] -= pattern[i];
                stack.Add(pattern);
                Search(remaining, barsLeft - 1, placed + length, length, stack);
                stack.RemoveAt(stack.Count - 1);
                for (int i = 0; i < pattern.Length; i++) remaining[i] += pattern[i];
            }
        }
    }
}
