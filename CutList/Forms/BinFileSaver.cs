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
        private readonly IEnumerable<Bin> _bins;

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

                PaddingWidthOfItemLength = _bins
                    .SelectMany(b => b.Items)
                    .Select(i => FormatHelper.ConvertToMixedFraction(i.Length).Length)
                    .DefaultIfEmpty(0)
                    .Max();

                WriteHeader(writer);

                var id = 1;
                foreach (var bin in _bins)
                {
                    WriteBinSummary(writer, bin, id++);
                }

                WriteFinalSummary(writer);
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
            catch
            {
                Process.Start(file);
            }
        }

        private void WriteHeader(StreamWriter writer)
        {
            var totalBars = _bins.Count();
            var totalItems = _bins.Sum(b => b.Items.Count);
            var avgUtil = _bins.Any()
                ? Math.Round(_bins.Average(b => b.Utilization) * 100, 1)
                : 0;

            writer.WriteLine("CUT LIST");
            writer.WriteLine($"Created: {DateTime.Now:g}");
            writer.WriteLine();

            writer.WriteLine($"Bars: {totalBars}   Items: {totalItems}   Avg utilization: {avgUtil:0.0}%");
            writer.WriteLine(new string('─', 80));
            writer.WriteLine();
        }

        private void WriteBinSummary(StreamWriter writer, Bin bin, int id)
        {
            var stockLength = FormatHelper.ConvertToMixedFraction(bin.Length);
            var dropLength = FormatHelper.ConvertToMixedFraction(bin.RemainingLength);
            var usedLengthValue = bin.Length - bin.RemainingLength;
            var usedLength = FormatHelper.ConvertToMixedFraction(usedLengthValue);
            var utilization = Math.Round(bin.Utilization * 100, 2);

            var barLabel = $"BAR {id:000}";

            writer.WriteLine(barLabel);
            writer.WriteLine(
                $"Stock: {stockLength}   Used: {usedLength}   Drop: {dropLength}   " +
                $"Items: {bin.Items.Count}   Utilization: {utilization:0.##}%");
            writer.WriteLine();

            WriteBinItems(writer, bin);

            writer.WriteLine();
            writer.WriteLine(new string('─', 80));
            writer.WriteLine();
        }

        private void WriteBinItems(StreamWriter writer, Bin bin)
        {
            // Group by name + length to keep same behavior as before
            var groups = bin.Items
                .GroupBy(i => new { i.Name, i.Length })
                .OrderBy(g => g.Key.Name)
                .ThenBy(g => g.Key.Length);

            // Header
            var lengthHeader = "Length".PadLeft(PaddingWidthOfItemLength);
            writer.WriteLine($"  QTY   {lengthHeader}   TAG");
            writer.WriteLine($"  ---   {new string('-', PaddingWidthOfItemLength)}   ----------------");

            foreach (var group in groups)
            {
                var first = group.First();
                var count = group.Count();
                var length = FormatHelper
                    .ConvertToMixedFraction(first.Length)
                    .PadLeft(PaddingWidthOfItemLength);

                var tag = first.Name;

                writer.WriteLine($"  {count,3}   {length}   {tag}");
            }
        }

        private void WriteFinalSummary(StreamWriter writer)
        {
            var totalBars = _bins.Count();
            var totalItems = _bins.Sum(b => b.Items.Count);

            var totalStock = _bins.Sum(b => b.Length);
            var totalUsed = _bins.Sum(b => b.Length - b.RemainingLength);
            var totalDrop = _bins.Sum(b => b.RemainingLength);

            var avgUtil = _bins.Any()
                ? Math.Round(_bins.Average(b => b.Utilization) * 100, 2)
                : 0;

            var best = _bins.OrderByDescending(b => b.Utilization).FirstOrDefault();
            var worst = _bins.OrderBy(b => b.Utilization).FirstOrDefault();

            string fmt(double v) => FormatHelper.ConvertToMixedFraction(v);

            writer.WriteLine();
            writer.WriteLine("FINAL SUMMARY");
            writer.WriteLine(new string('═', 80));
            writer.WriteLine();

            writer.WriteLine($"Total bars:       {totalBars}");
            writer.WriteLine($"Total items:      {totalItems}");
            writer.WriteLine();

            writer.WriteLine($"Total stock:      {fmt(totalStock)}");
            writer.WriteLine($"Total used:       {fmt(totalUsed)}");
            writer.WriteLine($"Total drop:       {fmt(totalDrop)}");
            writer.WriteLine($"Average util:     {avgUtil}%");
            writer.WriteLine();

            if (best != null)
            {
                writer.WriteLine($"Best bar:         Util {Math.Round(best.Utilization * 100, 2)}%   " +
                                 $"Drop {fmt(best.RemainingLength)}");
            }

            if (worst != null)
            {
                writer.WriteLine($"Worst bar:        Util {Math.Round(worst.Utilization * 100, 2)}%   " +
                                 $"Drop {fmt(worst.RemainingLength)}");
            }

            writer.WriteLine();
            writer.WriteLine(new string('═', 80));
            writer.WriteLine("End of Report");
        }

    }
}
