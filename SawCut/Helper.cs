using System;
using System.Drawing;

namespace SawCut
{
    public static class Helper
    {
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