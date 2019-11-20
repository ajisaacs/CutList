using System;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace CutToLength
{
    public static class Fraction
    {
        public static readonly Regex FractionRegex = new Regex(@"((?<WholeNum>\d+)(\ |-))?(?<Fraction>\d+\/\d+)");

        public static bool IsValid(string s)
        {
            return FractionRegex.IsMatch(s);
        }

        public static double Parse(string s)
        {
            var match = FractionRegex.Match(s);

            if (!match.Success)
                throw new Exception("Invalid format.");

            var value = 0.0;

            var wholeNumGroup = match.Groups["WholeNum"];
            var fractionGroup = match.Groups["Fraction"];

            if (wholeNumGroup.Success)
            {
                value = double.Parse(wholeNumGroup.Value);
            }

            if (fractionGroup.Success)
            {
                var parts = fractionGroup.Value.Split('/');
                var numerator = double.Parse(parts[0]);
                var denominator = double.Parse(parts[1]);

                value += Math.Round(numerator / denominator, 8);
            }

            return value;
        }

        public static string ReplaceFractionsWithDecimals(string input)
        {
            var sb = new StringBuilder(input);

            // find all matches and sort by descending index number to avoid
            // changing all previous index numbers when the fraction is replaced
            // with the decimal equivalent.
            var fractionMatches = FractionRegex.Matches(sb.ToString())
                .Cast<Match>()
                .OrderByDescending(m => m.Index);

            foreach (var fractionMatch in fractionMatches)
            {
                // convert the fraction to a decimal value
                var decimalValue = Parse(fractionMatch.Value);

                // remove the fraction and insert the decimal value in its place.
                sb.Remove(fractionMatch.Index, fractionMatch.Length);
                sb.Insert(fractionMatch.Index, decimalValue);
            }

            return sb.ToString();
        }

        public static bool TryParse(string s, out double fraction)
        {
            try
            {
                fraction = Parse(s);
            }
            catch
            {
                fraction = 0;
                return false;
            }

            return true;
        }
    }
}
