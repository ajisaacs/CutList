namespace CutList.Core
{
    public static class BestCombination
    {
        public static BestComboResult FindFrom2(double length1, double length2, double stockLength)
        {
            var result = InitializeResult(length1, length2);

            bool item1Fits = length1 <= stockLength;
            bool item2Fits = length2 <= stockLength;

            if (!item1Fits && !item2Fits)
                return null;

            if (!item1Fits)
                return CalculateSingleItemResult(result, length2, stockLength, false);

            if (!item2Fits)
                return CalculateSingleItemResult(result, length1, stockLength, true);

            return CalculateOptimalCombination(result, length1, length2, stockLength);
        }

        private static BestComboResult InitializeResult(double length1, double length2)
        {
            return new BestComboResult
            {
                Item1Length = length1,
                Item2Length = length2
            };
        }

        private static BestComboResult CalculateSingleItemResult(BestComboResult result, double length, double stockLength, bool isItem1)
        {
            if (isItem1)
            {
                result.Item1Count = (int)Math.Floor(stockLength / length);
                result.Item2Count = 0;
            }
            else
            {
                result.Item1Count = 0;
                result.Item2Count = (int)Math.Floor(stockLength / length);
            }
            return result;
        }

        private static BestComboResult CalculateOptimalCombination(BestComboResult result, double length1, double length2, double stockLength)
        {
            var maxCountLength1 = (int)Math.Floor(stockLength / length1);
            result.Item1Count = maxCountLength1;
            result.Item2Count = 0;

            var remnant = stockLength - maxCountLength1 * length1;

            for (int countLength1 = 0; countLength1 <= maxCountLength1; ++countLength1)
            {
                var remnant1 = stockLength - countLength1 * length1;

                if (remnant1 >= length2)
                    remnant = UpdateResultForTwoItems(result, length2, remnant1, remnant, countLength1);
                else
                    remnant = UpdateResultForOneItem(result, remnant1, remnant, countLength1);

                if (remnant.IsEqualTo(0))
                    break;
            }

            return result;
        }

        private static double UpdateResultForTwoItems(BestComboResult result, double length2, double remnant1, double currentRemnant, int countLength1)
        {
            var countLength2 = (int)Math.Floor(remnant1 / length2);
            var remnant2 = remnant1 - countLength2 * length2;

            if (remnant2 < currentRemnant)
            {
                result.Item1Count = countLength1;
                result.Item2Count = countLength2;
                return remnant2;
            }

            return currentRemnant;
        }

        private static double UpdateResultForOneItem(BestComboResult result, double remnant1, double currentRemnant, int countLength1)
        {
            if (remnant1 < currentRemnant)
            {
                result.Item1Count = countLength1;
                result.Item2Count = 0;
                return remnant1;
            }

            return currentRemnant;
        }
    }

    public class BestComboResult
    {
        public double Item1Length { get; set; }
        public int Item1Count { get; set; }
        public double Item1TotalLength => Item1Length * Item1Count;

        public double Item2Length { get; set; }
        public int Item2Count { get; set; }
        public double Item2TotalLength => Item2Length * Item2Count;
    }
}