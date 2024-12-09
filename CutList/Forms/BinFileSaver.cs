using SawCut;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CutList.Forms
{
    public class BinFileSaver
    {
        private IEnumerable<Bin> _bins;

        public BinFileSaver(IEnumerable<Bin> bins)
        {
            _bins = bins ?? throw new ArgumentNullException(nameof(bins));
        }

        public void SaveBinsToFile(string file)
        {
            using (var writer = new StreamWriter(file))
            {
                writer.AutoFlush = true;
                var max = _bins.Max(b => b.Items.Max(i => SawCut.Helper.ConvertToMixedFraction(i.Length).Length));
                var id = 1;

                foreach (var bin in _bins)
                {
                    WriteBinSummary(writer, bin, id++, max);
                }
            }

            Process.Start(file);
        }

        private void WriteBinSummary(StreamWriter writer, Bin bin, int id, int max)
        {
            var totalLength = SawCut.Helper.ConvertToMixedFraction(bin.Length);
            var remainingLength = SawCut.Helper.ConvertToMixedFraction(bin.RemainingLength);
            var utilization = Math.Round(bin.Utilization * 100, 2);

            writer.WriteLine($"{id}. Length: {totalLength}, {remainingLength} remaining, {bin.Items.Count} items, {utilization}% utilization");
            WriteBinItems(writer, bin, max);
            writer.WriteLine("---------------------------------------------------------------------");
        }

        private void WriteBinItems(StreamWriter writer, Bin bin, int max)
        {
            var groups = bin.Items.GroupBy(i => $"{i.Name} {i.Length}");

            foreach (var group in groups)
            {
                var first = group.First();
                var count = group.Count();
                var length = SawCut.Helper.ConvertToMixedFraction(first.Length).PadLeft(max);
                var name = first.Name;
                var pcsSingularOrPlural = count == 1 ? "pc " : "pcs";

                writer.WriteLine($"   {count}{pcsSingularOrPlural}  @  {length} LG   Tag: {name}");
            }
        }
    }
}