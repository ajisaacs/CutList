using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace CutToLength
{
    public class BestFitEngine : IEngine
    {
        public double StockLength { get; set; }

        public double Spacing { get; set; }

        private List<BinItem> Items { get; set; }

        public Result Pack(List<BinItem> items)
        {
            if (StockLength <= 0)
                throw new Exception("Stock length must be greater than 0");

            Items = items.OrderByDescending(i => i.Length).ToList();

            var result = new Result();
            result.ItemsNotUsed = Items.Where(i => i.Length > StockLength).ToList();

            foreach (var item in result.ItemsNotUsed)
            {
                Items.Remove(item);
            }

            result.Bins = GetBins();

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

                    best_bin = CreateBin();
                    bins.Add(best_bin);
                }

                best_bin.Items.Add(item);
            }

            return bins;
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

    public class Engine2 : IEngine
    {
        public double StockLength { get; set; }

        public double Spacing { get; set; }

        private List<BinItem> Items { get; set; }

        public Result Pack(List<BinItem> items)
        {
            if (StockLength <= 0)
                throw new Exception("Stock length must be greater than 0");

            Items = items.OrderByDescending(i => i.Length).ToList();

            var result = new Result();
            result.ItemsNotUsed = Items.Where(i => i.Length > StockLength).ToList();

            foreach (var item in result.ItemsNotUsed)
            {
                Items.Remove(item);
            }

            result.Bins = GetBins();

            return result;
        }

        private List<Bin> GetBins()
        {
            var bins = new List<Bin>();

            while (Items.Count > 0)
            {
                var bin = new Bin(StockLength);
                bin.Spacing = Spacing;

                FillBin(bin);

                int count = 0;
                
                while (TryImprovePacking(bin))
                {
                    count++;
                }

                bins.Add(bin);
            }

            return bins;
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

    class OptimizeResult
    {
        public OptimizeResult()
        {
            OldItems = new List<BinItem>();
            NewItems = new List<BinItem>();
        }

        public List<BinItem> OldItems { get; set; }

        public List<BinItem> NewItems { get; set; }
    }

    public interface IEngine
    {
        Result Pack(List<BinItem> items);
    }

    public class Result
    {
        public Result()
        {
            ItemsNotUsed = new List<BinItem>();
        }

        public List<BinItem> ItemsNotUsed { get; set; }

        public List<Bin> Bins { get; set; }
    }
}
