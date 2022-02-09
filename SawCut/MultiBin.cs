namespace SawCut
{
    public class MultiBin
    {
        public int Quantity { get; set; } = 1;

        public double Length { get; set; }

        /// <summary>
        /// Lower value priority with be used first. Default to 25
        /// </summary>
        public int Priority { get; set; } = 25;
    }
}