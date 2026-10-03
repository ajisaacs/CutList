namespace CutList.Core.Nesting.Search
{
    internal static class CutPatterns
    {
        /// <summary>
        /// Every maximal pattern (parts per group on one bar) drawn from <paramref name="remaining"/>:
        /// no remaining part could still be added. With <paramref name="mustInclude"/> &gt;= 0 the pattern
        /// holds at least one part of that group. Parts fit within <see cref="CutDemand.Capacity"/> and
        /// lengths are in CutFit units. Sorted by placed length, longest first.
        /// </summary>
        public static List<(int[] Pattern, long Length)> Maximal(
            CutDemand d, int[] remaining, int mustInclude, long maxLength, SearchBudget budget)
        {
            var found = new List<(int[] Pattern, long Length)>();
            var current = new int[d.GroupCount];
            Recurse(0, d.Capacity, 0);
            found.Sort((a, b) => b.Length.CompareTo(a.Length));
            return found;

            void Recurse(int group, long room, long length)
            {
                budget.Charge(); // every step counts, including ones that reach no pattern
                if (group == d.GroupCount)
                {
                    if (length <= 0 || length > maxLength) return;
                    for (int i = 0; i < d.GroupCount; i++)
                        if (remaining[i] > current[i] && d.Sizes[i] <= room)
                            return; // not maximal
                    found.Add(((int[])current.Clone(), length));
                    return;
                }
                int most = (int)Math.Min(remaining[group], room / d.Sizes[group]);
                int least = group == mustInclude ? 1 : 0;
                for (int n = most; n >= least; n--)
                {
                    current[group] = n;
                    Recurse(group + 1, room - n * d.Sizes[group], length + n * d.LengthUnits[group]);
                }
                current[group] = 0;
            }
        }
    }
}
