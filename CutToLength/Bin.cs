using System;
using System.Collections.Generic;
using System.Linq;

namespace CutToLength
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
                return Items.Sum(i => i.Length) + Spacing * Items.Count;
            }
        }

        public double RemainingLength
        {
            get { return Length - UsedLength; }
        }

        public double Utilization
        {
            get { return (UsedLength / Length * 100.0); }
        }

        public override string ToString()
        {
            return string.Format(
                "Length: {0}\", {1}\" remaining, {2} items, {3}% utilization",
                Math.Round(Length, 4),
                Math.Round(RemainingLength, 4),
                Items.Count,
                Math.Round(Utilization, 2));
        }
    }
}
