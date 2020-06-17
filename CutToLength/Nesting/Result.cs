using System.Collections.Generic;

namespace CutToLength.Nesting
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
