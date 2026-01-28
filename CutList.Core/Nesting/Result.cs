using System.Collections.Generic;

namespace CutList.Core.Nesting
{
    public class Result
    {
        private readonly List<BinItem> _itemsNotUsed;
        private readonly List<Bin> _bins;

        public Result()
        {
            _itemsNotUsed = new List<BinItem>();
            _bins = new List<Bin>();
        }

        public IReadOnlyList<BinItem> ItemsNotUsed => _itemsNotUsed;

        public IReadOnlyList<Bin> Bins => _bins;

        public void AddItemNotUsed(BinItem item)
        {
            _itemsNotUsed.Add(item);
        }

        public void AddItemsNotUsed(IEnumerable<BinItem> items)
        {
            _itemsNotUsed.AddRange(items);
        }

        public void AddBin(Bin bin)
        {
            _bins.Add(bin);
        }

        public void AddBins(IEnumerable<Bin> bins)
        {
            _bins.AddRange(bins);
        }
    }
}