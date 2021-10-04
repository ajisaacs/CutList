using System.Collections.Generic;

namespace SawCut.Nesting
{
    public interface IEngine
    {
        Result Pack(List<BinItem> items);
    }
}
