namespace CutList.Core
{
    /// <summary>
    /// Represents an item to be placed in a bin.
    /// Enforces business rules for valid items.
    /// </summary>
    public class BinItem
    {
        private string _name = string.Empty;
        private double _length;

        public BinItem(string? name, double length)
        {
            if (length <= 0)
                throw new ArgumentException("Item length must be greater than zero", nameof(length));

            _name = name ?? string.Empty;
            _length = length;
        }

        /// <summary>
        /// Parameterless constructor for serialization only.
        /// Use the parameterized constructor for creating valid instances.
        /// </summary>
        public BinItem()
        {
        }

        public string Name
        {
            get => _name;
            set => _name = value ?? string.Empty;
        }

        public double Length
        {
            get => _length;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Item length must be greater than zero", nameof(value));
                _length = value;
            }
        }

        /// <summary>
        /// Checks if this item can fit in the given available length.
        /// </summary>
        public bool CanFitIn(double availableLength)
        {
            return Length <= availableLength;
        }

        /// <summary>
        /// Checks if this item can fit in the given available length with spacing.
        /// </summary>
        public bool CanFitInWithSpacing(double availableLength, double spacing)
        {
            return Length + spacing <= availableLength;
        }

        public override string ToString()
        {
            return $"{Name} ({Length}\")";
        }

        public override bool Equals(object? obj)
        {
            if (obj is BinItem other)
            {
                return Name == other.Name && Length.IsEqualTo(other.Length);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (Name?.GetHashCode() ?? 0);
                hash = hash * 23 + Length.GetHashCode();
                return hash;
            }
        }
    }
}