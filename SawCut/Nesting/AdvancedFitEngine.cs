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
        public double StockLength { get; set; }

        public double Spacing { get; set; }

        public int MaxBinCount { get; set; } = int.MaxValue;

        private List<BinItem> Items { get; set; }

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

            result.ItemsNotUsed = Items.Where(i => i.Length > StockLength).ToList();
            result.Bins = GetBins();

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

        private List<Bin> GetBins()
        {
            var bins = new List<Bin>();

            while (Items.Count > 0 && (MaxBinCount == -1 || bins.Count < MaxBinCount))
            {
                var bin = new Bin(StockLength);
                bin.Spacing = Spacing;

                FillBin(bin);

                int count = 0;

                while (TryImprovePacking(bin))
                {
                    count++;
                }

                bin.Items = bin.Items.OrderByDescending(i => i.Length).ToList();

                bins.Add(bin);
            }

            return bins
                .OrderByDescending(b => b.Utilization)
                .ThenBy(b => b.Items.Count)
                .ToList();
        }

        private void FillBin(Bin bin)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];

                if (bin.RemainingLength >= item.Length)
                    bin.Items.Add(item);
            }

            foreach (var item in bin.Items)
            {
                Items.Remove(item);
            }
        }

        private bool TryImprovePacking(Bin bin)
        {
            if (bin.Items.Count == 0)
                return false;

            if (Items.Count < 2)
                return false;

            var lengthGroups = bin.Items
                .OrderByDescending(i => i.Length)
                .GroupBy(i => i.Length)
                .Skip(1);

            var minItemLength = Items.Min(i => i.Length);

            foreach (var group in lengthGroups)
            {
                var minRemainingLength = bin.RemainingLength;

                var firstItem = group.First();
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
                        if (bin2.RemainingLength < minItemLength)
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
    }
}

