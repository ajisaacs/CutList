using Newtonsoft.Json;
using System;

namespace CutToLength
{
    public class UIItem
    {
        public UIItem()
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
                    double d;

                    if (double.TryParse(LengthInputValue, out d))
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

        private string GetDefaultName()
        {
            if (Length == 0)
                return "-";

            return string.Format("{0}\" LG", Length);
        }
    }
}
