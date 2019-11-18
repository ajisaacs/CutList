using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace CutToLength
{
    public class BestFitEngine : IEngine
    {
        public double StockLength { get; set; }

        public Tool CutTool { get; set; }

        public List<Bin> GetResults(List<BinItem> items)
        {
            var items2 = items.OrderByDescending(i => i.Length);
            var bins = new List<Bin>();
            var length = StockLength;

            foreach (var item in items2)
            {
                Bin best_bin;

                if (!FindBin(bins.ToArray(), item.Length, out best_bin))
                {
                    if (item.Length > length)
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
            var spacing = CutTool.Kerf;

            return new Bin(length)
            {
                Spacing = spacing
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

    public interface IEngine
    {
        List<Bin> GetResults(List<BinItem> items);
    }
}
