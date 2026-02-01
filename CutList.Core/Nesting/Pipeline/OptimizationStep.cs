namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Attempts to improve bin utilization by swapping items.
    /// For each bin, tries replacing a packed item with unpacked items
    /// to achieve better space utilization.
    /// </summary>
    public class OptimizationStep : IPackingStep
    {
        public void Execute(PackingContext context)
        {
            foreach (var bin in context.Bins)
            {
                while (TryImprovePacking(bin, context.RemainingItems, context.Spacing))
                {
                    // Keep optimizing until no improvement can be made
                }
            }
        }

        private static bool TryImprovePacking(Bin bin, List<BinItem> remainingItems, double spacing)
        {
            if (bin.Items.Count == 0)
                return false;

            if (remainingItems.Count < 2)
                return false;

            var lengthGroups = GroupItemsByLength(bin.Items);
            var shortestLengthItemAvailable = remainingItems.Min(i => i.Length);

            foreach (var group in lengthGroups)
            {
                var minRemainingLength = bin.RemainingLength;
                var firstItem = group.Items.FirstOrDefault();
                if (firstItem == null)
                    continue;

                bin.RemoveItem(firstItem);

                for (int i = 0; i < remainingItems.Count; i++)
                {
                    var item1 = remainingItems[i];

                    if (item1.Length > bin.RemainingLength)
                        continue;

                    var testBin = new Bin(bin.RemainingLength)
                    {
                        Spacing = spacing
                    };
                    testBin.AddItem(item1);

                    for (int j = i + 1; j < remainingItems.Count; j++)
                    {
                        if (testBin.RemainingLength < shortestLengthItemAvailable)
                            break;

                        var item2 = remainingItems[j];

                        if (item2.Length > testBin.RemainingLength)
                            continue;

                        testBin.AddItem(item2);
                    }

                    if (testBin.RemainingLength < minRemainingLength)
                    {
                        // Found improvement: swap the items
                        remainingItems.Add(firstItem);
                        bin.AddItems(testBin.Items);

                        foreach (var item in testBin.Items)
                        {
                            remainingItems.Remove(item);
                        }

                        return true;
                    }
                }

                bin.AddItem(firstItem);
            }

            return false;
        }

        private static List<LengthGroup> GroupItemsByLength(IReadOnlyList<BinItem> items)
        {
            var groups = new List<LengthGroup>();
            var groupMap = new Dictionary<double, LengthGroup>();

            foreach (var item in items)
            {
                if (!groupMap.TryGetValue(item.Length, out var group))
                {
                    group = new LengthGroup
                    {
                        Length = item.Length,
                        Items = new List<BinItem>()
                    };
                    groupMap[item.Length] = group;
                    groups.Add(group);
                }
                group.Items.Add(item);
            }

            groups.Sort((a, b) => b.Length.CompareTo(a.Length));
            if (groups.Count > 0)
            {
                groups.RemoveAt(0); // Remove the largest length group
            }

            return groups;
        }

        private class LengthGroup
        {
            public double Length { get; set; }
            public List<BinItem> Items { get; set; } = new();
        }
    }
}
