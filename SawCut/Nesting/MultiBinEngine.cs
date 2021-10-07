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
                .OrderByDescending(b => b.Priority)
                .ThenBy(b => b.Length)
                .ToList();

            var result = new Result();
            var remainingItems = new List<BinItem>(items);

            foreach (var bin in bins)
            {
                var e = new Engine2();
                e.MaxBinCount = bin.Quantity;
                e.StockLength = bin.Length;
                e.Spacing = Spacing;
                var r = e.Pack(remainingItems);

                result.Bins.AddRange(r.Bins);
                remainingItems = r.ItemsNotUsed;
            }

            result.ItemsNotUsed = remainingItems;

            // sanity check

            //var itemsNested = result.Bins.SelectMany(b => b.Items).ToList();
            //var lengths = items.Select(i => i.Length).Distinct();

            //foreach (var length in lengths)
            //{
            //    var nameLengthGroups1 = items.Where(i => i.Length == length).GroupBy(i => i.Name).ToList();
            //    var nameLengthGroups2 = itemsNested.Where(i => i.Length == length).GroupBy(i => i.Name).ToList();

            //    foreach (var group in nameLengthGroups1)
            //    {
            //        var name = group.Key;

            //        var nestedWithSameName = nameLengthGroups2.FirstOrDefault(g => g.Key == group.Key);

            //        if (group.Count() != nestedWithSameName.Count())
            //        {
            //            throw new Exception("Failed sanity check.");

            //        }
            //    }

            //    if (nameLengthGroups1.Count() != nameLengthGroups2.Count())
            //    {
            //        throw new Exception("Failed sanity check.");
            //    }
            //}

            return result;
        }
    }
}