using System;
using System.Collections.Generic;
using System.Linq;

namespace SawCut.Nesting
{
    public class MultiBinEngine : IEngine
    {
        public List<MultiBin> Bins { get; set; }

        public double Spacing { get; set; }

        public Result Pack(List<BinItem> items)
        {
            var bins = Bins
                .Where(b => b.Length > 0)
                .OrderBy(b => b.Priority)
                .ThenBy(b => b.Length)
                .ToList();

            var result = new Result();
            var remainingItems = new List<BinItem>(items);

            foreach (var bin in bins)
            {
                var e = new AdvancedFitEngine();
                e.MaxBinCount = bin.Quantity;
                e.StockLength = bin.Length;
                e.Spacing = Spacing;
                var r = e.Pack(remainingItems);

                result.Bins.AddRange(r.Bins);
                remainingItems = r.ItemsNotUsed;
            }

            result.ItemsNotUsed = remainingItems;

            return result;
        }
    }
}