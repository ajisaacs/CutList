using System;
using System.Text.RegularExpressions;
using System.Text;
using SawCut;

namespace SawCut
{
    public static class ArchUnits
    {
        public static double ParseToInches(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return 0;

            var sb = new StringBuilder(input.Trim().ToLower());

            // replace all units with their equivelant symbols
            sb.Replace("ft", "'");
            sb.Replace("feet", "'");
            sb.Replace("foot", "'");
            sb.Replace("inches", "\"");
            sb.Replace("inch", "\"");
            sb.Replace("in", "\"");

            var regex = new Regex("^(?<Feet>\\d+\\.?\\d*\\s*')?\\s*(?<Inches>\\d+\\.?\\d*\\s*\")?$");

            // input manipulation is done, put the value back
            input = Fraction.ReplaceFractionsWithDecimals(sb.ToString());

            var match2 = regex.Match(input);

            if (!match2.Success)
                throw new Exception("Input is not in a valid format.");

            var feet = match2.Groups["Feet"];
            var inches = match2.Groups["Inches"];

            var totalInches = 0.0;

            if (feet.Success)
            {
                var x = double.Parse(feet.Value.Remove(feet.Length - 1));
                totalInches += x * 12;
            }

            if (inches.Success)
            {
                var x = double.Parse(inches.Value.Remove(inches.Length - 1));
                totalInches += x;
            }

            return Math.Round(totalInches, 8);
        }

        public static double ParseToFeet(string input)
        {
            var inches = ParseToInches(input);
            return Math.Round(inches / 12.0, 8);
        }

        public static string FormatFromInches(double totalInches)
        {
            var feet = Math.Floor(totalInches / 12.0);
            var inches = totalInches - (feet * 12.0);

            if (feet > 0)
            {
                return $"{feet}' {inches}\"";
            }
            else
            {
                return $"{inches}\"";
            }
        }
    }
}
