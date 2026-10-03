namespace CutList.Core.Nesting.Search
{
    internal static class CutPatterns
    {
        /// <summary>
        /// Every maximal pattern (parts per group on one bar) drawn from <paramref name="remaining"/>:
        /// no remaining part could still be added. With <paramref name="mustInclude"/> &gt;= 0 the pattern
        /// holds at least one part of that group. Parts fit within <see cref="CutDemand.FitCapacity"/>.
        /// Sorted by placed length, longest first.
        /// </summary>
        public static List<(int[] Pattern, double Length)> Maximal(
            CutDemand d, int[] remaining, int mustInclude, double maxLength, SearchBudget budget)
        {
            var found = new List<(int[] Pattern, double Length)>();
            var current = new int[d.GroupCount];
            Recurse(0, d.FitCapacity, 0);
            found.Sort((a, b) => b.Length.CompareTo(a.Length));
            return found;

            void Recurse(int group, double room, double length)
            {
                if (group == d.GroupCount)
                {
                    budget.Charge();
                    if (length <= 0 || length > maxLength + CutDemand.Eps) return;
                    for (int i = 0; i < d.GroupCount; i++)
                        if (remaining[i] > current[i] && d.Sizes[i] <= room)
                            return; // not maximal
                    found.Add(((int[])current.Clone(), length));
                    return;
                }
                int most = Math.Min(remaining[group], (int)Math.Floor(room / d.Sizes[group]));
                int least = group == mustInclude ? 1 : 0;
                for (int n = most; n >= least; n--)
                {
                    current[group] = n;
                    Recurse(group + 1, room - n * d.Sizes[group], length + n * d.Lengths[group]);
                }
                current[group] = 0;
            }
        }
    }
}
