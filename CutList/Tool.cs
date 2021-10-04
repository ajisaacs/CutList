namespace CutList
{
    public class Tool
    {
        public string Name { get; set; }

        public double Kerf { get; set; }

        public bool AllowUserToChange { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
