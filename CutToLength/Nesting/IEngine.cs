using System.Collections.Generic;

namespace CutToLength.Nesting
{
    public interface IEngine
    {
        Result Pack(List<BinItem> items);
    }
}
