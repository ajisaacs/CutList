using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SawCut.Nesting
{
    public class BestFitEngine : IEngine
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

            var result = new Result();
            var itemsTooLarge = Items.Where(i => i.Length > StockLength).ToList();
            result.AddItemsNotUsed(itemsTooLarge);

            foreach (var item in itemsTooLarge)
            {
                Items.Remove(item);
            }

            var bins = GetBins();
            result.AddBins(bins);

            foreach (var bin in bins)
            {
                foreach (var item in bin.Items)
                {
                    Items.Remove(item);
                }
            }

            result.AddItemsNotUsed(Items);

            return result;
        }

        private List<Bin> GetBins()
        {
            var bins = new List<Bin>();

            foreach (var item in Items)
            {
                Bin best_bin;

                if (!FindBin(bins.ToArray(), item.Length, out best_bin))
                {
                    if (item.Length > StockLength)
                        continue;

                    if (bins.Count < MaxBinCount)
                    {
                        best_bin = CreateBin();
                        bins.Add(best_bin);
                    }
                }

                if (best_bin != null)
                    best_bin.AddItem(item);

                
            }

            return bins
                .OrderByDescending(b => b.Utilization)
                .ThenBy(b => b.Items.Count)
                .ToList();
        }

        private Bin CreateBin()
        {
            var length = StockLength;

            return new Bin(length)
            {
                Spacing = Spacing
            };
        }

        private static bool FindBin(IEnumerable<Bin> bins, double length, out Bin found)
        {
            found = null;

            foreach (var bin in bins)
            {
                if (bin.RemainingLength < length)
                    continue;

                if (found == null)
                    found = bin;

                if (bin.RemainingLength < found.RemainingLength)
                    found = bin;
            }

            return (found != null);
        }
    }
}