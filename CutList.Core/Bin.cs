using System;
using System.Collections.Generic;
using System.Linq;

namespace CutList.Core
{
    public class Bin
    {
        private readonly List<BinItem> _items;

        public Bin(double length)
        {
            _items = new List<BinItem>();
            Length = length;
        }

        public IReadOnlyList<BinItem> Items => _items;

        public void AddItem(BinItem item)
        {
            _items.Add(item);
        }

        public void AddItems(IEnumerable<BinItem> items)
        {
            _items.AddRange(items);
        }

        public void RemoveItem(BinItem item)
        {
            _items.Remove(item);
        }

        public void SortItems(Comparison<BinItem> comparison)
        {
            _items.Sort(comparison);
        }

        public double Spacing { get; set; }

        public double Length { get; set; }

        public double UsedLength
        {
            get
            {
                var usedLength = Math.Round(Items.Sum(i => i.Length) + Spacing * Items.Count, 8);

                if (usedLength > Length && (usedLength - Length) <= Spacing)
                    return Length;

                return Math.Round(Items.Sum(i => i.Length) + Spacing * Items.Count, 8);
            }
        }

        public double RemainingLength
        {
            get { return Math.Round(Length - UsedLength, 8); }
        }

        /// <summary>
        /// Returns a ratio of UsedLength to TotalLength
        /// 1.0 = 100% utilization
        /// </summary>
        public double Utilization
        {
            get { return UsedLength / Length; }
        }

        public override string ToString()
        {
            var totalLength = FormatHelper.ConvertToMixedFraction(Math.Round(Length, 4));
            var remainingLength = FormatHelper.ConvertToMixedFraction(Math.Round(RemainingLength, 4));
            var utilitation = Math.Round(Utilization * 100, 2);

            return $"Length: {totalLength}, {remainingLength} remaining, {Items.Count} items, {utilitation}% utilization";
        }
    }
}