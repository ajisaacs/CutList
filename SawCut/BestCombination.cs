using System;

namespace SawCut
{
    public static class BestCombination
    {
        public static BestComboResult FindFrom2(double length1, double length2, double stockLength)
        {
            var result = new BestComboResult();
            result.Item1Length = length1;
            result.Item2Length = length2;

            stockLength += Tolerance.Epsilon;

            if (length1 > stockLength)
            {
                if (length2 > stockLength)
                {
                    //throw new Exception($"Either {nameof(length1)} or {nameof(length2)} must be less than or equal to the {nameof(stockLength)}");
                    return null;
                }

                result.Item1Count = 0;
                result.Item2Count = (int)Math.Floor(stockLength / length2);
                return result;
            }

            if (length2 > stockLength)
            {
                result.Item1Count = (int)Math.Floor(stockLength / length1);
                result.Item2Count = 0;
                return result;
            }

            var maxCountLength1 = (int)Math.Floor(stockLength / length1);

            result.Item1Count = maxCountLength1;
            result.Item2Count = 0;

            var remnant = stockLength - maxCountLength1 * length1;

            if (remnant.IsEqualTo(0))
                return result;

            for (int countLength1 = 0; countLength1 <= maxCountLength1; ++countLength1)
            {
                var remnant1 = stockLength - countLength1 * length1;

                if (remnant1 >= length2)
                {
                    var countLength2 = (int)Math.Floor(remnant1 / length2);
                    var remnant2 = remnant1 - length2 * countLength2;

                    if (!(remnant2 < remnant))
                        continue;

                    result.Item1Count = countLength1;
                    result.Item2Count = countLength2;

                    if (remnant2.IsEqualTo(0))
                        break;

                    remnant = remnant2;
                }
                else
                {
                    if (!(remnant1 < remnant))
                        continue;

                    result.Item1Count = countLength1;
                    result.Item2Count = 0;

                    if (remnant1.IsEqualTo(0))
                        break;

                    remnant = remnant1;
                }
            }

            return result;
        }
    }

    public class BestComboResult
    {
        public double Item1Length { get; set; }

        public int Item1Count { get; set; }

        public double Item1TotalLength
        {
            get { return Item1Length * Item1Count; }
        }

        public double Item2Length { get; set; }

        public int Item2Count { get; set; }

        public double Item2TotalLength
        {
            get { return Item2Length * Item2Count; }
        }
    }
}