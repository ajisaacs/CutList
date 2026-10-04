namespace CutList.Core.Formatting
{
    /// <summary>
    /// Provides formatting utilities for displaying measurements and values.
    /// </summary>
    public static class FormatHelper
    {
        /// <summary>
        /// Converts a decimal measurement to a mixed fraction for display, rounding the
        /// complete value to the nearest denominator tick with midpoint ties away from zero.
        /// </summary>
        /// <param name="input">The decimal value to convert</param>
        /// <param name="precision">The positive denominator precision (default 16 for 1/16")</param>
        /// <returns>A string in the format "whole-numerator/denominator"</returns>
        /// <exception cref="ArgumentOutOfRangeException">The precision is not positive.</exception>
        public static string ConvertToMixedFraction(decimal input, int precision = 16)
        {
            decimal ticks = RoundToTicks(input, precision);
            string sign = ticks < 0 ? "-" : string.Empty;
            ticks = Math.Abs(ticks);

            decimal wholeNumber = decimal.Truncate(ticks / precision);
            int numerator = (int)(ticks % precision);
            if (numerator == 0)
            {
                return $"{sign}{wholeNumber}";
            }

            int gcd = GetGreatestCommonDivisor(numerator, precision);
            numerator /= gcd;
            int denominator = precision / gcd;

            if (wholeNumber == 0)
            {
                return $"{sign}{numerator}/{denominator}";
            }

            return $"{sign}{wholeNumber}-{numerator}/{denominator}";
        }

        /// <summary>
        /// Converts a double measurement to a mixed fraction string representation.
        /// </summary>
        /// <param name="input">The double value to convert</param>
        /// <returns>A string in the format "whole-numerator/denominator"</returns>
        public static string ConvertToMixedFraction(double input)
        {
            return ConvertToMixedFraction((decimal)input);
        }

        // Presentation only: keep decimal midpoint rounding separate from packing tolerances.
        internal static decimal RoundToTicks(decimal input, int precision)
        {
            if (precision <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(precision), precision,
                    "The denominator precision must be positive.");
            }

            return decimal.Round(input * precision, 0, MidpointRounding.AwayFromZero);
        }

        private static int GetGreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return Math.Abs(a);
        }
    }
}
