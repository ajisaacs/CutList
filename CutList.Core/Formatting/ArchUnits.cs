using System.Text;
using System.Text.RegularExpressions;

namespace CutList.Core.Formatting
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
            {
                // If no unit symbols, try to parse as plain inches (e.g., "0.5" or "1/2" converted to "0.5")
                if (!input.Contains("'") && !input.Contains("\""))
                {
                    if (double.TryParse(input.Trim(), out var plainInches))
                    {
                        return Math.Round(plainInches, 8);
                    }
                }
                throw new Exception("Input is not in a valid format.");
            }

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
            var inches = FormatHelper.ConvertToMixedFraction(totalInches - (feet * 12.0));

            if (feet > 0)
            {
                return $"{feet}'  {inches}\"";
            }
            else
            {
                return $"{inches}\"";
            }
        }

        /// <summary>
        /// Formats a measurement as a mixed fraction of total inches without converting to feet.
        /// </summary>
        public static string FormatInches(double totalInches)
        {
            return $"{FormatHelper.ConvertToMixedFraction(totalInches)}\"";
        }
    }
}
