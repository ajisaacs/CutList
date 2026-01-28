using System.Collections.Generic;

namespace CutList.Core.Nesting
{
    public interface IEngine
    {
        Result Pack(List<BinItem> items);
    }
}