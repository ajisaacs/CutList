namespace CutList
{
    /// <summary>
    /// Represents a cutting tool with its kerf (blade width).
    /// Enforces business rules for valid tools.
    /// </summary>
    public class Tool
    {
        private string _name;
        private double _kerf;

        public Tool(string name, double kerf, bool allowUserToChange = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tool name cannot be empty", nameof(name));

            if (kerf < 0)
                throw new ArgumentException("Kerf cannot be negative", nameof(kerf));

            _name = name;
            _kerf = kerf;
            AllowUserToChange = allowUserToChange;
        }

        /// <summary>
        /// Parameterless constructor for serialization only.
        /// Use the parameterized constructor for creating valid instances.
        /// </summary>
        public Tool()
        {
        }

        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Tool name cannot be empty", nameof(value));
                _name = value;
            }
        }

        public double Kerf
        {
            get => _kerf;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Kerf cannot be negative", nameof(value));
                _kerf = value;
            }
        }

        public bool AllowUserToChange { get; set; }

        /// <summary>
        /// Calculates the total material wasted for a given number of cuts.
        /// </summary>
        public double CalculateTotalWaste(int numberOfCuts)
        {
            if (numberOfCuts < 0)
                throw new ArgumentException("Number of cuts cannot be negative", nameof(numberOfCuts));

            return Kerf * numberOfCuts;
        }

        /// <summary>
        /// Checks if this tool is compatible with the given material thickness.
        /// Some tools may have minimum/maximum thickness requirements.
        /// </summary>
        public bool IsCompatibleWithThickness(double thickness)
        {
            // For now, all tools are compatible with any thickness
            // This can be extended based on specific tool requirements
            return thickness > 0;
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is Tool other)
            {
                return Name == other.Name &&
                       Math.Abs(Kerf - other.Kerf) < 0.0001 &&
                       AllowUserToChange == other.AllowUserToChange;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // Prime multiplier reduces collisions in hash-based collections
                const int HashMultiplier = 23;
                int hash = 17;
                hash = hash * HashMultiplier + (Name?.GetHashCode() ?? 0);
                hash = hash * HashMultiplier + Kerf.GetHashCode();
                hash = hash * HashMultiplier + AllowUserToChange.GetHashCode();
                return hash;
            }
        }
    }
}