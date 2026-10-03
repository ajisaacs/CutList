namespace CutList.Core
{
    /// <summary>
    /// The one rule for whether parts fit a bar: their cut length (part lengths plus a kerf between
    /// consecutive parts; the kerf after the last cut may run off the end) may exceed the stock length
    /// by at most <see cref="Tolerance.Epsilon"/> (0.00001", the same tolerance as OpenNest). Lengths are
    /// compared in whole billionths of an inch, so Bin, the pattern search and the tests get exactly the
    /// same answer however their sums are ordered.
    /// </summary>
    public static class CutFit
    {
        /// <summary>Inches per unit. Far below the fit tolerance, so rounding to it never matters.</summary>
        public const double Resolution = 1e-9;

        /// <summary>A length in whole units.</summary>
        public static long Units(double inches) => (long)Math.Round(inches / Resolution);

        /// <summary>What one part uses of a bar, in units: its length plus one kerf.</summary>
        public static long Size(double length, double kerf) => Units(length) + Units(kerf);

        /// <summary>What one bar holds, in units of <see cref="Size"/>: stock + kerf + the tolerance.</summary>
        public static long Capacity(double stockLength, double kerf) =>
            Units(stockLength) + Units(kerf) + Units(Tolerance.Epsilon);

        /// <summary>True when parts of these lengths fit one bar.</summary>
        public static bool Fits(IEnumerable<double> lengths, double stockLength, double kerf)
        {
            long total = 0;
            foreach (var length in lengths)
                total += Size(length, kerf);
            return total <= Capacity(stockLength, kerf);
        }
    }
}
