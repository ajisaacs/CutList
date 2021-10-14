using Newtonsoft.Json;
using SawCut;
using System;

namespace CutList.Models
{
    public class BinInputItem : LengthItem
    {
        public BinInputItem()
        {
        }

        public int Priority { get; set; } = 10;
    }
}