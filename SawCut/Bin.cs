using System;
using System.Collections.Generic;
using System.Linq;

namespace SawCut
{
    public class Bin
    {
        public List<BinItem> Items;

        public Bin(double length)
        {
            Items = new List<BinItem>();
            Length = length;
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
            get { return UsedLength / Length;  }
        }

        public override string ToString()
        {
            var totalLength = ArchUnits.FormatFromInches(Math.Round(Length, 4));
            var remainingLength = ArchUnits.FormatFromInches(Math.Round(RemainingLength, 4));
            var utilitation = Math.Round(Utilization * 100, 2);

            return $"Length: {totalLength}, {remainingLength} remaining, {Items.Count} items, {utilitation}% utilization";
        }
    }
}