using Newtonsoft.Json;

namespace CutList.Models
{
    public class Document
    {
        private List<PartInputItem> _partsToNest;
        private List<BinInputItem> _stockBins;

        public Document()
        {
            _partsToNest = new List<PartInputItem>();
            _stockBins = new List<BinInputItem>();
        }

        [JsonIgnore]
        public string LastFilePath { get; internal set; }

        /// <summary>
        /// Parts to be nested. For JSON serialization, this exposes the list.
        /// For runtime use, prefer using methods to modify the collection.
        /// </summary>
        public List<PartInputItem> PartsToNest
        {
            get => _partsToNest;
            set => _partsToNest = value ?? new List<PartInputItem>();
        }

        /// <summary>
        /// Stock bins available. For JSON serialization, this exposes the list.
        /// For runtime use, prefer using methods to modify the collection.
        /// </summary>
        public List<BinInputItem> StockBins
        {
            get => _stockBins;
            set => _stockBins = value ?? new List<BinInputItem>();
        }

        public Tool Tool { get; set; }

        /// <summary>
        /// Gets a read-only view of parts to nest.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<PartInputItem> PartsReadOnly => _partsToNest.AsReadOnly();

        /// <summary>
        /// Gets a read-only view of stock bins.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<BinInputItem> StockBinsReadOnly => _stockBins.AsReadOnly();
    }
}
