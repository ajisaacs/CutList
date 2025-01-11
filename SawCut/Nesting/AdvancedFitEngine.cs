using SawCut.Nesting;
using SawCut;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SawCut.Nesting
{
    public class AdvancedFitEngine : IEngine
    {
        public AdvancedFitEngine()
        {
            Bins = new List<Bin>();
        }

        public double StockLength { get; set; }

        public double Spacing { get; set; }

        public int MaxBinCount { get; set; } = int.MaxValue;

        private List<BinItem> Items { get; set; }

        private List<Bin> Bins { get; set; }

        public Result Pack(List<BinItem> items)
        {
            if (StockLength <= 0)
                throw new Exception("Stock length must be greater than 0");

            Items = items.OrderByDescending(i => i.Length).ToList();

            var result = new Result
            {
                ItemsNotUsed = Items.Where(i => i.Length > StockLength).ToList()
            };

            Items.RemoveAll(item => result.ItemsNotUsed.Contains(item));

            CreateBins();

            result.ItemsNotUsed = Items.Where(i => i.Length > StockLength).ToList();
            result.Bins = Bins;

            foreach (var bin in result.Bins)
            {
                foreach (var item in bin.Items)
                {
                    Items.Remove(item);
                }
            }

            result.ItemsNotUsed.AddRange(Items);

            return result;
        }

        private void CreateBins()
        {
            while (Items.Count > 0 && CanAddMoreBins())
            {
                var bin = new Bin(StockLength)
                {
                    Spacing = Spacing
                };

                FillBin(bin);

                while (TryImprovePacking(bin))
                {
                }
                
                bin.Items.Sort((a, b) =>
                {
                    int comparison = b.Length.CompareTo(a.Length);
                    return comparison != 0 ? comparison : a.Length.CompareTo(b.Length);
                });

                Bins.Add(bin);

                CreateDuplicateBins(bin);
            }

            Bins = Bins
                .OrderByDescending(b => b.Utilization)
                .ThenBy(b => b.Items.Count)
                .ToList();
        }

        private bool CanAddMoreBins()
        {
            if (MaxBinCount == -1)
                return true;

            if (Bins.Count < MaxBinCount)
                return true;

            return false;
        }

        private void FillBin(Bin bin)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (bin.RemainingLength >= Items[i].Length)
                {
                    bin.Items.Add(Items[i]);
                    Items.RemoveAt(i);
                    i--;
                }
            }
        }

        private void CreateDuplicateBins(Bin originalBin)
        {
            // Count how many times the bin can be duplicated
            int duplicateCount = GetDuplicateCount(originalBin);

            for (int i = 0; i < duplicateCount; i++)
            {
                if (!CanAddMoreBins())
                    break;

                var newBin = new Bin(originalBin.Length)
                {
                    Spacing = Spacing
                };

                foreach (var item in originalBin.Items)
                {
                    var newItem = Items.FirstOrDefault(a => a.Length == item.Length);
                    newBin.Items.Add(item);
                    Items.Remove(newItem);
                }

                Bins.Add(newBin);
            }
        }

        private int GetDuplicateCount(Bin bin)
        {
            int count = int.MaxValue;

            foreach (var item in bin.Items.GroupBy(i => i.Length))
            {
                int availableCount = Items.Count(i => i.Length == item.Key);
                count = Math.Min(count, availableCount / item.Count());
            }

            return count;
        }

        private bool TryImprovePacking(Bin bin)
        {
            if (bin.Items.Count == 0)
                return false;

            if (Items.Count < 2)
                return false;

            var lengthGroups = GroupItemsByLength(bin.Items);

            var shortestLengthItemAvailable = Items.Min(i => i.Length);

            foreach (var group in lengthGroups)
            {
                var minRemainingLength = bin.RemainingLength;
                var firstItem = group.Items.FirstOrDefault();
                bin.Items.Remove(firstItem);

                for (int i = 0; i < Items.Count; i++)
                {
                    var item1 = Items[i];

                    if (Items[i].Length > bin.RemainingLength)
                        continue;

                    var bin2 = new Bin(bin.RemainingLength);
                    bin2.Spacing = bin.Spacing;
                    bin2.Items.Add(item1);

                    for (int j = i + 1; j < Items.Count; j++)
                    {
                        if (bin2.RemainingLength < shortestLengthItemAvailable)
                            break;

                        var item2 = Items[j];

                        if (item2.Length > bin2.RemainingLength)
                            continue;

                        bin2.Items.Add(item2);
                    }

                    if (bin2.RemainingLength < minRemainingLength)
                    {
                        Items.Add(firstItem);
                        bin.Items.AddRange(bin2.Items);

                        foreach (var item in bin2.Items)
                        {
                            Items.Remove(item);
                        }

                        // improvement made
                        return true;
                    }
                }

                bin.Items.Add(firstItem);
            }

            return false;
        }

        private List<LengthGroup> GroupItemsByLength(List<BinItem> items)
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
    }

    internal class LengthGroup
    {
        public double Length { get; set; }
        public List<BinItem> Items { get; set; }
    }
}

