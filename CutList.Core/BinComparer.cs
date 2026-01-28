using System;
using System.Collections.Generic;
using System.Linq;

namespace CutList.Core
{
    /// <summary>
    /// Compares bins to determine if they are identical (same items in same order).
    /// </summary>
    public class BinComparer : IEqualityComparer<Bin>
    {
        public bool Equals(Bin x, Bin y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;

            // Check basic properties
            if (Math.Abs(x.Length - y.Length) > 0.0001) return false;
            if (Math.Abs(x.Spacing - y.Spacing) > 0.0001) return false;

            // Check item count
            if (x.Items.Count != y.Items.Count) return false;

            // Check each item in order
            for (int i = 0; i < x.Items.Count; i++)
            {
                var itemX = x.Items[i];
                var itemY = y.Items[i];

                if (itemX.Name != itemY.Name) return false;
                if (Math.Abs(itemX.Length - itemY.Length) > 0.0001) return false;
            }

            return true;
        }

        public int GetHashCode(Bin bin)
        {
            if (bin == null) return 0;

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + bin.Length.GetHashCode();
                hash = hash * 23 + bin.Spacing.GetHashCode();
                hash = hash * 23 + bin.Items.Count.GetHashCode();

                // Include first and last item in hash for better distribution
                if (bin.Items.Count > 0)
                {
                    var firstItem = bin.Items[0];
                    hash = hash * 23 + (firstItem.Name?.GetHashCode() ?? 0);
                    hash = hash * 23 + firstItem.Length.GetHashCode();

                    if (bin.Items.Count > 1)
                    {
                        var lastItem = bin.Items[bin.Items.Count - 1];
                        hash = hash * 23 + (lastItem.Name?.GetHashCode() ?? 0);
                        hash = hash * 23 + lastItem.Length.GetHashCode();
                    }
                }

                return hash;
            }
        }
    }

    /// <summary>
    /// Helper methods for grouping bins.
    /// </summary>
    public static class BinGroupingHelper
    {
        /// <summary>
        /// Groups identical bins together and returns a list of BinGroup objects.
        /// </summary>
        public static List<BinGroup> GroupIdenticalBins(List<Bin> bins)
        {
            if (bins == null || bins.Count == 0)
                return new List<BinGroup>();

            var comparer = new BinComparer();
            var groups = new List<BinGroup>();
            var processed = new HashSet<int>();

            for (int i = 0; i < bins.Count; i++)
            {
                if (processed.Contains(i))
                    continue;

                var currentBin = bins[i];
                int count = 1;

                // Find all identical bins
                for (int j = i + 1; j < bins.Count; j++)
                {
                    if (processed.Contains(j))
                        continue;

                    if (comparer.Equals(currentBin, bins[j]))
                    {
                        count++;
                        processed.Add(j);
                    }
                }

                processed.Add(i);
                groups.Add(new BinGroup(currentBin, count));
            }

            return groups;
        }
    }
}
