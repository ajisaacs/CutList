namespace CutList.Core
{
    /// <summary>
    /// Represents a group of identical bins (bins with the same items in the same order).
    /// Used for displaying consolidated results.
    /// </summary>
    public class BinGroup
    {
        public BinGroup(Bin representativeBin, int count)
        {
            if (representativeBin == null)
                throw new ArgumentNullException(nameof(representativeBin));

            if (count <= 0)
                throw new ArgumentException("Count must be greater than zero", nameof(count));

            RepresentativeBin = representativeBin;
            Count = count;
        }

        /// <summary>
        /// A representative bin from this group (all bins in the group are identical).
        /// </summary>
        public Bin RepresentativeBin { get; }

        /// <summary>
        /// The number of identical bins in this group.
        /// </summary>
        public int Count { get; }

        // Properties that delegate to the representative bin for data binding
        public double Spacing => RepresentativeBin.Spacing;
        public double Length => RepresentativeBin.Length;
        public double UsedLength => RepresentativeBin.UsedLength;
        public double RemainingLength => RepresentativeBin.RemainingLength;
        public double Utilization => RepresentativeBin.Utilization;
        public IReadOnlyList<BinItem> Items => RepresentativeBin.Items;

        public override string ToString()
        {
            if (Count == 1)
                return RepresentativeBin.ToString();

            return $"{RepresentativeBin} (x{Count})";
        }
    }
}
