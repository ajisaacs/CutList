namespace CutToLength
{
    public class UIItem
    {
        private string name;

        public UIItem()
        {
        }

        public string Name
        {
            get
            {
                return string.IsNullOrWhiteSpace(name) ?
                    GetDefaultName() :
                    name;
            }
            set
            {
                name = value;
            }
        }

        public double Length { get; set; }

        public double TotalLength
        {
            get { return Length * Quantity; }
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
