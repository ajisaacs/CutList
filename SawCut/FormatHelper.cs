using System;

namespace SawCut
{
    /// <summary>
    /// Provides formatting utilities for displaying measurements and values.
    /// </summary>
    public static class FormatHelper
    {
        /// <summary>
        /// Converts a decimal measurement to a mixed fraction string representation.
        /// </summary>
        /// <param name="input">The decimal value to convert</param>
        /// <param name="precision">The denominator precision (default 32 for 1/32")</param>
        /// <returns>A string in the format "whole-numerator/denominator"</returns>
        public static string ConvertToMixedFraction(decimal input, int precision = 32)
        {
            // Get the whole number part
            int wholeNumber = (int)input;

            // Get the fractional part
            decimal fractionalPart = Math.Abs(input - wholeNumber);

            if (fractionalPart == 0)
            {
                return wholeNumber.ToString();
            }

            // Convert the fractional part to a fraction
            int numerator = (int)(fractionalPart * precision);
            int denominator = precision;

            // Simplify the fraction
            int gcd = GetGreatestCommonDivisor(numerator, denominator);
            numerator /= gcd;
            denominator /= gcd;

            return $"{wholeNumber}-{numerator}/{denominator}";
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
