using System.Globalization;

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

        /// <summary>
        /// Formats exact sixteenth-inch multiples as simplified fractions; other values
        /// retain their full decimal precision, trimming only insignificant trailing zeros.
        /// Always displays total inches, without converting to double or splitting feet.
        /// </summary>
        public static string FormatExactOrFractionInches(decimal inches)
        {
            if (inches % 0.0625m != 0)
            {
                return $"{inches.ToString("0.############################", CultureInfo.InvariantCulture)}\"";
            }

            decimal whole = decimal.Truncate(inches);
            decimal remainder = Math.Abs(inches % 1m);
            string wholeText = whole.ToString("0", CultureInfo.InvariantCulture);
            if (remainder == 0)
            {
                return $"{wholeText}\"";
            }

            // Only an exact sixteenth reaches this reducer. Split first to avoid tick
            // multiplication overflowing for large whole-inch decimal values.
            string fraction = ConvertToMixedFraction(remainder);
            return whole == 0
                ? $"{(inches < 0 ? "-" : string.Empty)}{fraction}\""
                : $"{wholeText}-{fraction}\"";
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
