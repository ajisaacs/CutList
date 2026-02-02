using CutList.Core.Formatting;
using System.Diagnostics;

namespace CutList.Core
{
    public class BinFileSaver
    {
        private readonly IEnumerable<Bin> _bins;

        private int PaddingWidthOfItemLength { get; set; }

        public bool OpenFileAfterSave { get; set; }

        /// <summary>
        /// The cutting method/tool used (e.g., "Miter Saw", "Table Saw").
        /// </summary>
        public string? CutMethod { get; set; }

        /// <summary>
        /// The material shape (e.g., "Round Tube", "Square Tube", "Flat Bar").
        /// </summary>
        public string? MaterialShape { get; set; }

        public BinFileSaver(IEnumerable<Bin> bins)
        {
            _bins = bins ?? throw new ArgumentNullException(nameof(bins));
        }

        public void SaveBinsToFile(string file)
        {
            using (var writer = new StreamWriter(file))
            {
                writer.AutoFlush = true;
                WriteToWriter(writer);
            }

            if (OpenFileAfterSave)
            {
                OpenFile(file);
            }
        }

        public string GenerateReport()
        {
            using var writer = new StringWriter();
            WriteToWriter(writer);
            return writer.ToString();
        }

        private void WriteToWriter(TextWriter writer)
        {
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

        private void WriteHeader(TextWriter writer)
        {
            var totalBars = _bins.Count();
            var totalItems = _bins.Sum(b => b.Items.Count);

            writer.WriteLine("CUT LIST");
            WriteSeparator(writer, '=');
            writer.WriteLine();
            WriteAlignedLine(writer, "Date", DateTime.Now.ToString("g"));

            if (!string.IsNullOrEmpty(CutMethod))
                WriteAlignedLine(writer, "Cut Method", CutMethod);

            if (!string.IsNullOrEmpty(MaterialShape))
                WriteAlignedLine(writer, "Material", MaterialShape);

            WriteAlignedLine(writer, "Stock Bars Needed", totalBars.ToString());
            WriteAlignedLine(writer, "Total Pieces", totalItems.ToString());
            writer.WriteLine();
        }

        private static void WriteAlignedLine(TextWriter writer, string label, string value, int labelWidth = 20)
        {
            writer.WriteLine($"  {label.PadRight(labelWidth)} {value}");
        }

        private void WriteBinSummary(TextWriter writer, Bin bin, int id)
        {
            var stockLength = FormatHelper.ConvertToMixedFraction(bin.Length);
            var dropLength = FormatHelper.ConvertToMixedFraction(bin.RemainingLength);

            WriteSeparator(writer);

            writer.WriteLine($"BAR #{id}  -  Length: {stockLength}\"");
            writer.WriteLine();
            writer.WriteLine("  Cut these pieces:");

            WriteBinItems(writer, bin);

            writer.WriteLine();
            writer.WriteLine($"  Remaining drop: {dropLength}\"");
            writer.WriteLine();
        }

        private static void WriteSeparator(TextWriter writer, char c = '─', int length = 50)
        {
            writer.WriteLine(new string(c, length));
        }

        private void WriteBinItems(TextWriter writer, Bin bin)
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

        private void WriteFinalSummary(TextWriter writer)
        {
            var totalBars = _bins.Count();
            var totalItems = _bins.Sum(b => b.Items.Count);
            var totalStock = _bins.Sum(b => b.Length);
            var totalDrop = _bins.Sum(b => b.RemainingLength);

            string fmt(double v) => FormatHelper.ConvertToMixedFraction(v);

            WriteSeparator(writer, '=');
            writer.WriteLine("SUMMARY");
            writer.WriteLine();
            WriteAlignedLine(writer, "Stock Bars Needed", totalBars.ToString());
            WriteAlignedLine(writer, "Total Pieces", totalItems.ToString());
            WriteAlignedLine(writer, "Total Material Used", $"{fmt(totalStock)}\"");
            WriteAlignedLine(writer, "Total Drop/Waste", $"{fmt(totalDrop)}\"");
            WriteSeparator(writer, '=');
        }
    }
}
