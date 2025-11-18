using System;
using System.Collections.Generic;
using System.Linq;

namespace SawCut.Nesting
{
    public class MultiBinEngine : IEngine
    {
        private readonly IEngineFactory _engineFactory;

        public MultiBinEngine() : this(new EngineFactory())
        {
        }

        public MultiBinEngine(IEngineFactory engineFactory)
        {
            _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        }

        public List<MultiBin> Bins { get; set; }

        public double Spacing { get; set; }

        public Result Pack(List<BinItem> items)
        {
            var bins = Bins
                .Where(b => b.Length > 0)
                .OrderBy(b => b.Priority)
                .ThenBy(b => b.Length)
                .ToList();

            var result = new Result();
            var remainingItems = new List<BinItem>(items);

            foreach (var bin in bins)
            {
                var engine = _engineFactory.CreateEngine(bin.Length, Spacing, bin.Quantity);
                var r = engine.Pack(remainingItems);

                result.AddBins(r.Bins);
                remainingItems = r.ItemsNotUsed.ToList();
            }

            result.AddItemsNotUsed(remainingItems);

            return result;
        }
    }
}