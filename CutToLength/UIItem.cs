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

        public double Length { get; set; }

        [JsonIgnore]
        public double TotalLength
        {
            get { return Math.Round(Length * Quantity, 8); }
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
