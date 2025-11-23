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

        private int PaddingWidthOfItemLength { get; set; }

        public bool OpenFileAfterSave { get; set; }

        public BinFileSaver(IEnumerable<Bin> bins)
        {
            _bins = bins ?? throw new ArgumentNullException(nameof(bins));
        }

        public void SaveBinsToFile(string file)
        {
            using (var writer = new StreamWriter(file))
            {
                writer.AutoFlush = true;
                PaddingWidthOfItemLength = _bins.Max(b => b.Items.Max(i => SawCut.FormatHelper.ConvertToMixedFraction(i.Length).Length));
                var id = 1;

                foreach (var bin in _bins)
                {
                    WriteBinSummary(writer, bin, id++);
                }
            }

            if (OpenFileAfterSave)
            {
                OpenFile(file);
            }
        }

        private void OpenFile(string file)
        {
            try
            {
                Process.Start("notepad.exe", file);
            }
            catch (Exception)
            {
                // Notepad is not installed, so just open the file
                Process.Start(file);
            }
        }

        private void WriteBinSummary(StreamWriter writer, Bin bin, int id)
        {
            var totalLength = SawCut.FormatHelper.ConvertToMixedFraction(bin.Length);
            var remainingLength = SawCut.FormatHelper.ConvertToMixedFraction(bin.RemainingLength);
            var utilization = Math.Round(bin.Utilization * 100, 2);

            writer.WriteLine($"{id}. Length: {totalLength}, {remainingLength} remaining, {bin.Items.Count} items, {utilization}% utilization");
            WriteBinItems(writer, bin);
            writer.WriteLine("---------------------------------------------------------------------");
        }

        private void WriteBinItems(StreamWriter writer, Bin bin)
        {
            var groups = bin.Items.GroupBy(i => $"{i.Name} {i.Length}");

            foreach (var group in groups)
            {
                var first = group.First();
                var count = group.Count();
                var length = SawCut.FormatHelper.ConvertToMixedFraction(first.Length).PadLeft(PaddingWidthOfItemLength);
                var name = first.Name;
                var pcsSingularOrPlural = count == 1 ? "pc " : "pcs";

                writer.WriteLine($"   {count}{pcsSingularOrPlural}  @  {length} LG   Tag: {name}");
            }
        }
    }
}