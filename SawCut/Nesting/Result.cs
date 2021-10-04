using System.Collections.Generic;

namespace SawCut.Nesting
{
    public class Result
    {
        public Result()
        {
            ItemsNotUsed = new List<BinItem>();
        }

        public List<BinItem> ItemsNotUsed { get; set; }

        public List<Bin> Bins { get; set; }
    }
}
