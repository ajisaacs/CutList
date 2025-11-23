using System;

namespace SawCut
{
    /// <summary>
    /// Represents a type of bin with quantity and priority.
    /// Enforces business rules for valid bin configurations.
    /// </summary>
    public class MultiBin
    {
        private int _quantity;
        private double _length;
        private int _priority;

        public MultiBin(double length, int quantity = 1, int priority = 25)
        {
            if (length <= 0)
                throw new ArgumentException("Bin length must be greater than zero", nameof(length));

            if (quantity < -1 || quantity == 0)
                throw new ArgumentException("Quantity must be positive or -1 for unlimited", nameof(quantity));

            _length = length;
            _quantity = quantity;
            _priority = priority;
        }

        /// <summary>
        /// Parameterless constructor for serialization only.
        /// Use the parameterized constructor for creating valid instances.
        /// </summary>
        public MultiBin()
        {
            _quantity = 1;
            _priority = 25;
        }

        /// <summary>
        /// Quantity of bins available. Use -1 for unlimited bins.
        /// </summary>
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (value < -1 || value == 0)
                    throw new ArgumentException("Quantity must be positive or -1 for unlimited", nameof(value));
                _quantity = value;
            }
        }

        public double Length
        {
            get => _length;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Bin length must be greater than zero", nameof(value));
                _length = value;
            }
        }

        /// <summary>
        /// Lower value priority will be used first. Default is 25.
        /// </summary>
        public int Priority
        {
            get => _priority;
            set => _priority = value;
        }

        /// <summary>
        /// Checks if this bin type has unlimited quantity.
        /// </summary>
        public bool IsUnlimited => Quantity == -1;

        /// <summary>
        /// Checks if an item of the given length can fit in this bin.
        /// </summary>
        public bool CanFitItem(double itemLength)
        {
            return itemLength <= Length && itemLength > 0;
        }

        /// <summary>
        /// Calculates the waste for a single bin if the given length is used.
        /// </summary>
        public double CalculateWaste(double usedLength)
        {
            if (usedLength < 0 || usedLength > Length)
                throw new ArgumentException("Used length must be between 0 and bin length", nameof(usedLength));

            return Length - usedLength;
        }

        public override string ToString()
        {
            var quantityStr = IsUnlimited ? "Unlimited" : Quantity.ToString();
            return $"Bin {Length}\" (Qty: {quantityStr}, Priority: {Priority})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is MultiBin other)
            {
                return Quantity == other.Quantity &&
                       Length.IsEqualTo(other.Length) &&
                       Priority == other.Priority;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Quantity.GetHashCode();
                hash = hash * 23 + Length.GetHashCode();
                hash = hash * 23 + Priority.GetHashCode();
                return hash;
            }
        }
    }
}