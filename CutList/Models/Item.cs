using Newtonsoft.Json;
using SawCut;
using System;

namespace CutList.Models
{
    public class Item
    {
        public Item()
        {
        }

        public string Name { get; set; }

        public string LengthInputValue { get; set; }

        public double? Length
        {
            get
            {
                try
                {
                    var input = Fraction.ReplaceFractionsWithDecimals(LengthInputValue);

                    double d;

                    if (double.TryParse(input, out d))
                    {
                        LengthInputValue += "\"";
                        return d;
                    }

                    return ArchUnits.ParseToInches(LengthInputValue);
                }
                catch
                {
                    return null;
                }
            }
        }

        [JsonIgnore]
        public double? TotalLength
        {
            get
            {
                var length = Length;

                if (length == null)
                    return null;

                return Math.Round(length.Value * Quantity, 8);
            }
        }

        public int Quantity { get; set; } = 1;
    }
}