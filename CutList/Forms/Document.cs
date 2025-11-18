using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using CutList.Models;
using Newtonsoft.Json;

namespace CutList.Forms
{
    public class Document
    {
        public Document()
        {
            PartsToNest = new List<PartInputItem>();
            StockBins = new List<BinInputItem>();
        }

        [JsonIgnore]
        public string LastFilePath { get; private set; }

        public List<PartInputItem> PartsToNest { get; set; }

        public List<BinInputItem> StockBins { get; set; }

        public Tool Tool { get; set; }
    }
}