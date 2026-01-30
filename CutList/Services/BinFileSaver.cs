using CutList.Core;
using CutList.Core.Formatting;
using System.Diagnostics;

namespace CutList.Services
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

            writer.WriteLine("CUT LIST");
            writer.WriteLine($"Date: {DateTime.Now:g}");
            writer.WriteLine($"Total stock bars needed: {totalBars}");
            writer.WriteLine($"Total pieces to cut: {totalItems}");
            writer.WriteLine();
        }

        private void WriteBinSummary(StreamWriter writer, Bin bin, int id)
        {
            var stockLength = FormatHelper.ConvertToMixedFraction(bin.Length);
            var dropLength = FormatHelper.ConvertToMixedFraction(bin.RemainingLength);

            writer.WriteLine(new string('─', 50));
            writer.WriteLine($"BAR #{id}  -  Start with {stockLength}\" stock");
            writer.WriteLine();
            writer.WriteLine("  Cut these pieces:");

            WriteBinItems(writer, bin);

            writer.WriteLine();
            writer.WriteLine($"  Remaining drop: {dropLength}\"");
            writer.WriteLine();
        }

        private void WriteBinItems(StreamWriter writer, Bin bin)
        {
            var groups = bin.Items
                .GroupBy(i => new { i.Name, i.Length })
                .OrderBy(g => g.Key.Name)
                .ThenBy(g => g.Key.Length);

            var lengthHeader = "LENGTH".PadLeft(PaddingWidthOfItemLength + 1);
            writer.WriteLine($"    QTY   {lengthHeader}   LABEL");

            foreach (var group in groups)
            {
                var first = group.First();
                var count = group.Count();
                var length = FormatHelper
                    .ConvertToMixedFraction(first.Length)
                    .PadLeft(PaddingWidthOfItemLength) + "\"";

                writer.WriteLine($"    {count,3}   {length}   {first.Name}");
            }
        }

        private void WriteFinalSummary(StreamWriter writer)
        {
            var totalBars = _bins.Count();
            var totalItems = _bins.Sum(b => b.Items.Count);
            var totalStock = _bins.Sum(b => b.Length);
            var totalDrop = _bins.Sum(b => b.RemainingLength);

            string fmt(double v) => FormatHelper.ConvertToMixedFraction(v);

            writer.WriteLine(new string('═', 50));
            writer.WriteLine("SUMMARY");
            writer.WriteLine($"  Stock bars needed: {totalBars}");
            writer.WriteLine($"  Total pieces to cut: {totalItems}");
            writer.WriteLine($"  Total material used: {fmt(totalStock)}\"");
            writer.WriteLine($"  Total drop/waste: {fmt(totalDrop)}\"");
            writer.WriteLine(new string('═', 50));
        }

    }
}
